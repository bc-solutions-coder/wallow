import copy
import unittest
from unittest.mock import patch

from publication import PublicationError
from publication_pages_history import inspect_intent, pages_action, site_identity, TASK
from publication_release_github import ACTIONS_ACTOR


class PagesHistoryTests(unittest.TestCase):
    repository = 'example/repo'

    def setUp(self):
        self.source = 'a' * 40
        self.site = site_identity({'sha256': 'b' * 64, 'size': 10240,
                                   'files': [{'path': 'index.html', 'size': 5, 'sha256': 'c' * 64}]})
        self.payload = {'schema': 1, 'source_sha': self.source,
                        'producer': {'repository': self.repository, 'source_sha': self.source,
                                     'repository_id': 1, 'run_id': 2, 'run_attempt': 1, 'workflow_id': 3},
                        'artifact': {'id': 4, 'name': 'prepared-site-5-1', 'size': 1000, 'digest': 'sha256:' + 'd' * 64},
                        'site': self.site, 'recorder': {'run_id': 5, 'run_attempt': 1}}
        self.deployment = {'id': 6, 'task': TASK, 'environment': 'github-pages',
                           'sha': self.source, 'creator': ACTIONS_ACTOR, 'payload': self.payload,
                           'created_at': '2026-09-08T02:00:05Z'}
        self.job = {'started_at': '2026-09-08T02:00:00Z', 'completed_at': '2026-09-08T02:00:10Z',
                    'conclusion': 'failure', 'status': 'completed'}
        self.comparisons = {}

    def main_comparison(self, sha):
        return {'status': 'ahead', 'base_commit': {'sha': sha}, 'merge_base_commit': {'sha': sha}}

    def get(self, path):
        return self.comparisons[path]

    def test_failed_protected_job_still_authenticates_durable_intent(self):
        with patch('publication_pages_history.recorded_job', return_value=self.job):
            self.assertEqual(inspect_intent(self, self.deployment), self.payload)

    def test_foreign_unknown_wrong_time_or_mismatched_content_is_rejected(self):
        for change in ('actor', 'task', 'time', 'sha', 'artifact', 'site'):
            item = copy.deepcopy(self.deployment)
            if change == 'actor': item['creator']['id'] = 99
            elif change == 'task': item['task'] = 'deploy'
            elif change == 'time': item['created_at'] = '2026-09-08T03:00:00Z'
            elif change == 'sha': item['sha'] = 'f' * 40
            elif change == 'artifact': item['payload']['artifact']['name'] = 'prepared-site-99-1'
            else: del item['payload']['site']['index_sha256']
            with self.subTest(change=change), patch('publication_pages_history.recorded_job', return_value=self.job), self.assertRaises(PublicationError):
                inspect_intent(self, item)

    def test_older_retry_is_skipped_even_if_prior_newer_job_failed(self):
        older = '0' * 40
        self.comparisons[f'/compare/{self.source}...{older}'] = {
            'status': 'behind', 'base_commit': {'sha': self.source}, 'merge_base_commit': {'sha': older}}
        with patch('publication_pages_history.recorded_job', return_value=self.job):
            prior = inspect_intent(self, self.deployment)
        self.assertEqual(pages_action(self, older, self.site, [prior]), 'skip-older')

    def test_identical_retry_allowed_but_same_source_changed_bytes_rejected(self):
        self.assertEqual(pages_action(self, self.source, self.site, [self.payload]), 'deploy')
        with self.assertRaises(PublicationError):
            pages_action(self, self.source, self.site | {'tar_sha256': 'e' * 64}, [self.payload])

    def test_incomparable_intent_is_not_hidden_by_an_older_retry(self):
        candidate = '0' * 40
        self.comparisons[f'/compare/{self.source}...{candidate}'] = {
            'status': 'behind', 'base_commit': {'sha': self.source}, 'merge_base_commit': {'sha': candidate}}
        self.comparisons[f'/compare/{"f" * 40}...{candidate}'] = {'status': 'diverged'}
        with self.assertRaises(PublicationError):
            pages_action(self, candidate, self.site, [self.payload, self.payload | {'source_sha': 'f' * 40}])

    def test_newer_source_is_allowed(self):
        candidate = 'f' * 40
        self.comparisons[f'/compare/{self.source}...{candidate}'] = self.main_comparison(self.source)
        self.assertEqual(pages_action(self, candidate, self.site, [self.payload]), 'deploy')
