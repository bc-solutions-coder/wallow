"""Register immutable publication inputs after a successful main validation gate."""

import argparse
from dataclasses import asdict
import hashlib
import json
import os
from pathlib import Path
import subprocess

from publication import Producer, PublicationError, load_catalog, positive_integer
from publication_github import GitHub
from publication_plan import candidate_artifacts
from validation import artifact_identity


def source_inputs(root, catalog, paths):
    root = Path(root)
    versions = json.loads((root / '.release-please-manifest.json').read_text())
    expected_paths = {component['path'] for component in catalog['components']}
    if not isinstance(versions, dict) or set(versions) != expected_paths or any(not isinstance(value, str) or not value for value in versions.values()):
        raise PublicationError('Release versions do not cover the publication catalog')
    for component in catalog['components']:
        if 'package' in component:
            manifest = json.loads((root / component['path'] / 'package.json').read_text())
            if manifest.get('version') != versions[component['path']]:
                raise PublicationError('Package and release configuration versions differ')
    files = {}
    for path in sorted(paths):
        file = root / path
        if path.startswith('/') or any(part in ('', '.', '..') for part in path.split('/')) or file.is_symlink() or not file.is_file():
            raise PublicationError('Invalid tracked publication input')
        with file.open('rb') as stream:
            files[path] = hashlib.file_digest(stream, 'sha256').hexdigest()
    if not files:
        raise PublicationError('Publication toolchain and dependency inputs are missing')
    return {'component_versions': versions, 'input_sha256': files, 'catalog': catalog}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--route', choices=['docs', 'full'], required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    try:
        if os.environ.get('GITHUB_EVENT_NAME') != 'push' or os.environ.get('GITHUB_REF') != 'refs/heads/main':
            raise PublicationError('Registration is restricted to main pushes')
        identity = artifact_identity('registration', 'publication')
        sha = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
        if sha != identity['sha'] or identity['workflow_ref'] != identity['repository'] + '/.github/workflows/ci.yml@refs/heads/main':
            raise PublicationError('Registration checkout or workflow identity differs from the producer')
        client = GitHub(identity['repository'], os.environ.get('GH_TOKEN'))
        repository = client.get('')
        workflow = client.get('/actions/workflows/ci.yml')
        if not isinstance(repository, dict) or repository.get('full_name') != identity['repository'] or not positive_integer(repository.get('id')) or not isinstance(workflow, dict) or workflow.get('path') != '.github/workflows/ci.yml' or not positive_integer(workflow.get('id')):
            raise PublicationError('Missing registration repository or workflow identity')
        producer = Producer(identity['repository'], repository['id'], sha, int(identity['run_id']), int(identity['run_attempt']), workflow['id'], identity['workflow_ref'])
        artifacts = client.list(f'/actions/runs/{producer.run_id}/artifacts', 'artifacts')
        _, selected = candidate_artifacts(producer, [{'name': 'build', 'conclusion': 'success' if args.route == 'full' else 'skipped'}], artifacts)
        catalog = load_catalog('.')
        paths = subprocess.check_output(['git', 'ls-files', '-z', '--', 'global.json', '**/global.json', '.nvmrc', '.node-version', '.npmrc', 'package.json', 'pnpm-lock.yaml', 'pnpm-workspace.yaml', 'turbo.jsonc', '.github/workflows', '.github/ci', '.github/actions', 'docker', 'packages/*/package.json', 'apps/*/package.json', '*.csproj', '*.props', '*.targets', '**/packages.lock.json', '**/[Nn]u[Gg]et.[Cc]onfig'], text=True).rstrip('\0').split('\0')
        inputs = source_inputs('.', catalog, paths)
        record = {'schema': 1, 'producer': asdict(producer), 'route': args.route, 'artifacts': selected, **inputs}
        with Path(args.output).open('x') as output:
            json.dump(record, output, indent=2)
            output.write('\n')
    except (PublicationError, ValueError, OSError, subprocess.CalledProcessError) as error:
        parser.exit(1, f'Publication registration failed: {error}\n')
    print(f'Registered {len(selected)} exact publication inputs for {sha}.')


if __name__ == '__main__':
    main()
