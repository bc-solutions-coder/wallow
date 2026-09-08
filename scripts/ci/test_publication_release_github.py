import hashlib
from io import BytesIO
import json
import unittest
import urllib.error

from publication import PublicationError
from publication_release_github import ReleaseGitHub


class Response(BytesIO):
    def __init__(self, body, status=200):
        super().__init__(body)
        self.status = status


class Opener:
    def __init__(self, responses):
        self.responses = responses
        self.requests = []

    def open(self, request, timeout):
        self.requests.append(request)
        response = self.responses.pop(0)
        if isinstance(response, Exception):
            raise response
        return response


class TransportTests(unittest.TestCase):
    def client(self, responses):
        opener = Opener(responses)
        return ReleaseGitHub('owner/repo', 'private-token-value', opener), opener

    def test_fixed_upload_and_signed_download_never_forward_token(self):
        body = b'{"schema":1}'
        asset = {'id': 3, 'size': len(body), 'digest': 'sha256:' + hashlib.sha256(body).hexdigest()}
        redirect = urllib.error.HTTPError('https://api.github.com/', 302, 'Found', {'Location': 'https://signed.example/asset?token=signed'}, BytesIO())
        client, opener = self.client([Response(json.dumps(asset).encode(), 201), redirect, Response(body)])
        self.assertEqual(client.upload(2, 'wallow-release-origin-v1.json', body), asset)
        self.assertEqual(client.asset_bytes(asset), body)
        self.assertTrue(opener.requests[0].full_url.startswith('https://uploads.github.com/repos/owner/repo/releases/2/assets?name='))
        self.assertEqual(opener.requests[0].data, body)
        self.assertIsNotNone(opener.requests[1].get_header('Authorization'))
        self.assertIsNone(opener.requests[2].get_header('Authorization'))

    def test_duplicate_upload_and_malformed_response_are_sanitized(self):
        for response in [urllib.error.HTTPError('https://secret.invalid/private-token-value', 422, 'private-token-value', {}, BytesIO(b'private-token-value')), Response(b'[]', 201)]:
            client, _ = self.client([response])
            with self.assertRaises(PublicationError) as failure:
                client.upload(2, 'wallow-release-origin-v1.json', b'{}')
            self.assertNotIn('private-token-value', str(failure.exception))

    def test_wrong_digest_and_unsafe_redirect_rejected(self):
        body = b'{}'
        asset = {'id': 3, 'size': 2, 'digest': 'sha256:' + 'a' * 64}
        for response in [Response(body), urllib.error.HTTPError('https://api.github.com/', 302, 'Found', {'Location': 'file:///unsafe/asset'}, BytesIO())]:
            client, _ = self.client([response])
            with self.assertRaises(PublicationError):
                client.asset_bytes(asset)

    def test_bounded_complete_enumeration_rejects_duplicates_or_changed_total(self):
        values = [{'id': value} for value in range(1, 101)]
        client, _ = self.client([Response(json.dumps(values).encode()), Response(b'[{"id":1}]')])
        with self.assertRaises(PublicationError):
            client.array('/releases')
        client, _ = self.client([Response(json.dumps({'total_count': 101, 'workflow_runs': values}).encode()), Response(b'{"total_count":100,"workflow_runs":[]}')])
        with self.assertRaises(PublicationError):
            client.collection('/actions/workflows/ci.yml/runs?head_sha=' + 'a' * 40, 'workflow_runs')


if __name__ == '__main__':
    unittest.main()
