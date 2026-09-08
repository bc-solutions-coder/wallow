"""Deploy the exact prepared main site with durable forward-only Pages intents."""

import argparse
from dataclasses import asdict
import json
import os
from pathlib import Path

from publication import PublicationError, matches
from publication_pages_authorization import authorize_pages
from publication_pages_bootstrap import pages_history
from publication_pages_github import PagesGitHub
from publication_pages_history import JOB, inspect_intent, pages_action
from publication_release_origin import frame


def publish_pages(client, plan, artifact, identity, settings, current, output, oidc_url, oidc_token, bootstrap_path=None):
    source = plan['producer']['source_sha']
    site_url = settings['html_url']
    records = client.array('/deployments?environment=github-pages')
    intents = pages_history(client, records, source, identity, site_url, bootstrap_path)
    action = pages_action(client, source, identity, intents)
    progress = {'schema': 1, 'producer': plan['producer'], 'controller_sha': plan['controller_sha'],
                'artifact': asdict(artifact), 'site': identity, 'action': action, 'site_url': site_url}
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=False)

    def save():
        temporary = output.with_suffix('.tmp')
        temporary.write_text(json.dumps(progress, indent=2) + '\n')
        temporary.replace(output)

    save()
    if action == 'skip-older':
        return progress
    token = client.oidc(oidc_url, oidc_token)
    observed_ids = sorted(record['id'] for record in records)
    if sorted(record['id'] for record in client.array('/deployments?environment=github-pages')) != observed_ids:
        raise PublicationError('Pages history changed during preparation; retry')
    payload = {'schema': 1, 'source_sha': source, 'producer': plan['producer'],
               'artifact': asdict(artifact), 'site': identity, 'recorder': current[0],
               'history_tail_id': max(observed_ids, default=0)}
    intent = client.intent(payload)
    progress['intent_id'] = intent.get('id')
    save()
    inspect_intent(client, intent, current)
    readback = client.get('/deployments/' + str(intent['id']))
    if inspect_intent(client, readback, current) != payload or readback['id'] != intent['id']:
        raise PublicationError('Durable Pages intent changed after creation')
    if sorted(record['id'] for record in client.array('/deployments?environment=github-pages')) != sorted(observed_ids + [intent['id']]):
        raise PublicationError('Pages history changed while recording the deployment intent')
    try:
        client.status(intent['id'], 'in_progress', current[0]['run_id'], site_url, 'Deploying exact validated Pages artifact')
        deployment = client.deploy(artifact.id, source, token)
        progress['pages_deployment_id'] = deployment['id']
        save()
        result = client.wait_deployment(deployment['id'])
        progress['pages_status'] = result['status']
        save()
        progress['index_readback'] = client.verify_index(site_url, identity)
        completion = client.status(intent['id'], 'success', current[0]['run_id'], site_url,
                                   'Verified artifact ' + str(artifact.id) + ' deployment ' + deployment['id'])
        progress['success_status_id'] = completion['id']
        statuses = client.array(f"/deployments/{intent['id']}/statuses")
        if not any(status.get('id') == completion['id'] and status.get('state') == 'success' and status.get('environment_url') == site_url for status in statuses):
            raise PublicationError('Pages completion status could not be read back')
        progress['action'] = 'verified'
        save()
    except BaseException:
        progress['action'] = 'incomplete'
        save()
        raise
    return progress


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--producer-run', required=True)
    parser.add_argument('--producer-attempt', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    try:
        values = (args.producer_run, args.producer_attempt, os.environ.get('GITHUB_RUN_ID'), os.environ.get('GITHUB_RUN_ATTEMPT'))
        if os.environ.get('ENABLE_DOCS_DEPLOY') != 'true' or not all(matches(r'[1-9][0-9]*', value) for value in values):
            raise PublicationError('Pages requires explicit enablement and exact producer/invocation identities')
        context = {key: os.environ.get('GITHUB_' + key.upper(), '') for key in ('repository', 'ref', 'workflow_ref', 'workflow_sha', 'event_name')}
        client = PagesGitHub(context['repository'], os.environ.get('GH_TOKEN'))
        run, attempt, invocation_run, invocation_attempt = map(int, values)
        plan, artifact, identity, settings, _ = authorize_pages(client, context, run, attempt, invocation_run, invocation_attempt)
        current = frame(client, context, invocation_run, invocation_attempt, JOB)
        bootstrap = Path(__file__).resolve().parents[2] / '.github/ci/pages-bootstrap.json'
        result = publish_pages(client, plan, artifact, identity, settings, current, args.output,
                               os.environ.get('ACTIONS_ID_TOKEN_REQUEST_URL', ''), os.environ.get('ACTIONS_ID_TOKEN_REQUEST_TOKEN', ''), bootstrap)
        print('Pages result: ' + result['action'])
    except PublicationError as error:
        parser.exit(1, f'Pages publication failed: {error}\n')
    except (OSError, ValueError, TypeError, KeyError, RecursionError):
        parser.exit(1, 'Pages publication failed; inspect retained progress and protected job evidence.\n')


if __name__ == '__main__':
    main()
