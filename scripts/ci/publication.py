"""Validate publication configuration and exact, authorized output identities."""

import argparse
import json
from pathlib import Path
import re


class PublicationError(ValueError):
    pass


def fields(value, required, optional=()):
    if not isinstance(value, dict) or not set(required) <= value.keys() or value.keys() - set(required) - set(optional):
        raise PublicationError('Missing or unexpected publication configuration fields')


def matches(pattern, value):
    return isinstance(value, str) and re.fullmatch(pattern, value) is not None


def validate_catalog(catalog, release_config, manifests):
    fields(catalog, ('schema', 'default_branch', 'producer_workflow', 'package_registry', 'package_scope', 'components', 'images', 'docs'))
    if type(catalog['schema']) is not int or catalog['schema'] != 1:
        raise PublicationError('Unsupported publication catalog schema')
    if catalog['default_branch'] != 'main' or catalog['producer_workflow'] != '.github/workflows/ci.yml':
        raise PublicationError('Publication requires the approved main CI producer')
    if catalog['package_registry'] != 'https://npm.pkg.github.com' or not matches(r'@[a-z0-9][a-z0-9-]*', catalog['package_scope']):
        raise PublicationError('Invalid package registry or owner scope')
    components = catalog['components']
    images = catalog['images']
    if not isinstance(components, list) or not components or not isinstance(images, list) or not images:
        raise PublicationError('Publication components and images must be nonempty lists')
    if not isinstance(release_config, dict) or not isinstance(release_config.get('packages'), dict):
        raise PublicationError('Missing release component configuration')
    if not isinstance(manifests, dict):
        raise PublicationError('Missing package manifests')
    component_ids, paths, prefixes, package_names = set(), set(), set(), set()
    for component in components:
        fields(component, ('id', 'path', 'tag_prefix'), ('package',))
        component_id, path, prefix = component['id'], component['path'], component['tag_prefix']
        if not matches(r'[a-z0-9]+(?:-[a-z0-9]+)*', component_id) or component_id in component_ids:
            raise PublicationError('Invalid or duplicate release component')
        if not matches(r'\.|packages/[a-z0-9]+(?:-[a-z0-9]+)*', path) or path in paths:
            raise PublicationError('Invalid or duplicate component path')
        config = release_config['packages'].get(path)
        if not isinstance(config, dict):
            raise PublicationError('Catalog component is absent from release configuration')
        if 'package' in component:
            package = component['package']
            fields(package, ('name', 'tarball'))
            if path == '.' or package['name'] != catalog['package_scope'] + '/' + component_id:
                raise PublicationError('Published package does not match its owner scope and component')
            if package['name'] in package_names or package['tarball'] != component_id + '.tgz':
                raise PublicationError('Duplicate package or unexpected tarball name')
            manifest = manifests.get(path)
            if not isinstance(manifest, dict) or manifest.get('name') != package['name'] or manifest.get('private', False) is not False:
                raise PublicationError('Package source identity is invalid or private')
            publish_config = manifest.get('publishConfig')
            if not isinstance(publish_config, dict) or publish_config.get('registry') != catalog['package_registry']:
                raise PublicationError('Package registry differs from the publication catalog')
            if config.get('package-name') != package['name'] or config.get('component') != component_id or config.get('release-type') != 'node':
                raise PublicationError('Release Please package mapping differs from the catalog')
            package_names.add(package['name'])
        elif path != '.':
            raise PublicationError('A package-path component must declare its package')
        include_component = config.get('include-component-in-tag', release_config.get('include-component-in-tag', False))
        include_v = config.get('include-v-in-tag', release_config.get('include-v-in-tag', True))
        separator = config.get('tag-separator', release_config.get('tag-separator', '-'))
        if type(include_component) is not bool or type(include_v) is not bool or separator not in ('-', '/'):
            raise PublicationError('Unsupported release tag configuration')
        expected_prefix = (component_id + separator if include_component else '') + ('v' if include_v else '')
        if prefix != expected_prefix or prefix in prefixes:
            raise PublicationError('Release tag prefix is inconsistent or ambiguous')
        component_ids.add(component_id)
        paths.add(path)
        prefixes.add(prefix)
    if paths != set(release_config['packages']) or '.' not in paths:
        raise PublicationError('Catalog must cover every configured release component')
    image_ids, suffixes, tags = set(), set(), set()
    for image in images:
        fields(image, ('id', 'repository_suffix', 'bundle', 'component', 'tags', 'build_args'))
        if not matches(r'[a-z0-9]+(?:-[a-z0-9]+)*', image['id']) or image['id'] in image_ids:
            raise PublicationError('Invalid or duplicate image identity')
        if not matches(r'(?:-[a-z0-9]+(?:-[a-z0-9]+)*)?', image['repository_suffix']) or image['repository_suffix'] in suffixes:
            raise PublicationError('Invalid or duplicate image repository suffix')
        if image['bundle'] not in ('app', 'infra', 'docs') or image['component'] not in component_ids:
            raise PublicationError('Image references an unknown bundle or release component')
        fields(image['tags'], ('linux/amd64', 'linux/arm64'))
        for tag in image['tags'].values():
            if not matches(r'[a-z0-9][a-z0-9_.-]*:[a-zA-Z0-9_.-]+', tag) or tag in tags:
                raise PublicationError('Invalid or duplicate local image tag')
            tags.add(tag)
        if not isinstance(image['build_args'], dict) or any(not matches(r'[A-Z][A-Z0-9_]*', key) or not isinstance(value, str) for key, value in image['build_args'].items()):
            raise PublicationError('Invalid image build arguments')
        image_ids.add(image['id'])
        suffixes.add(image['repository_suffix'])
    if catalog['docs'] != {'bundle': 'docs', 'payload': 'site.tar.gz', 'variant': 'docfx'}:
        raise PublicationError('Unexpected validated documentation identity')
    return catalog


def image_repository(repository, image):
    if not matches(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repository):
        raise PublicationError('Invalid repository identity')
    return 'ghcr.io/' + repository.lower() + image['repository_suffix']


def load_catalog(root):
    root = Path(root)
    catalog = json.loads((root / '.github/ci/publication.json').read_text())
    release_config = json.loads((root / 'release-please-config.json').read_text())
    manifests = {str(path.parent.relative_to(root)): json.loads(path.read_text()) for path in (root / 'packages').glob('*/package.json')}
    return validate_catalog(catalog, release_config, manifests)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['catalog'])
    parser.add_argument('--root', default='.')
    args = parser.parse_args()
    try:
        catalog = load_catalog(args.root)
    except (PublicationError, OSError, json.JSONDecodeError) as error:
        parser.exit(1, f'Publication catalog validation failed: {error}\n')
    print(f"Validated {len(catalog['components'])} release components and {len(catalog['images'])} image variants.")


if __name__ == '__main__':
    main()
