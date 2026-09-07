from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

import action_policy


class ActionPolicyTests(unittest.TestCase):
    def test_supported_queue_values_require_noncancelling_max_queue(self):
        for concurrency in ({'group': 'publication', 'queue': 'max'}, {'group': 'publication', 'queue': 'max', 'cancel-in-progress': False}, {'group': 'publication', 'queue': 'single', 'cancel-in-progress': True}):
            action_policy.validate_document({'concurrency': concurrency})
        for concurrency in ({'group': 'publication', 'queue': 'invalid'}, {'group': 'publication', 'queue': 'max', 'cancel-in-progress': True}, {'group': 'publication', 'queue': 'max', 'cancel-in-progress': '${{ inputs.cancel }}'}):
            for document in ({'concurrency': concurrency}, {'jobs': {'publish': {'concurrency': concurrency}}}):
                with self.subTest(document=document), self.assertRaises(ValueError):
                    action_policy.validate_document(document)

    def test_nested_workflow_and_composite_actions_accept_immutable_refs(self):
        action_policy.validate_document({'jobs': {'build': {'uses': 'owner/repo/.github/workflows/build.yml@' + 'a' * 40}, 'test': {'steps': [{'uses': './.github/actions/setup'}, {'uses': 'actions/checkout@' + 'b' * 40}, {'uses': 'docker://alpine@sha256:' + 'c' * 64}]}}})
        action_policy.validate_document({'runs': {'using': 'composite', 'steps': [{'uses': 'owner/repo/action@' + 'd' * 40}]}})

    def test_mutable_malformed_and_traversing_refs_are_rejected(self):
        for ref in ('actions/checkout@v4', 'owner/repo@main', 'owner/repo@abc', './actions/../outside', './../outside', './actions//setup', 'docker://alpine:latest', 'https://example.test/action@' + 'a' * 40, None, 123):
            with self.subTest(ref=ref), self.assertRaises(ValueError):
                action_policy.validate_document({'jobs': {'build': {'steps': [{'uses': ref}]}}})
        for doc in (None, [], 'workflow'):
            with self.assertRaises(ValueError):
                action_policy.validate_document(doc)

    def test_cli_checks_local_actions_and_reports_only_paths(self):
        script = Path(__file__).with_name('action_policy.py')
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            workflow = root / '.github/workflows/ci.yml'
            workflow.parent.mkdir(parents=True)
            workflow.write_text('jobs:\n  build:\n    steps:\n      - uses: actions/checkout@' + 'a' * 40 + '\n')
            action = root / '.github/actions/setup/action.yml'
            action.parent.mkdir(parents=True)
            action.write_text('runs:\n  steps:\n    - uses: owner/repo@PRIVATE_MARKER\n')
            def run():
                return subprocess.run([sys.executable, str(script)], cwd=root, capture_output=True, text=True)
            result = run()
            self.assertEqual(result.returncode, 1)
            self.assertIn('.github/actions/setup/action.yml', result.stderr)
            self.assertNotIn('PRIVATE_MARKER', result.stdout + result.stderr)
            action.write_text('runs:\n  steps:\n    - uses: ./.github/actions/other\n')
            self.assertEqual(run().returncode, 0)
            for text in ('[not, a, mapping]', 'runs: [PRIVATE_MARKER', 'runs: &loop [*loop]'):
                action.write_text(text)
                result = run()
                self.assertEqual(result.returncode, 1)
                self.assertNotIn('PRIVATE_MARKER', result.stdout + result.stderr)
                self.assertNotIn('Traceback', result.stderr)


if __name__ == '__main__':
    unittest.main()
