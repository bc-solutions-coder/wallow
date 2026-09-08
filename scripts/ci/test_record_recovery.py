"""The recovery finalizer retains only a freshly authenticated exact release producer."""

import copy
import unittest
from unittest.mock import patch

from publication import PublicationError
from record_recovery import record
import test_publication_release_receipts as receipts


class RecordRecoveryTests(unittest.TestCase):
    def setUp(self):
        self.client = receipts.Client()
        self.catalog = {'components': [{'id': 'platform', 'path': '.', 'tag_prefix': 'v'}]}
        self.release = {'id': 5, 'component': 'platform', 'component_path': '.', 'version': '1.0.0',
                        'tag_name': 'v1.0.0', 'commit_sha': 'a' * 40}
        self.request = {'release': self.release, 'origin': {'asset_id': 80, 'sha256': 'sha256:' + 'b' * 64},
                        'selection': {'asset_id': 90, 'sha256': 'sha256:' + 'c' * 64}}
        self.plan = {'schema': 2, 'producer': {'run_id': 70, 'run_attempt': 2, 'release_id': 5, 'source_sha': 'a' * 40},
                     'route': 'full', 'registration': {'id': 100}, 'artifacts': [{'id': 110}],
                     'inputs': {'catalog': self.catalog, 'component_versions': {'.': '1.0.0'},
                                'input_sha256': {'global.json': 'd' * 64},
                                'recovery_request': {'record': self.request, 'sha256': 'sha256:' + 'e' * 64}}}
        self.context = {'event_name': 'workflow_dispatch'}
        self.frame = patch('record_recovery.frame', return_value=(receipts.FRAME, receipts.JOB))
        self.resolve = patch('record_recovery.resolve', return_value=self.plan)
        self.jobs = patch('publication_release_receipts.recorded_job', return_value=receipts.JOB)
        for patcher in (self.frame, self.resolve, self.jobs):
            patcher.start()
            self.addCleanup(patcher.stop)

    def record(self, release_id=5):
        return record(self.client, self.context, 10, 1, 70, 2, release_id, self.catalog)

    def test_retains_original_receipt_references_and_exact_recovered_inputs(self):
        first = self.record()
        before = copy.deepcopy(self.client.bodies)
        retry = self.record()
        self.assertEqual(first, retry)
        self.assertFalse(first['publication_authorized'])
        self.assertEqual(self.client.bodies, before)
        import json
        payload = json.loads(self.client.bodies[first['asset_id']])['payload']
        self.assertEqual(payload['origin'], self.request['origin'])
        self.assertEqual(payload['selection'], self.request['selection'])
        self.assertEqual(payload['recovery']['producer'], self.plan['producer'])
        self.assertEqual(payload['recovery']['artifacts'], self.plan['artifacts'])

    def test_wrong_release_or_source_cannot_write_receipt(self):
        with self.assertRaises(PublicationError):
            self.record(6)
        self.plan['producer']['source_sha'] = 'f' * 40
        with self.assertRaises(PublicationError):
            self.record()
        self.assertEqual(self.client.writes, [])

    def test_failed_authentication_and_automatic_invocation_cannot_write(self):
        with patch('record_recovery.resolve', side_effect=PublicationError('untrusted producer')):
            with self.assertRaises(PublicationError):
                self.record()
        self.context['event_name'] = 'workflow_run'
        with self.assertRaises(PublicationError):
            self.record()
        self.assertEqual(self.client.writes, [])


if __name__ == '__main__':
    unittest.main()
