import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import tempfile
import threading
import unittest
import urllib.request

from publication import PublicationError
from publication_artifacts import Artifact
from publication_github import GitHub, NoRedirect


class TransportTests(unittest.TestCase):
    def setUp(self):
        self.requests = []
        self.responses = {}
        outer = self

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                outer.requests.append((self.path, self.headers.get('Authorization')))
                status, headers, body = outer.responses.get(self.path, (404, {}, b''))
                self.send_response(status)
                for key, value in headers.items():
                    self.send_header(key, value)
                self.end_headers()
                self.wfile.write(body)

            def log_message(self, *args):
                pass

        self.server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.addCleanup(self.close_server)
        address = f'http://127.0.0.1:{self.server.server_port}'

        class LocalOpener:
            def open(self, request, timeout):
                # Preserve request headers and exercise real HTTP redirects/errors.
                target = urllib.parse.urlsplit(request.full_url)
                local = urllib.request.Request(address + target.path + ('?' + target.query if target.query else ''), headers=dict(request.header_items()))
                return urllib.request.build_opener(NoRedirect()).open(local, timeout=timeout)

        self.client = GitHub('example/repo', 'disposable-token', LocalOpener())
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.output = Path(self.temp.name) / 'artifact.zip'
        self.data = b'exact archive bytes'
        self.artifact = Artifact(50, 'payload-20-2', 'sha256:' + hashlib.sha256(self.data).hexdigest(), len(self.data))
        self.responses['/repos/example/repo/actions/artifacts/50/zip'] = (302, {'Location': 'https://storage.example/archive'}, b'')
        self.responses['/archive'] = (200, {}, self.data)

    def close_server(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join()

    def test_download_uses_auth_only_for_github_and_verifies_bytes(self):
        self.client.download(self.artifact, self.output)
        self.assertEqual(self.output.read_bytes(), self.data)
        self.assertEqual(self.requests, [('/repos/example/repo/actions/artifacts/50/zip', 'Bearer disposable-token'), ('/archive', None)])

    def test_rejects_redirect_downgrade_and_followup_redirect(self):
        self.responses['/repos/example/repo/actions/artifacts/50/zip'] = (302, {'Location': 'http://storage.example/archive'}, b'')
        with self.assertRaises(PublicationError):
            self.client.download(self.artifact, self.output)
        self.assertEqual(len(self.requests), 1)
        self.responses['/repos/example/repo/actions/artifacts/50/zip'] = (302, {'Location': 'https://storage.example/archive'}, b'')
        self.responses['/archive'] = (302, {'Location': 'https://other.example/stolen'}, b'')
        with self.assertRaises(PublicationError):
            self.client.download(self.artifact, self.output)
        self.assertFalse(any(path == '/stolen' for path, _ in self.requests))

    def test_rejects_missing_and_changed_bytes_without_partial_file(self):
        for status, data in [(410, b''), (200, b'short'), (200, b'x' * len(self.data)), (200, self.data + b'extra')]:
            self.responses['/archive'] = (status, {}, data)
            with self.subTest(status=status, data=data), self.assertRaises(PublicationError):
                self.client.download(self.artifact, self.output)
            self.assertFalse(self.output.exists())

    def test_paginates_complete_metadata_and_rejects_count_changes(self):
        path = '/repos/example/repo/actions/runs/20/artifacts'
        self.responses[path + '?per_page=100&page=1'] = (200, {}, json.dumps({'total_count': 2, 'artifacts': [{'id': 1}]}).encode())
        self.responses[path + '?per_page=100&page=2'] = (200, {}, json.dumps({'total_count': 2, 'artifacts': [{'id': 2}]}).encode())
        self.assertEqual(self.client.list('/actions/runs/20/artifacts', 'artifacts'), [{'id': 1}, {'id': 2}])
        self.responses[path + '?per_page=100&page=2'] = (200, {}, json.dumps({'total_count': 3, 'artifacts': [{'id': 2}]}).encode())
        with self.assertRaises(PublicationError):
            self.client.list('/actions/runs/20/artifacts', 'artifacts')

    def test_api_redirect_never_receives_forwarded_authorization(self):
        self.responses['/repos/example/repo/metadata'] = (302, {'Location': 'https://other.example/stolen'}, b'')
        with self.assertRaises(PublicationError):
            self.client.get('/metadata')
        self.assertEqual(self.requests, [('/repos/example/repo/metadata', 'Bearer disposable-token')])

    def producer_responses(self):
        sha, main_sha = 'a' * 40, 'b' * 40
        repository = {'id': 1, 'full_name': 'example/repo', 'default_branch': 'main'}
        workflow = {'id': 10, 'path': '.github/workflows/ci.yml'}
        run = {'id': 20, 'run_attempt': 2, 'workflow_id': 10, 'path': workflow['path'], 'event': 'push', 'head_branch': 'main', 'head_sha': sha, 'repository': repository, 'head_repository': repository, 'status': 'completed', 'conclusion': 'success'}
        gate = {'id': 30, 'run_id': 20, 'run_attempt': 2, 'head_sha': sha, 'name': 'CI / required', 'status': 'completed', 'conclusion': 'success', 'check_run_url': 'https://api.github.com/repos/example/repo/check-runs/40'}
        check = {'id': 40, 'name': 'CI / required', 'head_sha': sha, 'status': 'completed', 'conclusion': 'success', 'app': {'id': 15368, 'slug': 'github-actions'}}
        values = {
            '': repository, '/actions/workflows/ci.yml': workflow,
            '/actions/runs/20/attempts/2': run,
            '/actions/runs/20/attempts/2/jobs?per_page=100&page=1': {'total_count': 1, 'jobs': [gate]},
            '/git/ref/heads/main': {'ref': 'refs/heads/main', 'object': {'type': 'commit', 'sha': main_sha}},
            f'/compare/{sha}...{main_sha}': {'status': 'ahead', 'base_commit': {'sha': sha}, 'merge_base_commit': {'sha': sha}},
            '/check-runs/40': check,
        }
        for path, value in values.items():
            self.responses['/repos/example/repo' + path] = (200, {}, json.dumps(value).encode())
        return values

    def test_authorizes_exact_attempt_against_observed_main_tip(self):
        self.producer_responses()
        producer, jobs = self.client.producer(20, 2)
        self.assertEqual((producer.run_id, producer.run_attempt, producer.source_sha), (20, 2, 'a' * 40))
        self.assertEqual(len(jobs), 1)
        paths = [path for path, _ in self.requests]
        self.assertIn('/repos/example/repo/compare/' + 'a' * 40 + '...' + 'b' * 40, paths)
        self.assertFalse(any('latest' in path for path in paths))

    def test_rejects_foreign_check_url_without_requesting_it(self):
        values = self.producer_responses()
        path = '/actions/runs/20/attempts/2/jobs?per_page=100&page=1'
        values[path]['jobs'][0]['check_run_url'] = 'https://api.github.com/repos/foreign/repo/check-runs/40'
        self.responses['/repos/example/repo' + path] = (200, {}, json.dumps(values[path]).encode())
        with self.assertRaises(PublicationError):
            self.client.producer(20, 2)
        self.assertFalse(any('/check-runs/' in path for path, _ in self.requests))


if __name__ == '__main__':
    unittest.main()
