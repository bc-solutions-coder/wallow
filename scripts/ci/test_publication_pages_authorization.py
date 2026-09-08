from dataclasses import asdict
import hashlib
import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from publication import Producer, PublicationError
from publication_artifacts import Artifact
from publication_pages_authorization import authorize_pages, pages_environment, verify_prepared_site


class PagesAuthorizationTests(unittest.TestCase):
    def setUp(self):
        self.tar = b'verified prepared bytes'
        self.site = {'sha256': hashlib.sha256(self.tar).hexdigest(), 'size': len(self.tar),
                     'files': [{'path': 'index.html', 'size': 1, 'sha256': 'a' * 64}]}
        self.environment = {'name': 'github-pages', 'deployment_branch_policy': {'protected_branches': False, 'custom_branch_policies': True}}
        self.rules = [{'name': 'main', 'type': 'branch'}]
        self.settings = {'build_type': 'workflow', 'html_url': 'https://example.github.io/repo/'}
        self.paths = []
        self.make_zip()

    def make_zip(self, extra=False, changed=False):
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, 'w') as bundle:
            bundle.writestr('artifact.tar', b'changed' if changed else self.tar)
            if extra: bundle.writestr('extra.txt', b'not allowed')
        self.body = stream.getvalue()
        self.artifact = Artifact(40, 'prepared-site-20-1', 'sha256:' + hashlib.sha256(self.body).hexdigest(), len(self.body))

    def download(self, artifact, destination):
        self.assertEqual(artifact, self.artifact)
        destination.write_bytes(self.body)
        self.paths.append(destination)
        return destination

    def get(self, path):
        return {'/environments/github-pages': self.environment, '/pages': self.settings}[path]

    def list(self, path, key):
        self.assertEqual(path, '/environments/github-pages/deployment-branch-policies')
        return self.rules

    def test_exact_single_prepared_tar_is_checked_and_temporary_bytes_removed(self):
        result = verify_prepared_site(self, self.artifact, self.site)
        self.assertEqual(result['tar_sha256'], self.site['sha256'])
        self.assertTrue(all(not path.parent.exists() for path in self.paths))

    def test_extra_member_changed_bytes_and_wrong_digest_reject(self):
        for options in ({'extra': True}, {'changed': True}, {}):
            self.make_zip(**options)
            site = self.site if options else self.site | {'sha256': 'f' * 64}
            with self.subTest(options=options), self.assertRaises(PublicationError):
                verify_prepared_site(self, self.artifact, site)

    def test_enabled_pages_requires_exact_main_policy_workflow_and_https(self):
        self.assertEqual(pages_environment(self), self.settings)
        for rules in ([], [{'name': '*', 'type': 'branch'}], [{'name': 'main', 'type': 'tag'}]):
            self.rules = rules
            with self.assertRaises(PublicationError): pages_environment(self)
        self.rules = [{'name': 'main', 'type': 'branch'}]
        self.settings['build_type'] = 'legacy'
        with self.assertRaises(PublicationError): pages_environment(self)
        self.settings.update(build_type='workflow', html_url='file:///site/')
        with self.assertRaises(PublicationError): pages_environment(self)

    def test_writer_requires_matching_source_and_exact_prepared_invocation(self):
        source = {'kind': 'docs', 'id': 9}
        plan = {'artifacts': [source], 'verified_site': self.site | {'source_artifact': source}}
        preparation = Producer('example/repo', 1, 'b' * 40, 20, 1, 2, 'example/repo/.github/workflows/publish.yml@refs/heads/main')
        metadata = {**asdict(self.artifact), 'size_in_bytes': self.artifact.size,
                    'expired': False, 'expires_at': '2030-01-01T00:00:00Z',
                    'workflow_run': {'id': 20, 'repository_id': 1, 'head_repository_id': 1, 'head_sha': 'b' * 40, 'head_branch': 'main'}}
        with patch('publication_pages_authorization.authorize_preparation', return_value=(plan, preparation, [metadata], {})):
            self.assertEqual(authorize_pages(self, {}, 1, 1, 20, 1)[1], self.artifact)
            plan['verified_site']['source_artifact'] = {'kind': 'docs', 'id': 10}
            with self.assertRaises(PublicationError): authorize_pages(self, {}, 1, 1, 20, 1)
