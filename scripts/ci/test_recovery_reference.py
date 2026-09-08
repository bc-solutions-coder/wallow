"""Completed publication can verify recovery lineage from durable assets alone."""

import unittest

from publication import PublicationError
from recovery_reference import verify_reference
import test_record_recovery


class RecoveryReferenceTests(unittest.TestCase):
    def setUp(self):
        self.fixture = test_record_recovery.RecordRecoveryTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        result = self.fixture.record()
        self.reference = {key: result[key] for key in ('asset_id', 'name', 'sha256')}

    def verify(self, reference=None, release=None, origin=None, selection=None):
        fixture = self.fixture
        return verify_reference(fixture.client, reference or self.reference, release or fixture.release,
                                origin or fixture.request['origin'], selection or fixture.request['selection'])

    def test_verifies_actual_retained_asset_without_actions_artifacts(self):
        receipt = self.verify()
        self.assertEqual(receipt['asset_id'], self.reference['asset_id'])
        self.assertEqual(receipt['sha256'], self.reference['sha256'])

    def test_rejects_changed_asset_id_digest_or_producer_name(self):
        for change in ({'asset_id': 2}, {'sha256': 'sha256:' + 'f' * 64}, {'name': 'wallow-recovery-v1-70-1.json'},
                       {'asset_id': True}, {'unexpected': True}):
            with self.subTest(change=change), self.assertRaises(PublicationError):
                self.verify(reference=self.reference | change)

    def test_rejects_changed_original_release_or_receipt_references(self):
        for change in ({'release': self.fixture.release | {'commit_sha': 'f' * 40}},
                       {'origin': self.fixture.request['origin'] | {'asset_id': 81}},
                       {'selection': self.fixture.request['selection'] | {'asset_id': 91}}):
            with self.subTest(change=change), self.assertRaises(PublicationError):
                self.verify(**change)


if __name__ == '__main__':
    unittest.main()
