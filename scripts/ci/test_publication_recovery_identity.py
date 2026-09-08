"""Recovery artifacts bind historical bytes to their actual Actions invocation."""

from dataclasses import asdict
import hashlib
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from publication import Producer, PublicationError
from publication_artifacts import Artifact, select_artifact, unpack_payload
from publication_identity import RecoveryProducer, payload_identity, producer_from_record
from validation import artifact


class RecoveryIdentityTests(unittest.TestCase):
    def setUp(self):
        self.producer = RecoveryProducer('owner/repo', 1, 'a' * 40, 20, 2, 30,
                                         'owner/repo/.github/workflows/ci.yml@refs/heads/main',
                                         'b' * 40, 'sha256:' + 'c' * 64, 40)
        self.environment = {'GITHUB_REPOSITORY': 'owner/repo', 'GITHUB_SHA': 'b' * 40,
                            'GITHUB_WORKFLOW_SHA': 'b' * 40, 'GITHUB_RUN_ID': '20', 'GITHUB_RUN_ATTEMPT': '2',
                            'GITHUB_WORKFLOW_REF': self.producer.workflow_ref, 'GITHUB_EVENT_NAME': 'workflow_dispatch',
                            'GITHUB_REF': 'refs/heads/main', 'CI_RECOVERY_SOURCE_SHA': 'a' * 40,
                            'CI_RECOVERY_REQUEST_SHA256': 'sha256:' + 'c' * 64, 'CI_RECOVERY_RELEASE_ID': '40'}
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.payload = self.root / 'packages.tar.gz'
        self.payload.write_bytes(b'exact validated historical bytes')
        self.manifest = self.root / 'packages.tar.gz.json'

    def sealed_archive(self, change=None):
        with patch.dict(os.environ, self.environment, clear=True):
            artifact('seal', self.payload, self.manifest, 'packages', 'pnpm')
        seal = json.loads(self.manifest.read_text()) | (change or {})
        archive = self.root / 'input.zip'
        with zipfile.ZipFile(archive, 'w') as output:
            output.writestr(self.payload.name, self.payload.read_bytes())
            output.writestr(self.manifest.name, json.dumps(seal))
        selected = Artifact(50, 'js-packages-20-2', 'sha256:' + hashlib.sha256(archive.read_bytes()).hexdigest(), archive.stat().st_size)
        return archive, selected

    def test_real_seal_and_unpack_preserve_source_and_controller(self):
        archive, selected = self.sealed_archive()
        result = unpack_payload(archive, self.root / 'result', selected, self.producer, self.payload.name, 'packages', 'pnpm', 1024)
        self.assertEqual(result.read_bytes(), self.payload.read_bytes())
        seal = json.loads(self.manifest.read_text())
        self.assertEqual(seal['sha'], 'b' * 40)
        self.assertEqual(seal['source_sha'], 'a' * 40)
        self.assertEqual(seal['schema'], 2)

    def test_api_artifact_revision_is_controller_not_historical_source(self):
        metadata = {'id': 50, 'name': 'js-packages-20-2', 'size_in_bytes': 100,
                    'digest': 'sha256:' + 'd' * 64, 'expired': False, 'expires_at': '2099-01-01T00:00:00Z',
                    'workflow_run': {'id': 20, 'repository_id': 1, 'head_repository_id': 1,
                                     'head_sha': 'b' * 40, 'head_branch': 'main'}}
        self.assertEqual(select_artifact([metadata], self.producer, 'js-packages').id, 50)
        metadata['workflow_run']['head_sha'] = 'a' * 40
        with self.assertRaises(PublicationError):
            select_artifact([metadata], self.producer, 'js-packages')

    def test_changed_source_request_release_or_schema_rejects_payload(self):
        for change in ({'source_sha': 'd' * 40}, {'sha': 'a' * 40}, {'recovery_request_sha256': 'sha256:' + 'e' * 64},
                       {'release_id': 41}, {'schema': 1}):
            with self.subTest(change=change):
                archive, selected = self.sealed_archive(change)
                with self.assertRaises(PublicationError):
                    unpack_payload(archive, self.root / 'result', selected, self.producer, self.payload.name, 'packages', 'pnpm', 1024)
                self.assertFalse((self.root / 'result').exists())

    def test_recovery_sealing_rejects_wrong_event_and_partial_identity(self):
        for changes in ({'GITHUB_EVENT_NAME': 'pull_request'}, {'GITHUB_REF': 'refs/heads/feature'},
                        {'GITHUB_WORKFLOW_SHA': 'd' * 40}, {'CI_RECOVERY_SOURCE_SHA': ''},
                        {'CI_RECOVERY_RELEASE_ID': '0'}):
            with self.subTest(changes=changes), patch.dict(os.environ, self.environment | changes, clear=True), self.assertRaises(ValueError):
                artifact('seal', self.payload, self.manifest, 'packages', 'pnpm')

    def test_record_round_trip_and_normal_schema_remain_distinct(self):
        self.assertEqual(producer_from_record(asdict(self.producer)), self.producer)
        normal = Producer('owner/repo', 1, 'a' * 40, 20, 2, 30, self.producer.workflow_ref)
        self.assertEqual(producer_from_record(asdict(normal)), normal)
        identity = payload_identity(normal, 'packages', 'pnpm')
        self.assertEqual(identity['schema'], 1)
        self.assertEqual(identity['sha'], normal.source_sha)
        self.assertNotIn('source_sha', identity)
        with self.assertRaises(PublicationError):
            producer_from_record(asdict(self.producer) | {'release_id': True})


if __name__ == '__main__':
    unittest.main()
