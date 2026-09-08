"""Verify durable recovery lineage without requiring expired Actions artifacts."""

from publication import PublicationError, matches, positive_integer
from publication_release_receipts import find_receipt, recovery_receipt


def verify_reference(client, reference, release, origin, selection):
    if not isinstance(reference, dict) or set(reference) != {'asset_id', 'name', 'sha256'} or not positive_integer(reference.get('asset_id')) or not recovery_receipt(reference.get('name')) or not matches(r'sha256:[0-9a-f]{64}', reference.get('sha256')):
        raise PublicationError('Published recovery reference lacks exact durable asset identity')
    receipt = find_receipt(client, release['id'], reference['name'])
    if receipt is None or {key: receipt[key] for key in reference} != reference:
        raise PublicationError('Published recovery reference differs from its authenticated immutable receipt')
    payload = receipt['record']['payload']
    if set(payload) != {'release', 'origin', 'selection', 'recovery_request', 'recovery'} or payload['release'] != release or payload['origin'] != origin or payload['selection'] != selection:
        raise PublicationError('Published recovery lineage differs from its original release authority')
    recovery = payload['recovery']
    producer = recovery.get('producer') if isinstance(recovery, dict) else None
    if not isinstance(producer, dict) or producer.get('source_sha') != release['commit_sha'] or producer.get('release_id') != release['id'] or recovery.get('component_versions', {}).get(release['component_path']) != release['version']:
        raise PublicationError('Published recovered producer differs from its exact release source and version')
    return receipt
