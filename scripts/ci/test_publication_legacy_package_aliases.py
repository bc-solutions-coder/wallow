import copy
from datetime import datetime, timezone
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from publication import PublicationError
from publication_legacy_package_aliases import authorize


class LegacyAliasTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.path = self.root / '.github/ci/legacy-package-aliases.json'
        self.path.parent.mkdir(parents=True)
        self.old = {'id': 1, 'version': '1.0.0', 'commit_sha': 'a' * 40, 'prerelease': False, 'component': 'sdk'}
        self.candidate = {'release': {'id': 2, 'version': '2.0.0', 'commit_sha': 'b' * 40, 'prerelease': False, 'component': 'sdk'},
                          'receipt': {'asset_id': 3, 'sha256': 'sha256:' + 'c' * 64},
                          'outputs': {'package': {'name': '@example/sdk', 'version': '2.0.0'}}}
        self.entry = {'repository': 'example/repo', 'package': '@example/sdk', 'alias': 'latest',
                      'previous': {key: self.old[key] for key in ('id', 'version', 'commit_sha')},
                      'destination': {key: self.candidate['release'][key] for key in ('id', 'version', 'commit_sha')},
                      'receipt': self.candidate['receipt'], 'expires_at': '2026-10-08T00:00:00Z'}
        self.client = SimpleNamespace(repository='example/repo', get=lambda path: {'status': 'ahead', 'merge_base_commit': {'sha': 'a' * 40}})
        self.now = datetime(2026, 9, 8, tzinfo=timezone.utc)

    def run_authorize(self, entries=None):
        self.path.write_text(json.dumps({'schema': 1, 'entries': [self.entry] if entries is None else entries}))
        with patch('publication_legacy_package_aliases.release_identity', return_value=self.old):
            return authorize(self.client, {}, self.root, self.candidate, 'latest', '1.0.0', now=self.now)

    def test_exact_transition_returns_bound_evidence(self):
        result = self.run_authorize()
        self.assertEqual(result['entry'], self.entry)
        self.assertTrue(result['sha256'].startswith('sha256:'))

    def test_changed_scope_target_receipt_expiry_and_ancestry_fail(self):
        original = copy.deepcopy(self.entry)
        for field in ('repository', 'package', 'alias', 'previous', 'destination', 'receipt', 'expires_at'):
            self.entry = copy.deepcopy(original)
            if field in ('previous', 'destination'):
                self.entry[field]['version'] = '9.0.0'
            elif field == 'receipt':
                self.entry[field]['asset_id'] = 99
            elif field == 'expires_at':
                self.entry[field] = '2026-09-07T00:00:00Z'
            else:
                self.entry[field] = 'wrong'
            with self.subTest(field=field), self.assertRaises(PublicationError):
                self.run_authorize()
        self.entry = original
        self.client.get = lambda path: {'status': 'diverged', 'merge_base_commit': {'sha': 'd' * 40}}
        with self.assertRaises(PublicationError):
            self.run_authorize()

    def test_duplicate_and_malformed_configuration_fail(self):
        with self.assertRaises(PublicationError):
            self.run_authorize([self.entry, self.entry])
        self.entry['unexpected'] = True
        with self.assertRaises(PublicationError):
            self.run_authorize()

    def test_missing_or_retired_configuration_grants_nothing(self):
        self.assertIsNone(authorize(self.client, {}, self.root, self.candidate, 'latest', '1.0.0', now=self.now))
        self.assertIsNone(self.run_authorize([]))
