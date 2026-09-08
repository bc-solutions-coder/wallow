"""Publish release packages from one successful main CI run, without durable receipts."""

import argparse
import json
import os
from pathlib import Path

from publication import PublicationError, load_catalog
from publication_github import GitHub
from publication_npm import PackageRegistry
from publication_packages import inspect_package
from publication_plan import resolve
from publication_releases import current_aliases, selected_releases, stable_version
from publication_verify_packages import verify_packages


def publish(client, plan, catalog, token, release_id=None):
    selected, history = selected_releases(client, catalog, plan['producer']['source_sha'], release_id)
    selected = {release['component']: release for release in selected}
    results = []

    def write(directory, candidates, packages):
        by_name = {component['package']['name']: (tarball, component) for tarball, component in candidates.items()}
        with PackageRegistry(catalog['package_scope'], token) as registry:
            for package in packages:
                tarball, component = by_name[package.name]
                release = selected.get(component['id'])
                if release is None:
                    continue
                if release['version'] != package.version:
                    raise PublicationError('Release tag version differs from the validated package')
                candidate = inspect_package(directory / tarball, package.name, package.version,
                                            catalog['package_registry'], client.repository)
                for dependency in packages:
                    if dependency.name in package.dependencies and not registry.read(dependency):
                        raise PublicationError('Required internal dependency has not been published')
                entry = registry.publish(directory / tarball, candidate)
                entry['aliases'] = {}
                for alias in current_aliases(release, history, 'package'):
                    previous = registry.dist_tags(package.name, package.version).get(alias)
                    before = stable_version({'version': previous}) if previous is not None else None
                    if before is not None and before > stable_version(release):
                        entry['aliases'][alias] = 'skip-older'
                        continue
                    entry['aliases'][alias] = registry.replace_dist_tag(candidate, alias, previous)
                results.append(entry)

    verify_packages(client, plan, catalog, prepare=write)
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--producer-run', type=int, required=True)
    parser.add_argument('--producer-attempt', type=int, required=True)
    parser.add_argument('--release-id', type=int)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    try:
        if os.environ.get('ENABLE_PACKAGE_PUBLISH') != 'true':
            raise PublicationError('Package publishing is disabled')
        context = {key: os.environ.get('GITHUB_' + key.upper(), '') for key in ('repository', 'ref', 'workflow_ref', 'workflow_sha', 'event_name')}
        client = GitHub(context['repository'], os.environ.get('GH_TOKEN'))
        plan = resolve(client, context, args.producer_run, args.producer_attempt)
        result = publish(client, plan, load_catalog(Path(__file__).resolve().parents[2]), os.environ['GH_TOKEN'], args.release_id)
        output = Path(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(result, indent=2) + '\n')
    except (PublicationError, OSError, ValueError, KeyError) as error:
        parser.exit(1, f'Package publication failed: {error}\n')


if __name__ == '__main__':
    main()
