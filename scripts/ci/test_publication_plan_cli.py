"""Publication command inspects the exact successful main build."""

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
    def execute(self):
        with tempfile.TemporaryDirectory() as directory, ExitStack() as stack:
            output = Path(directory) / 'plan.json'
            stack.enter_context(patch.dict(os.environ, {'GITHUB_REPOSITORY': 'owner/repo', 'GITHUB_EVENT_NAME': 'workflow_dispatch', 'GH_TOKEN': 'job-token'}, clear=True))
            stack.enter_context(patch('sys.argv', ['publication_plan.py', '--run-id', '20', '--attempt', '2', '--output', str(output)]))
            catalog = {'components': []}
            stack.enter_context(patch('publication_plan.load_catalog', return_value=catalog))
            normal = stack.enter_context(patch('publication_plan.GitHub'))
            normal_plan = {'schema': 1, 'route': 'full', 'controller_sha': 'b' * 40, 'producer': {'source_sha': 'a' * 40}}
            resolver = stack.enter_context(patch('publication_plan.resolve', return_value=normal_plan))
            for name in ('verify_packages', 'verify_images', 'verify_site'):
                stack.enter_context(patch('publication_plan.' + name, return_value=[]))
            stack.enter_context(patch('publication_plan.ImagePreparation'))
            stack.enter_context(redirect_stdout(io.StringIO()))
            main()
            return json.loads(output.read_text()), normal, resolver

    def test_command_resolves_the_requested_attempt(self):
        plan, normal, resolver = self.execute()
        self.assertEqual(plan['schema'], 1)
        normal.assert_called_once()
        self.assertEqual(resolver.call_args.args[-2:], (20, 2))


if __name__ == '__main__':
    unittest.main()
