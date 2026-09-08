"""Discover, prepare, publish and record immutable release images in separate jobs."""

import argparse
import copy
import json
import os
from pathlib import Path

from publication import PublicationError, load_catalog, matches
from publication_preparation_authorization import authorize_preparation
from publication_prepare_images import ImagePreparation
from publication_release_github import ReleaseGitHub
from publication_release_image_authorization import PREPARE_JOB, WRITER_JOB, authorize_prepared, discover, job_name, policy_digest, release_authority
from publication_release_image_writer import finalize, publish_release_images
from publication_release_origin import frame
from publication_verify_images import verify_images

DISCOVERY_JOB = 'Discover image releases'


def prepare(client, context, producer_run, producer_attempt, run_id, attempt, release_id, catalog, root, output, explicit=None):
    authorize_preparation(client, context, producer_run, producer_attempt, run_id, attempt)
    frame(client, context, run_id, attempt, DISCOVERY_JOB, 'success')
    invocation, _ = frame(client, context, run_id, attempt, job_name(PREPARE_JOB, release_id))
    authority = release_authority(client, context, catalog, release_id, explicit)
    source = copy.deepcopy(authority['plan'])
    output = Path(output)
    output.mkdir(parents=True)
    preparation = ImagePreparation(source, catalog, output / 'images', output / 'scans', controller=root)
    source['verified_images'] = verify_images(client, source, catalog, preparation)
    return {'schema': 1, 'invocation': invocation, 'authority': authority, 'source': source,
            'policy_sha256': policy_digest(root), 'publication_authorized': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=('discover', 'prepare', 'publish', 'record'))
    parser.add_argument('--producer-run', required=True)
    parser.add_argument('--producer-attempt', required=True)
    parser.add_argument('--release-id', default='')
    parser.add_argument('--manual-release-id', default='')
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    output = Path(args.output)
    progress = {'schema': 1, 'scope': 'immutable-release-images', 'images': []}
    error = None
    try:
        values = (args.producer_run, args.producer_attempt, os.environ.get('GITHUB_RUN_ID'), os.environ.get('GITHUB_RUN_ATTEMPT'))
        if os.environ.get('ENABLE_IMAGE_PUBLISH') != 'true' or not all(matches(r'[1-9][0-9]*', value) for value in values):
            raise PublicationError('Image release jobs require literal enablement and exact invocation identities')
        if args.release_id and not matches(r'[1-9][0-9]*', args.release_id) or args.manual_release_id and not matches(r'[1-9][0-9]*', args.manual_release_id):
            raise PublicationError('Image release selection requires exact numeric IDs')
        producer_run, producer_attempt, run_id, attempt = (int(value) for value in values)
        release_id = int(args.release_id) if args.release_id else None
        manual = int(args.manual_release_id) if args.manual_release_id else None
        context = {key: os.environ.get('GITHUB_' + key.upper(), '') for key in ('repository', 'ref', 'workflow_ref', 'workflow_sha', 'event_name')}
        if manual is not None and (context['event_name'] != 'workflow_dispatch' or args.mode != 'discover' and manual != release_id):
            raise PublicationError('Manual image retry differs from its requested release')
        explicit = (producer_run, producer_attempt) if manual else None
        root = Path(__file__).resolve().parents[2]
        catalog = load_catalog(root)
        token = os.environ.get('GH_TOKEN')
        client = ReleaseGitHub(context['repository'], token)
        if args.mode == 'discover':
            authorize_preparation(client, context, producer_run, producer_attempt, run_id, attempt)
            frame(client, context, run_id, attempt, DISCOVERY_JOB)
            matrix, pending = discover(client, context, catalog, manual, explicit)
            progress = {'schema': 1, 'matrix': matrix, 'pending': pending}
            with open(os.environ['GITHUB_OUTPUT'], 'a') as stream:
                stream.write('matrix=' + json.dumps(matrix, separators=(',', ':')) + '\n')
                stream.write('count=' + str(len(matrix['include'])) + '\n')
        elif args.mode == 'prepare':
            progress = prepare(client, context, producer_run, producer_attempt, run_id, attempt, release_id, catalog, root, output.parent, explicit)
        elif args.mode == 'publish':
            invocation, _ = frame(client, context, run_id, attempt, job_name(WRITER_JOB, release_id))
            progress['invocation'] = invocation
            plan, preparation, artifacts, evidence = authorize_prepared(client, context, producer_run, producer_attempt, run_id, attempt, release_id, catalog, root, explicit)
            progress['authority'] = {key: plan['authority'][key] for key in ('release', 'origin', 'selection')}
            progress['preparation'] = evidence
            output.parent.mkdir(parents=True)
            publish_release_images(client, plan, preparation, artifacts, catalog, os.environ.get('GITHUB_ACTOR'), token, progress,
                                   lambda: output.write_text(json.dumps(progress, indent=2) + '\n'))
        else:
            progress = finalize(client, context, producer_run, producer_attempt, run_id, attempt, release_id, catalog, root, explicit)
    except (PublicationError, OSError, ValueError, TypeError, KeyError, RecursionError) as failure:
        error = str(failure) if isinstance(failure, PublicationError) else 'Image release operation failed; verify retained evidence before retry.'
        progress['error'] = error
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(progress, indent=2) + '\n')
    if error:
        parser.exit(1, error + '\n')


if __name__ == '__main__':
    main()
