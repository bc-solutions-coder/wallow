"""Read exact successful protected package-writer evidence for durable finalization."""

from dataclasses import asdict
import hashlib
import json
from pathlib import Path
import tempfile

from publication import Producer, PublicationError, positive_integer
from publication_artifacts import select_artifact, unpack_payload
from publication_package_records import WRITER_JOB
from publication_release_origin import verify_frame


def writer_progress(client, invocation):
    verify_frame(client, invocation, WRITER_JOB)
    producer = Producer(client.repository, invocation['repository_id'], invocation['controller_sha'], invocation['run_id'], invocation['run_attempt'], invocation['workflow_id'], invocation['workflow_ref'])
    artifacts = client.list(f"/actions/runs/{producer.run_id}/artifacts", 'artifacts')
    artifact = select_artifact(artifacts, producer, 'package-progress')
    if artifact.size > 17 * 1024 * 1024:
        raise PublicationError('Package progress artifact exceeds its bounded download size')
    with tempfile.TemporaryDirectory(prefix='wallow-package-progress-') as directory:
        root = Path(directory)
        archive = client.download(artifact, root / 'progress.zip')
        path = unpack_payload(archive, root / 'verified', artifact, producer, 'progress.json', 'package-progress', 'releases', 16 * 1024 * 1024)
        body = path.read_bytes()
        document = json.loads(body)
    if not isinstance(document, dict) or document.get('schema') != 1 or document.get('scope') != 'immutable-package-versions' or document.get('invocation') != invocation or 'error' in document or not isinstance(document.get('entries'), list) or len(document['entries']) > 1000:
        raise PublicationError('Package writer progress is not exact successful immutable-version evidence')
    identities = []
    for entry in document['entries']:
        if not isinstance(entry, dict) or set(entry) != {'release', 'origin', 'selection', 'package', 'readback'} or not isinstance(entry.get('release'), dict) or not positive_integer(entry['release'].get('id')):
            raise PublicationError('Package writer progress contains an invalid release entry')
        identities.append(entry['release']['id'])
    if len(identities) != len(set(identities)):
        raise PublicationError('Package writer progress has duplicate release identities')
    return document, {'frame': invocation, 'artifact': asdict(artifact), 'sha256': 'sha256:' + hashlib.sha256(body).hexdigest()}


def revalidate_partial_receipt(client, receipt):
    payload = receipt['record']['payload']
    writer = payload.get('writer', {})
    document, actual = writer_progress(client, writer.get('frame'))
    entries = [entry for entry in document['entries'] if entry['release']['id'] == receipt['record']['release_id']]
    if actual != writer or len(entries) != 1 or entries[0] | {'writer': writer} != payload:
        raise PublicationError('Unendorsed package receipt differs from its original successful writer evidence')
