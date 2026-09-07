import io
import json
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest
from unittest.mock import patch

from publication import PublicationError
from publication_npm import publish_tarball
from publication_packages import inspect_package


class NpmTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / 'candidate.tgz'
        manifest = {'name': '@example/sdk', 'version': '1.0.0', 'repository': {'type': 'git', 'url': 'https://github.com/example/repo.git'}, 'publishConfig': {'registry': 'https://npm.pkg.github.com'}, 'scripts': {'prepublishOnly': 'exit 99'}}
        data = json.dumps(manifest).encode()
        with tarfile.open(self.path, 'w:gz') as archive:
            member = tarfile.TarInfo('package/package.json')
            member.size = len(data)
            archive.addfile(member, io.BytesIO(data))
        self.package = inspect_package(self.path, '@example/sdk', '1.0.0', 'https://npm.pkg.github.com', 'example/repo')
        self.metadata = {'name': '@example/sdk', 'version': '1.0.0', 'dist': {'integrity': self.package.integrity}}
        self.missing = (1, {'error': {'code': 'E404'}})
        self.calls = []

    def publish(self, responses, real_dry_run=False):
        def runner(arguments, **options):
            self.calls.append(arguments)
            self.assertNotIn('dummy-token', arguments)
            self.assertNotIn('NODE_OPTIONS', options['env'])
            self.assertEqual(options['env']['NODE_AUTH_TOKEN'], 'dummy-token')
            self.assertFalse((options['cwd'] / 'package.json').exists())
            if real_dry_run and 'publish' in arguments:
                result = subprocess.run(arguments + ['--dry-run'], **options)
                self.assertEqual(json.loads(result.stdout)['integrity'], self.package.integrity)
                return result
            code, data = responses.pop(0)
            return subprocess.CompletedProcess(arguments, code, json.dumps(data), '')
        with patch.dict('os.environ', {'NODE_OPTIONS': '--require=/untrusted/hook.js'}):
            return publish_tarball(self.path, '@example/sdk', '1.0.0', 'example/repo', 'dummy-token', runner)

    def test_identical_existing_version_is_read_only(self):
        result = self.publish([(0, self.metadata)])
        self.assertEqual(result['state'], 'already-published')
        self.assertFalse(any('publish' in command for command in self.calls))

    def test_conflicting_version_never_publishes(self):
        with self.assertRaises(PublicationError):
            self.publish([(0, self.metadata | {'dist': {'integrity': 'different'}})])
        self.assertFalse(any('publish' in command for command in self.calls))

    def test_publishes_immutable_candidate_then_requires_readback(self):
        result = self.publish([self.missing, (0, {}), (0, self.metadata)])
        self.assertEqual(result['state'], 'published')
        publication = next(command for command in self.calls if 'publish' in command)
        self.assertEqual(publication[publication.index('--tag') + 1], 'validated-' + self.package.sha256)

    def test_missing_readback_and_registry_errors_do_not_report_success(self):
        for responses in ([self.missing, (0, {}), self.missing], [(1, {'error': {'code': 'E403'}})], [self.missing, (1, {'error': {'code': 'ECONFLICT'}})]):
            with self.assertRaises(PublicationError):
                self.publish(list(responses))

    def test_actual_npm_dry_run_accepts_exact_tarball_without_lifecycle_execution(self):
        result = self.publish([self.missing, (0, self.metadata)], real_dry_run=True)
        self.assertEqual(result['integrity'], self.package.integrity)


if __name__ == '__main__':
    unittest.main()
