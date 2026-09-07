"""Inspect already-packed candidates without installing or executing package code."""

import base64
from dataclasses import dataclass
import gzip
import hashlib
import json
from pathlib import Path
import tarfile

from publication import PublicationError, matches
from publication_archives import bounded_tar


@dataclass(frozen=True)
class Package:
    name: str
    version: str
    sha256: str
    integrity: str
    dependencies: dict


def inspect_package(path, name, version, registry, repository):
    number = r'(?:0|[1-9][0-9]*)'
    prerelease = r'(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)'
    semver = rf'{number}\.{number}\.{number}(?:-{prerelease}(?:\.{prerelease})*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?'
    if not matches(r'@[a-z0-9][a-z0-9-]*/[a-z0-9][a-z0-9_.-]*', name) or not matches(semver, version):
        raise PublicationError('An authorized package name and version are required')
    if registry != 'https://npm.pkg.github.com':
        raise PublicationError('Unexpected package publication registry')
    if not matches(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repository) or name.split('/')[0] != '@' + repository.split('/')[0].lower():
        raise PublicationError('Package scope must belong to the target repository owner')
    path = Path(path)
    if path.is_symlink() or not path.is_file() or path.stat().st_size > 100 * 1024 * 1024:
        raise PublicationError('Package must be a bounded regular tarball')
    seen, total, manifest = set(), 0, None
    try:
        with bounded_tar(path, 250 * 1024 * 1024) as bundle:
            for member in bundle:
                parts = member.name.split('/')
                if member.name in seen or parts[0] != 'package' or any(part in ('', '.', '..') for part in parts) or '\\' in member.name or member.issparse() or not (member.isfile() or member.isdir()):
                    raise PublicationError('Package contains duplicate or unsafe archive members')
                seen.add(member.name)
                total += member.size
                if len(seen) > 20000 or total > 250 * 1024 * 1024:
                    raise PublicationError('Package archive exceeds its unpacked limits')
                if member.name == 'package/package.json':
                    if not member.isfile() or member.size > 1024 * 1024:
                        raise PublicationError('Invalid packed package manifest')
                    with bundle.extractfile(member) as stream:
                        manifest = json.load(stream)
    except (tarfile.TarError, gzip.BadGzipFile, UnicodeDecodeError, json.JSONDecodeError, EOFError):
        raise PublicationError('Package archive could not be inspected') from None
    if not isinstance(manifest, dict) or manifest.get('name') != name or manifest.get('version') != version or manifest.get('private', False) is not False:
        raise PublicationError('Packed package identity differs from its authorized release')
    config = manifest.get('publishConfig')
    if not isinstance(config, dict) or config.get('registry') != registry:
        raise PublicationError('Packed package registry differs from the approved target')
    if set(config) - {'registry', 'access', 'exports'} or config.get('access', 'restricted') != 'restricted':
        raise PublicationError('Packed publish configuration may not override controlled publishing options')
    source_repository = manifest.get('repository')
    if not isinstance(source_repository, dict) or source_repository.get('type') != 'git' or source_repository.get('url') != f'https://github.com/{repository}.git':
        raise PublicationError('Packed package must link to the target GitHub repository')
    dependencies = {}
    for field in ('dependencies', 'optionalDependencies', 'peerDependencies'):
        values = manifest.get(field, {})
        if not isinstance(values, dict):
            raise PublicationError('Invalid packed dependency manifest')
        for dependency, requirement in values.items():
            if not matches(r'(?:@[a-z0-9][a-z0-9-]*/)?[a-z0-9][a-z0-9_.-]*', dependency) or not isinstance(requirement, str) or not requirement or any(marker in requirement for marker in (':', '/', '\\', '\n', '\r')):
                raise PublicationError('Packed dependencies must resolve to registry version requirements')
            if field in ('dependencies', 'optionalDependencies'):
                dependencies[dependency] = requirement
    with path.open('rb') as stream:
        sha256 = hashlib.file_digest(stream, 'sha256').hexdigest()
        stream.seek(0)
        integrity = 'sha512-' + base64.b64encode(hashlib.file_digest(stream, 'sha512').digest()).decode('ascii')
    return Package(name, version, sha256, integrity, dependencies)


def dependency_order(packages):
    """Order only authorized candidates; registry readiness is checked separately."""
    by_name = {package.name: package for package in packages}
    if len(by_name) != len(packages):
        raise PublicationError('Duplicate package publication candidates')
    result, visiting, visited = [], set(), set()

    def visit(name):
        if name in visiting:
            raise PublicationError('Authorized package dependencies contain a cycle')
        if name in visited:
            return
        visiting.add(name)
        for dependency in sorted(by_name[name].dependencies):
            if dependency in by_name:
                visit(dependency)
        visiting.remove(name)
        visited.add(name)
        result.append(by_name[name])

    for name in sorted(by_name):
        visit(name)
    return result
