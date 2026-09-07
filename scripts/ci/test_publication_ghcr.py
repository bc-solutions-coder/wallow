import base64
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import threading
import unittest
import urllib.parse
import urllib.request

from publication import PublicationError
from publication_ghcr import GHCR, JSON_LIMIT, MANIFEST_LIMIT, MEDIA_TYPES, NoRedirect


class GHCRTests(unittest.TestCase):
    def setUp(self):
        self.requests, self.urls, self.responses = [], [], []
        self.token_response = (200, {}, b'{"token":"registry-bearer"}')
        outer = self

        class Handler(BaseHTTPRequestHandler):
            def respond(self):
                body = self.rfile.read(int(self.headers.get('Content-Length', 0)))
                outer.requests.append((self.command, self.path, dict(self.headers), body))
                status, headers, content = outer.token_response if self.path.startswith('/token?') else outer.responses.pop(0)
                self.send_response(status)
                for name, value in headers.items():
                    self.send_header(name, value)
                self.end_headers()
                try:
                    self.wfile.write(content)
                except (BrokenPipeError, ConnectionResetError):
                    pass

            do_GET = respond
            do_PUT = respond

            def log_message(self, *args):
                pass

        self.server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.addCleanup(self.close)
        address = f'http://127.0.0.1:{self.server.server_port}'

        class LocalOpener:
            def open(self, request, timeout):
                outer.urls.append(request.full_url)
                outer.assertEqual(timeout, 30)
                parsed = urllib.parse.urlsplit(request.full_url)
                outer.assertEqual((parsed.scheme, parsed.netloc), ('https', 'ghcr.io'))
                local = urllib.request.Request(address + parsed.path + ('?' + parsed.query if parsed.query else ''), data=request.data,
                                               headers=dict(request.header_items()), method=request.method)
                return urllib.request.build_opener(NoRedirect()).open(local, timeout=timeout)

        self.opener = LocalOpener()
        self.data = json.dumps({'schemaVersion': 2, 'mediaType': MEDIA_TYPES[1], 'manifests': []}).encode()
        self.digest = 'sha256:' + hashlib.sha256(self.data).hexdigest()

    def close(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join()

    def client(self):
        return GHCR('ghcr.io/example/repo-api', 'example', 'github-secret', self.opener)

    def manifest(self, body=None):
        body = self.data if body is None else body
        return 200, {'Content-Type': MEDIA_TYPES[1], 'Docker-Content-Digest': 'sha256:' + hashlib.sha256(body).hexdigest()}, body

    def missing(self):
        return 404, {}, b'{"errors":[{"code":"MANIFEST_UNKNOWN"}]}'

    def test_absent_write_and_exact_readback_use_only_scoped_registry_credentials(self):
        client = self.client()
        self.responses = [self.missing(), (201, {'Docker-Content-Digest': self.digest}, b''), self.manifest()]
        self.assertEqual(client.write_manifest('v1', self.data, MEDIA_TYPES[1], self.digest), self.digest)
        method, path, headers, _ = self.requests[0]
        self.assertEqual(method, 'GET')
        self.assertEqual(urllib.parse.parse_qs(urllib.parse.urlsplit(path).query), {'service': ['ghcr.io'], 'scope': ['repository:example/repo-api:pull,push']})
        self.assertEqual(headers['Authorization'], 'Basic ' + base64.b64encode(b'example:github-secret').decode())
        for method, path, headers, body in self.requests[1:]:
            self.assertEqual(headers['Authorization'], 'Bearer registry-bearer')
            self.assertEqual(path, '/v2/example/repo-api/manifests/v1')
            if method == 'PUT':
                self.assertEqual(body, self.data)
                self.assertEqual(headers['Content-Type'], MEDIA_TYPES[1])

    def test_identical_existing_is_idempotent_but_conflicting_bytes_are_not_overwritten(self):
        client = self.client()
        self.responses = [self.manifest()]
        self.assertEqual(client.write_manifest('v1', self.data, MEDIA_TYPES[1], self.digest), self.digest)
        self.assertNotIn('PUT', [request[0] for request in self.requests])
        self.responses = [self.manifest(self.data + b' ')]
        with self.assertRaisesRegex(PublicationError, 'conflicts'):
            client.write_manifest('v1', self.data, MEDIA_TYPES[1], self.digest)
        self.assertNotIn('PUT', [request[0] for request in self.requests])

    def test_only_explicit_manifest_unknown_is_absence(self):
        client = self.client()
        for response in [(404, {}, b''), (404, {}, b'{"errors":[]}'), (404, {}, b'{"errors":[{"code":"NAME_UNKNOWN"}]}'),
                         (404, {}, b'{"errors":[{"code":"MANIFEST_UNKNOWN"},{"code":"DENIED"}]}'),
                         (403, {}, b'github-secret registry-bearer'), (500, {}, b'github-secret')]:
            self.responses = [response]
            with self.subTest(status=response[0]), self.assertRaises(PublicationError) as error:
                client.read_manifest('v1')
            self.assertNotIn('github-secret', str(error.exception))
            self.assertNotIn('registry-bearer', str(error.exception))

    def test_auth_malformed_oversized_or_redirected_never_leaks_credentials(self):
        for response in [(200, {}, b'not json github-secret'), (200, {}, b'{}'), (200, {}, b'{"token":"bad\\nheader"}'),
                         (200, {}, b'{"token":"one","access_token":"two"}'), (200, {}, b'x' * (JSON_LIMIT + 1)),
                         (302, {'Location': 'https://other.example/steal'}, b'github-secret'), (401, {}, b'github-secret')]:
            self.token_response = response
            before = len(self.requests)
            with self.subTest(status=response[0]), self.assertRaises(PublicationError) as error:
                self.client()
            self.assertEqual(len(self.requests), before + 1)
            self.assertNotIn('github-secret', str(error.exception))

    def test_manifest_redirect_and_bad_digest_media_or_size_are_rejected(self):
        client = self.client()
        for response in [(302, {'Location': 'https://other.example/steal'}, b''),
                         (200, {'Content-Type': MEDIA_TYPES[1], 'Docker-Content-Digest': 'sha256:' + 'a' * 64}, self.data),
                         (200, {'Content-Type': 'text/plain', 'Docker-Content-Digest': self.digest}, self.data),
                         (200, {}, b'x' * (MANIFEST_LIMIT + 1))]:
            self.responses = [response]
            before = len(self.requests)
            with self.assertRaises(PublicationError):
                client.read_manifest('v1')
            self.assertEqual(len(self.requests), before + 1)
        self.responses = [self.manifest()]
        with self.assertRaises(PublicationError):
            client.read_manifest('sha256:' + 'a' * 64)

    def test_failed_put_acknowledgment_and_different_or_absent_readback_fail(self):
        client = self.client()
        for responses in [[self.missing(), (201, {}, b'')], [self.missing(), (403, {}, b'registry-bearer')],
                          [self.missing(), (201, {'Docker-Content-Digest': self.digest}, b''), self.manifest(self.data + b' ')],
                          [self.missing(), (201, {'Docker-Content-Digest': self.digest}, b''), self.missing()]]:
            self.responses = responses
            with self.assertRaises(PublicationError) as error:
                client.write_manifest('v1', self.data, MEDIA_TYPES[1], self.digest)
            self.assertNotIn('registry-bearer', str(error.exception))

    def test_transport_errors_are_sanitized(self):
        class FailedOpener:
            def open(self, request, timeout):
                raise urllib.error.URLError('github-secret registry-bearer')
        with self.assertRaises(PublicationError) as error:
            GHCR('ghcr.io/example/repo-api', 'example', 'github-secret', FailedOpener())
        self.assertNotIn('github-secret', str(error.exception))
        self.assertNotIn('registry-bearer', str(error.exception))

    def test_invalid_authorized_inputs_fail_before_registry_requests(self):
        for repository in ['https://ghcr.io/example/repo', 'other.io/example/repo', 'ghcr.io/example/../repo', 'ghcr.io/example/repo?token=x']:
            with self.assertRaises(PublicationError):
                GHCR(repository, 'example', 'github-secret', self.opener)
        self.assertEqual(self.requests, [])
        client = self.client()
        for reference in ['../other', 'v1?x=y', 'v1#fragment', 'v1\n']:
            with self.assertRaises(PublicationError):
                client.read_manifest(reference)
        with self.assertRaises(PublicationError):
            client.write_manifest('v1', self.data, MEDIA_TYPES[1], 'sha256:' + '0' * 64)
        self.assertEqual(len(self.requests), 1)


if __name__ == '__main__':
    unittest.main()
