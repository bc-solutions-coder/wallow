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

    def test_manual_retry_cannot_replace_pinned_producer(self):
        with self.assertRaises(PublicationError):
            self.discover(explicit=(21, 1))


if __name__ == '__main__':
    unittest.main()
