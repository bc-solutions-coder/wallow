import copy
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from publication import PublicationError
from publication_alias_progress import finalize
from publication_release_github import ReleaseGitHub
from test_publication_release_receipts import Client, JOB


class ProgressTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.client = Client()
        self.client.repository = 'example/repo'
        self.writer = {'repository_id': 1, 'controller_sha': 'a' * 40, 'run_id': 30, 'run_attempt': 1,
                       'workflow_id': 12, 'workflow_ref': 'example/repo/.github/workflows/publish.yml@refs/heads/main', 'job_id': 100}
        self.recorder = self.writer | {'job_id': 101}
        self.records = [{'release': {'id': 5}, 'receipt': {'asset_id': 50, 'sha256': 'sha256:' + 'b' * 64}}]
        self.entry = {'release_id': 5, 'output': 'docs', 'alias': 'latest', 'previous': None, 'previous_release_id': None, 'action': 'advance'}
        self.plan = {'records': self.records, 'entries': [self.entry]}
        self.document = {'schema': 1, 'kind': 'image', 'invocation': self.writer, 'records': self.records, 'entries': [self.entry | {'outcome': 'verified'}]}

    def run_finalizer(self, document=None):
        body = json.dumps(document or self.document).encode()
        seal = {'schema': 1, 'repository': self.client.repository, 'sha': self.writer['controller_sha'], 'run_id': '30', 'run_attempt': '1',
                'workflow_ref': self.writer['workflow_ref'], 'kind': 'alias-progress', 'variant': 'image', 'file': 'progress.json', 'sha256': hashlib.sha256(body).hexdigest()}
        output = io.BytesIO()
        with zipfile.ZipFile(output, 'w') as archive:
            archive.writestr('progress.json', body)
            archive.writestr('progress.json.json', json.dumps(seal))
        data = output.getvalue()
        artifact = {'id': 90, 'name': 'image-alias-progress-30-1', 'size_in_bytes': len(data), 'digest': 'sha256:' + hashlib.sha256(data).hexdigest(),
                    'expired': False, 'expires_at': '2030-01-01T00:00:00Z', 'workflow_run': {'id': 30, 'repository_id': 1, 'head_repository_id': 1, 'head_sha': self.writer['controller_sha'], 'head_branch': 'main'}}
        self.client.list = lambda path, key: [artifact]
        self.client.download = lambda selected, destination: destination.write_bytes(data) and destination
        with patch('publication_alias_progress.frame', side_effect=[(self.recorder, JOB), (self.writer, JOB)]), \
             patch('publication_alias_progress.authorize_plan', return_value=self.plan), \
             patch('publication_release_receipts.recorded_job', return_value=JOB):
            return finalize(self.client, {}, 1, 1, 30, 1, 'image', {}, self.root)

    def test_successful_sealed_progress_retained_and_identical_retry_has_no_write(self):
        first = self.run_finalizer()
        self.assertEqual(self.run_finalizer(), first)
        self.assertEqual(len(self.client.writes), 1)
        release_id, name, body = self.client.writes[0]
        self.assertEqual((release_id, name), (5, 'wallow-alias-image-30-1.json'))
        payload = json.loads(body)['payload']
        self.assertEqual(payload['immutable_receipt'], self.records[0]['receipt'])
        self.assertEqual(payload['writer'], self.writer)
        self.assertEqual(payload['authority'], 'immutable-publication-receipt-only')

    def test_partial_changed_or_wrong_writer_progress_never_creates_observation(self):
        for change in ('partial', 'outcome', 'writer', 'error'):
            document = copy.deepcopy(self.document)
            if change == 'partial': document['entries'] = []
            elif change == 'outcome': document['entries'][0]['outcome'] = 'assumed'
            elif change == 'writer': document['invocation']['job_id'] = 102
            else: document['error'] = 'failed'
            with self.subTest(change=change), self.assertRaises(PublicationError): self.run_finalizer(document)
            self.assertEqual(self.client.writes, [])

    def test_fixed_github_upload_accepts_only_bounded_alias_observation_names(self):
        client = object.__new__(ReleaseGitHub)
        client.repository, client.token = 'example/repo', 'fixture-token'
        requests = []
        client.send = lambda request, status: requests.append((request, status)) or {'id': 1}
        client.upload(5, 'wallow-alias-image-30-1.json', b'{}')
        self.assertEqual(requests[0][0].full_url, 'https://uploads.github.com/repos/example/repo/releases/5/assets?name=wallow-alias-image-30-1.json')
        self.assertEqual(requests[0][1], 201)
        for name in ('wallow-alias-image-0-1.json', 'wallow-alias-unknown-30-1.json', '../latest', 'wallow-alias-package-30-1.json?other=1'):
            with self.subTest(name=name), self.assertRaises(PublicationError): client.upload(5, name, b'{}')
        self.assertEqual(len(requests), 1)


if __name__ == '__main__': unittest.main()
