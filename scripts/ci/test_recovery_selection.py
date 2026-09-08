"""Explicit recovered inputs cannot replace or conflict with original selection authority."""

import copy
import unittest
from unittest.mock import patch

from publication import PublicationError
from recovery_selection import authorized_recovery
import test_record_recovery


class RecoverySelectionTests(unittest.TestCase):
    def setUp(self):
        self.fixture = test_record_recovery.RecordRecoveryTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.fixture.record()
        self.origin = copy.deepcopy(self.fixture.request['origin'])
        self.original = {'producer': {'run_id': 7, 'run_attempt': 1, 'source_sha': 'a' * 40}}
        self.selection = self.fixture.request['selection'] | {'record': {'payload': {
            'origin_asset_id': self.origin['asset_id'], 'origin_sha256': self.origin['sha256'],
            'release': self.fixture.release, 'selection': self.original}}}
        self.origin_patch = patch('publication_release_candidates.authenticate_origin')
        self.resolve_patch = patch('recovery_selection.resolve', return_value=self.fixture.plan)
        for patcher in (self.origin_patch, self.resolve_patch):
            patcher.start()
            self.addCleanup(patcher.stop)

    def authorize(self):
        fixture = self.fixture
        return authorized_recovery(fixture.client, fixture.context, fixture.release, self.origin,
                                   self.selection, 70, 2, fixture.catalog)

    def test_keeps_original_producer_separate_from_recovered_inputs(self):
        before = copy.deepcopy(self.selection)
        result = self.authorize()
        self.assertEqual(result['original']['producer']['run_id'], 7)
        self.assertEqual(result['plan']['producer']['run_id'], 70)
        self.assertEqual(result['receipt']['name'], 'wallow-recovery-v1-70-2.json')
        self.assertEqual(self.selection, before)
        self.assertEqual(len(self.fixture.client.writes), 1)

    def test_changed_original_receipt_reference_is_rejected(self):
        self.selection['asset_id'] = 91
        with self.assertRaises(PublicationError):
            self.authorize()

    def test_missing_receipt_or_changed_recovered_artifacts_is_rejected(self):
        self.fixture.plan['artifacts'] = [{'id': 111}]
        with self.assertRaises(PublicationError):
            self.authorize()
        self.fixture.client.assets.clear()
        with self.assertRaises(PublicationError):
            self.authorize()

    def test_original_selected_source_cannot_be_replaced(self):
        self.original['producer']['source_sha'] = 'f' * 40
        with self.assertRaises(PublicationError):
            self.authorize()


if __name__ == '__main__':
    unittest.main()
