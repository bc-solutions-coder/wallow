"""Select explicit recovery inputs without replacing a release's original producer."""

from publication import PublicationError, positive_integer
from publication_plan import resolve
from publication_release_authorization import release_identity
from publication_release_candidates import authorized_selection
from publication_release_receipts import ORIGIN, SELECTION, find_receipt
from recovery_selection import authorized_recovery


def resolve_selection(client, context, run_id, attempt, catalog, recovery=None):
    if recovery is None:
        return resolve(client, context, run_id, attempt)
    if not isinstance(recovery, dict) or set(recovery) != {'release_id', 'run_id', 'run_attempt'} or not all(
        positive_integer(value) for value in (*recovery.values(), run_id, attempt)
    ) or not isinstance(context, dict) or context.get('event_name') != 'workflow_dispatch':
        raise PublicationError('Recovery selection requires a manual request with exact positive release and producer identities')
    client.controller(context)
    release = release_identity(client, client.get('/releases/' + str(recovery['release_id'])), catalog)
    if release['id'] != recovery['release_id']:
        raise PublicationError('Recovery selection differs from the requested release API identity')
    origin = find_receipt(client, release['id'], ORIGIN)
    selection = find_receipt(client, release['id'], SELECTION)
    if origin is None or selection is None:
        raise PublicationError('Recovery selection requires the original durable release origin and producer selection')
    original = authorized_selection(client, release, origin, selection, (run_id, attempt))
    recovered = authorized_recovery(client, context, release, origin, selection, recovery['run_id'], recovery['run_attempt'], catalog)
    if recovered['original'] != original:
        raise PublicationError('Original release selection changed during recovery authorization')
    return recovered['plan'] | {'recovery': {'original': original, 'receipt': recovered['receipt']}}
