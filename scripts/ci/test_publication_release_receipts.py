import copy
import hashlib
import unittest
from unittest.mock import patch

from publication import PublicationError
from publication_release_github import ACTIONS_ACTOR
from publication_release_receipts import IMAGE, ORIGIN, PACKAGE, PACKAGE_JOB, SELECTION, find_receipt, inspect_receipt, retain, write_receipt


FRAME = {'run_id': 10, 'run_attempt': 1, 'job_id': 100}
JOB = {'started_at': '2026-09-07T01:00:00Z', 'completed_at': '2026-09-07T02:00:00Z', 'conclusion': 'success'}


class Client:
    def __init__(self):
        self.assets, self.bodies = [], {}
        self.writes = []
        self.corrupt = False

    def array(self, path):
        return copy.deepcopy(self.assets)

    def upload(self, release_id, name, body):
        if any(asset['name'] == name for asset in self.assets):
            raise PublicationError('Duplicate asset')
        ident = len(self.assets) + 1
        asset = {'id': ident, 'name': name, 'state': 'uploaded', 'content_type': 'application/json', 'uploader': ACTIONS_ACTOR,
                 'created_at': '2026-09-07T01:10:00Z', 'updated_at': '2026-09-07T01:10:01Z', 'size': len(body), 'digest': 'sha256:' + hashlib.sha256(body).hexdigest()}
        self.assets.append(asset)
        self.bodies[ident] = body
        self.writes.append((release_id, name, body))
        return copy.deepcopy(asset)

    def get(self, path):
        return copy.deepcopy(next(asset for asset in self.assets if asset['id'] == int(path.rsplit('/', 1)[1])))

    def asset_bytes(self, asset):
        return self.bodies[asset['id']] + (b' ' if self.corrupt else b'')


class ReceiptTests(unittest.TestCase):
    def setUp(self):
        self.client = Client()
        self.jobs = {10: JOB}
        self.job_patch = patch('publication_release_receipts.recorded_job', side_effect=lambda client, frame, name: self.jobs[frame['run_id']])
        self.job_patch.start()
        self.addCleanup(self.job_patch.stop)

    def test_write_readback_identical_retry_and_conflict(self):
        payload = {'release': 5, 'producer': {'run_id': 7}}
        first = retain(self.client, 5, ORIGIN, payload, (FRAME, JOB))
        retry = retain(self.client, 5, ORIGIN, payload, ({**FRAME, 'run_id': 11}, JOB))
        self.assertEqual(first['sha256'], retry['sha256'])
        self.assertEqual(len(self.client.writes), 1)
        with self.assertRaises(PublicationError):
            retain(self.client, 5, ORIGIN, {'producer': {'run_id': 8}}, (FRAME, JOB))

    def test_failed_writer_requires_new_successful_endorsement_preserving_bytes(self):
        first = retain(self.client, 5, SELECTION, {'producer': {'run_id': 7}}, (FRAME, JOB))
        original = self.client.bodies[1]
        self.jobs[10] = {**JOB, 'conclusion': 'failure'}
        with self.assertRaises(PublicationError):
            find_receipt(self.client, 5, SELECTION)
        next_frame = {**FRAME, 'run_id': 11}
        self.jobs[11] = {**JOB, 'conclusion': 'failure'}
        retain(self.client, 5, SELECTION, {'producer': {'run_id': 7}}, (next_frame, JOB))
        self.assertEqual(self.client.bodies[1], original)
        with self.assertRaises(PublicationError):
            find_receipt(self.client, 5, SELECTION)
        self.jobs[11] = JOB
        accepted = find_receipt(self.client, 5, SELECTION)
        self.assertEqual(accepted['sha256'], first['sha256'])
        self.assertEqual(len(self.client.writes), 2)

    def test_package_receipt_and_recovery_require_separate_package_finalizer(self):
        names = []
        with patch('publication_release_receipts.recorded_job', side_effect=lambda client, frame, name: names.append(name) or self.jobs[frame['run_id']]):
            retain(self.client, 5, PACKAGE, {'package': 'exact bytes'}, (FRAME, JOB))
            self.jobs[10] = {**JOB, 'conclusion': 'failure'}
            self.jobs[11] = JOB
            retain(self.client, 5, PACKAGE, {'package': 'exact bytes'}, ({**FRAME, 'run_id': 11}, JOB))
            self.assertIsNotNone(find_receipt(self.client, 5, PACKAGE))
        self.assertEqual(set(names), {PACKAGE_JOB})
        self.assertEqual(self.client.assets[1]['name'], 'wallow-package-endorsement-v1-1-11-1.json')

    def test_image_receipt_is_bound_to_exact_release_matrix_finalizer(self):
        names = []
        with patch('publication_release_receipts.recorded_job', side_effect=lambda client, frame, name: names.append(name) or self.jobs[frame['run_id']]):
            retain(self.client, 5, IMAGE, {'images': 'exact digests'}, (FRAME, JOB))
            self.jobs[10] = {**JOB, 'conclusion': 'failure'}
            self.jobs[11] = JOB
            retain(self.client, 5, IMAGE, {'images': 'exact digests'}, ({**FRAME, 'run_id': 11}, JOB))
            self.assertIsNotNone(find_receipt(self.client, 5, IMAGE))
        self.assertEqual(set(names), {'Record image publication (5)'})
        self.assertEqual(self.client.assets[1]['name'], 'wallow-image-endorsement-v1-1-11-1.json')

    def test_alias_observation_uses_its_exact_kind_and_invocation_job(self):
        names = []
        with patch('publication_release_receipts.recorded_job', side_effect=lambda client, frame, name: names.append(name) or self.jobs[frame['run_id']]):
            write_receipt(self.client, 5, 'wallow-alias-image-10-1.json', {'authority': 'immutable-publication-receipt-only'}, (FRAME, JOB))
            self.assertIsNotNone(inspect_receipt(self.client, 5, 'wallow-alias-image-10-1.json'))
        self.assertEqual(names, ['Record image aliases'])
        with self.assertRaises(PublicationError):
            write_receipt(self.client, 5, 'wallow-alias-package-11-1.json', {}, (FRAME, JOB))

    def test_wrong_actor_late_edit_and_changed_body_rejected(self):
        retain(self.client, 5, ORIGIN, {'release': 5}, (FRAME, JOB))
        original = copy.deepcopy(self.client.assets[0])
        for field, value in [('uploader', {**ACTIONS_ACTOR, 'id': 8}), ('updated_at', '2026-09-08T01:00:00Z')]:
            self.client.assets[0] = {**original, field: value}
            with self.assertRaises(PublicationError):
                inspect_receipt(self.client, 5, ORIGIN)
        self.client.assets[0] = original
        self.client.corrupt = True
        with self.assertRaises(PublicationError):
            inspect_receipt(self.client, 5, ORIGIN)

    def test_recovery_receipt_retry_preserves_original_selection_and_failed_writer_bytes(self):
        names = []
        name = 'wallow-recovery-v1-70-2.json'
        payload = {'origin': {'asset_id': 1}, 'selection': {'asset_id': 2},
                   'recovery': {'producer': {'run_id': 70, 'run_attempt': 2}}}
        retain(self.client, 5, ORIGIN, {'original': 'origin'}, (FRAME, JOB))
        retain(self.client, 5, SELECTION, {'original': 'selection'}, (FRAME, JOB))
        with patch('publication_release_receipts.recorded_job', side_effect=lambda client, frame, job_name: names.append(job_name) or self.jobs[frame['run_id']]):
            first = retain(self.client, 5, name, payload, (FRAME, JOB))
            originals = copy.deepcopy(self.client.bodies)
            self.jobs[10] = {**JOB, 'conclusion': 'failure'}
            with self.assertRaises(PublicationError):
                find_receipt(self.client, 5, name)
            self.jobs[11] = JOB
            retain(self.client, 5, name, payload, ({**FRAME, 'run_id': 11}, JOB))
            accepted = find_receipt(self.client, 5, name)
            self.assertEqual(accepted['sha256'], first['sha256'])
            self.assertEqual({key: self.client.bodies[key] for key in originals}, originals)
            self.assertEqual(self.client.assets[-1]['name'], 'wallow-recovery-endorsement-v1-3-11-1.json')
            count = len(self.client.writes)
            retain(self.client, 5, name, payload, ({**FRAME, 'run_id': 11}, JOB))
            self.assertEqual(len(self.client.writes), count)
        self.assertEqual(set(names), {'Record recovered producer'})

    def test_recovery_receipt_rejects_wrong_producer_name_and_immutable_conflict(self):
        name = 'wallow-recovery-v1-70-2.json'
        payload = {'recovery': {'producer': {'run_id': 70, 'run_attempt': 2}}}
        with self.assertRaises(PublicationError):
            retain(self.client, 5, 'wallow-recovery-v1-70-1.json', payload, (FRAME, JOB))
        self.assertEqual(self.client.writes, [])
        retain(self.client, 5, name, payload, (FRAME, JOB))
        with self.assertRaises(PublicationError):
            retain(self.client, 5, name, payload | {'changed': True}, (FRAME, JOB))
        self.assertEqual(len(self.client.writes), 1)

    def test_failed_readback_does_not_claim_success(self):
        self.client.corrupt = True
        with self.assertRaises(PublicationError):
            retain(self.client, 5, ORIGIN, {'release': 5}, (FRAME, JOB))
        self.assertEqual(len(self.client.assets), 1)


if __name__ == '__main__':
    unittest.main()
