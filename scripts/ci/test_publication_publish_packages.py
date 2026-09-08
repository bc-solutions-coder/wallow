import hashlib
import unittest
from unittest.mock import patch

from publication import PublicationError
from publication_publish_packages import publish
from test_publication_verify_packages import VerifyPackagesTests


class Registry:
    def __init__(self):
        self.present, self.tags, self.writes = {}, {}, []

    def __enter__(self):
        return self

    def __exit__(self, *args):
        pass

    def read(self, package):
        digest = self.present.get(package.name)
        if digest is not None and digest != package.sha256:
            raise PublicationError('Conflicting immutable version')
        return digest is not None

    def satisfies(self, name, requirement):
        return name in self.present

    def publish(self, path, package):
        assert hashlib.sha256(path.read_bytes()).hexdigest() == package.sha256
        if not self.read(package):
            self.present[package.name] = package.sha256
            self.writes.append(package.name)
        return {'name': package.name}

    def dist_tags(self, name, version):
        return self.tags.get(name, {})

    def replace_dist_tag(self, package, alias, previous):
        self.tags.setdefault(package.name, {})[alias] = package.version
        return 'verified'


class WriterTests(unittest.TestCase):
    def setUp(self):
        fixture = VerifyPackagesTests()
        fixture.setUp()
        self.plan, self.client, _ = fixture.fixture()
        self.client.repository = 'example/repo'
        self.catalog = fixture.catalog | {'package_scope': '@example'}
        self.registry = Registry()
        self.releases = [{'component': name, 'version': '1.0.0', 'prerelease': False} for name in ('base', 'sdk')]

    def run_writer(self):
        with patch('publication_publish_packages.selected_releases', return_value=(self.releases, self.releases)), \
             patch('publication_publish_packages.PackageRegistry', return_value=self.registry):
            return publish(self.client, self.plan, self.catalog, 'credential')

    def test_dependency_order_and_identical_retry(self):
        self.run_writer()
        self.run_writer()
        self.assertEqual(self.registry.writes, ['@example/base', '@example/sdk'])
        self.assertEqual(self.registry.tags['@example/sdk']['latest'], '1.0.0')

    def test_wrong_release_version_is_rejected_before_publication(self):
        self.releases[0]['version'] = '2.0.0'
        with self.assertRaises(PublicationError):
            self.run_writer()
        self.assertEqual(self.registry.writes, [])

    def test_missing_dependency_prevents_dependent_publication(self):
        self.releases = self.releases[1:]
        with self.assertRaises(PublicationError):
            self.run_writer()
        self.assertEqual(self.registry.writes, [])

    def test_conflicting_version_is_not_replaced(self):
        self.registry.present['@example/base'] = 'different-bytes'
        with self.assertRaises(PublicationError):
            self.run_writer()
        self.assertEqual(self.registry.writes, [])
