"""Behavior checks for the historical recovery dispatch boundary."""

import copy
import unittest
from unittest.mock import Mock, patch

from publication import PublicationError
from recovery_request import authorize_dispatch, request


class RecoveryDispatchTests(unittest.TestCase):
    def setUp(self):
        self.sha = 'a' * 40
        self.repository = {'id': 11, 'full_name': 'owner/repo', 'default_branch': 'main'}
        self.workflow = {'id': 22, 'path': '.github/workflows/ci.yml'}
        self.context = {'repository': 'owner/repo', 'ref': 'refs/heads/main', 'event_name': 'workflow_dispatch',
                        'workflow_ref': 'owner/repo/.github/workflows/ci.yml@refs/heads/main', 'workflow_sha': self.sha}
        self.run = {'id': 33, 'run_attempt': 2, 'workflow_id': 22, 'path': '.github/workflows/ci.yml',
                    'event': 'workflow_dispatch', 'head_branch': 'main', 'head_sha': self.sha, 'status': 'in_progress',
                    'repository': self.repository.copy(), 'head_repository': self.repository.copy()}
        self.comparison = {'status': 'ahead', 'base_commit': {'sha': self.sha}, 'merge_base_commit': {'sha': self.sha}}

    def authorize(self, **overrides):
        args = {'context': self.context, 'repository': self.repository, 'workflow': self.workflow,
                'run': self.run, 'run_id': 33, 'attempt': 2, 'comparison': self.comparison}
        args.update(overrides)
        return authorize_dispatch(**args)

    def test_exact_active_main_dispatch_keeps_controller_identity(self):
        self.assertEqual(self.authorize(), self.sha)

    def test_rejects_other_events_refs_and_workflow_contexts(self):
        for key, value in [('event_name', 'push'), ('event_name', 'pull_request'), ('ref', 'refs/heads/feature'),
                           ('workflow_ref', 'owner/repo/.github/workflows/publish.yml@refs/heads/main'),
                           ('repository', 'other/repo')]:
            with self.subTest(key=key, value=value), self.assertRaises(PublicationError):
                self.authorize(context=self.context | {key: value})

    def test_rejects_changed_api_run_identity(self):
        for key, value in [('id', 34), ('run_attempt', 1), ('workflow_id', 23), ('head_sha', 'b' * 40),
                           ('event', 'push'), ('head_branch', 'feature'), ('status', 'completed'),
                           ('path', '.github/workflows/other.yml')]:
            with self.subTest(key=key), self.assertRaises(PublicationError):
                self.authorize(run=self.run | {key: value})

    def test_rejects_foreign_and_boolean_repository_ids(self):
        for key in ('repository', 'head_repository'):
            for identity in ({'id': 12, 'full_name': 'owner/repo'}, {'id': True, 'full_name': 'owner/repo'},
                             {'id': 11, 'full_name': 'other/repo'}):
                with self.subTest(key=key, identity=identity), self.assertRaises(PublicationError):
                    self.authorize(run=self.run | {key: identity})

    def test_rejects_non_main_controller_ancestry(self):
        for key, value in [('status', 'diverged'), ('base_commit', {'sha': 'b' * 40}),
                           ('merge_base_commit', {'sha': 'b' * 40})]:
            with self.subTest(key=key), self.assertRaises(PublicationError):
                self.authorize(comparison=self.comparison | {key: value})

    def test_rejects_malformed_invocations_without_coercion(self):
        for value in (True, 0, '33', None):
            with self.subTest(value=value), self.assertRaises(PublicationError):
                self.authorize(run_id=value)
        bad = copy.deepcopy(self.run)
        bad['run_attempt'] = True
        with self.assertRaises(PublicationError):
            self.authorize(run=bad)


class RecoveryRequestTests(unittest.TestCase):
    def setUp(self):
        RecoveryDispatchTests.setUp(self)
        self.source = 'b' * 40
        self.release = {'id': 44, 'commit_sha': self.source, 'component_path': '.', 'version': '1.2.3'}
        self.origin = {'asset_id': 55, 'sha256': 'sha256:' + 'c' * 64}
        self.selection = {'asset_id': 66, 'sha256': 'sha256:' + 'd' * 64, 'record': {'payload': {
            'origin_asset_id': 55, 'origin_sha256': self.origin['sha256'], 'release': self.release,
            'selection': {'producer': {'repository': 'owner/repo', 'source_sha': self.source, 'run_id': 77, 'run_attempt': 1},
                          'component_versions': {'.': '1.2.3'}}}}}
        self.client = Mock(repository='owner/repo')
        metadata = {'': self.repository, '/actions/workflows/ci.yml': self.workflow,
                    '/actions/runs/33/attempts/2': self.run, '/releases/44': {'id': 44}}
        self.client.get.side_effect = metadata.__getitem__
        self.client.main_comparison.side_effect = lambda sha: {'status': 'ahead', 'base_commit': {'sha': sha}, 'merge_base_commit': {'sha': sha}}

    def requested(self):
        with patch('recovery_request.release_identity', return_value=self.release), \
             patch('recovery_request.find_receipt', side_effect=[self.origin, self.selection]), \
             patch('recovery_request.authenticate_origin') as origin_check:
            result = request(self.client, self.context, 33, 2, self.source, 44, {})
            origin_check.assert_called_once_with(self.client, self.release, self.origin)
            return result

    def test_request_preserves_distinct_source_controller_and_original_assets(self):
        result = self.requested()
        self.assertEqual(result['controller_sha'], self.sha)
        self.assertEqual(result['source_sha'], self.source)
        self.assertEqual(result['selection']['asset_id'], 66)
        self.assertEqual(result['origin']['asset_id'], 55)
        self.assertEqual(result['route'], 'full')
        self.assertFalse(result['publication_authorized'])

    def test_wrong_release_source_cannot_start_recovery(self):
        self.release['commit_sha'] = 'e' * 40
        with self.assertRaisesRegex(PublicationError, 'exact release commit'):
            self.requested()

    def test_missing_original_origin_or_selection_is_not_manufactured(self):
        self.origin = None
        with self.assertRaisesRegex(PublicationError, 'existing authenticated release origin'):
            self.requested()
        self.origin = {'asset_id': 55, 'sha256': 'sha256:' + 'c' * 64}
        self.selection = None
        with self.assertRaisesRegex(PublicationError, 'original immutable producer selection'):
            self.requested()

    def test_selection_origin_and_original_source_must_match(self):
        original = copy.deepcopy(self.selection)
        self.selection['record']['payload']['origin_asset_id'] = 99
        with self.assertRaisesRegex(PublicationError, 'authenticated release origin'):
            self.requested()
        self.selection = original
        self.selection['record']['payload']['selection']['producer']['source_sha'] = 'e' * 40
        with self.assertRaisesRegex(PublicationError, 'original release producer'):
            self.requested()


if __name__ == '__main__':
    unittest.main()
