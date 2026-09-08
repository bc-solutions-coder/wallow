import copy
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

from publication import PublicationError
from publication_release_image_authorization import discover, job_name, release_authority
from publication_release_receipts import ORIGIN


class DiscoveryTests(unittest.TestCase):
    def setUp(self):
        self.catalog = {'components': [{'id': 'platform', 'tag_prefix': 'v'}], 'images': [{'id': 'api', 'component': 'platform'}]}
        self.release = {'id': 5, 'component': 'platform', 'version': '1.2.3'}
        self.origin = {'asset_id': 1, 'sha256': 'sha256:' + '1' * 64}
        self.selection = {'asset_id': 2, 'sha256': 'sha256:' + '2' * 64}
        self.client = SimpleNamespace(controller=Mock(), array=Mock(return_value=[{'id': 5, 'tag_name': 'v1.2.3', 'draft': False}]))
        self.receipt = None
        self.context = {'event_name': 'workflow_dispatch'}

    def run_discovery(self, release_id=None, origin=True):
        with patch('publication_release_image_authorization.image_environment'), \
             patch('publication_release_image_authorization.release_identity', side_effect=lambda client, value, catalog: self.release | {'id': value['id']}), \
             patch('publication_release_image_authorization.find_receipt', side_effect=lambda client, release_id, name: self.origin if name == ORIGIN and origin else self.selection if name != ORIGIN else None), \
             patch('publication_release_image_authorization.authorized_selection'), \
             patch('publication_release_image_authorization.inspect_receipt', return_value=self.receipt), \
             patch('publication_release_image_authorization.endorsed', return_value=True), \
             patch('publication_release_image_authorization.release_authority') as selected:
            result = discover(self.client, self.context, self.catalog, release_id, (20, 1) if release_id else None)
            return result, selected.call_count

    def test_pending_origin_does_not_invent_matrix_member(self):
        (matrix, pending), calls = self.run_discovery(origin=False)
        self.assertEqual(matrix, {'include': []})
        self.assertEqual(pending, [5])
        self.assertEqual(calls, 0)
        with self.assertRaises(PublicationError):
            self.run_discovery(5, origin=False)

    def test_exact_selected_release_and_bounded_complete_matrix(self):
        (matrix, pending), calls = self.run_discovery(5)
        self.assertEqual(matrix, {'include': [{'release_id': 5}]})
        self.assertEqual(calls, 1)
        self.client.array.return_value = [{'id': value, 'tag_name': 'v1.2.3', 'draft': False} for value in range(1, 102)]
        with self.assertRaises(PublicationError):
            self.run_discovery()

    def test_completed_durable_release_skips_expired_producer_inputs(self):
        self.receipt = {'record': {'payload': {'release': self.release, 'origin': self.origin, 'selection': self.selection}}}
        (matrix, _), calls = self.run_discovery()
        self.assertEqual(matrix['include'], [])
        self.assertEqual(calls, 0)
        self.receipt['record']['payload']['selection'] = {'asset_id': 99}
        with self.assertRaises(PublicationError):
            self.run_discovery()

    def test_exact_matrix_job_names_reject_untrusted_ids(self):
        self.assertEqual(job_name('Prepare release images', 5), 'Prepare release images (5)')
        for value in (True, 0, -1, '5', '5) injected'):
            with self.subTest(value=value), self.assertRaises(PublicationError):
                job_name('Prepare release images', value)


if __name__ == '__main__':
    unittest.main()
