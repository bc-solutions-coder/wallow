"""Stable release alias names and monotonic version/source decisions."""

import re

from publication import PublicationError

STABLE = re.compile(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)')


def stable_version(release):
    version = release.get('version')
    if not isinstance(version, str) or len(version) > 128 or '+' in version:
        raise PublicationError('Release alias version is invalid or unsupported')
    if release.get('prerelease') is True or '-' in version:
        return None
    match = STABLE.fullmatch(version)
    if match is None:
        raise PublicationError('Release alias requires a canonical stable component version')
    return tuple(int(value) for value in match.groups())


def aliases(release, kind):
    version = stable_version(release)
    if version is None:
        return []
    major, minor, _ = version
    if kind == 'image':
        return ['latest', str(major), f'{major}.{minor}']
    if kind == 'package':
        return ['latest', f'major-{major}', f'minor-{major}.{minor}']
    raise PublicationError('Unknown release alias capability')
