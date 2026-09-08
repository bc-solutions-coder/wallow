import copy
from dataclasses import asdict
import hashlib
import io
import json
import unittest
import zipfile

from publication import Producer, PublicationError
from publication_release_github import ACTIONS_ACTOR
from publication_release_origin import ACTION_JOB, PR_JOB, action_evidence, record_pr_origins, verify_pr_origin
from release_evidence import ACTION


class OriginTests(unittest.TestCase):
    repository = 'example/repo'

    def setUp(self):
        self.context = {'repository': self.repository, 'ref': 'refs/heads/main', 'workflow_ref': self.repository + '/.github/workflows/publish.yml@refs/heads/main', 'workflow_sha': 'b' * 40, 'event_name': 'workflow_dispatch'}
        self.identity = Producer(self.repository, 1, 'a' * 40, 20, 1, 10, self.repository + '/.github/workflows/ci.yml@refs/heads/main')
        self.user = {'id': 4, 'login': 'owner', 'type': 'User'}
        self.run = {'id': 30, 'run_attempt': 1, 'workflow_id': 12, 'path': '.github/workflows/publish.yml', 'head_sha': 'b' * 40, 'head_branch': 'main', 'event': 'workflow_dispatch', 'repository': {'id': 1, 'full_name': self.repository}, 'head_repository': {'id': 1, 'full_name': self.repository}, 'actor': self.user, 'triggering_actor': self.user}
        common = {'head_sha': 'b' * 40, 'run_id': 30, 'run_attempt': 1, 'started_at': '2026-09-07T01:00:00Z', 'completed_at': '2026-09-07T02:00:00Z'}
        self.jobs = [common | {'id': 60, 'name': ACTION_JOB, 'status': 'completed', 'conclusion': 'success'}, common | {'id': 61, 'name': PR_JOB, 'status': 'in_progress', 'conclusion': None}]
        self.pr = {'id': 5, 'number': 6, 'user': self.user, 'head': {'sha': 'c' * 40, 'ref': 'release-please--main', 'repo': {'full_name': self.repository}}, 'base': {'ref': 'main', 'repo': {'full_name': self.repository}}}
        self.observed = {'id': 5, 'number': 6, 'author': self.user, 'head_sha': 'c' * 40, 'head_ref': 'release-please--main'}
        self.document = {'schema': 1, 'publication_authorized': False, 'action_outcome': 'success', 'pull_requests': [self.observed], 'releases': [], 'invocation': {'action': ACTION, 'repository': self.repository, 'controller_sha': 'b' * 40, 'workflow_id': 12, 'workflow_ref': self.context['workflow_ref'], 'run_id': 30, 'run_attempt': 1, 'job_id': 60, 'producer': asdict(self.identity), 'actor': self.user, 'triggering_actor': self.user, 'credential_actor': self.user}}
        self.comments, self.downloads = [], []
        self.make_artifact()

    def make_artifact(self, extra=False):
        body = json.dumps(self.document).encode()
        seal = {'schema': 1, 'repository': self.repository, 'sha': 'b' * 40, 'run_id': '30', 'run_attempt': '1', 'workflow_ref': self.context['workflow_ref'], 'kind': 'release-evidence', 'variant': 'release-please', 'file': 'evidence.json', 'sha256': hashlib.sha256(body).hexdigest()}
        archive = io.BytesIO()
        with zipfile.ZipFile(archive, 'w') as stream:
            stream.writestr('evidence.json', body)
            stream.writestr('evidence.json.json', json.dumps(seal))
            if extra:
                stream.writestr('invocation.json', '{}')
        self.archive = archive.getvalue()
        self.artifact = {'id': 70, 'name': 'release-evidence-30-1', 'size_in_bytes': len(self.archive), 'digest': 'sha256:' + hashlib.sha256(self.archive).hexdigest(), 'expired': False, 'expires_at': '2030-01-01T00:00:00Z', 'workflow_run': {'id': 30, 'repository_id': 1, 'head_repository_id': 1, 'head_sha': 'b' * 40, 'head_branch': 'main'}}

    def controller(self, context):
        if context != self.context:
            raise PublicationError('Wrong controller')
        return 'b' * 40

    def producer(self, run, attempt):
        return self.identity, []

    def get(self, path):
        values = {'': {'id': 1, 'full_name': self.repository}, '/actions/runs/30/attempts/1': self.run, '/actions/workflows/publish.yml': {'id': 12, 'path': '.github/workflows/publish.yml'}, '/pulls/6': self.pr}
        if path.startswith('/issues/comments/'):
            return copy.deepcopy(next(comment for comment in self.comments if comment['id'] == int(path.rsplit('/', 1)[1])))
        return copy.deepcopy(values[path])

    def list(self, path, key):
        return copy.deepcopy(self.jobs if key == 'jobs' else [self.artifact])

    def array(self, path):
        return copy.deepcopy(self.comments)

    def download(self, artifact, destination):
        destination.write_bytes(self.archive)
        self.downloads.append(destination)
        return destination

    def comment(self, number, body):
        value = {'id': len(self.comments) + 1, 'body': body, 'user': ACTIONS_ACTOR, 'created_at': '2026-09-07T01:30:00Z', 'updated_at': '2026-09-07T01:30:00Z', 'issue_url': f'https://api.github.com/repos/{self.repository}/issues/{number}'}
        self.comments.append(value)
        return copy.deepcopy(value)

    def test_exact_archive_origin_survives_artifact_expiry_and_identical_retry(self):
        origins = record_pr_origins(self, self.context, 30, 1)
        self.assertEqual(origins[0]['record']['pull_request']['head_sha'], 'c' * 40)
        self.assertTrue(all(not item.parent.exists() for item in self.downloads))
        self.jobs[1].update(status='completed', conclusion='success')
        self.artifact['expired'] = True
        self.assertEqual(verify_pr_origin(self, self.comments[0], self.pr), origins[0])

    def test_failed_action_or_extra_archive_files_never_posts_comment(self):
        self.document['action_outcome'] = 'failure'
        self.make_artifact()
        with self.assertRaises(PublicationError):
            record_pr_origins(self, self.context, 30, 1)
        self.document['action_outcome'] = 'success'
        self.make_artifact(extra=True)
        with self.assertRaises(PublicationError):
            record_pr_origins(self, self.context, 30, 1)
        self.assertEqual(self.comments, [])

    def test_changed_pr_edited_comment_wrong_actor_or_issue_rejected(self):
        record_pr_origins(self, self.context, 30, 1)
        self.jobs[1].update(status='completed', conclusion='success')
        original = copy.deepcopy(self.comments[0])
        for changes in [{'updated_at': '2026-09-07T01:31:00Z'}, {'user': self.user}, {'issue_url': 'https://api.github.com/repos/example/repo/issues/7'}]:
            with self.assertRaises(PublicationError):
                verify_pr_origin(self, original | changes, self.pr)
        self.pr['head']['sha'] = 'd' * 40
        with self.assertRaises(PublicationError):
            verify_pr_origin(self, original, self.pr)

    def test_failed_origin_job_never_becomes_automation_authority(self):
        record_pr_origins(self, self.context, 30, 1)
        self.jobs[1].update(status='completed', conclusion='failure')
        with self.assertRaises(PublicationError):
            verify_pr_origin(self, self.comments[0], self.pr)


if __name__ == '__main__':
    unittest.main()
