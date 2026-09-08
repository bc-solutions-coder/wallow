"""Copy immutable release images and retain exact readback receipts without moving aliases."""

from dataclasses import asdict
import hashlib
import json
from pathlib import Path
import tempfile

from publication import Producer, PublicationError, image_repository
from publication_artifacts import select_artifact, unpack_payload
from publication_ghcr import GHCR
from publication_image_provenance import OCI_INDEX, main_index
from publication_prepared_bundle import extract_prepared
from publication_publish_images import SkopeoRegistry, copy_verified_image
from publication_release_image_authorization import RECEIPT_JOB, WRITER_JOB, authorize_prepared, job_name
from publication_release_origin import frame, verify_frame
from publication_release_receipts import IMAGE, endorsed, inspect_receipt, retain


def expected_images(plan, catalog, repository):
    source = plan['source']
    result = []
    for image in catalog['images']:
        inventory = source['verified_images'][image['bundle']]['prepared']['inventory']
        variants = {item['platform']: item['prepared'] for item in inventory['images'] if item['id'] == image['id']}
        data = main_index(repository, source['producer']['source_sha'], image['id'], variants)
        result.append({'image': image['id'], 'repository': image_repository(repository, image), 'version': plan['authority']['release']['version'],
                       'source_sha': source['producer']['source_sha'], 'index_digest': 'sha256:' + hashlib.sha256(data).hexdigest(),
                       'platforms': variants, 'readback': 'verified'})
    return result


def publish_release_images(client, plan, preparation, artifacts, catalog, username, credential, progress, record,
                           transport_factory=SkopeoRegistry, registry_factory=GHCR):
    expected = {item['image']: item for item in expected_images(plan, catalog, client.repository)}
    source = plan['source']
    with tempfile.TemporaryDirectory(prefix='wallow-release-image-credentials-') as credentials:
        transport = None
        for bundle, artifact in artifacts.items():
            with tempfile.TemporaryDirectory(prefix='wallow-release-image-inputs-') as directory:
                temporary = Path(directory)
                archive = client.download(artifact, temporary / 'prepared.zip')
                payload = unpack_payload(archive, temporary / 'sealed', artifact, preparation, 'images.tar', 'prepared-images', bundle + '-amd64-arm64', 16 * 1024**3)
                extracted = temporary / 'inputs'
                images = extract_prepared(payload, extracted, bundle, source, catalog, preparation)
                if transport is None:
                    transport = transport_factory(username, credential, Path(credentials) / 'auth.json')
                for image in (item for item in catalog['images'] if item['bundle'] == bundle):
                    entry = expected[image['id']]
                    registry = registry_factory(entry['repository'], username, credential)
                    variants = {}
                    for item in images[image['id']]:
                        copy_verified_image(registry, transport, entry['repository'], extracted / item['directory'], item)
                        variants[item['platform']] = item['prepared']
                    data = main_index(client.repository, entry['source_sha'], image['id'], variants)
                    digest = 'sha256:' + hashlib.sha256(data).hexdigest()
                    if digest != entry['index_digest']:
                        raise PublicationError('Release index differs from exact prepared identity')
                    for reference in ('sha-' + entry['source_sha'], entry['version']):
                        registry.write_manifest(reference, data, OCI_INDEX, digest)
                    if registry.read_manifest(digest) != {'digest': digest, 'media_type': OCI_INDEX, 'bytes': data}:
                        raise PublicationError('Immutable release index readback differs from its exact bytes')
                    progress['images'].append(entry)
                    record()
    if sorted(progress['images'], key=lambda item: item['image']) != sorted(expected.values(), key=lambda item: item['image']):
        raise PublicationError('Immutable image publication did not cover the complete release')
    return progress


def writer_progress(client, invocation, release_id):
    verify_frame(client, invocation, job_name(WRITER_JOB, release_id))
    producer = Producer(invocation['repository'], invocation['repository_id'], invocation['controller_sha'], invocation['run_id'], invocation['run_attempt'], invocation['workflow_id'], invocation['workflow_ref'])
    artifacts = client.list(f'/actions/runs/{producer.run_id}/artifacts', 'artifacts')
    selected = select_artifact(artifacts, producer, f'release-image-progress-{release_id}')
    if selected.size > 17 * 1024 * 1024:
        raise PublicationError('Release image progress exceeds its bounded size')
    with tempfile.TemporaryDirectory(prefix='wallow-release-image-progress-') as directory:
        temporary = Path(directory)
        archive = client.download(selected, temporary / 'progress.zip')
        path = unpack_payload(archive, temporary / 'verified', selected, producer, 'progress.json', 'release-image-progress', str(release_id), 16 * 1024 * 1024)
        body = path.read_bytes()
        document = json.loads(body)
    if not isinstance(document, dict) or document.get('schema') != 1 or document.get('scope') != 'immutable-release-images' or document.get('invocation') != invocation or document.get('error') or not isinstance(document.get('images'), list):
        raise PublicationError('Protected image writer progress is incomplete or malformed')
    return document, {'frame': invocation, 'artifact': asdict(selected), 'sha256': 'sha256:' + hashlib.sha256(body).hexdigest()}


def finalize(client, context, producer_run, producer_attempt, run_id, attempt, release_id, catalog, root, explicit=None):
    current = frame(client, context, run_id, attempt, job_name(RECEIPT_JOB, release_id))
    writer, _ = frame(client, context, run_id, attempt, job_name(WRITER_JOB, release_id), 'success')
    document, reference = writer_progress(client, writer, release_id)
    plan, _, _, _ = authorize_prepared(client, context, producer_run, producer_attempt, run_id, attempt, release_id, catalog, root, explicit)
    identity = {key: plan['authority'][key] for key in ('release', 'origin', 'selection')}
    images = expected_images(plan, catalog, client.repository)
    if document.get('authority') != identity or sorted(document['images'], key=lambda item: item['image']) != sorted(images, key=lambda item: item['image']):
        raise PublicationError('Image writer progress differs from exact release preparation')
    payload = identity | {'images': images, 'writer': reference}
    previous = inspect_receipt(client, release_id, IMAGE)
    if previous is not None:
        original = previous['record']['payload']
        if set(original) != set(payload) or any(original.get(key) != payload[key] for key in ('release', 'origin', 'selection', 'images')):
            raise PublicationError('Immutable image publication conflicts with its prior receipt')
        verify_frame(client, original['writer']['frame'], job_name(WRITER_JOB, release_id))
        if not endorsed(client, release_id, previous):
            old, old_reference = writer_progress(client, original['writer']['frame'], release_id)
            if old_reference != original['writer'] or old.get('authority') != identity or sorted(old['images'], key=lambda item: item['image']) != sorted(images, key=lambda item: item['image']):
                raise PublicationError('Incomplete image receipt needs original successful writer evidence')
        payload = original
    receipt = retain(client, release_id, IMAGE, payload, current)
    return {'schema': 1, 'release_id': release_id, 'asset_id': receipt['asset_id'], 'sha256': receipt['sha256']}
