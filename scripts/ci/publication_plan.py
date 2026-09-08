"""Resolve a read-only publication plan from trusted control and exact CI metadata."""

import argparse
from dataclasses import asdict
import json
import os
from pathlib import Path

from publication import PublicationError, load_catalog, matches
from publication_artifacts import select_artifact
from publication_github import GitHub
from publication_verify_images import verify_images
from publication_verify_packages import verify_packages
from publication_prepare_images import ImagePreparation
from publication_image_authorization import image_environment
from publication_verify_site import verify_site


def candidate_artifacts(producer, jobs, artifacts):
    builds = [job for job in jobs if isinstance(job, dict) and job.get('name') == 'build']
    if len(builds) != 1 or builds[0].get('conclusion') not in ('success', 'skipped'):
        raise PublicationError('Producer route cannot be established')
    full = builds[0]['conclusion'] == 'success'
    outputs = [('docfx-site', 'site.tar.gz', 'docs', 'docfx'), ('images-docs', 'images.tar.gz', 'images', 'docs-amd64-arm64')]
    if full:
        outputs += [('js-packages', 'packages.tar.gz', 'packages', 'pnpm'), ('images-app', 'images.tar.gz', 'images', 'app-amd64-arm64'), ('images-infra', 'images.tar.gz', 'images', 'infra-amd64-arm64')]
    selected = [asdict(select_artifact(artifacts, producer, prefix)) | {'payload': payload, 'kind': kind, 'variant': variant} for prefix, payload, kind, variant in outputs]
    return 'full' if full else 'docs', selected


def resolve(client, context, run_id, attempt):
    controller_sha = client.controller(context)
    producer, jobs = client.producer(run_id, attempt)
    artifacts = client.list(f'/actions/runs/{producer.run_id}/artifacts', 'artifacts')
    route, selected = candidate_artifacts(producer, jobs, artifacts)
    return {'schema': 1, 'controller_sha': controller_sha, 'producer': asdict(producer), 'route': route, 'artifacts': selected}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run-id', required=True)
    parser.add_argument('--attempt', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    if not all(matches(r'[1-9][0-9]*', value) for value in (args.run_id, args.attempt)):
        parser.exit(1, 'Explicit positive producer run and attempt are required.\n')
    context = {key: os.environ.get('GITHUB_' + key.upper(), '') for key in ('repository', 'ref', 'workflow_ref', 'workflow_sha', 'event_name')}
    try:
        catalog = load_catalog(Path(__file__).resolve().parents[2])
        client = GitHub(context['repository'], os.environ.get('GH_TOKEN'))
        plan = resolve(client, context, int(args.run_id), int(args.attempt))
        if os.environ.get('ENABLE_IMAGE_PUBLISH') == 'true':
            image_environment(client)
        plan['verified_packages'] = verify_packages(client, plan, catalog)
        reports = Path(args.output).parent
        preparation = ImagePreparation(plan, catalog, '.ci-prepared', reports / 'image-scan')
        plan['verified_images'] = verify_images(client, plan, catalog, prepare=preparation)
        plan['verified_site'] = verify_site(client, plan, '.ci-prepared/site')
        with Path(args.output).open('x') as output:
            json.dump(plan, output, indent=2)
            output.write('\n')
        if os.environ.get('GITHUB_OUTPUT'):
            with Path(os.environ['GITHUB_OUTPUT']).open('a') as output:
                output.write('route=' + plan['route'] + '\n')
                source = plan['producer']['source_sha']
                current = client.get('/git/ref/heads/main')['object']['sha'] == source
                output.write('source=' + source + '\n')
                output.write('current=' + str(current).lower() + '\n')
    except (PublicationError, OSError) as error:
        parser.exit(1, f'Publication authorization failed: {error}\n')
    print(f"Authorized {plan['route']} producer {plan['producer']['source_sha']} run {args.run_id}, attempt {args.attempt}; controller {plan['controller_sha']}.")


if __name__ == '__main__':
    main()
