import copy
from dataclasses import asdict
import unittest
from unittest.mock import patch

from publication import PublicationError
from publication_record_packages import finalize
from publication_package_records import package_record
import test_publication_publish_packages as fixtures


class FinalizerTests(unittest.TestCase):
    def setUp(self):
        fixture = fixtures.WriterTests()
        fixture.setUp()
        self.addCleanup(fixture.temp.cleanup)
        self.client, self.plan, _, _ = fixture.prepared()
        self.catalog, self.root = fixture.catalog, fixture.root
        self.plan['prepared']['published'] = []
        self.entries = []
        for candidate in self.plan['prepared']['candidates']:
            package = package_record(candidate['package'], candidate['package']['name'], '1.0.0', self.client.repository)
            self.entries.append({key: candidate[key] for key in ('release', 'origin', 'selection', 'package')} | {
                'readback': {'name': package.name, 'version': package.version, 'sha256': package.sha256, 'integrity': package.integrity,
                             'state': 'published', 'registry_bytes_verified': True}})
        self.reference = {'frame': {'job_id': 100}, 'artifact': {'id': 200}, 'sha256': 'sha256:' + '1' * 64}
        self.previous = None
        self.retained = []
        self.revalidated = []

    def run_finalizer(self):
        def retain(client, release_id, name, payload, current):
            self.retained.append((release_id, copy.deepcopy(payload)))
            return {'asset_id': release_id + 1000, 'sha256': 'sha256:' + '2' * 64}
        with patch('publication_record_packages.frame', return_value=({'job_id': 101}, {})), \
             patch('publication_record_packages.writer_progress', return_value=({'entries': self.entries}, self.reference)), \
             patch('publication_record_packages.authorize_packages', return_value=(self.plan, None, None, None)), \
             patch('publication_record_packages.inspect_receipt', side_effect=lambda client, release_id, name: self.previous if release_id == 1 else None), \
             patch('publication_record_packages.published_package'), \
             patch('publication_record_packages.endorsed', return_value=False), \
             patch('publication_record_packages.revalidate_partial_receipt', side_effect=lambda client, receipt: self.revalidated.append(receipt)), \
             patch('publication_record_packages.retain', side_effect=retain):
            return finalize(self.client, {}, 1, 1, 2, 1, self.catalog, self.root, {'receipts': []})

    def test_exact_readback_receipts_cover_all_authorized_versions(self):
        result = self.run_finalizer()
        self.assertEqual([item['release_id'] for item in result['receipts']], [1, 2])
        self.assertEqual(self.retained[0][1], self.entries[0] | {'writer': self.reference})

    def test_failed_finalizer_adoption_keeps_original_writer_and_payload(self):
        original = self.entries[0] | {'writer': {'frame': {'job_id': 50}, 'artifact': {'id': 60}, 'sha256': 'sha256:' + '3' * 64}}
        self.previous = {'record': {'payload': copy.deepcopy(original)}}
        self.run_finalizer()
        self.assertEqual(self.revalidated, [self.previous])
        self.assertEqual(self.retained[0][1], original)
        self.assertNotEqual(self.retained[0][1]['writer'], self.reference)

    def test_incomplete_progress_or_mismatched_identity_cannot_write_receipt(self):
        original = copy.deepcopy(self.entries)
        for change in ('missing', 'digest', 'selection', 'readback'):
            self.entries = copy.deepcopy(original)
            if change == 'missing':
                self.entries.pop()
            elif change == 'readback':
                self.entries[0]['readback']['registry_bytes_verified'] = False
            elif change == 'digest':
                self.entries[0]['package']['sha256'] = '0' * 64
            else:
                self.entries[0]['selection']['asset_id'] = 999
            with self.subTest(change=change), self.assertRaises(PublicationError):
                self.run_finalizer()
            self.assertEqual(self.retained, [])


if __name__ == '__main__':
    unittest.main()
