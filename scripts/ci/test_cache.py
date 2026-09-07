import unittest
from unittest.mock import patch
import urllib.error
from cache import reachable, select_cache


class CacheTests(unittest.TestCase):
    def setUp(self):
        self.main = dict(GITHUB_EVENT_NAME='push', GITHUB_REF='refs/heads/main', MAIN_CACHE='true', TAILNET_RESULT='success', TURBO_API='https://cache.example.test', TURBO_TEAM='ci', TURBO_TOKEN='test-value', TURBO_REMOTE_CACHE_SIGNATURE_KEY='test-signing-key')

    def test_only_configured_main_push_can_probe_private_cache(self):
        self.assertEqual(select_cache(self.main, lambda _: True)[0], 'local:rw,remote:rw')
        for override in [{'GITHUB_EVENT_NAME': 'pull_request'}, {'GITHUB_EVENT_NAME': 'schedule'}, {'GITHUB_REF': 'refs/heads/feature'}, {'MAIN_CACHE': ''}, {'TAILNET_RESULT': 'failure'}, {'TURBO_TOKEN': ''}, {'TURBO_REMOTE_CACHE_SIGNATURE_KEY': ''}]:
            def probe(_):
                self.fail('private cache must not be contacted')
            self.assertEqual(select_cache({**self.main, **override}, probe)[0], 'local:rw')

    def test_unavailable_cache_falls_back_without_credentials_in_diagnostics(self):
        mode, reason = select_cache(self.main, lambda _: False)
        self.assertEqual(mode, 'local:rw')
        for secret in ['https://cache.example.test', 'test-value', 'test-signing-key']:
            self.assertNotIn(secret, reason)

    def test_probe_reports_safe_transport_category_without_endpoint_or_error_text(self):
        reports = []
        with patch('cache.urllib.request.urlopen', side_effect=urllib.error.URLError(OSError('private-host secret-value'))):
            self.assertFalse(reachable('http://private-host:3000', reports.append))
        self.assertEqual(reports, ['Remote cache probe: transport failure (OSError)'])

    def test_probe_rejects_malformed_configuration_without_network_access(self):
        for url in ['private-host:3000', 'http://name:secret@private-host', 'http://private-host?token=secret', 'http://private-host/secret value', 'http://private host:3000', 'http://private-host/secret\nvalue']:
            with self.subTest(url=url), patch('cache.urllib.request.urlopen') as request:
                reports = []
                self.assertFalse(reachable(url, reports.append))
                request.assert_not_called()
                self.assertEqual(reports, ['Remote cache probe: invalid endpoint configuration'])

    def test_probe_reports_http_status_and_preserves_authenticated_server_reachability(self):
        for status in [401, 403, 404, 500]:
            reports = []
            with self.subTest(status=status), patch('cache.urllib.request.urlopen', side_effect=urllib.error.HTTPError('http://private-host', status, 'secret', {}, None)):
                self.assertEqual(reachable('http://private-host', reports.append), status in (401, 403))
                self.assertEqual(reports, [f'Remote cache probe: HTTP {status}'])
