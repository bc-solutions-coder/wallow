from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from publication import PublicationError
from publication_pages_bootstrap import pages_history
from publication_pages_history import TASK


class PagesBootstrapTests(unittest.TestCase):
    repository = 'example/repo'

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.path = Path(temporary.name) / 'bootstrap.json'
        now = datetime.now(timezone.utc)
        self.source = 'a' * 40
        self.url = 'https://example.github.io/repo/'
        self.bootstrap = {'schema': 1, 'repository': self.repository,
                          'created_at': (now - timedelta(hours=1)).isoformat(), 'expires_at': (now + timedelta(days=1)).isoformat(),
                          'deployment_id': 10, 'source_sha': 'git:' + self.source,
                          'run_id': 20, 'run_attempt': 1, 'workflow_id': 30, 'job_id': 40,
                          'status_id': 50, 'site_url': self.url, 'index_size': 5, 'index_sha256': 'sha256:' + 'b' * 64}
        self.record = {'id': 10, 'sha': self.source, 'task': 'deploy', 'environment': 'github-pages',
                       'payload': {}, 'created_at': '2026-09-01T00:00:00Z'}
        self.events = []
        self.run = {'id': 20, 'run_attempt': 1, 'workflow_id': 30, 'path': '.github/workflows/docs.yml',
                    'head_sha': self.source, 'event': 'push', 'head_branch': 'main', 'status': 'completed', 'conclusion': 'success',
                    'repository': {'full_name': self.repository}, 'head_repository': {'full_name': self.repository}}
        self.job = {'id': 40, 'head_sha': self.source, 'status': 'completed', 'conclusion': 'success'}
        self.status = {'id': 50, 'state': 'success', 'log_url': f'https://github.com/{self.repository}/actions/runs/20/job/40',
                       'environment_url': self.url}

    def get(self, path):
        return self.run

    def list(self, path, key):
        return [self.job]

    def array(self, path):
        return [self.status]

    def main_comparison(self, source):
        return {'status': 'ahead', 'base_commit': {'sha': source}, 'merge_base_commit': {'sha': source}}

    def verify_index(self, url, identity):
        self.events.append('verified-index')

    def ensure_unpublished(self, url):
        self.events.append('unpublished')

    def history(self, records, source='f' * 40):
        self.path.write_text(json.dumps(self.bootstrap))
        return pages_history(self, records, source, {'index_size': 5, 'index_sha256': 'b' * 64}, self.url, self.path)

    def test_reviewed_legacy_run_status_and_current_index_establish_first_floor(self):
        result = self.history([self.record])
        self.assertEqual(result, [{'source_sha': self.source, 'site': None}])
        self.assertEqual(self.events, ['verified-index'])

    def test_wrong_run_changed_deployment_expired_bootstrap_and_failed_status_reject(self):
        for change in ('run', 'deployment', 'expiry', 'status'):
            with self.subTest(change=change):
                self.setUp()
                if change == 'run': self.run['head_branch'] = 'feature'
                elif change == 'deployment': self.record['id'] = 11
                elif change == 'expiry': self.bootstrap['expires_at'] = '2020-01-01T00:00:00Z'
                else: self.status['state'] = 'failure'
                with self.assertRaises(PublicationError): self.history([self.record])
                self.assertEqual(self.events, [])

    def test_new_site_requires_confirmed_absence(self):
        self.assertEqual(self.history([]), [])
        self.assertEqual(self.events, ['unpublished'])

    def test_verified_cutover_subsumes_older_legacy_records_but_rejects_new_unknown(self):
        intent = {'id': 11, 'task': TASK, 'created_at': '2026-09-02T00:00:00Z'}
        payload = {'source_sha': 'f' * 40, 'site': {}, 'history_tail_id': 10}
        with patch('publication_pages_bootstrap.inspect_intent', return_value=payload):
            self.assertEqual(self.history([intent, self.record]), [payload])
            with self.assertRaises(PublicationError): self.history([intent, self.record | {'id': 12}])

    def test_newest_verified_intent_is_the_only_workflow_reauthenticated(self):
        records = [{'id': number, 'task': TASK, 'created_at': '2026-09-01T00:00:00Z'} for number in range(1, 101)]
        payload = {'source_sha': 'f' * 40, 'site': {}, 'history_tail_id': 99}
        with patch('publication_pages_bootstrap.inspect_intent', return_value=payload) as inspect:
            self.assertEqual(self.history(records), [payload])
            inspect.assert_called_once_with(self, records[-1])

    def test_concurrent_insertion_after_observed_tail_blocks_later_retries(self):
        records = [self.record, {'id': 11, 'task': 'deploy', 'created_at': '2026-09-01T00:00:00Z'},
                   {'id': 12, 'task': TASK, 'created_at': '2026-09-02T00:00:00Z'}]
        with patch('publication_pages_bootstrap.inspect_intent', return_value={'history_tail_id': 10}):
            with self.assertRaises(PublicationError): self.history(records)
