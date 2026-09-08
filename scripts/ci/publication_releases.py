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


def release_producer(client, catalog, release_id):
    release = client.get('/releases/' + str(release_id))
    if release.get('id') != release_id or release.get('draft') is not False:
        raise PublicationError('Requested release is not published')
    tag = release.get('tag_name', '')
    if not any(tag.startswith(component['tag_prefix']) for component in catalog['components']):
        raise PublicationError('Release is outside the component catalog')
    source = tag_commit(client, tag)
    runs = client.get('/actions/workflows/ci.yml/runs?event=push&branch=main&status=success&head_sha=' + source + '&per_page=100')
    for run in runs.get('workflow_runs', []):
        if run.get('head_sha') == source and run.get('event') == 'push' and run.get('head_branch') == 'main' and run.get('conclusion') == 'success':
            producer, _ = client.producer(run['id'], run['run_attempt'])
            if producer.source_sha == source:
                return producer
    raise PublicationError('Release commit has no successful main CI run; complete its CI before publishing')


def main():
    import argparse
    import os
    from pathlib import Path
    import subprocess
    from publication import load_catalog
    from publication_github import GitHub

    parser = argparse.ArgumentParser(description='Dispatch normal publication for a newly published release')
    parser.add_argument('--release-id', type=int, required=True)
    args = parser.parse_args()
    try:
        client = GitHub(os.environ['GITHUB_REPOSITORY'], os.environ['GH_TOKEN'])
        producer = release_producer(client, load_catalog(Path(__file__).resolve().parents[2]), args.release_id)
        subprocess.run(['gh', 'workflow', 'run', 'publish.yml', '--repo', client.repository, '--ref', 'main',
                        '-f', 'run_id=' + str(producer.run_id), '-f', 'run_attempt=' + str(producer.run_attempt),
                        '-f', 'release_id=' + str(args.release_id)], check=True)
    except (PublicationError, OSError, KeyError, ValueError, subprocess.CalledProcessError) as error:
        parser.exit(1, f'Release publication dispatch failed: {error}\n')


if __name__ == '__main__':
    main()
