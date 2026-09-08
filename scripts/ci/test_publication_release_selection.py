import unittest

from publication import PublicationError
from publication_release_selection import choose_producer


class Client:
    def __init__(self):
        self.runs = [{'id': 20, 'run_attempt': 2, 'head_sha': 'a' * 40, 'head_branch': 'main', 'event': 'push'}, {'id': 10, 'run_attempt': 1, 'head_sha': 'a' * 40, 'head_branch': 'main', 'event': 'push'}]
        self.registered = {10, 20}
        self.attempts = {(10, 1): 'success', (20, 1): 'success', (20, 2): 'failure'}

    def collection(self, path, key):
        return self.runs

    def get(self, path):
        parts = path.split('/')
        run, attempt = int(parts[3]), int(parts[5])
        return {'id': run, 'run_attempt': attempt, 'head_sha': 'a' * 40, 'status': 'completed', 'conclusion': self.attempts[run, attempt]}

    def list(self, path, key):
        run = int(path.split('/')[3])
        return [{'name': 'register-publication', 'conclusion': 'success'}] if run in self.registered else []


class SelectionTests(unittest.TestCase):
    def setUp(self):
        self.client = Client()
        self.catalog = {'components': [{'id': 'api', 'path': 'api', 'tag_prefix': 'api-v'}]}
        self.release = {'commit_sha': 'a' * 40, 'component': 'api', 'component_path': 'api', 'version': '1.2.3', 'tag_name': 'api-v1.2.3'}
        self.calls = []

    def resolve(self, client, context, run, attempt):
        self.calls.append((run, attempt))
        return {'producer': {'source_sha': 'a' * 40, 'run_id': run, 'run_attempt': attempt}, 'route': 'full', 'registration': {'id': run + 100}, 'artifacts': [],
                'inputs': {'catalog': self.catalog, 'component_versions': {'api': '1.2.3'}, 'input_sha256': {'lock': 'b' * 64}}}

    def choose(self, **kwargs):
        return choose_producer(self.client, {}, self.release, self.catalog, resolver=self.resolve, **kwargs)

    def test_earliest_registered_success_includes_prior_successful_attempt(self):
        selected = self.choose()
        self.assertEqual(selected['producer']['run_id'], 10)
        self.client.registered.remove(10)
        selected = self.choose()
        self.assertEqual((selected['producer']['run_id'], selected['producer']['run_attempt']), (20, 1))

    def test_newer_release_waits_for_own_success(self):
        self.client.attempts = {pair: 'failure' for pair in self.client.attempts}
        self.assertIsNone(self.choose())
        self.assertEqual(self.calls, [])

    def test_pinned_identity_wins_and_conflicting_explicit_retry_fails(self):
        pinned = self.choose(explicit=(20, 1))
        self.assertEqual(self.choose(pinned=pinned), pinned)
        with self.assertRaises(PublicationError):
            self.choose(pinned=pinned, explicit=(10, 1))

    def test_expired_pinned_artifacts_never_fall_back(self):
        pinned = self.choose()
        def unavailable(*args):
            raise PublicationError('Expired artifact')
        with self.assertRaisesRegex(PublicationError, 'Expired'):
            choose_producer(self.client, {}, self.release, self.catalog, pinned=pinned, resolver=unavailable)

    def test_altered_pinned_registration_or_source_version_rejected(self):
        pinned = self.choose()
        pinned['registration']['id'] += 1
        with self.assertRaises(PublicationError):
            self.choose(pinned=pinned)
        self.release['version'] = '2.0.0'
        with self.assertRaises(PublicationError):
            self.choose()

    def test_incomplete_attempt_enumeration_fails(self):
        self.client.runs[0]['run_attempt'] = 101
        with self.assertRaises(PublicationError):
            self.choose()


if __name__ == '__main__':
    unittest.main()
