from dataclasses import asdict
import copy
import hashlib
import io
import json
from pathlib import Path
import unittest
from unittest.mock import patch
import zipfile

from publication import PublicationError
from publication_image_authorization import authorize_images, image_environment
import test_publication_publish_images as fixtures


class AuthorizationTests(unittest.TestCase):
    def setUp(self):
        self.fixture = fixtures.WriterTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.plan = self.fixture.plan
        self.fresh = {key: copy.deepcopy(value) for key, value in self.plan.items() if key != 'verified_images'}
        self.context = {'repository': 'example/repo', 'workflow_sha': 'b' * 40, 'workflow_ref': 'example/repo/.github/workflows/publish.yml@refs/heads/main', 'event_name': 'workflow_dispatch', 'ref': 'refs/heads/main'}
        self.run = {'id': 30, 'run_attempt': 1, 'workflow_id': 12, 'path': '.github/workflows/publish.yml', 'head_sha': 'b' * 40, 'head_branch': 'main', 'event': 'workflow_dispatch',
                    'repository': {'id': 1, 'full_name': 'example/repo'}, 'head_repository': {'id': 1, 'full_name': 'example/repo'}}
        self.jobs = [{'id': 60, 'name': 'authorize', 'status': 'completed', 'conclusion': 'success', 'head_sha': 'b' * 40, 'run_id': 30, 'run_attempt': 1}]
        self.environment = {'name': 'image-publish', 'deployment_branch_policy': {'protected_branches': False, 'custom_branch_policies': True}}
        self.rules = [{'name': 'main', 'type': 'branch'}]
        self.downloads = []
        self.make_plan()

    def make_plan(self):
        body = json.dumps(self.plan).encode()
        seal = {'schema': 1, 'repository': 'example/repo', 'sha': 'b' * 40, 'run_id': '30', 'run_attempt': '1', 'workflow_ref': self.context['workflow_ref'],
                'kind': 'publication-plan', 'variant': 'authorized', 'file': 'plan.json', 'sha256': hashlib.sha256(body).hexdigest()}
        archive = io.BytesIO()
        with zipfile.ZipFile(archive, 'w') as output:
            output.writestr('plan.json', body)
            output.writestr('plan.json.json', json.dumps(seal))
        data = archive.getvalue()
        self.archive = data
        self.artifacts = [{'id': 70, 'name': 'publication-plan-30-1', 'size_in_bytes': len(data), 'digest': 'sha256:' + hashlib.sha256(data).hexdigest()},
                          {'id': 50, 'name': 'prepared-images-docs-30-1', 'size_in_bytes': self.fixture.artifacts['docs'].size, 'digest': self.fixture.artifacts['docs'].digest}]
        for item in self.artifacts:
            item.update(expired=False, expires_at='2030-01-01T00:00:00Z', workflow_run={'id': 30, 'repository_id': 1, 'head_repository_id': 1, 'head_sha': 'b' * 40, 'head_branch': 'main'})

    def get(self, path):
        return {'/environments/image-publish': self.environment, '/actions/runs/30/attempts/1': self.run,
                '/actions/workflows/publish.yml': {'id': 12, 'path': '.github/workflows/publish.yml'}}[path]

    def list(self, path, key):
        return {'/environments/image-publish/deployment-branch-policies': self.rules, '/actions/runs/30/attempts/1/jobs': self.jobs, '/actions/runs/30/artifacts': self.artifacts}[path]

    def download(self, artifact, destination):
        self.assertEqual(artifact.id, 70)
        destination.write_bytes(self.archive)
        self.downloads.append(destination)
        return destination

    def authorize(self):
        with patch('publication_plan.resolve', return_value=self.fresh):
            return authorize_images(self, self.context, 20, 2, 30, 1)

    def test_exact_successful_preparation_and_sealed_plan_are_reauthorized(self):
        plan, preparation, artifacts, evidence = self.authorize()
        self.assertEqual(plan, self.plan)
        self.assertEqual(asdict(preparation), asdict(self.fixture.preparation))
        self.assertEqual(artifacts, self.fixture.artifacts)
        self.assertEqual(evidence['authorize_job_id'], 60)
        self.assertTrue(all(not path.parent.exists() for path in self.downloads))

    def test_main_environment_rejects_missing_wildcard_tag_and_extra_branch_policies(self):
        for rules in ([], [{'name': '*', 'type': 'branch'}], [{'name': 'main', 'type': 'tag'}], self.rules * 2):
            self.rules = rules
            with self.assertRaises(PublicationError):
                image_environment(self)
        self.environment['deployment_branch_policy'] = None
        with self.assertRaises(PublicationError):
            image_environment(self)

    def test_failed_foreign_wrong_attempt_or_ambiguous_preparation_never_downloads(self):
        original_run, original_jobs = copy.deepcopy(self.run), copy.deepcopy(self.jobs)
        for change in ('failed', 'job_attempt', 'job_sha', 'duplicate', 'branch', 'repository', 'workflow', 'run_attempt'):
            self.run, self.jobs = copy.deepcopy(original_run), copy.deepcopy(original_jobs)
            if change == 'failed': self.jobs[0]['conclusion'] = 'failure'
            elif change == 'job_attempt': self.jobs[0]['run_attempt'] = 2
            elif change == 'job_sha': self.jobs[0]['head_sha'] = 'c' * 40
            elif change == 'duplicate': self.jobs *= 2
            elif change == 'branch': self.run['head_branch'] = 'other'
            elif change == 'repository': self.run['head_repository']['id'] = 2
            elif change == 'workflow': self.run['workflow_id'] = 13
            else: self.run['run_attempt'] = 2
            with self.subTest(change=change), self.assertRaises(PublicationError):
                self.authorize()
            self.assertEqual(self.downloads, [])

    def test_changed_source_plan_or_missing_prepared_artifact_rejects(self):
        self.plan['producer']['source_sha'] = 'f' * 40
        self.make_plan()
        with self.assertRaises(PublicationError):
            self.authorize()
        self.plan['producer']['source_sha'] = 'a' * 40
        self.make_plan()
        self.artifacts.pop()
        with self.assertRaises(PublicationError):
            self.authorize()
        self.assertTrue(all(not path.parent.exists() for path in self.downloads))
