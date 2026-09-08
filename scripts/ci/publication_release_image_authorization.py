"""Bind each image release matrix job to durable origin, selected producer and preparation."""

from dataclasses import asdict
import hashlib
import json
from pathlib import Path
import tempfile

from publication import PublicationError, positive_integer
from publication_artifacts import select_artifact, unpack_payload
from publication_image_authorization import image_environment
from publication_plan import resolve
from publication_preparation_authorization import authorize_preparation
from publication_release_authorization import release_identity
from publication_release_candidates import authorized_selection
from publication_release_origin import frame
from publication_release_receipts import IMAGE, ORIGIN, SELECTION, endorsed, find_receipt, inspect_receipt
from publication_release_selection import validate_candidate

PREPARE_JOB = 'Prepare release images'
WRITER_JOB = 'Publish release images'
RECEIPT_JOB = 'Record image publication'


def job_name(name, release_id):
    if not positive_integer(release_id):
        raise PublicationError('Image release requires an exact positive release ID')
    return f'{name} ({release_id})'


def policy_digest(root):
    return 'sha256:' + hashlib.sha256((Path(root) / '.github/ci/security-exceptions.json').read_bytes()).hexdigest()


def release_authority(client, context, catalog, release_id, explicit=None):
    client.controller(context)
    image_environment(client)
    job_name(PREPARE_JOB, release_id)
    release = release_identity(client, client.get('/releases/' + str(release_id)), catalog)
    if release['id'] != release_id:
        raise PublicationError('Image release API identity differs from its exact request')
    images = [image for image in catalog['images'] if image['component'] == release['component']]
    if not images or len(images) != len(catalog['images']):
        raise PublicationError('Image release must cover the complete current platform component')
    origin, selection = find_receipt(client, release_id, ORIGIN), find_receipt(client, release_id, SELECTION)
    if origin is None or selection is None:
        raise PublicationError('Image release is pending its durable origin or selected producer')
    pinned = authorized_selection(client, release, origin, selection, explicit)
    producer = pinned['producer']
    plan = resolve(client, context, producer['run_id'], producer['run_attempt'])
    if validate_candidate(plan, release, catalog) != pinned or plan['route'] != 'full':
        raise PublicationError('Image release differs from its selected complete producer')
    return {'release': release, 'origin': {'asset_id': origin['asset_id'], 'sha256': origin['sha256']},
            'selection': {'asset_id': selection['asset_id'], 'sha256': selection['sha256']}, 'plan': plan}


def discover(client, context, catalog, release_id=None, explicit=None):
    client.controller(context)
    image_environment(client)
    if release_id is not None and context.get('event_name') != 'workflow_dispatch':
        raise PublicationError('Explicit image release selection requires a protected manual retry')
    components = {image['component'] for image in catalog['images']}
    pending, selected = [], []
    releases = client.array('/releases')
    identities = [item.get('id') for item in releases if isinstance(item, dict)]
    if len(identities) != len(releases) or any(not positive_integer(value) for value in identities) or len(set(identities)) != len(identities):
        raise PublicationError('Image release enumeration contains invalid or duplicate identities')
    if release_id is not None and not any(item.get('id') == release_id for item in releases):
        raise PublicationError('Requested image release is absent')
    for value in releases:
        if release_id is not None and value.get('id') != release_id:
            continue
        component = next((item for item in catalog['components'] if isinstance(value.get('tag_name'), str) and value['tag_name'].startswith(item['tag_prefix'])), None)
        if component is None or component['id'] not in components or value.get('draft') is not False:
            if value.get('id') == release_id:
                raise PublicationError('Requested release does not own catalog images')
            continue
        release = release_identity(client, value, catalog)
        origin, selection = find_receipt(client, release['id'], ORIGIN), find_receipt(client, release['id'], SELECTION)
        if origin is None or selection is None:
            pending.append(release['id'])
            continue
        authorized_selection(client, release, origin, selection, explicit if release_id else None)
        receipt = inspect_receipt(client, release['id'], IMAGE)
        if release_id is None and receipt is not None and endorsed(client, release['id'], receipt):
            # The protected immutable receipt finalizer only records complete readback.
            payload = receipt['record']['payload']
            if payload.get('release') != release or payload.get('origin') != {'asset_id': origin['asset_id'], 'sha256': origin['sha256']} or payload.get('selection') != {'asset_id': selection['asset_id'], 'sha256': selection['sha256']}:
                raise PublicationError('Completed image receipt conflicts with its live release')
            continue
        release_authority(client, context, catalog, release['id'], explicit if release_id else None)
        selected.append(release['id'])
    if len(selected) > 100:
        raise PublicationError('Pending image release matrix exceeds its bounded limit')
    if release_id is not None and pending:
        raise PublicationError('Requested image release remains pending exact producer evidence')
    return {'include': [{'release_id': value} for value in sorted(selected)]}, pending


def authorize_prepared(client, context, producer_run, producer_attempt, run_id, attempt, release_id, catalog, root, explicit=None):
    _, preparation, artifacts, _ = authorize_preparation(client, context, producer_run, producer_attempt, run_id, attempt)
    invocation, _ = frame(client, context, run_id, attempt, job_name(PREPARE_JOB, release_id), 'success')
    selected = select_artifact(artifacts, preparation, f'release-image-plan-{release_id}')
    if selected.size > 17 * 1024 * 1024:
        raise PublicationError('Release image plan exceeds its bounded size')
    with tempfile.TemporaryDirectory(prefix='wallow-release-image-plan-') as directory:
        temporary = Path(directory)
        archive = client.download(selected, temporary / 'plan.zip')
        path = unpack_payload(archive, temporary / 'verified', selected, preparation, 'plan.json', 'release-image-plan', str(release_id), 16 * 1024 * 1024)
        plan = json.loads(path.read_text())
    authority = release_authority(client, context, catalog, release_id, explicit)
    if not isinstance(plan, dict) or plan.get('schema') != 1 or plan.get('invocation') != invocation or plan.get('authority') != authority or plan.get('policy_sha256') != policy_digest(root) or plan.get('publication_authorized') is not False:
        raise PublicationError('Release image preparation differs from current exact authority or policy')
    source = plan.get('source', {})
    if {key: source.get(key) for key in authority['plan']} != authority['plan'] or set(source.get('verified_images', {})) != {'app', 'infra', 'docs'}:
        raise PublicationError('Release image preparation differs from the complete selected source')
    prepared = {bundle: select_artifact(artifacts, preparation, f'release-images-{release_id}-{bundle}') for bundle in ('app', 'infra', 'docs')}
    if any(item.size > 16 * 1024**3 + 1024 * 1024 for item in prepared.values()):
        raise PublicationError('Release prepared image archive exceeds its bounded size')
    return plan, preparation, prepared, {'plan_artifact': asdict(selected), 'prepare_job_id': invocation['job_id']}
