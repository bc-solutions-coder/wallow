"""Retain an authenticated recovered producer without changing its original selection."""

import argparse
import json
import os
from pathlib import Path

from publication import PublicationError, load_catalog, matches, positive_integer
from publication_release_github import ReleaseGitHub
from publication_release_origin import frame
from publication_release_receipts import RECOVERY_JOB, retain
from publication_release_selection import validate_candidate
from recovery_plan import resolve


def record(client, context, run_id, attempt, producer_run, producer_attempt, release_id, catalog):
    if not all(positive_integer(value) for value in (run_id, attempt, producer_run, producer_attempt, release_id)):
        raise PublicationError('Recovery recorder requires exact positive invocation, producer, and release IDs')
    if not isinstance(context, dict) or context.get('event_name') != 'workflow_dispatch':
        raise PublicationError('Recovery recording requires an explicit protected dispatch')
    current = frame(client, context, run_id, attempt, RECOVERY_JOB)
    plan = resolve(client, context, producer_run, producer_attempt, catalog)
    request = plan['inputs']['recovery_request']['record']
    release = request['release']
    if release['id'] != release_id or plan['producer']['release_id'] != release_id:
        raise PublicationError('Recovered producer belongs to another requested release')
    snapshot = validate_candidate(plan, release, catalog)
    payload = {'release': release, 'origin': request['origin'], 'selection': request['selection'],
               'recovery_request': plan['inputs']['recovery_request'], 'recovery': snapshot}
    name = f'wallow-recovery-v1-{producer_run}-{producer_attempt}.json'
    receipt = retain(client, release_id, name, payload, current)
    return {'schema': 1, 'release_id': release_id, 'asset_id': receipt['asset_id'],
            'name': name, 'sha256': receipt['sha256'], 'publication_authorized': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--producer-run', required=True)
    parser.add_argument('--producer-attempt', required=True)
    parser.add_argument('--release-id', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    try:
        values = [os.environ.get('GITHUB_RUN_ID', ''), os.environ.get('GITHUB_RUN_ATTEMPT', ''),
                  args.producer_run, args.producer_attempt, args.release_id]
        if not all(matches(r'[1-9][0-9]{0,19}', value) for value in values):
            raise PublicationError('Recovery recorder IDs must be bounded positive integers')
        context = {key: os.environ.get('GITHUB_' + key.upper(), '') for key in
                   ('repository', 'ref', 'workflow_ref', 'workflow_sha', 'event_name')}
        client = ReleaseGitHub(context['repository'], os.environ.get('GH_TOKEN'))
        result = record(client, context, *map(int, values), load_catalog(Path(__file__).resolve().parents[2]))
        with Path(args.output).open('x') as output:
            json.dump(result, output, indent=2)
            output.write('\n')
    except PublicationError as error:
        parser.exit(1, f'Recovery recording failed: {error}\n')
    except (OSError, ValueError, TypeError, KeyError, RecursionError):
        parser.exit(1, 'Recovery receipt could not be authenticated or retained.\n')


if __name__ == '__main__':
    main()
