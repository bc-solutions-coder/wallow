import os
import json
import hashlib
import validation
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).with_name('validation.py')


class ValidationTests(unittest.TestCase):
    def test_docs_allowlist_and_uncertain_diff(self):
        for data in (b'M\0README.md\0', b'D\0docs/old.md\0', b'A\0docs/toc.yml\0', b'R100\0docs/old.md\0docs/new.md\0'):
            self.assertEqual(validation.classify_diff(data)[0], 'docs')
        for data in (b'', b'M\0README.md', b'R100\0docs/old.md\0', b'U\0README.md\0', b'M\0docs/script.js\0', b'M\0.github/workflows/ci.yml\0', b'M\0docs/../app.md\0'):
            self.assertEqual(validation.classify_diff(data)[0], 'full')
        self.assertEqual(validation.classify('push', '0' * 40, 'a' * 40)[0], 'full')
        self.assertEqual(validation.classify('push', 'a' * 40, 'b' * 40)[0], 'full')

    def test_gate_requires_every_expected_result(self):
        common = ['route', 'docs']
        full = ['test', 'images']
        success = {job: {'result': 'success'} for job in common + full}
        validation.gate('full', success, common, full)
        docs = {**success, 'test': {'result': 'skipped'}, 'images': {'result': 'skipped'}}
        validation.gate('docs', docs, common, full)
        for route, needs in [('full', docs), ('docs', {**docs, 'docs': {'result': 'skipped'}}), ('full', {**success, 'test': {'result': 'failure'}}), ('full', {**success, 'test': {'result': 'cancelled'}}), ('full', {k: v for k, v in success.items() if k != 'images'}), ('full', {**success, 'unknown': {'result': 'success'}})]:
            with self.assertRaises(ValueError):
                validation.gate(route, needs, common, full)

    def test_portable_gate_only_allows_codeql_skip(self):
        needs = {'policy': {'result': 'success'}, 'js': {'result': 'success'}, 'codeql': {'result': 'skipped'}}
        validation.gate('full', needs, ['policy'], ['js', 'codeql'], 'portable')
        with self.assertRaises(ValueError):
            validation.gate('full', needs, ['policy'], ['js', 'codeql'], 'codeql')
        with self.assertRaises(ValueError):
            validation.gate('full', {**needs, 'js': {'result': 'skipped'}}, ['policy'], ['js', 'codeql'], 'portable')

    def test_gate_cli_rejects_malformed_and_missing_results(self):
        for needs in ('{}', 'null', '{broken'):
            result = subprocess.run([sys.executable, str(SCRIPT), 'gate', '--route', 'full', '--needs-json', needs, '--common', 'policy', '--full', 'js'], capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertNotIn('Traceback', result.stderr)

    def test_manifest_cli_rejects_wrong_identity_and_modified_payload(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            payload = root / 'image.tar'
            payload.write_bytes(b'abc')
            manifest = root / 'manifest.json'
            env = {**os.environ, 'GITHUB_REPOSITORY': 'owner/repo', 'GITHUB_SHA': 'a' * 40, 'GITHUB_RUN_ID': '123', 'GITHUB_RUN_ATTEMPT': '1', 'GITHUB_WORKFLOW_REF': 'owner/repo/.github/workflows/ci.yml@refs/heads/main'}
            def run(command, overrides=None):
                return subprocess.run([sys.executable, str(SCRIPT), command, '--file', str(payload), '--manifest', str(manifest), '--kind', 'image', '--variant', 'api-amd64'], env={**env, **(overrides or {})}, capture_output=True, text=True)
            result = run('seal')
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(json.loads(manifest.read_text())['sha256'], hashlib.sha256(b'abc').hexdigest())
            self.assertEqual(run('verify').returncode, 0)
            for key, value in [('GITHUB_REPOSITORY', 'other/repo'), ('GITHUB_SHA', 'b' * 40), ('GITHUB_RUN_ID', '124'), ('GITHUB_RUN_ATTEMPT', '2'), ('GITHUB_WORKFLOW_REF', ''), ('GITHUB_WORKFLOW_REF', 'owner/repo/.github/workflows/other.yml@refs/heads/main')]:
                self.assertNotEqual(run('verify', {key: value}).returncode, 0)
            original_manifest = manifest.read_text()
            for field, value in [('schema', 2), ('kind', 'other'), ('variant', 'other'), ('file', 'other.tar')]:
                metadata = json.loads(original_manifest)
                metadata[field] = value
                manifest.write_text(json.dumps(metadata))
                self.assertNotEqual(run('verify').returncode, 0)
            manifest.write_text('{malformed')
            self.assertNotEqual(run('verify').returncode, 0)
            manifest.write_text(original_manifest)
            payload.unlink()
            self.assertNotEqual(run('verify').returncode, 0)
            payload.write_bytes(b'replaced image')
            self.assertNotEqual(run('verify').returncode, 0)
            self.assertNotEqual(run('seal', {'GITHUB_SHA': 'short'}).returncode, 0)
            self.assertNotEqual(run('seal', {'GITHUB_WORKFLOW_REF': ''}).returncode, 0)

    def test_classifier_uses_complete_pr_diff_and_both_rename_paths(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            def git(*args):
                return subprocess.check_output(['git', *args], cwd=root, stderr=subprocess.DEVNULL).decode().strip()
            git('init')
            git('config', 'user.email', 'ci@example.test')
            git('config', 'user.name', 'CI')
            (root / 'app.py').write_text('hello\n')
            git('add', '.')
            git('commit', '-m', 'initial')
            base = git('rev-parse', 'HEAD')
            (root / 'docs').mkdir()
            git('mv', 'app.py', 'docs/guide.md')
            git('commit', '-m', 'rename')
            before_docs = git('rev-parse', 'HEAD')
            (root / 'README.md').write_text('docs\n')
            git('add', '.')
            git('commit', '-m', 'docs')
            output = root / 'output'
            result = subprocess.run([sys.executable, str(SCRIPT), 'classify', '--event', 'pull_request', '--base', base, '--head', git('rev-parse', 'HEAD')], cwd=root, env={**os.environ, 'GITHUB_OUTPUT': str(output)}, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn('route=full\n', output.read_text())
            summary = root / 'summary'
            result = subprocess.run([sys.executable, str(SCRIPT), 'classify', '--event', 'push', '--base', before_docs, '--head', git('rev-parse', 'HEAD')], cwd=root, env={**os.environ, 'GITHUB_STEP_SUMMARY': str(summary)}, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn('route=docs\n', result.stdout)
            self.assertIn('**docs**', summary.read_text())
            git('rm', 'docs/guide.md')
            git('commit', '-m', 'delete docs')
            result = subprocess.run([sys.executable, str(SCRIPT), 'classify', '--event', 'push', '--base', before_docs, '--head', git('rev-parse', 'HEAD')], cwd=root, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn('route=docs\n', result.stdout)


if __name__ == '__main__':
    unittest.main()
