"""Authenticate explicitly selected recovery inputs while retaining original release authority."""

from publication import PublicationError, positive_integer
from publication_release_candidates import authorized_selection
from publication_release_receipts import find_receipt
from publication_release_selection import validate_candidate
from recovery_plan import resolve


def receipt_payload(plan, catalog):
    request = plan['inputs']['recovery_request']['record']
    release = request['release']
    snapshot = validate_candidate(plan, release, catalog)
    return {'release': release, 'origin': request['origin'], 'selection': request['selection'],
            'recovery_request': plan['inputs']['recovery_request'], 'recovery': snapshot}


def authorized_recovery(client, context, release, origin, selection, run_id, attempt, catalog):
    if not all(positive_integer(value) for value in (run_id, attempt)):
        raise PublicationError('Recovery selection requires an explicit positive run and attempt')
    original = authorized_selection(client, release, origin, selection)
    name = f'wallow-recovery-v1-{run_id}-{attempt}.json'
    receipt = find_receipt(client, release['id'], name)
    if receipt is None:
        raise PublicationError('Explicit recovery has no successful durable receipt')
    plan = resolve(client, context, run_id, attempt, catalog)
    expected = receipt_payload(plan, catalog)
    if expected['release'] != release or expected['origin'] != {'asset_id': origin['asset_id'], 'sha256': origin['sha256']} or expected['selection'] != {'asset_id': selection['asset_id'], 'sha256': selection['sha256']} or receipt['record']['payload'] != expected:
        raise PublicationError('Recovery receipt differs from the exact original release authority or recovered artifacts')
    if original['producer']['source_sha'] != plan['producer']['source_sha']:
        raise PublicationError('Recovered source differs from the original selected producer')
    return {'original': original, 'plan': plan,
            'receipt': {'asset_id': receipt['asset_id'], 'name': name, 'sha256': receipt['sha256']}}
