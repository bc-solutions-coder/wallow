import unittest
from cache import select_cache


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
