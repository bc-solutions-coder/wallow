"""Resolve a read-only publication plan from trusted control and exact CI metadata."""

import argparse
from dataclasses import asdict
import json
import os
from pathlib import Path
import tempfile

from publication import PublicationError, load_catalog, matches
from publication_artifacts import select_artifact, unpack_payload
from publication_github import GitHub
from publication_verify_images import verify_images
from publication_verify_packages import verify_packages


def candidate_artifacts(producer, jobs, artifacts):
    builds = [job for job in jobs if isinstance(job, dict) and job.get('name') == 'build']
    if len(builds) != 1 or builds[0].get('conclusion') not in ('success', 'skipped'):
        raise PublicationError('Producer route cannot be established')
    full = builds[0]['conclusion'] == 'success'
    outputs = [('docfx-site', 'site.tar.gz', 'docs', 'docfx'), ('images-docs', 'images.tar.gz', 'images', 'docs-amd64-arm64')]
    if full:
        outputs += [('js-packages', 'packages.tar.gz', 'packages', 'pnpm'), ('images-app', 'images.tar.gz', 'images', 'app-amd64-arm64'), ('images-infra', 'images.tar.gz', 'images', 'infra-amd64-arm64'), ('dependency-inputs', 'dependencies.tar.gz', 'dependencies', 'resolved-locks')]
    selected = [asdict(select_artifact(artifacts, producer, prefix)) | {'payload': payload, 'kind': kind, 'variant': variant} for prefix, payload, kind, variant in outputs]
    return 'full' if full else 'docs', selected


def resolve(client, context, run_id, attempt):
    controller_sha = client.controller(context)
    producer, jobs = client.producer(run_id, attempt)
    artifacts = client.list(f'/actions/runs/{producer.run_id}/artifacts', 'artifacts')
    route, selected = candidate_artifacts(producer, jobs, artifacts)
    registration = select_artifact(artifacts, producer, 'producer-registration')
    with tempfile.TemporaryDirectory(prefix='wallow-registration-') as directory:
        root = Path(directory)
        archive = client.download(registration, root / 'registration.zip')
        payload = unpack_payload(archive, root / 'verified', registration, producer, 'registration.json', 'registration', 'publication', 1024 * 1024)
        record = json.loads(payload.read_text())
    validate_registration(record, producer, route, selected)
    return {'schema': 1, 'controller_sha': controller_sha, 'producer': asdict(producer), 'route': route, 'artifacts': selected, 'registration': asdict(registration), 'inputs': record}


def validate_registration(record, producer, route, artifacts):
    expected = {'schema', 'producer', 'route', 'artifacts', 'component_versions', 'input_sha256', 'catalog'}
    if not isinstance(record, dict) or set(record) != expected or type(record['schema']) is not int or record['schema'] != 1:
        raise PublicationError('Invalid immutable producer registration')
    if record['producer'] != asdict(producer) or record['route'] != route or record['artifacts'] != artifacts:
        raise PublicationError('Registered producer or artifacts differ from current GitHub evidence')
    versions, fingerprints, catalog = record['component_versions'], record['input_sha256'], record['catalog']
    if not isinstance(versions, dict) or not versions or not isinstance(catalog, dict) or not isinstance(fingerprints, dict) or not fingerprints:
        raise PublicationError('Registration lacks source versions or build inputs')
    for path, digest in fingerprints.items():
        if not isinstance(path, str) or path.startswith('/') or any(part in ('', '.', '..') for part in path.split('/')) or not matches(r'[0-9a-f]{64}', digest):
            raise PublicationError('Invalid registered build input fingerprint')


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
        client = GitHub(context['repository'], os.environ.get('GH_TOKEN'))
        plan = resolve(client, context, int(args.run_id), int(args.attempt))
        catalog = load_catalog(Path(__file__).resolve().parents[2])
        plan['verified_images'] = verify_images(client, plan, catalog)
        plan['verified_packages'] = verify_packages(client, plan, catalog)
        with Path(args.output).open('x') as output:
            json.dump(plan, output, indent=2)
            output.write('\n')
    except (PublicationError, OSError) as error:
        parser.exit(1, f'Publication authorization failed: {error}\n')
    print(f"Authorized {plan['route']} producer {plan['producer']['source_sha']} run {args.run_id}, attempt {args.attempt}; controller {plan['controller_sha']}.")


if __name__ == '__main__':
    main()
