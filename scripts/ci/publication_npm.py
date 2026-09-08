"""Publish exact authorized tarballs from an empty directory and verify registry bytes."""

import base64
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

from publication import PublicationError, matches
from publication_packages import inspect_package


class PackageRegistry:
    """Fixed GitHub registry operations; reads never publish an absent version."""

    def __init__(self, scope, token, runner=subprocess.run):
        if not matches(r'@[a-z0-9][a-z0-9-]*', scope) or not isinstance(token, str) or not token:
            raise PublicationError('Package registry requires an approved scope and credential')
        self.scope, self.token, self.runner = scope, token, runner
        self.directory = None

    def __enter__(self):
        self.directory = tempfile.TemporaryDirectory(prefix='wallow-npm-')
        self.root = Path(self.directory.name)
        config = self.root / '.npmrc'
        config.write_text(f'registry=https://npm.pkg.github.com\n{self.scope}:registry=https://npm.pkg.github.com\n//npm.pkg.github.com/:_authToken=${{NODE_AUTH_TOKEN}}\nignore-scripts=true\nprovenance=false\n')
        config.chmod(0o600)
        self.environment = {key: value for key, value in os.environ.items() if key in ('PATH', 'HOME', 'SYSTEMROOT', 'TMPDIR')}
        self.environment['NODE_AUTH_TOKEN'] = self.token
        self.base = ['npm', '--userconfig', str(config), '--globalconfig', os.devnull, '--cache', str(self.root / 'cache'), '--ignore-scripts', '--registry', 'https://npm.pkg.github.com', '--json']
        return self

    def __exit__(self, *args):
        self.directory.cleanup()

    def _run(self, arguments):
        try:
            result = self.runner(self.base + arguments, cwd=self.root, env=self.environment, capture_output=True, text=True, timeout=180)
        except (OSError, subprocess.TimeoutExpired):
            raise PublicationError('Package registry operation did not complete; verify registry state before retrying') from None
        if not isinstance(result.stdout, str) or len(result.stdout.encode()) > 1024 * 1024:
            raise PublicationError('Package registry output exceeds its bounded limit')
        return result

    def command(self, arguments):
        result = self._run(arguments)
        try:
            data = json.loads(result.stdout)
        except (TypeError, json.JSONDecodeError):
            raise PublicationError('Package registry returned malformed output') from None
        return result.returncode, data

    def versions(self, name):
        if not matches(r'@[a-z0-9][a-z0-9-]*/[a-z0-9][a-z0-9_.-]*', name) or name.split('/')[0] != self.scope:
            raise PublicationError('Dependency registry lookup belongs to another owner scope')
        code, data = self.command(['view', name, 'versions'])
        if code != 0:
            if isinstance(data, dict) and isinstance(data.get('error'), dict) and data['error'].get('code') == 'E404':
                return []
            raise PublicationError('Dependency registry versions could not be read')
        if isinstance(data, str):
            data = [data]
        if not isinstance(data, list) or len(data) > 1000 or any(not isinstance(value, str) or len(value) > 200 for value in data) or len(data) != len(set(data)):
            raise PublicationError('Dependency registry version list is malformed or unbounded')
        return data

    def read(self, package):
        if package.name.split('/')[0] != self.scope:
            raise PublicationError('Package registry lookup belongs to another owner scope')
        code, data = self.command(['view', package.name + '@' + package.version])
        if code != 0:
            if isinstance(data, dict) and isinstance(data.get('error'), dict) and data['error'].get('code') == 'E404':
                return False
            raise PublicationError('Package registry metadata could not be read')
        if not isinstance(data, dict) or data.get('name') != package.name or data.get('version') != package.version or not isinstance(data.get('dist'), dict) or data['dist'].get('integrity') != package.integrity:
            raise PublicationError('Existing package version conflicts with the validated tarball')
        with tempfile.TemporaryDirectory(prefix='readback-', dir=self.root) as directory:
            destination = Path(directory)
            code, packed = self.command(['pack', package.name + '@' + package.version, '--pack-destination', str(destination)])
            if code != 0 or not isinstance(packed, list) or len(packed) != 1 or not isinstance(packed[0], dict):
                raise PublicationError('Package registry tarball could not be read back')
            filename = packed[0].get('filename')
            if not matches(r'[A-Za-z0-9_.-]+\.tgz', filename):
                raise PublicationError('Package registry returned an unsafe tarball filename')
            path = destination / filename
            if path.is_symlink() or not path.is_file() or path.stat().st_size > 100 * 1024 * 1024 or sorted(item.name for item in destination.iterdir()) != [filename]:
                raise PublicationError('Package registry readback has an unexpected archive layout')
            with path.open('rb') as stream:
                sha256 = hashlib.file_digest(stream, 'sha256').hexdigest()
                stream.seek(0)
                integrity = 'sha512-' + base64.b64encode(hashlib.file_digest(stream, 'sha512').digest()).decode('ascii')
            if sha256 != package.sha256 or integrity != package.integrity:
                raise PublicationError('Package registry tarball bytes differ from the authorized candidate')
        return True

    def dist_tags(self, name):
        if not matches(r'@[a-z0-9][a-z0-9-]*/[a-z0-9][a-z0-9_.-]*', name) or name.split('/')[0] != self.scope:
            raise PublicationError('Package alias lookup belongs to another owner scope')
        code, data = self.command(['view', name, 'dist-tags'])
        if code != 0 or not isinstance(data, dict) or len(data) > 1000 or any(not isinstance(tag, str) or not matches(r'[A-Za-z0-9_.-]{1,200}', tag) or not isinstance(version, str) or not 0 < len(version) <= 200 for tag, version in data.items()):
            raise PublicationError('Package aliases are missing, malformed or unbounded')
        return data

    def replace_dist_tag(self, package, alias, previous):
        if alias != 'latest' and not matches(r'(?:major-(?:0|[1-9][0-9]*)|minor-(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*))', alias):
            raise PublicationError('Package alias must be latest, major-X or minor-X.Y')
        if not self.read(package):
            raise PublicationError('Package alias target is absent')
        if self.dist_tags(package.name).get(alias) != previous:
            raise PublicationError('Package alias changed after its provenance was checked')
        if previous == package.version:
            return 'identical'
        result = self._run(['dist-tag', 'add', package.name + '@' + package.version, alias])
        if result.returncode != 0 or self.dist_tags(package.name).get(alias) != package.version or not self.read(package):
            raise PublicationError('Package alias mutation or exact registry readback failed')
        return 'verified'

    def publish(self, path, package):
        if self.read(package):
            state = 'already-published'
        else:
            candidate = self.root / 'candidate.tgz'
            shutil.copyfile(path, candidate)
            with candidate.open('rb') as stream:
                if hashlib.file_digest(stream, 'sha256').hexdigest() != package.sha256:
                    raise PublicationError('Package changed before publication staging')
            try:
                # This content-derived staging tag never advances latest/major/minor.
                code, _ = self.command(['publish', str(candidate), '--tag', 'validated-' + package.sha256, '--access', 'restricted'])
                if code != 0:
                    raise PublicationError('Package publication did not report success; retry verifies existing registry state')
                if not self.read(package):
                    raise PublicationError('Published package was not found during required readback')
                state = 'published'
            finally:
                candidate.unlink(missing_ok=True)
        return {'name': package.name, 'version': package.version, 'integrity': package.integrity, 'sha256': package.sha256,
                'state': state, 'registry_bytes_verified': True}


def publish_tarball(path, name, version, repository, token, runner=subprocess.run):
    package = inspect_package(path, name, version, 'https://npm.pkg.github.com', repository)
    with PackageRegistry(name.split('/')[0], token, runner) as registry:
        return registry.publish(path, package)
