"""Authorize only reviewed, expiring replacements of receipt-less package aliases."""

from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path

from publication import PublicationError, matches, positive_integer
from publication_release_authorization import release_identity


def identity(value):
    return (isinstance(value, dict) and set(value) == {'id', 'version', 'commit_sha'}
            and positive_integer(value['id']) and matches(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)', value['version'])
            and matches(r'[a-f0-9]{40}', value['commit_sha']))


def authorize(client, catalog, root, candidate, alias, current, *, now=None):
    path = Path(root) / '.github/ci/legacy-package-aliases.json'
    if not path.exists():
        return None
    if path.is_symlink() or not path.is_file() or path.stat().st_size > 65536:
        raise PublicationError('Legacy package alias configuration is not a bounded file')
    body = path.read_bytes()
    try:
        document = json.loads(body)
        if not isinstance(document, dict) or set(document) != {'schema', 'entries'} or type(document['schema']) is not int or document['schema'] != 1 or not isinstance(document['entries'], list) or len(document['entries']) > 32:
            raise ValueError()
        keys = set()
        for entry in document['entries']:
            if not isinstance(entry, dict) or set(entry) != {'repository', 'package', 'alias', 'previous', 'destination', 'receipt', 'expires_at'}:
                raise ValueError()
            if not matches(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', entry['repository']) or not matches(r'@[a-z0-9-]+/[a-z0-9_.-]+', entry['package']) or entry['alias'] != 'latest' or not identity(entry['previous']) or not identity(entry['destination']):
                raise ValueError()
            receipt = entry['receipt']
            if not isinstance(receipt, dict) or set(receipt) != {'asset_id', 'sha256'} or not positive_integer(receipt['asset_id']) or not matches(r'sha256:[a-f0-9]{64}', receipt['sha256']):
                raise ValueError()
            if not matches(r'\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z', entry['expires_at']):
                raise ValueError()
            datetime.fromisoformat(entry['expires_at'])
            key = entry['repository'], entry['package'], entry['alias']
            if key in keys:
                raise ValueError()
            keys.add(key)
    except (ValueError, TypeError, KeyError, RecursionError):
        raise PublicationError('Legacy package alias configuration is malformed or ambiguous') from None
    if not document['entries']:
        return None
    selected = [entry for entry in document['entries'] if (entry['repository'], entry['package'], entry['alias']) == (client.repository, candidate['outputs']['package']['name'], alias)]
    if len(selected) != 1:
        raise PublicationError('Existing package alias has no exact reviewed migration')
    entry = selected[0]
    release = candidate['release']
    destination = {key: release[key] for key in ('id', 'version', 'commit_sha')}
    instant = now if now is not None else datetime.now(timezone.utc)
    if instant >= datetime.fromisoformat(entry['expires_at']) or entry['previous']['version'] != current or entry['destination'] != destination or entry['receipt'] != candidate['receipt'] or release['prerelease']:
        raise PublicationError('Legacy package alias migration is expired or differs from its exact destination')
    if tuple(map(int, entry['previous']['version'].split('.'))) >= tuple(map(int, destination['version'].split('.'))):
        raise PublicationError('Legacy package alias migration must advance its version')
    old = release_identity(client, client.get('/releases/' + str(entry['previous']['id'])), catalog)
    if {key: old[key] for key in ('id', 'version', 'commit_sha')} != entry['previous'] or old['prerelease'] or old['component'] != release['component']:
        raise PublicationError('Legacy package release identity changed')
    comparison = client.get('/compare/' + old['commit_sha'] + '...' + release['commit_sha'])
    if comparison.get('status') != 'ahead' or comparison.get('merge_base_commit', {}).get('sha') != old['commit_sha']:
        raise PublicationError('Legacy package release is not an ancestor of its destination')
    return {'sha256': 'sha256:' + hashlib.sha256(body).hexdigest(), 'entry': entry}
