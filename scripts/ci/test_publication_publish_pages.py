from datetime import datetime, timezone
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from publication import PublicationError
from publication_artifacts import Artifact
from publication_pages_history import TASK
from publication_publish_pages import publish_pages
from publication_release_github import ACTIONS_ACTOR


class PagesWriterTests(unittest.TestCase):
    repository = 'example/repo'

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.output = Path(temporary.name) / 'progress' / 'progress.json'
        self.source = 'a' * 40
        self.plan = {'producer': {'repository': self.repository, 'repository_id': 1, 'source_sha': self.source,
                                  'run_id': 2, 'run_attempt': 1, 'workflow_id': 3}, 'controller_sha': 'b' * 40}
        self.artifact = Artifact(4, 'prepared-site-5-1', 'sha256:' + 'c' * 64, 1000)
        self.identity = {'tar_sha256': 'd' * 64, 'tar_size': 10240, 'inventory_sha256': 'e' * 64,
                         'file_count': 1, 'index_sha256': 'f' * 64, 'index_size': 5}
        self.settings = {'html_url': 'https://example.github.io/repo/'}
        self.current = ({'run_id': 5, 'run_attempt': 1}, {'started_at': '2020-01-01T00:00:00Z'})
        self.events, self.statuses = [], []
        self.failed = False

    def array(self, path):
        return self.statuses if path.endswith('/statuses') else ([self.record] if hasattr(self, 'record') else [])

    def ensure_unpublished(self, url):
        self.events.append('unpublished')

    def main_comparison(self, source):
        return {'status': 'ahead', 'base_commit': {'sha': source}, 'merge_base_commit': {'sha': source}}

    def oidc(self, url, token):
        self.events.append('oidc')
        return 'private-oidc-token'

    def intent(self, payload):
        self.events.append('intent')
        self.record = {'id': 6, 'task': TASK, 'environment': 'github-pages', 'sha': self.source,
                       'creator': ACTIONS_ACTOR, 'payload': payload, 'created_at': datetime.now(timezone.utc).isoformat()}
        return self.record

    def get(self, path):
        self.events.append('intent-readback')
        return self.record

    def status(self, deployment, state, run, url, description):
        self.events.append(state)
        result = {'id': len(self.statuses) + 10, 'state': state, 'environment_url': url}
        self.statuses.append(result)
        return result

    def deploy(self, artifact, source, token):
        self.events.append('deploy')
        self.assertEqual((artifact, source, token), (4, self.source, 'private-oidc-token'))
        if self.failed:
            raise PublicationError('simulated Pages failure')
        return {'id': self.source}

    def wait_deployment(self, deployment):
        self.events.append('wait')
        return {'status': 'succeed'}

    def verify_index(self, url, identity):
        self.events.append('read-index')
        return {'sha256': identity['index_sha256'], 'size': identity['index_size']}

    def publish(self):
        return publish_pages(self, self.plan, self.artifact, self.identity, self.settings,
                             self.current, self.output, 'trusted-runtime-url', 'runtime-token')

    def test_intent_is_verified_before_deployment_and_readback_before_success(self):
        result = self.publish()
        self.assertEqual(self.events, ['unpublished', 'oidc', 'intent', 'intent-readback', 'in_progress', 'deploy', 'wait', 'read-index', 'success'])
        self.assertEqual(result['action'], 'verified')
        saved = json.loads(self.output.read_text())
        self.assertEqual(saved['artifact']['id'], 4)
        self.assertEqual(saved['pages_deployment_id'], self.source)
        self.assertNotIn('private-oidc-token', self.output.read_text())

    def test_older_retry_has_no_oidc_intent_or_deployment_writes(self):
        with patch('publication_publish_pages.pages_action', return_value='skip-older'):
            result = self.publish()
        self.assertEqual(result['action'], 'skip-older')
        self.assertEqual(self.events, ['unpublished'])

    def test_failed_service_preserves_intent_and_partial_progress(self):
        self.failed = True
        with self.assertRaises(PublicationError): self.publish()
        saved = json.loads(self.output.read_text())
        self.assertEqual(saved['intent_id'], 6)
        self.assertEqual(saved['action'], 'incomplete')
        self.assertNotIn('success', self.events)

    def test_unknown_history_fails_before_any_oidc_or_write(self):
        with patch('publication_publish_pages.pages_history', side_effect=PublicationError('unknown')):
            with self.assertRaises(PublicationError): self.publish()
        self.assertEqual(self.events, [])
        self.assertFalse(self.output.exists())
