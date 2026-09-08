import hashlib
import json
import unittest

from publication import PublicationError
from publication_image_provenance import OCI_INDEX, main_index, nightly_action, prior_source


class ProvenanceTests(unittest.TestCase):
    def setUp(self):
        self.variants = {f'linux/{arch}': {'platform': f'linux/{arch}', 'manifest_digest': 'sha256:' + digit * 64, 'manifest_size': 500} for arch, digit in [('amd64', 'a'), ('arm64', 'b')]}
        self.source = 'c' * 40
        self.data = main_index('example/repo', self.source, 'api', self.variants)
        self.current = {'bytes': self.data, 'media_type': OCI_INDEX, 'digest': 'sha256:' + hashlib.sha256(self.data).hexdigest()}
        self.registry = self
        self.immutable = self.current
        self.comparison = {'status': 'ahead', 'base_commit': {'sha': self.source}, 'merge_base_commit': {'sha': self.source}}

    def read_manifest(self, reference):
        self.assertEqual(reference, 'sha-' + self.source)
        return self.immutable

    def main_comparison(self, sha):
        self.assertEqual(sha, self.source)
        return {'status': 'ahead', 'base_commit': {'sha': sha}, 'merge_base_commit': {'sha': sha}}

    def get(self, path):
        self.assertEqual(path, '/compare/' + self.source + '...' + 'd' * 40)
        return self.comparison

    def test_stable_index_binds_source_and_both_child_digests_without_invocation_state(self):
        self.assertEqual(self.data, main_index('example/repo', self.source, 'api', dict(reversed(list(self.variants.items())))))
        self.assertEqual(prior_source(self.current, 'example/repo', 'api'), self.source)
        document = json.loads(self.data)
        self.assertEqual(document['mediaType'], OCI_INDEX)
        self.assertEqual([item['digest'] for item in document['manifests']], ['sha256:' + 'a' * 64, 'sha256:' + 'b' * 64])
        self.assertEqual(document['annotations']['io.wallow.image.amd64.digest'], 'sha256:' + 'a' * 64)

    def test_missing_advance_and_older_retry_preserves_alias(self):
        self.assertEqual(nightly_action(self, self, None, 'example/repo', 'api', 'd' * 40), 'advance')
        self.assertEqual(nightly_action(self, self, self.current, 'example/repo', 'api', 'd' * 40), 'advance')
        self.comparison = {'status': 'behind', 'base_commit': {'sha': self.source}, 'merge_base_commit': {'sha': 'd' * 40}}
        self.assertEqual(nightly_action(self, self, self.current, 'example/repo', 'api', 'd' * 40), 'skip-older')

    def test_unknown_legacy_or_changed_provenance_is_rejected(self):
        for change in ('missing', 'repo', 'source', 'digest', 'extra'):
            document = json.loads(self.data)
            if change == 'missing':
                del document['annotations']
            elif change == 'repo':
                document['annotations']['org.opencontainers.image.source'] = 'https://github.com/other/repo'
            elif change == 'source':
                document['annotations']['org.opencontainers.image.revision'] = 'short'
            elif change == 'digest':
                document['annotations']['io.wallow.image.amd64.digest'] = 'sha256:' + 'f' * 64
            else:
                document['unknown'] = True
            body = json.dumps(document).encode()
            value = self.current | {'bytes': body, 'digest': 'sha256:' + hashlib.sha256(body).hexdigest()}
            with self.subTest(change=change), self.assertRaises(PublicationError):
                prior_source(value, 'example/repo', 'api')

    def test_missing_immutable_or_incomparable_history_never_advances(self):
        self.immutable = None
        with self.assertRaises(PublicationError):
            nightly_action(self, self, self.current, 'example/repo', 'api', 'd' * 40)
        self.immutable = self.current
        self.comparison = {'status': 'diverged'}
        with self.assertRaises(PublicationError):
            nightly_action(self, self, self.current, 'example/repo', 'api', 'd' * 40)
