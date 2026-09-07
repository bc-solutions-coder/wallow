import copy
import unittest

from publication import PublicationError, image_repository, validate_catalog


class CatalogTests(unittest.TestCase):
    def setUp(self):
        self.catalog = {
            'schema': 1,
            'default_branch': 'main',
            'producer_workflow': '.github/workflows/ci.yml',
            'package_registry': 'https://npm.pkg.github.com',
            'package_scope': '@example',
            'components': [
                {'id': 'platform', 'path': '.', 'tag_prefix': 'v'},
                {'id': 'sdk', 'path': 'packages/sdk', 'tag_prefix': 'sdk-v', 'package': {'name': '@example/sdk', 'tarball': 'sdk.tgz'}},
            ],
            'images': [{'id': 'api', 'repository_suffix': '', 'bundle': 'app', 'component': 'platform', 'tags': {'linux/amd64': 'candidate:test', 'linux/arm64': 'candidate:test-arm64'}, 'build_args': {}}],
            'docs': {'bundle': 'docs', 'payload': 'site.tar.gz', 'variant': 'docfx'},
        }
        self.release = {'release-type': 'simple', 'packages': {'.': {}, 'packages/sdk': {'release-type': 'node', 'package-name': '@example/sdk', 'component': 'sdk', 'include-component-in-tag': True}}}
        self.manifests = {'packages/sdk': {'name': '@example/sdk', 'publishConfig': {'registry': 'https://npm.pkg.github.com'}}}

    def test_fork_catalog_resolves_only_fork_owned_registry_names(self):
        catalog = validate_catalog(self.catalog, self.release, self.manifests)
        self.assertEqual(image_repository('Example/My-Fork', catalog['images'][0]), 'ghcr.io/example/my-fork')
        image = {**catalog['images'][0], 'repository_suffix': '-auth'}
        self.assertEqual(image_repository('Example/My-Fork', image), 'ghcr.io/example/my-fork-auth')

    def test_rejects_unconfirmed_scope_private_package_and_other_registry(self):
        for change in [{'name': '@upstream/sdk'}, {'private': True}, {'publishConfig': {'registry': 'https://registry.npmjs.org'}}]:
            with self.subTest(change=change):
                manifests = copy.deepcopy(self.manifests)
                manifests['packages/sdk'].update(change)
                with self.assertRaises(PublicationError):
                    validate_catalog(self.catalog, self.release, manifests)

    def test_rejects_ambiguous_or_unmapped_release_identity(self):
        for key, value in [('component', 'other'), ('package-name', '@example/other'), ('include-component-in-tag', False), ('include-v-in-tag', False)]:
            with self.subTest(key=key):
                release = copy.deepcopy(self.release)
                release['packages']['packages/sdk'][key] = value
                with self.assertRaises(PublicationError):
                    validate_catalog(self.catalog, release, self.manifests)
        release = copy.deepcopy(self.release)
        release['packages']['packages/new'] = {}
        with self.assertRaises(PublicationError):
            validate_catalog(self.catalog, release, self.manifests)

    def test_rejects_missing_platform_and_unknown_component(self):
        for change in [{'tags': {'linux/amd64': 'candidate:test'}}, {'component': 'unknown'}, {'tags': {'linux/amd64': 'candidate:test', 'linux/arm64': 'candidate:test'}}, {'repository_suffix': '/upstream'}]:
            with self.subTest(change=change):
                catalog = copy.deepcopy(self.catalog)
                catalog['images'][0].update(change)
                with self.assertRaises(PublicationError):
                    validate_catalog(catalog, self.release, self.manifests)

    def test_rejects_duplicates_and_unknown_configuration(self):
        for field in ['components', 'images']:
            catalog = copy.deepcopy(self.catalog)
            catalog[field].append(copy.deepcopy(catalog[field][0]))
            with self.assertRaises(PublicationError):
                validate_catalog(catalog, self.release, self.manifests)
        for change in [{'schema': True}, {'default_branch': 'feature'}, {'producer_workflow': '.github/workflows/foreign.yml'}, {'unknown': 'value'}]:
            with self.subTest(change=change), self.assertRaises(PublicationError):
                validate_catalog({**self.catalog, **change}, self.release, self.manifests)


if __name__ == '__main__':
    unittest.main()
