"""Publication writers reauthorize the explicitly selected recovery and its sealed preparation."""

import copy
import unittest
from unittest.mock import patch

from publication import PublicationError
from publication_preparation_authorization import authorize_preparation
import test_publication_image_authorization


class RecoveryPreparationTests(unittest.TestCase):
    def setUp(self):
        self.fixture = test_publication_image_authorization.AuthorizationTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.ordinary = copy.deepcopy(self.fixture.fresh)
        self.selector = {'release_id': 40, 'run_id': 80, 'run_attempt': 1}
        self.catalog = {'components': []}
        self.fixture.plan.update(schema=2, recovery={'original': {'producer': {'run_id': 20, 'run_attempt': 2}},
                                                    'receipt': {'asset_id': 90, 'sha256': 'sha256:' + 'd' * 64}})
        self.fixture.fresh = {key: copy.deepcopy(value) for key, value in self.fixture.plan.items() if key != 'verified_images'}
        self.fixture.make_plan()

    def authorize(self):
        fixture = self.fixture
        return authorize_preparation(fixture, fixture.context, 20, 2, 30, 1,
                                     recovery=self.selector, catalog=self.catalog)

    def test_explicit_recovery_reauthorization_matches_actual_sealed_preparation(self):
        with patch('publication_selection.resolve_selection', return_value=self.fixture.fresh) as resolver:
            result = self.authorize()
        resolver.assert_called_once_with(self.fixture, self.fixture.context, 20, 2, self.catalog, self.selector)
        self.assertEqual(result[0]['recovery'], self.fixture.fresh['recovery'])
        self.assertEqual(result[3]['authorize_job_id'], 60)

    def test_changed_recovery_receipt_in_prepared_plan_is_rejected(self):
        self.fixture.plan['recovery']['receipt']['asset_id'] = 91
        self.fixture.make_plan()
        with patch('publication_selection.resolve_selection', return_value=self.fixture.fresh):
            with self.assertRaises(PublicationError):
                self.authorize()

    def test_failed_fresh_recovery_authentication_never_downloads_preparation(self):
        with patch('publication_selection.resolve_selection', side_effect=PublicationError('recovery changed')):
            with self.assertRaises(PublicationError):
                self.authorize()
        self.assertEqual(self.fixture.downloads, [])

    def test_ordinary_writer_does_not_infer_recovery_from_sealed_plan(self):
        with patch('publication_plan.resolve', return_value=self.ordinary):
            with self.assertRaises(PublicationError):
                authorize_preparation(self.fixture, self.fixture.context, 20, 2, 30, 1)


if __name__ == '__main__':
    unittest.main()
