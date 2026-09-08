"""Preserve exact current control files before checking out historical application source."""

import argparse
import os
from pathlib import Path, PurePosixPath
import subprocess

from publication import PublicationError, matches


def preserve(root, destination, controller):
    root, destination = Path(root), Path(destination)
    if not matches(r'[0-9a-f]{40}', controller):
        raise PublicationError('Recovery control snapshot requires an exact controller revision')
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
    if head != controller:
        raise PublicationError('Recovery control checkout differs from its protected workflow revision')
    directories = ('scripts/ci/', '.github/ci/', '.github/actions/openapi-document/')
    config = '.github/actionlint.yaml'
    entries = subprocess.check_output(['git', 'ls-tree', '-rz', '--full-tree', controller, '--', config, *(name.rstrip('/') for name in directories)], cwd=root)
    records = []
    for raw in entries.rstrip(b'\0').split(b'\0'):
        try:
            metadata, name = raw.decode().split('\t', 1)
            mode, kind, object_id = metadata.split()
        except (UnicodeDecodeError, ValueError):
            raise PublicationError('Recovery control tree is malformed') from None
        path = PurePosixPath(name)
        if mode not in ('100644', '100755') or kind != 'blob' or not matches(r'[0-9a-f]{40}', object_id) or path.is_absolute() or any(part in ('', '.', '..') for part in path.parts) or not (name == config or name.startswith(directories)):
            raise PublicationError('Recovery controls must be regular tracked files within approved directories')
        records.append((name, mode, object_id))
    names = {name for name, _, _ in records}
    if len(records) > 1000 or not {'scripts/ci/validation.py', 'scripts/ci/security.py', '.github/ci/security-exceptions.json', '.github/actions/openapi-document/action.yml', config} <= names:
        raise PublicationError('Recovery control snapshot is incomplete or oversized')
    destination.mkdir(parents=True, exist_ok=False)
    total = 0
    for name, mode, object_id in records:
        size = int(subprocess.check_output(['git', 'cat-file', '-s', object_id], cwd=root, text=True))
        total += size
        if size > 4 * 1024 * 1024 or total > 32 * 1024 * 1024:
            raise PublicationError('Recovery control files exceed their size limit')
        content = subprocess.check_output(['git', 'cat-file', 'blob', object_id], cwd=root)
        if len(content) != size:
            raise PublicationError('Recovery control object size changed')
        target = destination / name
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open('xb') as stream:
            stream.write(content)
        target.chmod(0o755 if mode == '100755' else 0o644)
    return destination.resolve()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--destination', required=True)
    args = parser.parse_args()
    try:
        repository = os.environ.get('GITHUB_REPOSITORY', '')
        controller = os.environ.get('GITHUB_WORKFLOW_SHA', '')
        source = os.environ.get('RECOVERY_SOURCE', '')
        digest = os.environ.get('RECOVERY_REQUEST', '')
        release = os.environ.get('RECOVERY_RELEASE', '')
        if os.environ.get('GITHUB_EVENT_NAME') != 'workflow_dispatch' or os.environ.get('GITHUB_REF') != 'refs/heads/main' or os.environ.get('GITHUB_WORKFLOW_REF') != repository + '/.github/workflows/ci.yml@refs/heads/main' or os.environ.get('GITHUB_SHA') != controller:
            raise PublicationError('Historical checkout requires a current main CI dispatch')
        if not matches(r'[0-9a-f]{40}', source) or not matches(r'sha256:[0-9a-f]{64}', digest) or not matches(r'[1-9][0-9]{0,19}', release):
            raise PublicationError('Historical checkout requires the complete validated recovery request')
        root = Path(os.environ['GITHUB_WORKSPACE']).resolve()
        destination = Path(args.destination).resolve()
        if destination == root or root in destination.parents or any(character in str(destination) for character in ('\r', '\n')):
            raise PublicationError('Recovery controls must remain outside the historical source checkout')
        controls = preserve(root, destination, controller)
        with Path(os.environ['GITHUB_ENV']).open('a') as stream:
            for key, value in {'CI_CONTROL_ROOT': controls, 'CI_RECOVERY_SOURCE_SHA': source,
                               'CI_RECOVERY_REQUEST_SHA256': digest, 'CI_RECOVERY_RELEASE_ID': release}.items():
                stream.write(f'{key}={value}\n')
    except PublicationError as error:
        parser.exit(1, f'Recovery checkout failed: {error}\n')
    except (OSError, ValueError, KeyError, subprocess.SubprocessError):
        parser.exit(1, 'Recovery checkout could not preserve current controls.\n')


if __name__ == '__main__':
    main()
