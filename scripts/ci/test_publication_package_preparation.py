import copy
from dataclasses import asdict
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from publication import PublicationError
from publication_package_preparation import authorize_packages, policy_digest, snapshot
from publication_prepare_packages import prepare_packages
import test_publication_prepare_packages as fixtures


class AuthorizationTests(unittest.TestCase):
    def setUp(self):
        fixture = fixtures.PreparationTests()
        fixture.setUp()
        self.addCleanup(fixture.temp.cleanup)
        self.fixture = fixture
        self.authority, self.client, _ = fixture.authority()
        self.producer = fixture.fixture.producer
        self.authority['ready'][0]['plan']['artifacts'].append({'kind': 'dependencies', 'id': 77})
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / '.github/ci').mkdir(parents=True)
        (self.root / '.github/ci/security-exceptions.json').write_text('{}')
        def scan(client, plan, reports):
            return fixture.scan(client, plan, reports) | {'artifact_id': 77}
        prepared = prepare_packages(self.client, self.authority, fixture.catalog, self.root / 'prepared', self.root / 'reports', scan=scan)
        self.invocation = {'job_id': 100}
        self.plan = {'schema': 1, 'invocation': self.invocation, 'trigger_producer': asdict(self.producer), 'catalog': fixture.catalog,
                     'authority': snapshot(self.authority), 'policy_sha256': policy_digest(self.root), 'prepared': prepared, 'publication_authorized': False}

    def artifact(self, plan, bad_seal=False):
        body = json.dumps(plan).encode()
        seal = {'schema': 1, 'repository': self.producer.repository, 'sha': self.producer.source_sha, 'run_id': str(self.producer.run_id),
                'run_attempt': '1' if bad_seal else str(self.producer.run_attempt), 'workflow_ref': self.producer.workflow_ref,
                'kind': 'package-plan', 'variant': 'release', 'file': 'plan.json', 'sha256': hashlib.sha256(body).hexdigest()}
        output = io.BytesIO()
        with zipfile.ZipFile(output, 'w') as archive:
            archive.writestr('plan.json', body)
            archive.writestr('plan.json.json', json.dumps(seal))
        self.client.archive = output.getvalue()
        base = {'id': 50, 'name': 'package-plan-20-2', 'digest': 'sha256:' + hashlib.sha256(self.client.archive).hexdigest(),
                'size_in_bytes': len(self.client.archive), 'expired': False, 'expires_at': '2030-01-01T00:00:00Z',
                'workflow_run': {'id': 20, 'repository_id': 1, 'head_repository_id': 1, 'head_sha': self.producer.source_sha, 'head_branch': 'main'}}
        return [base, base | {'id': 51, 'name': 'prepared-packages-20-2'}]

    def authorize(self, plan=None, bad_seal=False, recovery=None):
        artifacts = self.artifact(plan or self.plan, bad_seal)
        with patch('publication_package_preparation.package_configuration'), patch('publication_package_preparation.package_environment'), \
             patch('publication_package_preparation.authorize_preparation', return_value=({'producer': asdict(self.producer)}, self.producer, artifacts, {})) as trigger, \
             patch('publication_package_preparation.frame', return_value=(self.invocation, {})), \
             patch('publication_package_preparation.package_releases', return_value=self.authority) as releases:
            result = authorize_packages(self.client, {}, 1, 1, 20, 2, self.fixture.catalog, self.root, recovery=recovery)
            self.trigger_call, self.releases_call = trigger.call_args, releases.call_args
            return result

    def test_recovery_receipt_is_reauthorized_and_bound_into_sealed_package_plan(self):
        recovery = {'release_id': 5, 'run_id': 70, 'run_attempt': 2}
        self.authority['recovery'] = {'original': {'producer': {'run_id': 1, 'run_attempt': 1}},
                                      'receipt': {'asset_id': 90, 'sha256': 'sha256:' + 'd' * 64}}
        self.plan['authority'] = copy.deepcopy(snapshot(self.authority))
        self.authorize(recovery=recovery)
        self.assertEqual(self.trigger_call.kwargs, {'recovery': recovery, 'catalog': self.fixture.catalog})
        self.assertEqual(self.releases_call.kwargs, {'recovery': recovery})
        self.plan['authority']['recovery']['receipt']['asset_id'] = 91
        with self.assertRaises(PublicationError):
            self.authorize(recovery=recovery)

    def test_exact_sealed_plan_preserves_producer_and_selected_artifact(self):
        plan, producer, artifact, evidence = self.authorize()
        self.assertEqual(plan, self.plan)
        self.assertEqual(producer, self.producer)
        self.assertEqual(artifact.id, 51)
        self.assertEqual(evidence['prepare_job_id'], 100)
        self.assertTrue(all(not path.parent.exists() for path in self.client.paths))

    def test_changed_policy_job_receipt_scan_or_missing_candidate_fails(self):
        for change in ('policy', 'job', 'selection', 'scan', 'missing'):
            plan = copy.deepcopy(self.plan)
            if change == 'policy':
                plan['policy_sha256'] = 'sha256:' + '0' * 64
            elif change == 'job':
                plan['invocation']['job_id'] = 101
            elif change == 'selection':
                plan['prepared']['candidates'][0]['selection']['asset_id'] = 999
            elif change == 'scan':
                plan['prepared']['candidates'][0]['dependency_scan']['artifact_id'] = 999
            else:
                plan['prepared']['candidates'] = []
            with self.subTest(change=change), self.assertRaises(PublicationError):
                self.authorize(plan)

    def test_wrong_attempt_seal_is_rejected(self):
        with self.assertRaises(PublicationError):
            self.authorize(bad_seal=True)


if __name__ == '__main__':
    unittest.main()
