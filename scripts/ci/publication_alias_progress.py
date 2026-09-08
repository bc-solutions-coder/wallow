"""Retain append-only alias observations; immutable publication receipts remain authority."""

from dataclasses import asdict
import json
from pathlib import Path
import tempfile

from publication import Producer, PublicationError
from publication_alias_pipeline import authorize_plan, job
from publication_artifacts import select_artifact, unpack_payload
from publication_release_origin import frame
from publication_release_receipts import inspect_receipt, write_receipt


def finalize(client, context, producer_run, producer_attempt, run_id, attempt, kind, catalog, root, target=None, *, recovery=None):
    current = frame(client, context, run_id, attempt, job(kind, 'Record'))
    invocation, _ = frame(client, context, run_id, attempt, job(kind, 'Promote'), 'success')
    producer = Producer(client.repository, invocation['repository_id'], invocation['controller_sha'], run_id, attempt, invocation['workflow_id'], invocation['workflow_ref'])
    selected = select_artifact(client.list(f'/actions/runs/{run_id}/artifacts', 'artifacts'), producer, kind + '-alias-progress')
    if selected.size > 17 * 1024 * 1024:
        raise PublicationError('Alias writer progress exceeds its bounded size')
    with tempfile.TemporaryDirectory(prefix='wallow-alias-progress-') as directory:
        temporary = Path(directory)
        archive = client.download(selected, temporary / 'progress.zip')
        path = unpack_payload(archive, temporary / 'verified', selected, producer, 'progress.json', 'alias-progress', kind, 16 * 1024 * 1024)
        document = json.loads(path.read_text())
    plan = authorize_plan(client, context, producer_run, producer_attempt, run_id, attempt, kind, catalog, root, target, recovery=recovery)
    if not isinstance(document, dict) or document.get('schema') != 1 or document.get('kind') != kind or document.get('invocation') != invocation or document.get('records') != plan['records'] or 'error' in document or not isinstance(document.get('entries'), list) or len(document['entries']) != len(plan['entries']):
        raise PublicationError('Alias progress differs from the successful exact writer and preparation')
    for observed, expected in zip(document['entries'], plan['entries'], strict=True):
        outcome = 'verified' if expected['action'] == 'advance' else expected['action']
        if observed != expected | {'outcome': outcome}:
            raise PublicationError('Alias progress lacks exact readback or skip evidence')
    assets = []
    for release_id in sorted({entry['release_id'] for entry in document['entries']}):
        name = f'wallow-alias-{kind}-{run_id}-{attempt}.json'
        payload = {'immutable_receipt': next(item['receipt'] for item in plan['records'] if item['release']['id'] == release_id),
                   'writer': invocation, 'artifact': asdict(selected), 'entries': [entry for entry in document['entries'] if entry['release_id'] == release_id],
                   'authority': 'immutable-publication-receipt-only'}
        previous = inspect_receipt(client, release_id, name)
        if previous is not None:
            if previous['record']['payload'] != payload or previous['record']['recorder'] != current[0]:
                raise PublicationError('Append-only alias progress conflicts with an existing invocation record')
            receipt = previous
        else:
            receipt = write_receipt(client, release_id, name, payload, current)
        assets.append({'release_id': release_id, 'asset_id': receipt['asset_id'], 'sha256': receipt['sha256']})
    return {'schema': 1, 'kind': kind, 'assets': assets}
