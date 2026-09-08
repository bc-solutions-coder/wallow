import copy
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from publication import Producer, PublicationError
from release_evidence import begin, finish, output_list, release_commit


class Client:
    def __init__(self):
        self.repository = 'example/repo'
        self.producer_identity = Producer(self.repository, 1, 'a' * 40, 20, 2, 10, self.repository + '/.github/workflows/ci.yml@refs/heads/main')
        self.values = {}
        self.calls = []
        self.jobs = [{'id': 99, 'name': 'Release automation / Release Please'}]

    def controller(self, context):
        self.calls.append('controller')
        return 'b' * 40

    def producer(self, run, attempt):
        self.calls.append(('producer', run, attempt))
        return self.producer_identity, []

    def get(self, path):
        self.calls.append(path)
        return copy.deepcopy(self.values[path])

    def list(self, path, key):
        self.calls.append(path)
        return self.jobs


class ReleaseEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.client = Client()
        self.user = {'id': 123, 'login': 'release-owner', 'type': 'User'}
        self.client.values = {'/user': self.user, '/git/ref/heads/main': {'object': {'sha': 'a' * 40}},
                              '/actions/runs/30/attempts/1': {'id': 30, 'run_attempt': 1, 'workflow_id': 4, 'path': '.github/workflows/publish.yml',
                                  'repository': {'id': 1, 'full_name': 'example/repo'}, 'actor': self.user, 'triggering_actor': self.user}}
        self.invocation = {'repository': 'example/repo', 'producer': {'source_sha': 'a' * 40}}
        self.catalog = {'components': [{'id': 'sdk', 'path': 'packages/sdk', 'tag_prefix': 'sdk-v'}]}
        branch = {'sha': 'a' * 40, 'ref': 'main', 'repo': {'full_name': 'example/repo'}}
        self.client.values['/pulls/5'] = {'id': 55, 'number': 5, 'user': self.user, 'head': branch | {'ref': 'release-please--branches--main'},
                                         'base': branch, 'state': 'open', 'merged': False, 'merge_commit_sha': None}
        self.client.values['/releases/6'] = {'id': 6, 'tag_name': 'sdk-v1.2.3', 'author': self.user, 'draft': False, 'prerelease': False}
        self.client.values['/git/ref/tags/sdk-v1.2.3'] = {'ref': 'refs/tags/sdk-v1.2.3', 'object': {'type': 'tag', 'sha': 'c' * 40}}
        self.client.values['/git/tags/' + 'c' * 40] = {'object': {'type': 'commit', 'sha': 'd' * 40}}
        self.outputs = {'prs': json.dumps([{'number': 5}]), 'paths_released': '["packages/sdk"]', 'packages/sdk--id': '6', 'packages/sdk--tag_name': 'sdk-v1.2.3'}

    def test_begin_records_exact_api_invocation_job_producer_and_credential_actor(self):
        record = begin(self.client, self.client, {'workflow_ref': 'example/repo/.github/workflows/publish.yml@refs/heads/main'}, 20, 2, 30, 1)
        self.assertEqual(record['job_id'], 99)
        self.assertEqual(record['credential_actor']['id'], 123)
        self.assertEqual((record['run_id'], record['run_attempt']), (30, 1))
        self.assertEqual(record['producer']['run_attempt'], 2)
        self.assertFalse(record['publication_authorized'])
        self.assertEqual(self.client.calls[:2], ['controller', ('producer', 20, 2)])
        original = copy.deepcopy(self.client.values['/actions/runs/30/attempts/1'])
        for change in [{'id': 31}, {'run_attempt': 2}, {'repository': {'id': 2, 'full_name': 'foreign/repo'}}]:
            self.client.values['/actions/runs/30/attempts/1'] = original | change
            with self.assertRaises(PublicationError):
                begin(self.client, self.client, {'workflow_ref': 'x'}, 20, 2, 30, 1)

    def test_records_exact_pr_and_resolved_release_but_never_authorizes_newer_sha(self):
        record = finish(self.client, self.invocation, self.outputs, 'success', self.catalog)
        self.assertNotIn('reconciliation_error', record)
        self.assertEqual(record['pull_requests'][0]['id'], 55)
        self.assertEqual(record['pull_requests'][0]['head_sha'], 'a' * 40)
        release = record['releases'][0]
        self.assertEqual((release['id'], release['version'], release['component']), (6, '1.2.3', 'sdk'))
        self.assertEqual(release['commit_sha'], 'd' * 40)
        self.assertFalse(release['matches_producer_sha'])
        self.assertFalse(record['publication_authorized'])

    def test_preserves_created_pr_evidence_when_later_release_reconciliation_fails(self):
        self.client.values['/releases/6']['tag_name'] = 'unexpected'
        record = finish(self.client, self.invocation, self.outputs, 'failure', self.catalog)
        self.assertIn('reconciliation_error', record)
        self.assertEqual(record['pull_requests'][0]['number'], 5)
        self.assertFalse(record['publication_authorized'])

    def test_noop_and_failed_action_without_outputs_are_still_recorded(self):
        for outcome in ('success', 'failure'):
            record = finish(self.client, self.invocation, {}, outcome, self.catalog)
            self.assertEqual(record['action_outcome'], outcome)
            self.assertEqual(record['releases'], [])
            self.assertEqual(record['pull_requests'], [])

    def test_foreign_pr_wrong_action_identity_or_unknown_component_fail_closed(self):
        original = copy.deepcopy(self.client.values['/pulls/5'])
        for change in ('foreign', 'number', 'component'):
            self.client.values['/pulls/5'] = copy.deepcopy(original)
            outputs = copy.deepcopy(self.outputs)
            if change == 'foreign':
                self.client.values['/pulls/5']['head']['repo']['full_name'] = 'foreign/repo'
            elif change == 'number':
                outputs['prs'] = '[{"number":true}]'
            else:
                outputs['paths_released'] = '["packages/unknown"]'
            record = finish(self.client, self.invocation, outputs, 'success', self.catalog)
            self.assertIn('reconciliation_error', record)
            self.assertFalse(record['publication_authorized'])

    def test_json_and_tag_lookups_are_bounded_and_do_not_follow_output_urls(self):
        for raw in ('{}', 'bad', '[1]' * 40000, json.dumps([{}] * 21)):
            with self.assertRaises(PublicationError):
                output_list({'prs': raw}, 'prs')
        before = len(self.client.calls)
        for tag in ('../other', '/tag', 'x?url=https://other', 'x\n'):
            with self.assertRaises(PublicationError):
                release_commit(self.client, tag)
        self.assertEqual(len(self.client.calls), before)
        self.client.values['/git/tags/' + 'c' * 40] = {'object': {'type': 'tag', 'sha': 'c' * 40}}
        with self.assertRaises(PublicationError):
            release_commit(self.client, 'sdk-v1.2.3')

    def test_disabled_or_missing_token_cli_fails_before_network_or_action(self):
        script = Path(__file__).with_name('release_evidence.py')
        with tempfile.TemporaryDirectory() as directory:
            for enabled, credential in [('false', 'not-used'), ('TRUE', 'not-used'), ('true', '')]:
                environment = os.environ | {'GITHUB_REPOSITORY': 'example/repo', 'GITHUB_RUN_ID': '30', 'GITHUB_RUN_ATTEMPT': '1',
                                            'GH_TOKEN': 'not-used', 'ENABLE_RELEASE_AUTOMATION': enabled, 'RELEASE_PLEASE_TOKEN': credential}
                result = subprocess.run([sys.executable, str(script), 'begin', '--run-id', '20', '--attempt', '2', '--output', directory + '/result.json'],
                                        env=environment, text=True, capture_output=True, timeout=10)
                self.assertNotEqual(result.returncode, 0)
                self.assertNotIn('not-used', result.stderr + result.stdout)
                self.assertFalse(Path(directory, 'result.json').exists())


if __name__ == '__main__':
    unittest.main()
