import copy
from dataclasses import asdict
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

from publication import Producer, PublicationError
from publication_package_releases import package_releases
from publication_release_receipts import ORIGIN


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.producer = Producer('example/repo', 1, 'a' * 40, 20, 2, 10, 'example/repo/.github/workflows/ci.yml@refs/heads/main')
        self.component = {'id': 'sdk', 'path': 'packages/sdk', 'tag_prefix': 'sdk-v', 'package': {'name': '@example/sdk'}}
        self.catalog = {'components': [self.component]}
        self.release = {'id': 5, 'version': '1.0.0', 'commit_sha': self.producer.source_sha}
        self.origin = {'asset_id': 1, 'sha256': 'sha256:' + '1' * 64}
        pinned = {'producer': asdict(self.producer), 'component_versions': {'packages/sdk': '1.0.0'}}
        self.selection = {'asset_id': 2, 'sha256': 'sha256:' + '2' * 64, 'record': {'payload': {
            'release': self.release, 'origin_asset_id': 1, 'origin_sha256': self.origin['sha256'], 'selection': pinned}}}
        self.client = SimpleNamespace(controller=Mock(), array=Mock(return_value=[{'id': 5, 'tag_name': 'sdk-v1.0.0', 'draft': False}]), producer=Mock(return_value=(self.producer, {})))
        self.completed = {'asset_id': 3}

    def discover(self, approved=True, partial_error=None, explicit=None):
        with patch('publication_package_releases.release_identity', return_value=self.release), \
             patch('publication_package_releases.find_receipt', side_effect=lambda client, release_id, name: self.origin if name == ORIGIN else self.selection), \
             patch('publication_release_candidates.authenticate_origin'), \
             patch('publication_package_releases.inspect_receipt', return_value=self.completed), \
             patch('publication_package_releases.endorsed', return_value=approved), \
             patch('publication_package_releases.revalidate_partial_receipt', side_effect=partial_error) as recovery, \
             patch('publication_package_releases.published_package', return_value={'release': self.release}) as record, \
             patch('publication_package_releases.resolve', side_effect=AssertionError('Expired original artifact must not be fetched')):
            result = package_releases(self.client, {'event_name': 'workflow_dispatch'}, self.catalog, 5 if explicit else None, explicit)
            return result, recovery.call_count

    def test_untracked_release_waits_without_resolving_historical_source(self):
        with patch('publication_package_releases.release_identity', side_effect=PublicationError('Unsupported old source')) as identity, \
             patch('publication_package_releases.find_receipt', return_value=None):
            result = package_releases(self.client, {'event_name': 'workflow_dispatch'}, self.catalog)
            self.assertEqual(result['pending'], [{'release_id': 5, 'state': 'pending-origin'}])
            self.assertEqual(result['ready'], [])
            identity.assert_not_called()
            with self.assertRaises(PublicationError):
                package_releases(self.client, {'event_name': 'workflow_dispatch'}, self.catalog, 5, (20, 2))
        with patch('publication_package_releases.release_identity', side_effect=PublicationError('Changed tracked source')), \
             patch('publication_package_releases.find_receipt', return_value=self.origin):
            with self.assertRaises(PublicationError):
                package_releases(self.client, {'event_name': 'workflow_dispatch'}, self.catalog)

    def test_successful_durable_publication_does_not_require_original_artifact(self):
        result, recoveries = self.discover()
        self.assertEqual(result['published'], [{'release': self.release, 'needs_endorsement': False}])
        self.assertEqual(recoveries, 0)
        self.client.producer.assert_called_once_with(20, 2)

    def test_partial_finalizer_requires_original_writer_evidence_before_adoption(self):
        result, recoveries = self.discover(approved=False)
        self.assertTrue(result['published'][0]['needs_endorsement'])
        self.assertEqual(recoveries, 1)
        with self.assertRaises(PublicationError):
            self.discover(approved=False, partial_error=PublicationError('Original writer progress expired; explicit recovery required'))


    def recovered_discovery(self, completed=None, recovery=None):
        recovery = recovery or {'release_id': 5, 'run_id': 70, 'run_attempt': 1}
        pinned = self.selection['record']['payload']['selection']
        metadata = {'original': pinned, 'receipt': {'asset_id': 90, 'sha256': 'exact-recovery'}}
        plan = {'schema': 2, 'producer': asdict(self.producer) | {'run_id': 70, 'run_attempt': 1}, 'recovery': metadata}
        with patch('publication_package_releases.release_identity', return_value=self.release), \
             patch('publication_package_releases.find_receipt', side_effect=lambda client, release_id, name: self.origin if name == ORIGIN else self.selection), \
             patch('publication_release_candidates.authenticate_origin'), \
             patch('publication_package_releases.inspect_receipt', return_value=completed), \
             patch('publication_package_releases.endorsed', return_value=True), \
             patch('publication_package_releases.published_package', return_value={'release': self.release, 'package': 'original-byte-authority'}), \
             patch('publication_package_releases.resolve', side_effect=AssertionError('Expired original artifact must not be fetched')), \
             patch('publication_package_releases.resolve_selection', return_value=plan) as resolver, \
             patch('publication_package_releases.validate_candidate', return_value={'producer': plan['producer']}):
            result = package_releases(self.client, {'event_name': 'workflow_dispatch'}, self.catalog, 5, (20, 2), recovery=recovery)
        resolver.assert_called_once_with(self.client, {'event_name': 'workflow_dispatch'}, 20, 2, self.catalog, recovery=recovery)
        return result, plan

    def test_unfinished_explicit_target_uses_recovered_plan_and_original_receipt(self):
        before = copy.deepcopy(self.selection)
        result, plan = self.recovered_discovery()
        self.assertEqual(result['ready'][0]['plan'], plan)
        self.assertEqual(result['ready'][0]['selection'], {'asset_id': 2, 'sha256': self.selection['sha256']})
        self.assertEqual(result['recovery']['original']['producer']['run_id'], 20)
        self.assertEqual(result['ready'][0]['plan']['producer']['run_id'], 70)
        self.assertEqual(self.selection, before)
        self.assertEqual(result['published'], [])

    def test_completed_explicit_target_retains_original_published_bytes(self):
        result, _ = self.recovered_discovery(self.completed)
        self.assertEqual(result['ready'], [])
        self.assertEqual(result['published'], [{'release': self.release, 'package': 'original-byte-authority', 'needs_endorsement': False}])
        self.assertEqual(result['recovery']['receipt']['asset_id'], 90)
        self.client.producer.assert_called_once_with(20, 2)


    def test_unrelated_completed_dependency_does_not_resolve_historical_artifacts(self):
        other = self.release | {'id': 6, 'version': '2.0.0'}
        original = self.selection['record']['payload']['selection']
        other_selection = copy.deepcopy(self.selection)
        other_selection['record']['payload']['release'] = other
        other_selection['record']['payload']['selection']['component_versions']['packages/sdk'] = '2.0.0'
        recovery = {'release_id': 5, 'run_id': 70, 'run_attempt': 1}
        plan = {'schema': 2, 'producer': asdict(self.producer) | {'run_id': 70}, 'recovery': {'original': original, 'receipt': {'asset_id': 90}}}
        self.client.array.return_value.append({'id': 6, 'tag_name': 'sdk-v2.0.0', 'draft': False})
        with patch('publication_package_releases.release_identity', side_effect=lambda client, api, catalog: self.release if api['id'] == 5 else other), \
             patch('publication_package_releases.find_receipt', side_effect=lambda client, release_id, name: self.origin if name == ORIGIN else (self.selection if release_id == 5 else other_selection)), \
             patch('publication_release_candidates.authenticate_origin'), \
             patch('publication_package_releases.inspect_receipt', side_effect=lambda client, release_id, name: None if release_id == 5 else self.completed), \
             patch('publication_package_releases.endorsed', return_value=True), \
             patch('publication_package_releases.published_package', return_value={'release': other}), \
             patch('publication_package_releases.resolve', side_effect=AssertionError('Unrelated completed dependency artifacts expired')), \
             patch('publication_package_releases.resolve_selection', return_value=plan) as resolver, \
             patch('publication_package_releases.validate_candidate', return_value={'producer': plan['producer']}):
            result = package_releases(self.client, {'event_name': 'workflow_dispatch'}, self.catalog, 5, (20, 2), recovery=recovery)
        self.assertEqual([item['release']['id'] for item in result['ready']], [5])
        self.assertEqual([item['release']['id'] for item in result['published']], [6])
        self.assertEqual(resolver.call_count, 1)

    def test_invalid_recovery_selection_fails_before_api_access(self):
        for value in ({}, {'release_id': 6, 'run_id': 70, 'run_attempt': 1}, {'release_id': 5, 'run_id': True, 'run_attempt': 1}, {'release_id': 5, 'run_id': 70, 'run_attempt': 1, 'extra': 1}):
            with self.subTest(value=value), self.assertRaises(PublicationError):
                package_releases(self.client, {'event_name': 'workflow_dispatch'}, self.catalog, 5, (20, 2), recovery=value)
        with self.assertRaises(PublicationError):
            package_releases(self.client, {'event_name': 'workflow_run'}, self.catalog, 5, (20, 2), recovery={'release_id': 5, 'run_id': 70, 'run_attempt': 1})
        self.client.controller.assert_not_called()

    def test_manual_retry_cannot_replace_pinned_producer(self):
        with self.assertRaises(PublicationError):
            self.discover(explicit=(21, 1))


if __name__ == '__main__':
    unittest.main()
