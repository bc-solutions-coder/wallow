import unittest

from publication import PublicationError
from publication_package_dependencies import publication_order
from publication_packages import Package


class Registry:
    def __init__(self):
        self.present = {}
        self.reported = {}
        self.reads = []

    def versions(self, name):
        return self.reported.get(name, [version for package, version in self.present if package == name])

    def read(self, package):
        self.reads.append((package.name, package.version))
        digest = self.present.get((package.name, package.version))
        if digest is not None and digest != package.sha256:
            raise PublicationError('Conflicting registry bytes')
        return digest is not None


class DependencyTests(unittest.TestCase):
    def setUp(self):
        self.api = Package('@owner/api-errors', '1.0.0', 'a' * 64, 'sha512-example-api', {})
        self.sdk = Package('@owner/sdk', '2.0.0', 'b' * 64, 'sha512-example-sdk', {'@owner/api-errors': '^1.0.0', 'jose': '^6.0.0'})
        self.catalog = {'components': [{'package': {'name': package.name}} for package in (self.api, self.sdk)]}
        self.registry = Registry()

    def test_authorized_absent_dependency_is_ordered_before_dependent(self):
        result = publication_order([{'package': self.sdk}, {'package': self.api}], [], self.catalog, self.registry)
        self.assertEqual([entry['candidate']['package'].name for entry in result['ordered']], [self.api.name, self.sdk.name])
        self.assertEqual(result['dependencies'][0]['dependency_version'], '1.0.0')
        self.assertEqual(self.registry.reads, [(self.api.name, self.api.version), (self.sdk.name, self.sdk.version)])

    def test_durable_published_dependency_needs_no_expired_candidate(self):
        self.registry.present[self.api.name, self.api.version] = self.api.sha256
        result = publication_order([{'package': self.sdk}], [{'package': self.api}], self.catalog, self.registry)
        self.assertEqual([entry['candidate']['package'].name for entry in result['ordered']], [self.sdk.name])

    def test_missing_unreleased_conflicting_or_unknown_higher_dependency_fails(self):
        for mode in ('missing', 'unreleased', 'conflict', 'unknown-newer'):
            self.registry = Registry()
            published = [{'package': self.api}]
            if mode == 'unreleased':
                published = []
            elif mode == 'conflict':
                self.registry.present[self.api.name, self.api.version] = 'c' * 64
            elif mode == 'unknown-newer':
                self.registry.present[self.api.name, self.api.version] = self.api.sha256
                self.registry.reported[self.api.name] = ['1.0.0', '1.9.0']
            with self.subTest(mode=mode), self.assertRaises(PublicationError):
                publication_order([{'package': self.sdk}], published, self.catalog, self.registry)

    def test_internal_peer_range_is_also_required(self):
        sdk = Package(self.sdk.name, self.sdk.version, self.sdk.sha256, self.sdk.integrity, {}, peer_dependencies={self.api.name: '^2.0.0'})
        with self.assertRaises(PublicationError):
            publication_order([{'package': sdk}, {'package': self.api}], [], self.catalog, self.registry)

    def test_cycle_and_conflicting_same_version_provenance_fail(self):
        api = Package(self.api.name, self.api.version, self.api.sha256, self.api.integrity, {self.sdk.name: '^2.0.0'})
        with self.assertRaises(PublicationError):
            publication_order([{'package': api}, {'package': self.sdk}], [], self.catalog, self.registry)
        changed = Package(self.api.name, self.api.version, 'c' * 64, self.api.integrity, {})
        with self.assertRaises(PublicationError):
            publication_order([{'package': changed}], [{'package': self.api}], self.catalog, self.registry)


if __name__ == '__main__':
    unittest.main()
