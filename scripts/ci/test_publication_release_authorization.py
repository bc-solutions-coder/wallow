import base64
import copy
import json
import unittest

from publication import PublicationError
from publication_release_authorization import ReleaseOutsideMain, authenticate_origin, release_identity
from publication_release_origin import COMMENT_PREFIX, COMMENT_SUFFIX, canonical, comment_record, record_pr_origins
import test_publication_release_origin as fixtures


class AuthorizationTests(unittest.TestCase):
    def setUp(self):
        self.fixture = fixtures.OriginTests()
        self.fixture.setUp()
        self.catalog = {'components': [{'id': 'sdk', 'path': 'packages/sdk', 'tag_prefix': 'sdk-v'}]}
        self.release = {'id': 80, 'tag_name': 'sdk-v1.2.3', 'draft': False, 'prerelease': False, 'author': self.fixture.user}
        self.contents = json.dumps({'packages/sdk': '1.2.3'}).encode()
        self.values = {'/git/ref/tags/sdk-v1.2.3': {'ref': 'refs/tags/sdk-v1.2.3', 'object': {'type': 'commit', 'sha': 'a' * 40}},
                       '/contents/.release-please-manifest.json?ref=' + 'a' * 40: {'type': 'file', 'encoding': 'base64', 'size': len(self.contents), 'content': base64.b64encode(self.contents).decode()}}
        self.original_get = self.fixture.get
        self.fixture.get = lambda path: copy.deepcopy(self.values[path]) if path in self.values else self.original_get(path)
        self.fixture.main_comparison = lambda sha: {'status': 'ahead', 'merge_base_commit': {'sha': sha}}
        self.fixture.collection = lambda path, key: [self.fixture.run]
        original_array = self.fixture.array
        self.fixture.array = lambda path: [{'id': 5, 'number': 6}] if path.startswith('/commits/') else original_array(path)
        record_pr_origins(self.fixture, self.fixture.context, 30, 1)
        self.fixture.jobs[1].update(status='completed', conclusion='success')
        self.fixture.pr.update(merged=True, merge_commit_sha='a' * 40)
        self.identity = release_identity(self.fixture, self.release, self.catalog)
        self.fixture.document['releases'] = [self.identity | {'matches_producer_sha': True}]
        self.fixture.make_artifact()

    def test_non_main_ancestry_is_distinct_from_invalid_comparison(self):
        for status in ('behind', 'diverged'):
            self.fixture.main_comparison = lambda sha: {'status': status, 'merge_base_commit': {'sha': 'b' * 40}}
            with self.assertRaises(ReleaseOutsideMain):
                release_identity(self.fixture, self.release, self.catalog)
        for comparison in ({}, {'status': 'unknown'}, {'status': 'behind'},
                           {'status': 'ahead', 'merge_base_commit': {'sha': 'b' * 40}},
                           {'status': 'behind', 'merge_base_commit': {'sha': 'a' * 40}}):
            self.fixture.main_comparison = lambda sha: comparison
            with self.assertRaises(PublicationError) as failure:
                release_identity(self.fixture, self.release, self.catalog)
            self.assertNotIsInstance(failure.exception, ReleaseOutsideMain)

    def test_build_metadata_release_fails_without_registry_normalization(self):
        with self.assertRaisesRegex(PublicationError, 'build metadata is unsupported'):
            release_identity(self.fixture, self.release | {'tag_name': 'sdk-v1.2.3+build.1'}, self.catalog)

    def test_exact_action_merged_pr_tag_source_and_durable_origin(self):
        origin = authenticate_origin(self.fixture, self.identity)
        self.assertEqual(origin['merged_pr']['head_sha'], 'c' * 40)
        self.assertEqual(origin['release']['commit_sha'], 'a' * 40)
        self.fixture.artifact['expired'] = True
        self.assertEqual(authenticate_origin(self.fixture, self.identity, {'record': {'payload': origin}}), origin)

    def test_manual_release_same_author_without_action_output_remains_pending(self):
        self.fixture.document['releases'] = []
        self.fixture.make_artifact()
        self.assertIsNone(authenticate_origin(self.fixture, self.identity))

    def test_tag_version_or_merged_pr_mismatch_never_authorizes(self):
        self.fixture.pr['merge_commit_sha'] = 'd' * 40
        self.assertIsNone(authenticate_origin(self.fixture, self.identity))
        self.release['tag_name'] = 'other-v1.2.3'
        with self.assertRaises(PublicationError):
            release_identity(self.fixture, self.release, self.catalog)
        self.release['tag_name'] = 'sdk-v1.2.3'
        self.values['/contents/.release-please-manifest.json?ref=' + 'a' * 40]['content'] = base64.b64encode(b'{"packages/sdk":"1.2.4"}').decode()
        with self.assertRaises(PublicationError):
            release_identity(self.fixture, self.release, self.catalog)

    def test_failed_recorder_comment_does_not_hide_successful_replacement(self):
        valid = copy.deepcopy(self.fixture.comments[0])
        failed = copy.deepcopy(valid)
        failed['id'] = 90
        record = comment_record(failed)
        record['recorder']['run_id'] = 29
        record['recorder']['job_id'] = 59
        failed['body'] = COMMENT_PREFIX + canonical(record).decode() + COMMENT_SUFFIX
        self.fixture.comments.insert(0, failed)
        self.values['/actions/runs/29/attempts/1'] = self.fixture.run | {'id': 29}
        original_list = self.fixture.list
        failed_job = self.fixture.jobs[1] | {'id': 59, 'run_id': 29, 'conclusion': 'failure'}
        self.fixture.list = lambda path, key: [failed_job] if path == '/actions/runs/29/attempts/1/jobs' else original_list(path, key)
        origin = authenticate_origin(self.fixture, self.identity)
        self.assertEqual(origin['merged_pr']['origin']['comment_id'], valid['id'])

    def test_changed_release_after_origin_receipt_rejected(self):
        origin = authenticate_origin(self.fixture, self.identity)
        changed = self.identity | {'commit_sha': 'd' * 40}
        with self.assertRaises(PublicationError):
            authenticate_origin(self.fixture, changed, {'record': {'payload': origin}})


if __name__ == '__main__':
    unittest.main()
