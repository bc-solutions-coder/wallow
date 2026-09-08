import contextlib
import io
import os
from pathlib import Path
from types import SimpleNamespace
import tempfile
import unittest
from unittest.mock import Mock, patch

from publication import PublicationError
from publication_package_preparation import main, prepare
from publication_release_image_authorization import discover


class RoutingTests(unittest.TestCase):
    def setUp(self):
        self.catalog = {'components': [{'id': 'platform'}, {'id': 'sdk', 'package': {'name': '@example/sdk'}}], 'images': [{'component': 'platform'}]}
        self.client = SimpleNamespace(controller=Mock(), get=Mock(return_value={}), repository='example/repo')
        self.context = {'event_name': 'workflow_dispatch'}

    def test_package_retry_for_valid_image_release_skips_before_package_configuration(self):
        with patch('publication_package_preparation.release_identity', return_value={'id': 5, 'component': 'platform'}), \
             patch('publication_package_preparation.package_configuration', side_effect=AssertionError('Unrelated package destination must not be used')):
            result = prepare(self.client, self.context, 1, 1, 2, 1, self.catalog, '.', '/unused', 'fixture', 5)
        self.assertEqual(result['unrelated_release_id'], 5)

    def test_image_retry_for_valid_package_release_skips_before_image_environment(self):
        with patch('publication_release_image_authorization.release_identity', return_value={'id': 5, 'component': 'sdk'}), \
             patch('publication_release_image_authorization.image_environment', side_effect=AssertionError('Unrelated image environment must not be used')):
            matrix, pending = discover(self.client, self.context, self.catalog, 5, (1, 1))
        self.assertEqual(matrix, {'include': []})
        self.assertEqual(pending, [])

    def test_unknown_or_unpublished_release_never_becomes_unrelated_skip(self):
        for module, callback in [('publication_package_preparation', lambda: prepare(self.client, self.context, 1, 1, 2, 1, self.catalog, '.', '/unused', 'fixture', 5)),
                                 ('publication_release_image_authorization', lambda: discover(self.client, self.context, self.catalog, 5, (1, 1)))]:
            with patch(module + '.release_identity', side_effect=PublicationError('Release must be an exact published GitHub release')):
                with self.assertRaises(PublicationError):
                    callback()

    def test_cli_reports_fixed_configuration_error_without_registry_writes(self):
        error = io.StringIO()
        with tempfile.TemporaryDirectory() as directory:
            environment = {'ENABLE_PACKAGE_PUBLISH': 'true', 'GITHUB_RUN_ID': '2', 'GITHUB_RUN_ATTEMPT': '1', 'GH_TOKEN': 'never-print-this'}
            with patch.dict(os.environ, environment), patch('sys.argv', ['prepare', '--producer-run', '1', '--producer-attempt', '1', '--output', str(Path(directory) / 'plan.json')]), \
                 patch('publication_package_preparation.ReleaseGitHub', return_value=self.client), patch('publication_package_preparation.load_catalog', return_value=self.catalog), \
                 patch('publication_package_preparation.package_configuration', side_effect=PublicationError('Enabled package publication requires repository-owned package metadata')), \
                 patch('publication_package_preparation.PackageRegistry', side_effect=AssertionError('Registry must not be reached')), contextlib.redirect_stderr(error):
                with self.assertRaises(SystemExit) as exit_:
                    main()
            self.assertEqual(exit_.exception.code, 1)
            self.assertFalse((Path(directory) / 'plan.json').exists())
        self.assertIn('repository-owned package metadata', error.getvalue())
        self.assertNotIn('never-print-this', error.getvalue())


if __name__ == '__main__':
    unittest.main()
