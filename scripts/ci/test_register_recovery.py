"""Recovery registration binds actual source inputs and a sealed request archive."""

from dataclasses import asdict
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import Mock, patch
import zipfile

from publication import Producer, PublicationError
from publication_identity import payload_identity
from register_recovery import register


class RecoveryRegistrationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / 'source'
        self.source.mkdir()
        for args in (['init', '-q'], ['config', 'user.name', 'Test'], ['config', 'user.email', 'test@example.invalid']):
            subprocess.run(['git', '-C', str(self.source), *args], check=True, capture_output=True)
        self.catalog = {'components': [{'id': 'platform', 'path': '.'}]}
        (self.source / '.release-please-manifest.json').write_text(json.dumps({'.': '1.0.0'}))
        (self.source / 'global.json').write_text('{"sdk":{"version":"10.0.100"}}')
        subprocess.run(['git', '-C', str(self.source), 'add', '.'], check=True)
        subprocess.run(['git', '-C', str(self.source), 'commit', '-qm', 'historical source'], check=True)
        sha = subprocess.check_output(['git', '-C', str(self.source), 'rev-parse', 'HEAD'], text=True).strip()
        self.controls = self.root / 'controls'
        (self.controls / '.github/ci').mkdir(parents=True)
        (self.controls / '.github/ci/publication.json').write_text(json.dumps(self.catalog))
        self.controller = Producer('owner/repo', 1, 'b' * 40, 20, 2, 30,
                                   'owner/repo/.github/workflows/ci.yml@refs/heads/main')
        self.expected = {'controller_sha': 'b' * 40, 'workflow_id': 30, 'source_sha': sha, 'release': {'id': 40}}
        self.data = json.dumps(self.expected).encode()
        self.identity = {'schema': 2, 'source_sha': sha, 'run_id': '20', 'run_attempt': '2', 'release_id': 40,
                         'workflow_ref': self.controller.workflow_ref,
                         'recovery_request_sha256': 'sha256:' + hashlib.sha256(self.data).hexdigest()}
        self.archive = self.root / 'request.zip'
        seal = payload_identity(self.controller, 'recovery-request', 'source') | {
            'file': 'request.json', 'sha256': hashlib.sha256(self.data).hexdigest()}
        with zipfile.ZipFile(self.archive, 'w') as archive:
            archive.writestr('request.json', self.data)
            archive.writestr('request.json.json', json.dumps(seal))
        self.metadata = {'id': 50, 'name': 'recovery-request-20-2', 'size_in_bytes': self.archive.stat().st_size,
                         'digest': 'sha256:' + hashlib.sha256(self.archive.read_bytes()).hexdigest(),
                         'expired': False, 'expires_at': '2099-01-01T00:00:00Z',
                         'workflow_run': {'id': 20, 'repository_id': 1, 'head_repository_id': 1,
                                          'head_sha': 'b' * 40, 'head_branch': 'main'}}
        self.client = Mock(repository='owner/repo')
        self.client.get.return_value = {'id': 1}
        self.jobs = [{'name': 'build', 'conclusion': 'success'}, {'name': 'CI / required', 'conclusion': 'success'}]
        self.client.list.side_effect = lambda path, key: [self.metadata] if key == 'artifacts' else self.jobs
        self.client.download.side_effect = lambda artifact, target: Path(shutil.copyfile(self.archive, target))
        self.catalog_patch = patch('register_recovery.load_catalog', return_value=self.catalog)
        self.request_patch = patch('register_recovery.request', return_value=self.expected)
        self.candidates_patch = patch('register_recovery.candidate_artifacts', return_value=('full', [{'id': 60}]))
        for patcher in (self.catalog_patch, self.request_patch, self.candidates_patch):
            patcher.start()
            self.addCleanup(patcher.stop)

    def register(self):
        return register(self.client, {}, self.identity, self.source, self.controls)

    def test_records_source_controller_request_and_actual_input_bytes(self):
        record = self.register()
        self.assertEqual(record['schema'], 2)
        self.assertEqual(record['producer']['source_sha'], self.identity['source_sha'])
        self.assertEqual(record['producer']['controller_sha'], self.controller.source_sha)
        self.assertEqual(record['recovery_request']['record'], self.expected)
        self.assertEqual(record['recovery_request']['artifact']['id'], 50)
        self.assertEqual(record['input_sha256']['global.json'], hashlib.sha256((self.source / 'global.json').read_bytes()).hexdigest())

    def test_rejects_changed_request_digest(self):
        self.identity['recovery_request_sha256'] = 'sha256:' + 'f' * 64
        with self.assertRaises(PublicationError):
            self.register()

    def test_rejects_changed_release_authority(self):
        self.expected['release']['id'] = 41
        with self.assertRaises(PublicationError):
            self.register()

    def test_rejects_other_source_catalog_or_failed_gate(self):
        original = self.identity['source_sha']
        self.identity['source_sha'] = 'c' * 40
        with self.assertRaises(PublicationError):
            self.register()
        self.identity['source_sha'] = original
        (self.controls / '.github/ci/publication.json').write_text('{}')
        with self.assertRaises(PublicationError):
            self.register()
        (self.controls / '.github/ci/publication.json').write_text(json.dumps(self.catalog))
        self.jobs[1]['conclusion'] = 'failure'
        with self.assertRaises(PublicationError):
            self.register()


if __name__ == '__main__':
    unittest.main()
