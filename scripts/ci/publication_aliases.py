"""Stable release alias names and monotonic version/source decisions."""

import re

from publication import PublicationError, main_ancestor

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


def alias_action(alias, kind, previous, candidate, comparison=None):
    release = candidate['release']
    if alias not in aliases(release, kind):
        raise PublicationError('Alias does not belong to the candidate stable version scope')
    if previous is None:
        return 'advance'
    old = previous['release']
    if old['component'] != release['component'] or alias not in aliases(old, kind):
        raise PublicationError('Previous alias belongs to another component or version scope')
    before, after = stable_version(old), stable_version(release)
    if before == after:
        if previous != candidate:
            raise PublicationError('Same-version alias conflicts with immutable release identity')
        return 'identical'
    base, target = old['commit_sha'], release['commit_sha']
    if base == target:
        raise PublicationError('Different component versions claim one source revision')
    ahead = main_ancestor(comparison, base)
    behind = isinstance(comparison, dict) and comparison.get('status') == 'behind' and comparison.get('base_commit', {}).get('sha') == base and comparison.get('merge_base_commit', {}).get('sha') == target
    if not ahead and not behind:
        raise PublicationError('Alias release sources are incomparable or unresolved')
    if after < before:
        return 'skip-older'
    if not ahead:
        raise PublicationError('Newer component version precedes the current alias source')
    return 'advance'
