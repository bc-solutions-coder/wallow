"""Authorize an explicit historical validation request without authorizing publication."""

import argparse
import hashlib
import json
import os
from pathlib import Path

from publication import PublicationError, load_catalog, main_ancestor, matches, positive_integer
from publication_release_authorization import authenticate_origin, release_identity
from publication_release_github import ReleaseGitHub
from publication_release_receipts import ORIGIN, SELECTION, find_receipt


def authorize_dispatch(context, repository, workflow, run, run_id, attempt, comparison):
    if not all(isinstance(item, dict) for item in (context, repository, workflow, run)):
        raise PublicationError('Recovery requires complete controller metadata')
    name, repository_id = repository.get('full_name'), repository.get('id')
    workflow_id, controller = workflow.get('id'), context.get('workflow_sha')
    if not matches(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', name) or not positive_integer(repository_id) or repository.get('default_branch') != 'main':
        raise PublicationError('Recovery requires this repository with default main')
    if not positive_integer(workflow_id) or workflow.get('path') != '.github/workflows/ci.yml':
        raise PublicationError('Recovery requires the current CI workflow')
    if not all(positive_integer(value) for value in (run_id, attempt, run.get('id'), run.get('run_attempt'), run.get('workflow_id'))):
        raise PublicationError('Recovery requires exact positive invocation identities')
    expected_context = {'repository': name, 'ref': 'refs/heads/main', 'event_name': 'workflow_dispatch',
                        'workflow_ref': name + '/.github/workflows/ci.yml@refs/heads/main'}
    expected_run = {'id': run_id, 'run_attempt': attempt, 'workflow_id': workflow_id, 'path': workflow['path'],
                    'event': 'workflow_dispatch', 'head_branch': 'main', 'head_sha': controller, 'status': 'in_progress'}
    if any(context.get(key) != value for key, value in expected_context.items()) or any(run.get(key) != value for key, value in expected_run.items()):
        raise PublicationError('Recovery must start from an exact active CI dispatch on main')
    for key in ('repository', 'head_repository'):
        identity = run.get(key)
        if not isinstance(identity, dict) or type(identity.get('id')) is not int or identity.get('id') != repository_id or identity.get('full_name') != name:
            raise PublicationError('Recovery invocation belongs to another repository')
    if not matches(r'[0-9a-f]{40}', controller) or not main_ancestor(comparison, controller):
        raise PublicationError('Recovery controller is not on protected main ancestry')
    return controller


def request(client, context, run_id, attempt, source, release_id, catalog):
    if not matches(r'[0-9a-f]{40}', source) or not positive_integer(release_id) or not positive_integer(run_id) or not positive_integer(attempt):
        raise PublicationError('Recovery requires an exact source commit, release, run, and attempt')
    repository = client.get('')
    workflow = client.get('/actions/workflows/ci.yml')
    run = client.get(f'/actions/runs/{run_id}/attempts/{attempt}')
    controller = authorize_dispatch(context, repository, workflow, run, run_id, attempt, client.main_comparison(context.get('workflow_sha')))
    api_release = client.get(f'/releases/{release_id}')
    if not isinstance(api_release, dict) or api_release.get('id') != release_id:
        raise PublicationError('Recovery release API identity differs from the request')
    release = release_identity(client, api_release, catalog)
    if release['commit_sha'] != source or not main_ancestor(client.main_comparison(source), source):
        raise PublicationError('Recovery source must equal the exact release commit on main')
    origin = find_receipt(client, release_id, ORIGIN)
    if origin is None:
        raise PublicationError('Artifact recovery requires an existing authenticated release origin')
    authenticate_origin(client, release, origin)
    selection = find_receipt(client, release_id, SELECTION)
    if selection is None:
        raise PublicationError('Artifact recovery requires the original immutable producer selection')
    payload = selection['record']['payload']
    if set(payload) != {'origin_asset_id', 'origin_sha256', 'release', 'selection'} or payload['release'] != release or payload['origin_asset_id'] != origin['asset_id'] or payload['origin_sha256'] != origin['sha256']:
        raise PublicationError('Recovery selection differs from its authenticated release origin')
    selected = payload['selection']
    producer = selected.get('producer') if isinstance(selected, dict) else None
    if not isinstance(producer, dict) or producer.get('source_sha') != source or producer.get('repository') != client.repository or not all(positive_integer(producer.get(key)) for key in ('run_id', 'run_attempt')) or selected.get('component_versions', {}).get(release['component_path']) != release['version']:
        raise PublicationError('Recovery selection does not identify the original release producer')
    return {'schema': 1, 'mode': 'historical-recovery', 'repository': client.repository,
            'controller_sha': controller, 'source_sha': source, 'run_id': run_id, 'run_attempt': attempt,
            'workflow_id': workflow['id'], 'release': release,
            'origin': {'asset_id': origin['asset_id'], 'sha256': origin['sha256']},
            'selection': {'asset_id': selection['asset_id'], 'sha256': selection['sha256']},
            'route': 'full', 'publication_authorized': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True)
    parser.add_argument('--release-id', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    try:
        ids = [os.environ.get('GITHUB_RUN_ID', ''), os.environ.get('GITHUB_RUN_ATTEMPT', ''), args.release_id]
        if not all(matches(r'[1-9][0-9]{0,19}', value) for value in ids):
            raise PublicationError('Recovery requires exact positive invocation and release IDs')
        context = {key: os.environ.get('GITHUB_' + key.upper(), '') for key in ('repository', 'ref', 'workflow_ref', 'workflow_sha', 'event_name')}
        client = ReleaseGitHub(context['repository'], os.environ.get('GH_TOKEN'))
        run_id, attempt, release_id = map(int, ids)
        record = request(client, context, run_id, attempt, args.source, release_id, load_catalog(Path(__file__).resolve().parents[2]))
        output = Path(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        data = (json.dumps(record, sort_keys=True, indent=2) + '\n').encode()
        with output.open('xb') as stream:
            stream.write(data)
        digest = 'sha256:' + hashlib.sha256(data).hexdigest()
        if os.environ.get('GITHUB_OUTPUT'):
            with Path(os.environ['GITHUB_OUTPUT']).open('a') as stream:
                for key, value in {'source_ref': record['source_sha'], 'controller_ref': record['controller_sha'],
                                   'recovery_request_sha256': digest, 'recovery_release_id': release_id,
                                   'recovery_mode': 'true', 'route': 'full'}.items():
                    stream.write(f'{key}={value}\n')
    except PublicationError as error:
        parser.exit(1, f'Recovery request failed: {error}\n')
    except (OSError, ValueError, TypeError, KeyError, RecursionError):
        parser.exit(1, 'Recovery request could not be validated.\n')


if __name__ == '__main__':
    main()
