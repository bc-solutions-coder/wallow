"""A recovered plan requires exact registration bytes and explicit selection."""

from dataclasses import asdict
import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import unittest
from unittest.mock import Mock, patch
import zipfile

from publication import PublicationError
from publication_artifacts import Artifact
from publication_identity import RecoveryProducer, payload_identity
from publication_plan import validate_registration as validate_ordinary
from recovery_plan import resolve, validate_registration


class RecoveryPlanTests(unittest.TestCase):
    def setUp(self):
        self.producer = RecoveryProducer('owner/repo', 1, 'a' * 40, 20, 2, 30,
                                         'owner/repo/.github/workflows/ci.yml@refs/heads/main',
                                         'b' * 40, 'sha256:' + 'c' * 64, 40)
        self.request_artifact = Artifact(50, 'recovery-request-20-2', 'sha256:' + 'd' * 64, 100)
        self.request = {'release': {'id': 40, 'component_path': '.', 'version': '1.0.0'}}
        self.catalog = {'components': [{'path': '.'}]}
        self.artifacts = [{'id': 60}]
        self.record = {'schema': 2, 'producer': asdict(self.producer), 'route': 'full', 'artifacts': self.artifacts,
                       'component_versions': {'.': '1.0.0'}, 'input_sha256': {'global.json': 'e' * 64},
                       'catalog': self.catalog, 'recovery_request': {'artifact': asdict(self.request_artifact),
                           'sha256': self.producer.recovery_request_sha256, 'record': self.request}}

    def validate(self, record):
        return validate_registration(record, self.producer, self.artifacts, self.request_artifact, self.request, self.catalog)

    def test_distinct_recovery_registration_is_not_an_ordinary_producer(self):
        self.validate(self.record)
        with self.assertRaises(PublicationError):
            validate_ordinary(self.record, self.producer, 'full', self.artifacts)

    def test_rejects_changed_registration_binding_inputs_or_schema(self):
        changes = [{'schema': 1}, {'schema': True}, {'route': 'docs'}, {'artifacts': []},
                   {'producer': asdict(self.producer) | {'source_sha': 'f' * 40}},
                   {'recovery_request': self.record['recovery_request'] | {'sha256': 'sha256:' + 'f' * 64}},
                   {'component_versions': {'.': '2.0.0'}}, {'catalog': {}}, {'input_sha256': {'../outside': 'e' * 64}},
                   {'unexpected': True}]
        for change in changes:
            with self.subTest(change=change), self.assertRaises(PublicationError):
                self.validate(self.record | change)

    def test_real_registration_archive_resolves_only_after_controller_and_request_authorization(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            archive = root / 'registration.zip'
            data = json.dumps(self.record).encode()
            seal = payload_identity(self.producer, 'registration', 'publication') | {
                'file': 'registration.json', 'sha256': hashlib.sha256(data).hexdigest()}
            with zipfile.ZipFile(archive, 'w') as output:
                output.writestr('registration.json', data)
                output.writestr('registration.json.json', json.dumps(seal))
            metadata = {'id': 70, 'name': 'producer-registration-20-2', 'size_in_bytes': archive.stat().st_size,
                        'digest': 'sha256:' + hashlib.sha256(archive.read_bytes()).hexdigest(),
                        'expired': False, 'expires_at': '2099-01-01T00:00:00Z', 'workflow_run': {
                            'id': 20, 'repository_id': 1, 'head_repository_id': 1, 'head_sha': 'b' * 40, 'head_branch': 'main'}}
            client = Mock()
            client.controller.return_value = 'f' * 40
            client.list.return_value = [metadata]
            client.download.side_effect = lambda selected, target: Path(shutil.copyfile(archive, target))
            with patch('recovery_plan.completed_controller', return_value=('authenticated controller', [])) as controller, \
                 patch('recovery_plan.authenticate_request', return_value=(self.producer, self.request_artifact, self.request)) as request, \
                 patch('recovery_plan.candidate_artifacts', return_value=('full', self.artifacts)):
                plan = resolve(client, {'event_name': 'workflow_dispatch'}, 20, 2, self.catalog)
                controller.assert_called_once_with(client, 20, 2)
                request.assert_called_once_with(client, 'authenticated controller', [metadata], self.catalog)
            self.assertEqual(plan['inputs'], self.record)
            self.assertEqual(plan['producer'], asdict(self.producer))
            self.assertEqual(plan['controller_sha'], 'f' * 40)
            self.assertEqual(plan['registration']['id'], 70)

    def test_automatic_or_pr_selection_is_rejected_before_api_access(self):
        client = Mock()
        for event in ('workflow_run', 'pull_request', 'push'):
            with self.subTest(event=event), self.assertRaises(PublicationError):
                resolve(client, {'event_name': event}, 20, 2, self.catalog)
        client.controller.assert_not_called()


if __name__ == '__main__':
    unittest.main()
