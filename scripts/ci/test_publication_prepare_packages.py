import hashlib
from pathlib import Path
import tarfile
import tempfile
import unittest

from publication import PublicationError
from publication_prepare_packages import prepare_packages
import test_publication_verify_packages as fixtures


class PreparationTests(unittest.TestCase):
    def setUp(self):
        fixture = fixtures.VerifyPackagesTests()
        fixture.setUp()
        self.fixture, self.catalog = fixture, fixture.catalog
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def authority(self, change=None):
        plan, client, packed = self.fixture.fixture(change)
        plan['registration'] = {'id': 60}
        client.repository = 'example/repo'
        ready = [{'release': {'id': 5, 'version': '1.0.0'}, 'component': self.catalog['components'][0], 'origin': {'asset_id': 1}, 'selection': {'asset_id': 2}, 'plan': plan}]
        return {'ready': ready, 'published': [], 'pending': [], 'target_release_id': None}, client, packed

    def scan(self, client, plan, reports):
        reports.mkdir()
        (reports / 'report.json').write_text('{}')
        return {'blocking_count': 0, 'reports': [{'file': reports.name + '/report.json', 'sha256': 'sha256:' + hashlib.sha256(b'{}').hexdigest()}]}

    def test_actual_sealed_archive_retains_only_exact_selected_package_bytes(self):
        authority, client, packed = self.authority()
        result = prepare_packages(client, authority, self.catalog, self.root / 'prepared', self.root / 'reports', scan=self.scan)
        with tarfile.open(self.root / 'prepared/packages.tar') as archive:
            self.assertEqual(archive.getnames(), ['release-5.tgz'])
            self.assertEqual(archive.extractfile('release-5.tgz').read(), packed['sdk.tgz'])
        self.assertEqual(result['candidates'][0]['package']['sha256'], hashlib.sha256(packed['sdk.tgz']).hexdigest())
        self.assertFalse(result['publication_authorized'])
        self.assertTrue(all(not path.parent.exists() for path in client.paths))

    def test_altered_seal_wrong_version_or_companion_prevents_prepared_output(self):
        for change in ('seal', 'version', 'unsafe_companion'):
            authority, client, _ = self.authority(change)
            destination = self.root / ('prepared-' + change)
            with self.subTest(change=change), self.assertRaises(PublicationError):
                prepare_packages(client, authority, self.catalog, destination, self.root / ('reports-' + change), scan=self.scan)
            self.assertFalse(destination.exists())

    def test_fresh_scan_failure_and_wrong_destination_repository_fail(self):
        authority, client, _ = self.authority()
        def blocked(*args):
            raise PublicationError('Current vulnerability blocks publication')
        with self.assertRaises(PublicationError):
            prepare_packages(client, authority, self.catalog, self.root / 'blocked', self.root / 'reports', scan=blocked)
        client.repository = 'example/fork'
        with self.assertRaises(PublicationError):
            prepare_packages(client, authority, self.catalog, self.root / 'fork', self.root / 'fork-reports', scan=self.scan)
        self.assertFalse((self.root / 'fork').exists())


if __name__ == '__main__':
    unittest.main()
