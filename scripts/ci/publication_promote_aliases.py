"""Run independently enabled stable alias preparation, promotion or progress recording."""

import argparse
import json
import os
from pathlib import Path

from publication import PublicationError, load_catalog, matches
from publication_alias_pipeline import prepare, promote
from publication_alias_progress import finalize
from publication_release_github import ReleaseGitHub


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=('prepare', 'promote', 'record'))
    parser.add_argument('--kind', choices=('image', 'package'), required=True)
    parser.add_argument('--producer-run', required=True)
    parser.add_argument('--producer-attempt', required=True)
    parser.add_argument('--release-id', default='')
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    progress = {'schema': 1, 'kind': args.kind, 'entries': []}
    output = Path(args.output)
    error = None
    try:
        values = (args.producer_run, args.producer_attempt, os.environ.get('GITHUB_RUN_ID'), os.environ.get('GITHUB_RUN_ATTEMPT'))
        flag = 'ENABLE_IMAGE_PUBLISH' if args.kind == 'image' else 'ENABLE_PACKAGE_PUBLISH'
        if os.environ.get(flag) != 'true' or not all(matches(r'[1-9][0-9]*', value) for value in values) or args.release_id and not matches(r'[1-9][0-9]*', args.release_id):
            raise PublicationError('Alias jobs require literal capability enablement and exact invocation identities')
        target = int(args.release_id) if args.release_id else None
        context = {key: os.environ.get('GITHUB_' + key.upper(), '') for key in ('repository', 'ref', 'workflow_ref', 'workflow_sha', 'event_name')}
        token = os.environ.get('GH_TOKEN')
        client = ReleaseGitHub(context['repository'], token)
        root = Path(__file__).resolve().parents[2]
        catalog = load_catalog(root)
        identifiers = [int(value) for value in values]
        if args.mode == 'prepare':
            progress = prepare(client, context, *identifiers, args.kind, catalog, root, output.parent, token, os.environ.get('GITHUB_ACTOR'), target)
        elif args.mode == 'promote':
            output.parent.mkdir(parents=True, exist_ok=True)
            promote(client, context, *identifiers, args.kind, catalog, root, token, os.environ.get('GITHUB_ACTOR'), progress,
                    lambda: output.write_text(json.dumps(progress, indent=2) + '\n'), target)
        else:
            progress = finalize(client, context, *identifiers, args.kind, catalog, root, target)
    except PublicationError as failure:
        error = str(failure)
    except (OSError, ValueError, TypeError, KeyError, RecursionError):
        error = 'Alias operation failed; inspect retained progress before retrying.'
    if error:
        progress['error'] = error
    body = json.dumps(progress, indent=2) + '\n'
    if len(body.encode()) > 16 * 1024 * 1024:
        parser.exit(1, 'Alias evidence exceeds its bounded size.\n')
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(body)
    if args.mode == 'prepare' and not error:
        with open(os.environ['GITHUB_OUTPUT'], 'a') as stream:
            stream.write('eligible=' + ('false' if progress['unrelated'] or not progress['entries'] else 'true') + '\n')
    if error:
        parser.exit(1, error + '\n')


if __name__ == '__main__':
    main()
