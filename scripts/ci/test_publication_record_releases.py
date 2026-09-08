import copy
import unittest
from unittest.mock import patch

from publication import PublicationError
from publication_record_releases import reconcile


class Client:
    def __init__(self):
        self.release = {'id': 5, 'tag_name': 'sdk-v1.2.3', 'draft': False}

    def array(self, path):
        return [copy.deepcopy(self.release)]

    def get(self, path):
        return copy.deepcopy(self.release)


class ReconciliationTests(unittest.TestCase):
    def setUp(self):
        self.client = Client()
        self.context = {'event_name': 'workflow_dispatch'}
        self.catalog = {'components': [{'tag_prefix': 'sdk-v'}]}
        self.release = {'id': 5, 'tag_name': 'sdk-v1.2.3', 'commit_sha': 'a' * 40}
        self.origin = {'release': self.release, 'merged_pr': {}, 'release_please': {}}
        self.selection = {'producer': {'run_id': 20, 'run_attempt': 1}}
        self.retained = []
        self.patches = []
        for name, kwargs in [('frame', {'return_value': ({'run_id': 30}, {})}), ('resolve', {'return_value': {}}), ('release_identity', {'return_value': self.release}),
                             ('inspect_receipt', {'return_value': None}), ('authenticate_origin', {'return_value': self.origin}), ('choose_producer', {'return_value': self.selection}), ('retain', {'side_effect': self.retain})]:
            replacement = patch('publication_record_releases.' + name, **kwargs)
            setattr(self, name, replacement.start())
            self.addCleanup(replacement.stop)

    def retain(self, client, release_id, name, payload, current):
        self.retained.append((name, payload))
        return {'asset_id': len(self.retained), 'sha256': 'sha256:' + str(len(self.retained)) * 64}

    def run_reconcile(self, release_id=None, result=None):
        return reconcile(self.client, self.context, 30, 1, 20, 1, self.catalog, release_id, result)

    def test_origin_is_retained_before_selection_and_no_publication_authority_claim(self):
        result = self.run_reconcile()
        self.assertEqual([name for name, _ in self.retained], ['wallow-release-origin-v1.json', 'wallow-release-selection-v1.json'])
        self.assertEqual(result['releases'][0]['state'], 'producer-selected')
        self.assertFalse(result['publication_authorized'])
        self.assertFalse(result['releases'][0]['publication_authorized'])

    def test_newer_release_waits_after_durably_preserving_origin(self):
        self.choose_producer.return_value = None
        result = self.run_reconcile()
        self.assertEqual(len(self.retained), 1)
        self.assertEqual(result['releases'][0]['state'], 'pending-producer')

    def test_manual_retry_binds_explicit_release_and_producer(self):
        self.run_reconcile(5)
        self.assertEqual(self.choose_producer.call_args.kwargs['explicit'], (20, 1))
        self.context['event_name'] = 'workflow_run'
        with self.assertRaises(PublicationError):
            self.run_reconcile(5)

    def test_partial_origin_progress_survives_later_selection_failure(self):
        self.choose_producer.side_effect = PublicationError('Expired producer')
        result = {'schema': 1, 'publication_authorized': False, 'releases': []}
        with self.assertRaises(PublicationError):
            self.run_reconcile(result=result)
        self.assertEqual(result['releases'][0]['origin_asset_id'], 1)
        self.assertEqual(len(self.retained), 1)

    def test_missing_automation_origin_is_pending_and_explicit_retry_fails(self):
        self.authenticate_origin.return_value = None
        self.assertEqual(self.run_reconcile()['releases'][0]['state'], 'pending-origin')
        self.assertEqual(self.retained, [])
        with self.assertRaises(PublicationError):
            self.run_reconcile(5)


if __name__ == '__main__':
    unittest.main()
