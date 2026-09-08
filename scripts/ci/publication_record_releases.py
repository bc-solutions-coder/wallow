"""Retain verified release origins and exact producer selections before future release writes."""

import argparse
import json
import os
from pathlib import Path

from publication import PublicationError, load_catalog, matches, positive_integer
from publication_plan import resolve
from publication_release_authorization import ReleaseOutsideMain, authenticate_origin, release_identity
from publication_release_github import ReleaseGitHub
from publication_release_origin import RECEIPT_JOB, frame, action_evidence
from publication_release_receipts import ORIGIN, SELECTION, inspect_receipt, retain, endorsed
from publication_release_selection import choose_producer


def reconcile(client, context, run_id, attempt, producer_run, producer_attempt, catalog, release_id=None, result=None):
    current = frame(client, context, run_id, attempt, RECEIPT_JOB)
    frame(client, context, run_id, attempt, 'authorize', 'success')
    resolve(client, context, producer_run, producer_attempt)
    if release_id is not None and (not positive_integer(release_id) or context.get('event_name') != 'workflow_dispatch'):
        raise PublicationError('An explicit release retry must be a protected manual invocation')
    releases = [client.get('/releases/' + str(release_id))] if release_id is not None else client.array('/releases')
    if release_id is not None and releases[0].get('id') != release_id:
        raise PublicationError('Requested release identity differs from API response')
    result = result if result is not None else {'schema': 1, 'publication_authorized': False, 'releases': []}
    result['invocation'] = current[0]
    for api_release in releases:
        if release_id is None and (api_release.get('draft') is not False or not any(isinstance(api_release.get('tag_name'), str) and api_release['tag_name'].startswith(component['tag_prefix']) for component in catalog['components'])):
            continue
        try:
            release = release_identity(client, api_release, catalog)
        except ReleaseOutsideMain:
            if release_id is not None or any(inspect_receipt(client, api_release['id'], name) is not None for name in (ORIGIN, SELECTION)):
                raise
            result['releases'].append({'release_id': api_release['id'], 'state': 'unsupported-ancestry', 'publication_authorized': False})
            continue
        prior_origin = inspect_receipt(client, release['id'], ORIGIN)
        if prior_origin is not None and not endorsed(client, release['id'], prior_origin):
            previous = prior_origin['record']['payload'].get('release_please', {})
            invocation = previous.get('frame', {})
            if action_evidence(client, invocation.get('run_id'), invocation.get('run_attempt')) != previous:
                raise PublicationError('Unendorsed release origin differs from freshly verified action evidence')
        origin = authenticate_origin(client, release, prior_origin)
        if origin is None:
            result['releases'].append({'release_id': release['id'], 'state': 'pending-origin', 'publication_authorized': False})
            if release_id is not None:
                raise PublicationError('Explicit release lacks retained protected automation origin; recovery is required')
            continue
        origin_receipt = retain(client, release['id'], ORIGIN, origin, current)
        entry = {'release_id': release['id'], 'state': 'pending-producer', 'publication_authorized': False,
                 'origin_asset_id': origin_receipt['asset_id'], 'origin_sha256': origin_receipt['sha256']}
        result['releases'].append(entry)
        prior_selection = inspect_receipt(client, release['id'], SELECTION)
        pinned = None
        if prior_selection:
            payload = prior_selection['record']['payload']
            if set(payload) != {'origin_asset_id', 'origin_sha256', 'release', 'selection'} or payload.get('release') != release or payload.get('origin_asset_id') != origin_receipt['asset_id'] or payload.get('origin_sha256') != origin_receipt['sha256']:
                raise PublicationError('Release selection is not bound to its exact immutable origin')
            pinned = payload['selection']
        explicit = (producer_run, producer_attempt) if release_id is not None else None
        selection = choose_producer(client, context, release, catalog, pinned=pinned, explicit=explicit)
        if selection is None:
            continue
        payload = {'origin_asset_id': origin_receipt['asset_id'], 'origin_sha256': origin_receipt['sha256'], 'release': release, 'selection': selection}
        receipt = retain(client, release['id'], SELECTION, payload, current)
        entry.update({'state': 'producer-selected', 'selection_asset_id': receipt['asset_id'], 'selection_sha256': receipt['sha256'], 'producer': selection['producer']})
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--producer-run', required=True)
    parser.add_argument('--producer-attempt', required=True)
    parser.add_argument('--release-id', default='')
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    result = {'schema': 1, 'publication_authorized': False, 'releases': []}
    error = None
    try:
        context = {key: os.environ.get('GITHUB_' + key.upper(), '') for key in ('repository', 'ref', 'workflow_ref', 'workflow_sha', 'event_name')}
        values = (os.environ.get('GITHUB_RUN_ID'), os.environ.get('GITHUB_RUN_ATTEMPT'), args.producer_run, args.producer_attempt)
        if os.environ.get('ENABLE_RELEASE_AUTOMATION') != 'true' or not all(matches(r'[1-9][0-9]*', value) for value in values) or (args.release_id and not matches(r'[1-9][0-9]*', args.release_id)):
            raise PublicationError('Release provenance requires literal enablement and exact invocation identities')
        client = ReleaseGitHub(context['repository'], os.environ.get('GH_TOKEN'))
        reconcile(client, context, *(int(value) for value in values), load_catalog(Path(__file__).resolve().parents[2]), int(args.release_id) if args.release_id else None, result)
    except PublicationError as failure:
        error = str(failure)
        result['error'] = error
    except (OSError, ValueError, TypeError, KeyError, RecursionError):
        error = 'Release provenance reconciliation failed; existing partial receipts require successful originating job evidence.'
        result['error'] = error
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open('x') as stream:
        json.dump(result, stream, indent=2)
        stream.write('\n')
    if error:
        parser.exit(1, error + '\n')


if __name__ == '__main__':
    main()
