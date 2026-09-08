"""Durable package receipts bind their effective producer to recovery authority."""

import unittest
from unittest.mock import patch

from publication import PublicationError
from publication_package_records import published_package
from publication_publish_packages import entry
import test_publication_record_packages


class PackageRecoveryLineageTests(unittest.TestCase):
    def setUp(self):
        self.fixture = test_publication_record_packages.FinalizerTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.payload = self.fixture.entries[0] | {'writer': self.fixture.reference,
            'recovery': {'receipt': {'asset_id': 90}, 'producer': {'run_id': 70, 'run_attempt': 2}}}
        for key in ('origin', 'selection'):
            self.payload[key] = self.payload[key] | {'sha256': 'sha256:' + 'd' * 64}
        self.receipt = {'asset_id': 100, 'sha256': 'exact', 'record': {'payload': self.payload}}

    def read(self):
        payload = self.payload
        return published_package(self.fixture.client, payload['release'], {'package': {'name': payload['package']['name']}},
                                 payload['origin'], payload['selection'], self.receipt)

    def test_completed_package_checks_durable_recovery_and_preserves_it(self):
        producer = dict(self.payload['recovery']['producer'])
        with patch('publication_package_records.verify_frame'), \
             patch('publication_package_records.verify_reference', return_value={'record': {'payload': {'recovery': {'producer': producer}}}}) as verify:
            result = self.read()
            self.assertEqual(result['recovery'], self.payload['recovery'])
            verify.assert_called_once()
            self.assertEqual(entry(result, self.payload['readback'])['recovery'], self.payload['recovery'])
            self.payload['recovery']['producer']['run_id'] = 71
            with self.assertRaises(PublicationError):
                self.read()


if __name__ == '__main__':
    unittest.main()
