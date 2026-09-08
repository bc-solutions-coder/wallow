"""Publication command selects recovery only from a complete explicit request."""

from contextlib import ExitStack, redirect_stdout
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

from publication_plan import main


class PlanCommandTests(unittest.TestCase):
    def execute(self, recovery):
        with tempfile.TemporaryDirectory() as directory, ExitStack() as stack:
            output = Path(directory) / 'plan.json'
            stack.enter_context(patch.dict(os.environ, {'GITHUB_REPOSITORY': 'owner/repo', 'GITHUB_EVENT_NAME': 'workflow_dispatch', 'GH_TOKEN': 'job-token'}, clear=True))
            stack.enter_context(patch('sys.argv', ['publication_plan.py', '--run-id', '20', '--attempt', '2', '--output', str(output), *recovery]))
            catalog = {'components': []}
            stack.enter_context(patch('publication_plan.load_catalog', return_value=catalog))
            normal = stack.enter_context(patch('publication_plan.GitHub'))
            recovered = stack.enter_context(patch('publication_release_github.ReleaseGitHub'))
            normal_plan = {'schema': 1, 'route': 'full', 'controller_sha': 'b' * 40, 'producer': {'source_sha': 'a' * 40}}
            recovery_plan = normal_plan | {'schema': 2, 'recovery': {'receipt': {'asset_id': 50}}}
            resolver = stack.enter_context(patch('publication_plan.resolve', return_value=normal_plan))
            recovery_resolver = stack.enter_context(patch('publication_selection.resolve_selection', return_value=recovery_plan))
            for name in ('verify_packages', 'verify_dependencies', 'verify_images', 'verify_site'):
                stack.enter_context(patch('publication_plan.' + name, return_value=[]))
            stack.enter_context(patch('publication_plan.ImagePreparation'))
            stack.enter_context(redirect_stdout(io.StringIO()))
            main()
            return json.loads(output.read_text()), normal, recovered, resolver, recovery_resolver

    def test_normal_command_does_not_select_recovery(self):
        plan, normal, recovered, resolver, recovery_resolver = self.execute([])
        self.assertEqual(plan['schema'], 1)
        normal.assert_called_once()
        recovered.assert_not_called()
        resolver.assert_called_once()
        recovery_resolver.assert_not_called()

    def test_explicit_command_uses_recovery_and_retains_binding_in_output(self):
        plan, normal, recovered, resolver, recovery_resolver = self.execute([
            '--recovery-run', '70', '--recovery-attempt', '1', '--release-id', '5'])
        self.assertEqual(plan['schema'], 2)
        self.assertEqual(plan['recovery']['receipt']['asset_id'], 50)
        normal.assert_not_called()
        recovered.assert_called_once()
        resolver.assert_not_called()
        self.assertEqual(recovery_resolver.call_args.args[-1], {'release_id': 5, 'run_id': 70, 'run_attempt': 1})


if __name__ == '__main__':
    unittest.main()
