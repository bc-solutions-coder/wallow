"""Preflight the independently enabled package destination and protected job environment."""

import json
from pathlib import Path

from publication import PublicationError


def package_configuration(repository, catalog, root):
    if catalog['package_registry'] != 'https://npm.pkg.github.com' or catalog['package_scope'] != '@' + repository.split('/')[0].lower():
        raise PublicationError('Package publication requires this repository owner scope and fixed GitHub registry')
    for component in catalog['components']:
        if 'package' not in component:
            continue
        manifest = json.loads((Path(root) / component['path'] / 'package.json').read_text())
        source = manifest.get('repository')
        if not isinstance(source, dict) or source.get('type') != 'git' or source.get('url') != f'https://github.com/{repository}.git':
            raise PublicationError('Enabled package publication requires manifests linked to this repository; configure fork package ownership first')


def package_environment(client):
    environment = client.get('/environments/package-publish')
    expected = {'protected_branches': False, 'custom_branch_policies': True}
    if environment.get('name') != 'package-publish' or environment.get('deployment_branch_policy') != expected:
        raise PublicationError('package-publish must use an explicit main-only environment policy')
    rules = client.list('/environments/package-publish/deployment-branch-policies', 'branch_policies')
    if len(rules) != 1 or rules[0].get('name') != 'main' or rules[0].get('type') != 'branch':
        raise PublicationError('package-publish requires exactly the main branch and no tag/wildcard policy')
