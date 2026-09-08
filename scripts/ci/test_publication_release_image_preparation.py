import copy
from dataclasses import asdict
import hashlib
import io
import json
from unittest.mock import patch
import unittest
import zipfile

from publication import PublicationError
from publication_release_image_authorization import authorize_prepared, policy_digest
import test_publication_release_image_writer as fixtures


class PreparationTests(unittest.TestCase):
    def setUp(self):
        self.fixture = fixtures.ReleaseImageTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.client = self.fixture.fixture
        self.plan = copy.deepcopy(self.fixture.plan)
        original = copy.deepcopy(self.plan['source'])
        del original['verified_images']
        self.plan['authority']['plan'] = original
        self.plan.update(schema=1, invocation={'job_id': 100}, policy_sha256=policy_digest(self.client.root), publication_authorized=False)
        self.authority = copy.deepcopy(self.plan['authority'])
        self.preparation = self.client.preparation

    def artifacts(self, plan, bad_seal=False):
        body = json.dumps(plan).encode()
        producer = self.preparation
        seal = {'schema': 1, 'repository': producer.repository, 'sha': producer.source_sha, 'run_id': str(producer.run_id), 'run_attempt': str(producer.run_attempt),
                'workflow_ref': producer.workflow_ref, 'kind': 'release-image-plan', 'variant': '6' if bad_seal else '5', 'file': 'plan.json', 'sha256': hashlib.sha256(body).hexdigest()}
        output = io.BytesIO()
        with zipfile.ZipFile(output, 'w') as archive:
            archive.writestr('plan.json', body)
            archive.writestr('plan.json.json', json.dumps(seal))
        data = output.getvalue()
        self.client.archives[90] = data
        metadata = {'id': 90, 'name': 'release-image-plan-5-30-1', 'digest': 'sha256:' + hashlib.sha256(data).hexdigest(), 'size_in_bytes': len(data),
                    'expired': False, 'expires_at': '2030-01-01T00:00:00Z', 'workflow_run': {'id': 30, 'repository_id': 1, 'head_repository_id': 1, 'head_sha': producer.source_sha, 'head_branch': 'main'}}
        return [metadata] + [metadata | {'id': 91 + index, 'name': f'release-images-5-{bundle}-30-1'} for index, bundle in enumerate(('app', 'infra', 'docs'))]

    def authorize(self, plan=None, bad_seal=False, missing_bundle=False):
        artifacts = self.artifacts(plan or self.plan, bad_seal)
        if missing_bundle:
            artifacts.pop()
        with patch('publication_release_image_authorization.authorize_preparation', return_value=({}, self.preparation, artifacts, {})), \
             patch('publication_release_image_authorization.frame', return_value=({'job_id': 100}, {})) as invocation, \
             patch('publication_release_image_authorization.release_authority', return_value=self.authority):
            result = authorize_prepared(self.client, {}, 20, 2, 30, 1, 5, self.client.catalog, self.client.root)
        invocation.assert_called_once_with(self.client, {}, 30, 1, 'Prepare release images (5)', 'success')
        return result

    def test_exact_matrix_plan_selects_its_three_bundles(self):
        plan, producer, artifacts, evidence = self.authorize()
        self.assertEqual(plan, self.plan)
        self.assertEqual(producer, self.preparation)
        self.assertEqual(set(artifacts), {'app', 'infra', 'docs'})
        self.assertEqual(evidence['prepare_job_id'], 100)

    def test_wrong_release_seal_missing_bundle_or_changed_authority_is_rejected(self):
        for mode in ('seal', 'bundle', 'policy', 'producer', 'job'):
            plan = copy.deepcopy(self.plan)
            if mode == 'policy': plan['policy_sha256'] = 'sha256:' + '0' * 64
            if mode == 'producer': plan['authority']['plan']['producer']['run_id'] = 999
            if mode == 'job': plan['invocation']['job_id'] = 101
            with self.subTest(mode=mode), self.assertRaises(PublicationError):
                self.authorize(plan, mode == 'seal', mode == 'bundle')


if __name__ == '__main__':
    unittest.main()
