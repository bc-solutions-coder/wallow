import hashlib
import json
import unittest

from publication import PublicationError
from publication_pages_github import PagesGitHub
from test_publication_release_github import Opener, Response


class PagesTransportTests(unittest.TestCase):
    def client(self, responses):
        opener = Opener(responses)
        return PagesGitHub('example/repo', 'github-credential', opener), opener

    def test_exact_historical_source_artifact_and_oidc_go_only_to_pages_api(self):
        client, opener = self.client([Response(b'{"value":"oidc-credential"}'), Response(b'{"id":"deployment-123"}')])
        token = client.oidc('https://pipelines.actions.githubusercontent.com/path?id=1', 'runtime-credential')
        result = client.deploy(123, 'a' * 40, token)
        self.assertEqual(result['id'], 'deployment-123')
        self.assertEqual(opener.requests[0].get_header('Authorization'), 'Bearer runtime-credential')
        self.assertEqual(opener.requests[1].get_header('Authorization'), 'Bearer github-credential')
        self.assertEqual(opener.requests[1].full_url, 'https://api.github.com/repos/example/repo/pages/deployments')
        self.assertEqual(json.loads(opener.requests[1].data), {'artifact_id': 123, 'pages_build_version': 'a' * 40,
                                                            'oidc_token': token, 'environment': 'github-pages'})

    def test_oidc_untrusted_host_and_oversized_response_fail(self):
        for url in ('https://actions.githubusercontent.com.attacker.invalid/path', 'file:///token',
                    'https://user@pipelines.actions.githubusercontent.com/path'):
            client, opener = self.client([])
            with self.assertRaises(PublicationError):
                client.oidc(url, 'runtime-credential')
            self.assertEqual(opener.requests, [])
        client, _ = self.client([Response(b'x' * 32769)])
        with self.assertRaises(PublicationError):
            client.oidc('https://pipelines.actions.githubusercontent.com/path', 'runtime-credential')

    def test_public_readback_has_no_credentials_and_requires_exact_bytes(self):
        body = b'validated index'
        identity = {'index_size': len(body), 'index_sha256': hashlib.sha256(body).hexdigest()}
        client, opener = self.client([Response(b'older'), Response(body)])
        result = client.verify_index('https://example.github.io/repo/', identity, pause=lambda _: None)
        self.assertEqual(result['sha256'], identity['index_sha256'])
        self.assertTrue(all(request.get_header('Authorization') is None for request in opener.requests))
        client, _ = self.client([Response(b'wrong') for _ in range(24)])
        with self.assertRaises(PublicationError):
            client.verify_index('https://example.github.io/repo/', identity, pause=lambda _: None)

    def test_polling_retries_pending_and_temporary_states_but_rejects_terminal_failure(self):
        responses = [Response(json.dumps({'status': status}).encode()) for status in ('deployment_pending', 'deployment_attempt_error', 'succeed')]
        client, _ = self.client(responses)
        self.assertEqual(client.wait_deployment('deployment-123', pause=lambda _: None)['status'], 'succeed')
        client, _ = self.client([Response(b'{"status":"deployment_failed"}')])
        with self.assertRaises(PublicationError):
            client.wait_deployment('deployment-123', pause=lambda _: None)

    def test_durable_intent_never_merges_source_or_runs_additional_contexts(self):
        client, opener = self.client([Response(b'{"id":123}', 201)])
        payload = {'source_sha': 'a' * 40}
        self.assertEqual(client.intent(payload)['id'], 123)
        body = json.loads(opener.requests[0].data)
        self.assertIs(body['auto_merge'], False)
        self.assertEqual(body['required_contexts'], [])
        self.assertEqual(body['ref'], payload['source_sha'])
        self.assertEqual(body['payload'], payload)
