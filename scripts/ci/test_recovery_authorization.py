"""Completed recovery must retain actual dispatch, controller, and job identities."""

import unittest
from unittest.mock import Mock

from publication import PublicationError, authorize_main_producer
from recovery_authorization import authorize_completed_controller, completed_controller
import test_publication


class CompletedRecoveryTests(unittest.TestCase):
    def setUp(self):
        fixture = test_publication.AuthorizationTests()
        fixture.setUp()
        self.values = {'repository': fixture.repository, 'workflow': fixture.workflow,
                       'run': fixture.run | {'event': 'workflow_dispatch'}, 'run_id': 20, 'attempt': 2,
                       'jobs': [fixture.gate], 'required_check': fixture.check, 'comparison': fixture.comparison}
        for index, (name, conclusion) in enumerate((('build', 'success'), ('js / local', 'success'),
                                                    ('js / main', 'skipped'), ('js / complete', 'success'),
                                                    ('register-publication', 'success')), 50):
            self.values['jobs'].append(fixture.gate | {'id': index, 'name': name, 'conclusion': conclusion})

    def test_authenticates_controller_without_relabeling_api_event_or_source(self):
        controller = authorize_completed_controller(**self.values)
        self.assertEqual(controller.source_sha, self.values['run']['head_sha'])
        self.assertEqual(self.values['run']['event'], 'workflow_dispatch')
        with self.assertRaises(PublicationError):
            authorize_main_producer(**self.values)

    def test_rejects_push_pr_foreign_or_incomplete_invocations(self):
        for change in ({'event': 'push'}, {'event': 'pull_request'}, {'status': 'in_progress'},
                       {'conclusion': 'failure'}, {'head_branch': 'feature'}, {'workflow_id': 11},
                       {'head_repository': {'id': 2, 'full_name': 'foreign/repo'}}):
            with self.subTest(change=change), self.assertRaises(PublicationError):
                authorize_completed_controller(**(self.values | {'run': self.values['run'] | change}))

    def test_rejects_wrong_app_and_changed_controller_ancestry(self):
        for replacement in ({'required_check': self.values['required_check'] | {'app': {'id': 42, 'slug': 'github-actions'}}},
                            {'comparison': self.values['comparison'] | {'status': 'diverged'}}):
            with self.subTest(replacement=replacement), self.assertRaises(PublicationError):
                authorize_completed_controller(**(self.values | replacement))

    def test_each_required_job_must_belong_to_exact_attempt_and_selected_route(self):
        for index in range(1, len(self.values['jobs'])):
            for change in ({'run_attempt': 1}, {'run_id': 21}, {'head_sha': 'b' * 40}, {'id': True},
                           {'status': 'in_progress'}, {'conclusion': 'failure'}):
                jobs = list(self.values['jobs'])
                jobs[index] = jobs[index] | change
                with self.subTest(index=index, change=change), self.assertRaises(PublicationError):
                    authorize_completed_controller(**(self.values | {'jobs': jobs}))
            for jobs in (self.values['jobs'][:index] + self.values['jobs'][index + 1:],
                         self.values['jobs'] + [self.values['jobs'][index]]):
                with self.assertRaises(PublicationError):
                    authorize_completed_controller(**(self.values | {'jobs': jobs}))

    def test_rejects_production_js_execution(self):
        jobs = [job | {'conclusion': 'success'} if job['name'] == 'js / main' else job for job in self.values['jobs']]
        with self.assertRaises(PublicationError):
            authorize_completed_controller(**(self.values | {'jobs': jobs}))

    def client(self):
        client = Mock(repository='example/fork')
        metadata = {'': self.values['repository'], '/actions/workflows/ci.yml': self.values['workflow'],
                    '/actions/runs/20/attempts/2': self.values['run'], '/check-runs/40': self.values['required_check']}
        client.get.side_effect = metadata.__getitem__
        client.list.return_value = self.values['jobs']
        client.main_comparison.return_value = self.values['comparison']
        return client

    def test_transport_fetches_exact_attempt_check_and_controller_comparison(self):
        client = self.client()
        controller, jobs = completed_controller(client, 20, 2)
        self.assertEqual(controller.run_attempt, 2)
        self.assertEqual(jobs, self.values['jobs'])
        client.list.assert_called_once_with('/actions/runs/20/attempts/2/jobs', 'jobs')
        client.main_comparison.assert_called_once_with(self.values['run']['head_sha'])
        self.assertIn('/check-runs/40', [call.args[0] for call in client.get.call_args_list])

    def test_transport_rejects_untrusted_check_locations_before_requesting_them(self):
        for url in ('https://other.invalid/check-runs/40',
                    'https://api.github.com/repos/foreign/repo/check-runs/40',
                    'https://api.github.com/repos/example/fork/check-runs/40?other=1',
                    'https://api.github.com/repos/example/fork/check-runs/../40'):
            self.values['jobs'][0]['check_run_url'] = url
            client = self.client()
            with self.subTest(url=url), self.assertRaises(PublicationError):
                completed_controller(client, 20, 2)
            self.assertEqual([call.args[0] for call in client.get.call_args_list],
                             ['', '/actions/workflows/ci.yml', '/actions/runs/20/attempts/2'])

    def test_transport_rejects_invalid_attempt_before_any_api_request(self):
        client = self.client()
        for attempt in (True, 0, '2', None):
            with self.subTest(attempt=attempt), self.assertRaises(PublicationError):
                completed_controller(client, 20, attempt)
        client.get.assert_not_called()


if __name__ == '__main__':
    unittest.main()
