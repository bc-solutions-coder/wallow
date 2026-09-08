import hashlib
from dataclasses import asdict
import io
import json
import os
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch
import zipfile

from publication import Producer, PublicationError
from publication_alias_authorization import publication_records
from publication_alias_pipeline import authorize_plan
from publication_promote_aliases import main
from publication_release_receipts import ORIGIN, SELECTION
import test_publication_npm as package_fixtures


class AuthorizationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / '.github/ci').mkdir(parents=True)
        (self.root / '.github/ci/security-exceptions.json').write_text('{}')
        self.producer = Producer('example/repo', 1, 'a' * 40, 30, 1, 12, 'example/repo/.github/workflows/publish.yml@refs/heads/main')
        self.records = [{'release': {'id': 5}, 'receipt': {'asset_id': 50}}]
        self.plan = {'schema': 1, 'kind': 'image', 'invocation': {'job_id': 100}, 'records': self.records, 'target': None, 'unrelated': False,
                     'policy_sha256': 'sha256:' + hashlib.sha256(b'{}').hexdigest(), 'publication_authorized': False}
        self.client = SimpleNamespace(repository='example/repo', download=self.download)

    def download(self, artifact, destination):
        destination.write_bytes(self.archive)
        return destination

    def authorize(self, plan=None, seal_kind='image'):
        body = json.dumps(plan or self.plan).encode()
        producer = self.producer
        seal = {'schema': 1, 'repository': producer.repository, 'sha': producer.source_sha, 'run_id': '30', 'run_attempt': '1', 'workflow_ref': producer.workflow_ref,
                'kind': 'alias-plan', 'variant': seal_kind, 'file': 'plan.json', 'sha256': hashlib.sha256(body).hexdigest()}
        output = io.BytesIO()
        with zipfile.ZipFile(output, 'w') as archive:
            archive.writestr('plan.json', body)
            archive.writestr('plan.json.json', json.dumps(seal))
        self.archive = output.getvalue()
        artifact = {'id': 90, 'name': 'image-alias-plan-30-1', 'size_in_bytes': len(self.archive), 'digest': 'sha256:' + hashlib.sha256(self.archive).hexdigest(),
                    'expired': False, 'expires_at': '2030-01-01T00:00:00Z', 'workflow_run': {'id': 30, 'repository_id': 1, 'head_repository_id': 1, 'head_sha': producer.source_sha, 'head_branch': 'main'}}
        with patch('publication_alias_pipeline.authorize_preparation', return_value=({}, producer, [artifact], {})), \
             patch('publication_alias_pipeline.frame', return_value=({'job_id': 100}, {})), \
             patch('publication_alias_pipeline.publication_records', return_value=(self.records, False)), patch('publication_alias_pipeline.configuration'):
            return authorize_plan(self.client, {}, 1, 1, 30, 1, 'image', {}, self.root)

    def test_unpublished_legacy_release_is_not_an_alias_candidate(self):
        client = SimpleNamespace(controller=Mock(), array=Mock(return_value=[{'id': 5, 'tag_name': 'sdk-v1.0.0', 'draft': False}]), get=Mock(return_value={'id': 5}))
        catalog = {'components': [{'id': 'sdk', 'tag_prefix': 'sdk-v', 'package': {}}]}
        with patch('publication_alias_authorization.inspect_receipt', return_value=None), \
             patch('publication_alias_authorization.release_identity', side_effect=PublicationError('Unsupported old source')) as identity:
            self.assertEqual(publication_records(client, {}, catalog, 'package'), ([], False))
            identity.assert_not_called()
            with self.assertRaises(PublicationError):
                publication_records(client, {'event_name': 'workflow_dispatch'}, catalog, 'package', 5)
        with patch('publication_alias_authorization.inspect_receipt', return_value={'asset_id': 7}), \
             patch('publication_alias_authorization.release_identity', side_effect=PublicationError('Changed published source')):
            with self.assertRaises(PublicationError):
                publication_records(client, {}, catalog, 'package')

    def test_exact_sealed_plan_requires_current_policy_receipts_and_job(self):
        self.assertEqual(self.authorize(), self.plan)
        for key, value in [('kind', 'package'), ('invocation', {'job_id': 101}), ('records', []), ('policy_sha256', 'sha256:' + '0' * 64)]:
            with self.subTest(key=key), self.assertRaises(PublicationError): self.authorize(self.plan | {key: value})
        with self.assertRaises(PublicationError): self.authorize(seal_kind='package')


    def test_durable_candidate_uses_api_identity_without_expired_build_downloads(self):
        fixture = package_fixtures.NpmTests()
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        component = {'id': 'sdk', 'path': 'packages/sdk', 'tag_prefix': 'sdk-v', 'package': {'name': fixture.package.name}}
        catalog = {'components': [component], 'images': []}
        release = {'id': 5, 'component': 'sdk', 'version': fixture.package.version, 'commit_sha': self.producer.source_sha}
        selected = {'producer': asdict(self.producer), 'component_versions': {'packages/sdk': fixture.package.version}}
        receipt = {'asset_id': 50, 'sha256': 'sha256:' + 'b' * 64}
        origin = {'asset_id': 51, 'sha256': 'sha256:' + 'c' * 64}
        selection = {'asset_id': 52, 'sha256': 'sha256:' + 'd' * 64}
        client = SimpleNamespace(repository='example/repo', controller=lambda context: None,
                                 array=lambda path: [{'id': 5, 'tag_name': 'sdk-v1.0.0', 'draft': False}],
                                 producer=lambda run, attempt: (self.producer, {}))
        with patch('publication_alias_authorization.release_identity', return_value=release), \
             patch('publication_alias_authorization.inspect_receipt', return_value=receipt), \
             patch('publication_alias_authorization.endorsed', return_value=True), \
             patch('publication_alias_authorization.find_receipt', side_effect=lambda client, release_id, name: {ORIGIN: origin, SELECTION: selection}[name]), \
             patch('publication_alias_authorization.authorized_selection', return_value=selected), \
             patch('publication_alias_authorization.published_package', return_value={'package': fixture.package}):
            records, unrelated = publication_records(client, {}, catalog, 'package')
            self.assertFalse(unrelated)
            self.assertEqual(records[0]['selected'], selected)
            self.assertEqual(records[0]['outputs']['package'], asdict(fixture.package))
            selected['producer'] = selected['producer'] | {'run_attempt': 2}
            with self.assertRaises(PublicationError): publication_records(client, {}, catalog, 'package')

    def test_recovered_image_alias_requires_durable_lineage(self):
        release = {'id': 5, 'component': 'app', 'version': '1.0.0', 'commit_sha': self.producer.source_sha}
        selected = {'producer': asdict(self.producer), 'component_versions': {'.': '1.0.0'}}
        pointer = {'asset_id': 51, 'sha256': 'sha256:' + 'c' * 64}
        binding = {'receipt': {'asset_id': 90}, 'producer': {'run_id': 70}}
        image = {'image': 'api', 'repository': 'example/api', 'version': '1.0.0', 'source_sha': self.producer.source_sha,
                 'index_digest': 'sha256:' + 'd' * 64, 'platforms': {'linux/amd64': {}}, 'readback': 'verified'}
        payload = {'release': release, 'origin': pointer, 'selection': pointer, 'images': [image], 'writer': {'frame': {}}, 'recovery': binding}
        receipt = {'asset_id': 50, 'sha256': 'sha256:' + 'b' * 64, 'record': {'payload': payload}}
        catalog = {'components': [{'id': 'app', 'path': '.', 'tag_prefix': 'v'}],
                   'images': [{'id': 'api', 'component': 'app', 'tags': {'linux/amd64': 'amd64'}}]}
        client = SimpleNamespace(repository='example/repo', controller=Mock(),
                                 array=Mock(return_value=[{'id': 5, 'tag_name': 'v1.0.0', 'draft': False}]),
                                 producer=Mock(return_value=(self.producer, {})))
        with patch('publication_alias_authorization.release_identity', return_value=release), \
             patch('publication_alias_authorization.inspect_receipt', return_value=receipt), \
             patch('publication_alias_authorization.endorsed', return_value=True), \
             patch('publication_alias_authorization.find_receipt', return_value=pointer), \
             patch('publication_alias_authorization.authorized_selection', return_value=selected), \
             patch('publication_alias_authorization.verify_frame'), \
             patch('publication_alias_authorization.image_repository', return_value='example/api'), \
             patch('publication_alias_authorization.main_index'), \
             patch('publication_release_image_authorization.verify_reference', return_value={'record': {'payload': {'recovery': {'producer': binding['producer']}}}}) as verify:
            records, _ = publication_records(client, {}, catalog, 'image')
            self.assertEqual(records[0]['outputs']['images'], [image])
            verify.assert_called_once_with(client, binding['receipt'], release, pointer, pointer)
            verify.side_effect = PublicationError('Recovery receipt changed')
            with self.assertRaises(PublicationError):
                publication_records(client, {}, catalog, 'image')

    def test_recovery_cli_forwards_exact_selector_in_each_mode(self):
        environment = {'ENABLE_IMAGE_PUBLISH': 'true', 'GITHUB_RUN_ID': '30', 'GITHUB_RUN_ATTEMPT': '1', 'GITHUB_OUTPUT': str(self.root / 'outputs')}
        for mode, operation in (('prepare', 'prepare'), ('promote', 'promote'), ('record', 'finalize')):
            output = self.root / (mode + '.json')
            argv = ['aliases', mode, '--kind', 'image', '--producer-run', '20', '--producer-attempt', '2', '--release-id', '5',
                    '--recovery-run', '70', '--recovery-attempt', '1', '--output', str(output)]
            with patch.dict(os.environ, environment), patch('sys.argv', argv), \
                 patch('publication_promote_aliases.ReleaseGitHub'), patch('publication_promote_aliases.load_catalog', return_value={}), \
                 patch('publication_promote_aliases.' + operation, return_value={'unrelated': False, 'entries': []}) as call:
                main()
                self.assertEqual(call.call_args.kwargs['recovery'], {'release_id': 5, 'run_id': 70, 'run_attempt': 1})
            with patch.dict(os.environ, environment), patch('sys.argv', argv[:-4] + ['--output', str(output)]), \
                 patch('publication_promote_aliases.ReleaseGitHub') as client:
                with self.assertRaises(SystemExit):
                    main()
                client.assert_not_called()

    def test_unrelated_prepare_cli_emits_false_eligibility_without_destination_access(self):
        output = self.root / 'output.json'
        environment = {'ENABLE_IMAGE_PUBLISH': 'true', 'GITHUB_RUN_ID': '30', 'GITHUB_RUN_ATTEMPT': '1', 'GITHUB_OUTPUT': str(self.root / 'outputs')}
        with patch.dict(os.environ, environment), patch('sys.argv', ['aliases', 'prepare', '--kind', 'image', '--producer-run', '1', '--producer-attempt', '1', '--release-id', '5', '--output', str(output)]), \
             patch('publication_promote_aliases.ReleaseGitHub', return_value=self.client), patch('publication_promote_aliases.load_catalog', return_value={}), \
             patch('publication_promote_aliases.prepare', return_value={'unrelated': True, 'entries': []}):
            main()
        self.assertEqual((self.root / 'outputs').read_text(), 'eligible=false\n')
        self.assertTrue(json.loads(output.read_text())['unrelated'])


if __name__ == '__main__': unittest.main()
