from copy import deepcopy
from datetime import datetime, timedelta, timezone
import hashlib
import json
import unittest

from publication import PublicationError
from publication_image_provenance import nightly_action
from publication_legacy_nightly import legacy_source


class LegacyTests(unittest.TestCase):
    def setUp(self):
        self.source, self.next = 'a' * 40, 'b' * 40
        body = json.dumps({'schemaVersion': 2, 'mediaType': 'application/vnd.docker.distribution.manifest.list.v2+json', 'manifests': []}).encode()
        self.current = {'bytes': body, 'media_type': 'application/vnd.docker.distribution.manifest.list.v2+json', 'digest': 'sha256:' + hashlib.sha256(body).hexdigest()}
        now = datetime.now(timezone.utc)
        self.record = {'schema': 1, 'repository': 'example/repo', 'source_sha': 'sha1:' + self.source, 'run_id': 100, 'run_attempt': 1,
                       'workflow_id': 5, 'tag': self.source[:7], 'created': (now - timedelta(minutes=1)).strftime('%Y-%m-%dT%H:%M:%SZ'),
                       'expires': (now + timedelta(days=1)).strftime('%Y-%m-%dT%H:%M:%SZ'), 'evidence': 'https://github.com/example/repo/issues/1',
                       'images': {'api': self.current['digest']}}
        self.run = {'id': 100, 'run_attempt': 1, 'workflow_id': 5, 'path': '.github/workflows/deploy.yml', 'head_sha': self.source,
                    'head_branch': 'main', 'event': 'push', 'status': 'completed', 'conclusion': 'success',
                    'repository': {'full_name': 'example/repo'}, 'head_repository': {'full_name': 'example/repo'}}
        self.tag_value = self.current
        self.comparison = {'status': 'ahead', 'base_commit': {'sha': self.source}, 'merge_base_commit': {'sha': self.source}}

    def get(self, path):
        if path == '/actions/runs/100/attempts/1':
            return self.run
        self.assertEqual(path, '/compare/' + self.source + '...' + self.next)
        return self.comparison

    def main_comparison(self, source):
        self.assertEqual(source, self.source)
        return {'status': 'ahead', 'base_commit': {'sha': source}, 'merge_base_commit': {'sha': source}}

    def read_manifest(self, reference):
        self.assertEqual(reference, self.source[:7])
        return self.tag_value

    def test_only_exact_reviewed_legacy_state_can_advance(self):
        self.assertEqual(nightly_action(self, self, self.current, 'example/repo', 'api', self.next, self.record), 'advance')
        for record in (None, self.record | {'repository': 'other/repo'}, self.record | {'images': {'api': 'sha256:' + 'c' * 64}}):
            with self.subTest(record=record), self.assertRaises(PublicationError):
                nightly_action(self, self, self.current, 'example/repo', 'api', self.next, record)

    def test_changed_deployment_tag_or_metadata_cannot_be_adopted(self):
        self.tag_value = None
        with self.assertRaises(PublicationError):
            legacy_source(self, self, self.current, 'example/repo', 'api', self.record)
        self.tag_value = self.current
        for key, value in [('head_sha', self.next), ('event', 'pull_request'), ('head_branch', 'feature'), ('conclusion', 'failure'), ('workflow_id', 6), ('run_attempt', 2), ('repository', {'full_name': 'other/repo'})]:
            original = deepcopy(self.run)
            self.run[key] = value
            with self.subTest(key=key), self.assertRaises(PublicationError):
                legacy_source(self, self, self.current, 'example/repo', 'api', self.record)
            self.run = original

    def test_expired_or_unbounded_snapshot_is_rejected(self):
        for change in ({'expires': '2020-01-01T00:00:00Z'}, {'created': '2020-01-01T00:00:00Z'}, {'expires': 'invalid'}, {'schema': True}, {'tag': 'different'}):
            with self.subTest(change=change), self.assertRaises(PublicationError):
                legacy_source(self, self, self.current, 'example/repo', 'api', self.record | change)

    def test_legacy_cutover_still_cannot_move_backward_or_across_history(self):
        self.comparison = {'status': 'behind', 'base_commit': {'sha': self.source}, 'merge_base_commit': {'sha': self.next}}
        self.assertEqual(nightly_action(self, self, self.current, 'example/repo', 'api', self.next, self.record), 'skip-older')
        self.comparison = {'status': 'diverged'}
        with self.assertRaises(PublicationError):
            nightly_action(self, self, self.current, 'example/repo', 'api', self.next, self.record)
