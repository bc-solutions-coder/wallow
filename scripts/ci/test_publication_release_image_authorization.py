import copy
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

from publication import PublicationError
from publication_release_image_authorization import authorize_prepared, discover, job_name, release_authority
import test_publication_release_image_preparation as preparation_fixtures
from publication_release_receipts import ORIGIN


class DiscoveryTests(unittest.TestCase):
    def setUp(self):
        self.catalog = {'components': [{'id': 'platform', 'tag_prefix': 'v'}], 'images': [{'id': 'api', 'component': 'platform'}]}
        self.release = {'id': 5, 'component': 'platform', 'version': '1.2.3'}
        self.origin = {'asset_id': 1, 'sha256': 'sha256:' + '1' * 64}
        self.selection = {'asset_id': 2, 'sha256': 'sha256:' + '2' * 64}
        self.client = SimpleNamespace(controller=Mock(), get=Mock(return_value={'id': 5}), array=Mock(return_value=[{'id': 5, 'tag_name': 'v1.2.3', 'draft': False}]))
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


    def test_recovered_authority_keeps_original_refs_and_uses_effective_inputs(self):
        original = {'producer': {'run_id': 20, 'run_attempt': 1}}
        recovery = {'release_id': 5, 'run_id': 70, 'run_attempt': 2}
        plan = {'route': 'full', 'producer': {'run_id': 70, 'run_attempt': 2},
                'recovery': {'original': original, 'receipt': {'asset_id': 90}}}
        with patch('publication_release_image_authorization.image_environment'), \
             patch('publication_release_image_authorization.release_identity', return_value=self.release), \
             patch('publication_release_image_authorization.find_receipt', side_effect=lambda client, release_id, name: self.origin if name == ORIGIN else self.selection), \
             patch('publication_release_image_authorization.authorized_selection', return_value=original) as selected, \
             patch('publication_release_image_authorization.resolve', side_effect=AssertionError('Expired original input must not be fetched')), \
             patch('publication_release_image_authorization.resolve_selection', return_value=plan) as recovered, \
             patch('publication_release_image_authorization.validate_candidate', return_value={'producer': plan['producer']}):
            result = release_authority(self.client, self.context, self.catalog, 5, (20, 1), recovery=recovery)
            self.assertEqual(result['plan'], plan)
            self.assertEqual(result['selection'], self.selection)
            self.assertEqual(result['origin'], self.origin)
            selected.assert_called_once_with(self.client, self.release, self.origin, self.selection, (20, 1))
            recovered.assert_called_once_with(self.client, self.context, 20, 1, self.catalog, recovery=recovery)
            plan['recovery']['original'] = {'producer': {'run_id': 21, 'run_attempt': 1}}
            with self.assertRaises(PublicationError): release_authority(self.client, self.context, self.catalog, 5, (20, 1), recovery=recovery)

    def test_invalid_recovery_is_rejected_before_environment_or_api(self):
        for recovery in ({}, {'release_id': 6, 'run_id': 70, 'run_attempt': 1}, {'release_id': 5, 'run_id': True, 'run_attempt': 1}):
            with self.subTest(recovery=recovery), self.assertRaises(PublicationError):
                discover(self.client, self.context, self.catalog, 5, (20, 1), recovery=recovery)
        recovery = {'release_id': 5, 'run_id': 70, 'run_attempt': 1}
        with self.assertRaises(PublicationError): release_authority(self.client, {'event_name': 'workflow_run'}, self.catalog, 5, (20, 1), recovery=recovery)
        with self.assertRaises(PublicationError): discover(self.client, self.context, self.catalog, 5, recovery=recovery)
        self.client.controller.assert_not_called()

    def test_sealed_preparation_reauthenticates_same_recovery_receipt(self):
        fixture = preparation_fixtures.PreparationTests()
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        recovery = {'release_id': 5, 'run_id': 70, 'run_attempt': 2}
        metadata = {'original': {'producer': {'run_id': 20, 'run_attempt': 2}}, 'receipt': {'asset_id': 90}}
        fixture.plan['authority']['plan']['recovery'] = copy.deepcopy(metadata)
        fixture.plan['source']['recovery'] = copy.deepcopy(metadata)
        fixture.authority = copy.deepcopy(fixture.plan['authority'])
        artifacts = fixture.artifacts(fixture.plan)
        with patch('publication_release_image_authorization.authorize_preparation', return_value=({}, fixture.preparation, artifacts, {})) as shared, \
             patch('publication_release_image_authorization.frame', return_value=({'job_id': 100}, {})), \
             patch('publication_release_image_authorization.release_authority', return_value=fixture.authority) as authority:
            result = authorize_prepared(fixture.client, self.context, 20, 2, 30, 1, 5, fixture.client.catalog, fixture.client.root, (20, 2), recovery=recovery)
            self.assertEqual(result[0]['authority']['plan']['recovery'], metadata)
            shared.assert_called_once_with(fixture.client, self.context, 20, 2, 30, 1, recovery=recovery, catalog=fixture.client.catalog)
            authority.assert_called_once_with(fixture.client, self.context, fixture.client.catalog, 5, (20, 2), recovery=recovery)
            fixture.authority['plan']['recovery']['receipt']['asset_id'] = 91
            with self.assertRaises(PublicationError):
                authorize_prepared(fixture.client, self.context, 20, 2, 30, 1, 5, fixture.client.catalog, fixture.client.root, (20, 2), recovery=recovery)

    def test_exact_matrix_job_names_reject_untrusted_ids(self):
        self.assertEqual(job_name('Prepare release images', 5), 'Prepare release images (5)')
        for value in (True, 0, -1, '5', '5) injected'):
            with self.subTest(value=value), self.assertRaises(PublicationError):
                job_name('Prepare release images', value)


if __name__ == '__main__':
    unittest.main()
