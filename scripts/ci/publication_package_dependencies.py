"""Preflight every internal dependency and order only explicitly authorized package writes."""

from publication import PublicationError
from publication_package_ranges import satisfying_versions


def publication_order(candidates, published, catalog, registry, targets=None):
    """Inputs already carry release authority; published records do not require old artifacts."""
    names = {item['package']['name'] for item in catalog['components'] if 'package' in item}
    available, writable = {}, {}
    for record in published:
        package = record['package']
        key = package.name, package.version
        if package.name not in names or key in available:
            raise PublicationError('Published package provenance contains an unknown or duplicate identity')
        available[key] = record
    for record in candidates:
        package = record['package']
        key = package.name, package.version
        if package.name not in names or key in writable:
            raise PublicationError('Authorized package batch contains an unknown or duplicate identity')
        if key in available and (available[key]['package'].sha256 != package.sha256 or available[key]['package'].integrity != package.integrity):
            raise PublicationError('Candidate conflicts with durable published package bytes')
        available[key], writable[key] = record, record
    selected = set(writable) if targets is None else set(targets)
    if not selected <= available.keys():
        raise PublicationError('Requested package has no explicit release authority')
    reads, versions, visiting, visited, ordered, dependencies = {}, {}, set(), set(), [], []

    def read(key):
        if key not in reads:
            reads[key] = registry.read(available[key]['package'])
        return reads[key]

    def visit(key):
        if key in visiting:
            raise PublicationError('Authorized package dependency graph contains a cycle')
        if key in visited:
            return
        visiting.add(key)
        package = available[key]['package']
        for dependency in sorted(package.dependencies.keys() | package.peer_dependencies.keys()):
            if dependency not in names:
                continue
            requirements = [mapping[dependency] for mapping in (package.dependencies, package.peer_dependencies) if dependency in mapping]
            if dependency not in versions:
                versions[dependency] = registry.versions(dependency)
            proposed = {version for name, version in writable if name == dependency}
            choices = sorted(set(versions[dependency]) | proposed)
            for requirement in requirements:
                choices = satisfying_versions(choices, requirement)
            if not choices:
                raise PublicationError('Internal dependency has no available authorized release satisfying its packed range')
            dependency_key = dependency, choices[0]
            if dependency_key not in available:
                raise PublicationError('Registry dependency range resolves to a version without durable release authority')
            visit(dependency_key)
            dependencies.append({'package': package.name, 'version': package.version, 'dependency': dependency, 'dependency_version': dependency_key[1], 'requirements': requirements})
        exists = read(key)
        if not exists and key not in writable:
            raise PublicationError('Previously published internal dependency is unavailable; explicit recovery is required')
        visiting.remove(key)
        visited.add(key)
        if key in writable:
            ordered.append({'candidate': writable[key], 'already_published': exists})

    for key in sorted(selected):
        visit(key)
    return {'ordered': ordered, 'dependencies': dependencies, 'verified': [available[key] for key in sorted(visited)]}
