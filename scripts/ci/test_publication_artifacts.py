import copy
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import stat
import tempfile
import unittest
import zipfile

from publication import Producer, PublicationError
from publication_artifacts import Artifact, select_artifact, unpack_payload


class ArtifactTests(unittest.TestCase):
    def setUp(self):
        self.producer = Producer('example/repo', 1, 'a' * 40, 20, 2, 10, 'example/repo/.github/workflows/ci.yml@refs/heads/main')
        self.metadata = {'id': 50, 'name': 'js-packages-20-2', 'size_in_bytes': 100, 'digest': 'sha256:' + 'b' * 64, 'expired': False, 'expires_at': '2030-01-01T00:00:00Z', 'workflow_run': {'id': 20, 'repository_id': 1, 'head_repository_id': 1, 'head_sha': 'a' * 40, 'head_branch': 'main'}}
        self.now = datetime(2026, 9, 7, tzinfo=timezone.utc)
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.payload = b'validated bytes'
        self.seal = {'schema': 1, 'repository': self.producer.repository, 'sha': self.producer.source_sha, 'run_id': '20', 'run_attempt': '2', 'workflow_ref': self.producer.workflow_ref, 'kind': 'packages', 'variant': 'pnpm', 'file': 'packages.tar.gz', 'sha256': hashlib.sha256(self.payload).hexdigest()}

    def select(self, items):
        return select_artifact(items, self.producer, 'js-packages', self.now)

    def archive(self, changes=None, extra=None, payload=None, symlink=False):
        path = self.root / 'artifact.zip'
        with zipfile.ZipFile(path, 'w') as bundle:
            member = zipfile.ZipInfo('packages.tar.gz')
            member.external_attr = ((stat.S_IFLNK if symlink else stat.S_IFREG) | 0o600) << 16
            bundle.writestr(member, self.payload if payload is None else payload)
            bundle.writestr('packages.tar.gz.json', json.dumps(self.seal | (changes or {})))
            if extra:
                bundle.writestr(extra, b'unexpected')
        descriptor = Artifact(50, 'js-packages-20-2', 'sha256:' + hashlib.sha256(path.read_bytes()).hexdigest(), path.stat().st_size)
        return path, descriptor

    def unpack(self, archive, artifact, limit=100):
        return unpack_payload(archive, self.root / 'output', artifact, self.producer, 'packages.tar.gz', 'packages', 'pnpm', limit)

    def test_selects_exact_retained_attempt(self):
        old = self.metadata | {'name': 'js-packages-20-1'}
        self.assertEqual(self.select([old, self.metadata]).id, 50)

    def test_rejects_missing_duplicate_expired_and_malformed_artifacts(self):
        cases = [[], [None], [self.metadata, self.metadata]]
        for change in [{'id': True}, {'size_in_bytes': 0}, {'digest': None}, {'expired': True}, {'expires_at': '2020-01-01T00:00:00Z'}, {'expires_at': '2030-01-01'}, {'expires_at': None}, {'name': 'js-packages-20-1'}]:
            cases.append([self.metadata | change])
        for items in cases:
            with self.subTest(items=items), self.assertRaises(PublicationError):
                self.select(items)

    def test_rejects_foreign_run_repository_branch_and_revision(self):
        for key, value in [('id', 21), ('repository_id', 2), ('head_repository_id', 2), ('head_sha', 'c' * 40), ('head_branch', 'feature')]:
            item = copy.deepcopy(self.metadata)
            item['workflow_run'][key] = value
            with self.subTest(key=key), self.assertRaises(PublicationError):
                self.select([item])

    def test_writes_only_verified_payload(self):
        archive, artifact = self.archive()
        output = self.unpack(archive, artifact)
        self.assertEqual(output.read_bytes(), self.payload)
        self.assertEqual(list(output.parent.iterdir()), [output])

    def test_rejects_download_tampering_before_creating_destination(self):
        archive, artifact = self.archive()
        archive.write_bytes(archive.read_bytes() + b'tampered')
        with self.assertRaises(PublicationError):
            self.unpack(archive, artifact)
        self.assertFalse((self.root / 'output').exists())

    def test_rejects_wrong_seal_identity_or_checksum_and_removes_partial_output(self):
        for change in [{'sha': 'c' * 40}, {'run_attempt': '1'}, {'repository': 'foreign/repo'}, {'workflow_ref': 'example/repo/.github/workflows/other.yml@refs/heads/main'}, {'kind': 'images'}, {'variant': 'other'}, {'schema': True}, {'sha256': '0' * 64}]:
            archive, artifact = self.archive(changes=change)
            with self.subTest(change=change), self.assertRaises(PublicationError):
                self.unpack(archive, artifact)
            self.assertFalse((self.root / 'output').exists())

    def test_rejects_traversal_extra_members_symlinks_and_size_overflow(self):
        for options in [{'extra': '../escaped'}, {'extra': '/absolute'}, {'extra': 'unexpected'}, {'symlink': True}, {'payload': b'x' * 101}]:
            archive, artifact = self.archive(**options)
            with self.subTest(options=options), self.assertRaises(PublicationError):
                self.unpack(archive, artifact)
            self.assertFalse((self.root / 'output').exists())
        self.assertFalse((self.root.parent / 'escaped').exists())

    def test_refuses_existing_destination(self):
        archive, artifact = self.archive()
        destination = self.root / 'output'
        destination.mkdir()
        marker = destination / 'existing'
        marker.write_text('keep')
        with self.assertRaises(PublicationError):
            self.unpack(archive, artifact)
        self.assertEqual(marker.read_text(), 'keep')


if __name__ == '__main__':
    unittest.main()
