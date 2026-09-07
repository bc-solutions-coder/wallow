"""Publish exact authorized tarballs from an empty directory and verify readback."""

import json
import hashlib
import os
from pathlib import Path
import subprocess
import shutil
import tempfile

from publication import PublicationError
from publication_packages import inspect_package


def publish_tarball(path, name, version, repository, token, runner=subprocess.run):
    registry = 'https://npm.pkg.github.com'
    package = inspect_package(path, name, version, registry, repository)
    if not isinstance(token, str) or not token:
        raise PublicationError('Package publication credential is missing')
    with tempfile.TemporaryDirectory(prefix='wallow-npm-') as directory:
        root = Path(directory)
        candidate = root / 'candidate.tgz'
        shutil.copyfile(path, candidate)
        with candidate.open('rb') as stream:
            if hashlib.file_digest(stream, 'sha256').hexdigest() != package.sha256:
                raise PublicationError('Package changed before publication staging')
        config = root / '.npmrc'
        config.write_text(f'registry={registry}\n{name.split("/")[0]}:registry={registry}\n//npm.pkg.github.com/:_authToken=${{NODE_AUTH_TOKEN}}\nignore-scripts=true\nprovenance=false\n')
        config.chmod(0o600)
        environment = {key: value for key, value in os.environ.items() if key in ('PATH', 'HOME', 'SYSTEMROOT', 'TMPDIR')}
        environment['NODE_AUTH_TOKEN'] = token
        base = ['npm', '--userconfig', str(config), '--globalconfig', os.devnull, '--cache', str(root / 'cache'), '--ignore-scripts', '--registry', registry, '--json']

        def command(arguments):
            try:
                result = runner(base + arguments, cwd=root, env=environment, capture_output=True, text=True, timeout=180)
            except (OSError, subprocess.TimeoutExpired):
                raise PublicationError('Package registry operation did not complete; verify registry state before retrying') from None
            try:
                data = json.loads(result.stdout)
            except (TypeError, json.JSONDecodeError):
                raise PublicationError('Package registry returned malformed output') from None
            return result.returncode, data

        def existing():
            code, data = command(['view', name + '@' + version])
            if code != 0:
                if isinstance(data, dict) and isinstance(data.get('error'), dict) and data['error'].get('code') == 'E404':
                    return False
                raise PublicationError('Package registry metadata could not be read')
            if not isinstance(data, dict) or data.get('name') != name or data.get('version') != version or not isinstance(data.get('dist'), dict) or data['dist'].get('integrity') != package.integrity:
                raise PublicationError('Existing package version conflicts with the validated tarball')
            return True

        if existing():
            return {'name': name, 'version': version, 'integrity': package.integrity, 'sha256': package.sha256, 'state': 'already-published'}
        # Avoid changing latest during immutable publication. Alias reconciliation
        # separately decides whether a mutable release tag may advance.
        tag = 'validated-' + package.sha256
        code, _ = command(['publish', str(candidate), '--tag', tag, '--access', 'restricted'])
        if code != 0:
            raise PublicationError('Package publication did not report success; retry verifies existing registry state')
        if not existing():
            raise PublicationError('Published package was not found during required readback')
        return {'name': name, 'version': version, 'integrity': package.integrity, 'sha256': package.sha256, 'state': 'published'}
