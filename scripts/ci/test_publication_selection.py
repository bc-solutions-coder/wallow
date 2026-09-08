import copy
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from publication import PublicationError
from publication_release_receipts import ORIGIN, SELECTION
from publication_selection import resolve_selection


class SelectionTests(unittest.TestCase):
    def setUp(self):
        self.context = {'event_name': 'workflow_dispatch'}
        self.recovery = {'release_id': 5, 'run_id': 70, 'run_attempt': 2}
        self.release = {'id': 5, 'component': 'sdk'}
        self.original = {'producer': {'run_id': 7, 'run_attempt': 1}}
        self.origin = {'asset_id': 50}
        self.selection = {'asset_id': 51, 'record': {'payload': {'release': self.release, 'origin_asset_id': 50,
                          'origin_sha256': 'exact', 'selection': self.original}}}
        self.origin['sha256'] = 'exact'
        self.recovered = {'original': self.original, 'plan': {'schema': 2, 'producer': {'run_id': 70, 'run_attempt': 2}},
                          'receipt': {'asset_id': 52, 'sha256': 'exact-recovery'}}
        self.calls = []
        self.client = SimpleNamespace(controller=lambda context: self.calls.append(('controller', context)),
                                      get=lambda path: self.calls.append(('get', path)) or {'id': 5})
        for target, kwargs in (
            ('publication_selection.release_identity', {'return_value': self.release}),
            ('publication_selection.find_receipt', {'side_effect': lambda client, release_id, name: {ORIGIN: self.origin, SELECTION: self.selection}[name]}),
            ('publication_release_candidates.authenticate_origin', {}),
            ('publication_selection.authorized_recovery', {'return_value': self.recovered}),
        ):
            patcher = patch(target, **kwargs)
            mock = patcher.start()
            self.addCleanup(patcher.stop)
            if target.endswith('authorized_recovery'): self.recovery_mock = mock

    def run_selection(self, recovery=None, context=None, run=7):
        return resolve_selection(self.client, context or self.context, run, 1, {}, self.recovery if recovery is None else recovery)

    def test_normal_path_is_unchanged_and_never_discovers_recovery(self):
        plan = {'schema': 1, 'producer': {'run_id': 7}}
        with patch('publication_selection.resolve', return_value=plan) as normal:
            result = resolve_selection(self.client, self.context, 7, 1, {})
        self.assertIs(result, plan)
        normal.assert_called_once_with(self.client, self.context, 7, 1)
        self.recovery_mock.assert_not_called()
        self.assertEqual(self.calls, [])

    def test_explicit_recovery_preserves_original_and_returns_distinct_metadata(self):
        before = copy.deepcopy(self.recovered)
        with patch('publication_selection.resolve') as normal:
            result = self.run_selection()
        normal.assert_not_called()
        self.assertEqual(result['producer']['run_id'], 70)
        self.assertEqual(result['recovery']['original']['producer']['run_id'], 7)
        self.assertEqual(result['recovery']['receipt'], self.recovered['receipt'])
        self.assertEqual(self.recovered, before)
        self.recovery_mock.assert_called_once_with(self.client, self.context, self.release, self.origin, self.selection, 70, 2, {})
        self.assertIn(('get', '/releases/5'), self.calls)

    def test_malformed_or_automatic_recovery_fails_before_api_access(self):
        for value in ({}, self.recovery | {'extra': 1}, self.recovery | {'run_id': True}, self.recovery | {'release_id': '5'}, self.recovery | {'run_attempt': 0}):
            with self.subTest(value=value), self.assertRaises(PublicationError): self.run_selection(value)
        with self.assertRaises(PublicationError): self.run_selection(context={'event_name': 'workflow_run'})
        self.assertEqual(self.calls, [])
        self.recovery_mock.assert_not_called()

    def test_original_run_mismatch_missing_receipts_and_wrong_release_fail(self):
        with self.assertRaises(PublicationError): self.run_selection(run=8)
        self.recovery_mock.assert_not_called()
        with patch('publication_selection.find_receipt', return_value=None), self.assertRaises(PublicationError): self.run_selection()
        self.release['id'] = 6
        with self.assertRaises(PublicationError): self.run_selection()
        self.recovery_mock.assert_not_called()

    def test_recovery_authority_failure_or_original_change_is_not_accepted(self):
        self.recovery_mock.side_effect = PublicationError('Changed recovered artifacts')
        with self.assertRaises(PublicationError): self.run_selection()
        self.recovery_mock.side_effect = None
        self.recovered['original'] = {'producer': {'run_id': 8, 'run_attempt': 1}}
        with self.assertRaises(PublicationError): self.run_selection()


if __name__ == '__main__': unittest.main()
