"""Retain durable exact package publication results with a separate contents-write job."""

import argparse
import json
import os
from pathlib import Path

from publication import PublicationError, load_catalog, matches
from publication_package_preparation import authorize_packages
from publication_package_progress import revalidate_partial_receipt, writer_progress
from publication_package_records import WRITER_JOB, package_record, published_package, validate_readback
from publication_release_github import ReleaseGitHub
from publication_release_origin import frame
from publication_release_receipts import PACKAGE, PACKAGE_JOB, endorsed, inspect_receipt, retain


def finalize(client, context, producer_run, producer_attempt, run_id, attempt, catalog, root, result, release_id=None):
    current = frame(client, context, run_id, attempt, PACKAGE_JOB)
    result['invocation'] = current[0]
    writer, _ = frame(client, context, run_id, attempt, WRITER_JOB, 'success')
    document, reference = writer_progress(client, writer)
    plan, _, _, _ = authorize_packages(client, context, producer_run, producer_attempt, run_id, attempt, catalog, root, release_id)
    expected = {item['release']['id']: item for item in plan['prepared']['candidates'] + plan['prepared']['published']}
    required = set(plan['ordered_release_ids'])
    target = plan['prepared']['target_release_id']
    if target is None:
        required.update(item['release']['id'] for item in plan['prepared']['published'] if item.get('needs_endorsement'))
    else:
        required.add(target)
    if not required <= {entry['release']['id'] for entry in document['entries']}:
        raise PublicationError('Package writer progress is missing required releases')
    for entry in document['entries']:
        release_id = entry['release']['id']
        if release_id not in expected:
            raise PublicationError('Package writer recorded an unauthorized release')
        source = expected[release_id]
        if any(entry.get(key) != source[key] for key in ('release', 'origin', 'selection', 'package')):
            raise PublicationError('Package writer progress differs from its exact prepared release')
        package = package_record(entry['package'], source['package']['name'], source['release']['version'], client.repository)
        validate_readback(entry['readback'], package)
        payload = entry | {'writer': reference}
        previous = inspect_receipt(client, release_id, PACKAGE)
        if previous is not None:
            component = next(item for item in catalog['components'] if item.get('package', {}).get('name') == package.name)
            published_package(client, entry['release'], component, entry['origin'], entry['selection'], previous)
            if not endorsed(client, release_id, previous):
                revalidate_partial_receipt(client, previous)
            if any(previous['record']['payload'].get(key) != entry[key] for key in ('release', 'origin', 'selection', 'package')):
                raise PublicationError('Package publication conflicts with its immutable prior receipt')
            payload = previous['record']['payload']
        receipt = retain(client, release_id, PACKAGE, payload, current)
        result['receipts'].append({'release_id': release_id, 'asset_id': receipt['asset_id'], 'sha256': receipt['sha256']})
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--producer-run', required=True)
    parser.add_argument('--producer-attempt', required=True)
    parser.add_argument('--release-id', default='')
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    result = {'schema': 1, 'scope': 'immutable-package-publication-receipts', 'receipts': []}
    error = None
    try:
        values = (args.producer_run, args.producer_attempt, os.environ.get('GITHUB_RUN_ID'), os.environ.get('GITHUB_RUN_ATTEMPT'))
        if os.environ.get('ENABLE_PACKAGE_PUBLISH') != 'true' or not all(matches(r'[1-9][0-9]*', value) for value in values) or (args.release_id and not matches(r'[1-9][0-9]*', args.release_id)):
            raise PublicationError('Package finalization requires literal enablement and exact invocation identities')
        context = {key: os.environ.get('GITHUB_' + key.upper(), '') for key in ('repository', 'ref', 'workflow_ref', 'workflow_sha', 'event_name')}
        root = Path(__file__).resolve().parents[2]
        client = ReleaseGitHub(context['repository'], os.environ.get('GH_TOKEN'))
        finalize(client, context, *(int(value) for value in values), load_catalog(root), root, result, int(args.release_id) if args.release_id else None)
    except (PublicationError, OSError, ValueError, TypeError, KeyError, RecursionError):
        error = 'Package publication receipt finalization failed; preserved immutable bytes require successful endorsement.'
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
