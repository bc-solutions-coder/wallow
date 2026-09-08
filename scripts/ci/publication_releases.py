"""Select release tags for a validated commit and keep stable aliases moving forward."""

from urllib.parse import quote

from publication import PublicationError, matches
from publication_aliases import aliases, stable_version


def releases(client, catalog):
    result = []
    for page in range(1, 101):
        batch = client.get(f'/releases?per_page=100&page={page}')
        if not isinstance(batch, list):
            raise PublicationError('Release listing is unavailable')
        for release in batch:
            if release.get('draft'):
                continue
            tag = release.get('tag_name', '')
            for component in catalog['components']:
                if not tag.startswith(component['tag_prefix']):
                    continue
                version = tag[len(component['tag_prefix']):]
                if not matches(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z.-]+)?', version):
                    continue
                result.append({'id': release['id'], 'component': component['id'], 'version': version,
                               'tag_name': tag, 'target': release.get('target_commitish', ''), 'prerelease': release.get('prerelease') is True})
        if len(batch) < 100:
            return result
    raise PublicationError('Release listing exceeds its limit')


def tag_commit(client, tag):
    value = client.get('/git/ref/tags/' + quote(tag, safe=''))['object']
    for _ in range(10):
        if value.get('type') == 'commit' and matches(r'[a-f0-9]{40}', value.get('sha')):
            return value['sha']
        if value.get('type') != 'tag' or not matches(r'[a-f0-9]{40}', value.get('sha')):
            break
        value = client.get('/git/tags/' + value['sha'])['object']
    raise PublicationError('Release tag does not resolve to a commit')


def selected_releases(client, catalog, source, release_id=None):
    history = releases(client, catalog)
    selected = [release for release in history if (release_id is None or release['id'] == release_id)
                and (not matches(r'[a-f0-9]{40}', release['target']) or release['target'] == source)
                and tag_commit(client, release['tag_name']) == source]
    if release_id is not None and len(selected) != 1:
        raise PublicationError('Requested release does not match the validated CI commit')
    return selected, history


def current_aliases(release, history, kind):
    version = stable_version(release)
    return [alias for alias in aliases(release, kind)
            if not any(other['component'] == release['component'] and alias in aliases(other, kind)
                       and stable_version(other) > version for other in history)]
