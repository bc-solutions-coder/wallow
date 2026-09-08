"""Preserve controls from Git objects without trusting mutable checkout contents."""

from pathlib import Path
import subprocess
import tempfile
import unittest

from publication import PublicationError
from recovery_checkout import preserve


class RecoveryCheckoutTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / 'source'
        self.source.mkdir()
        self.git('init', '--quiet')
        for name in ('scripts/ci/validation.py', 'scripts/ci/security.py', '.github/ci/security-exceptions.json', '.github/actions/openapi-document/action.yml', '.github/actionlint.yaml'):
            path = self.source / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('old source controls\n')
        self.old = self.commit()
        (self.source / 'scripts/ci/security.py').write_text('current security controls\n')
        (self.source / '.github/actions/openapi-document/action.yml').write_text('current OpenAPI controls\n')
        self.current = self.commit()

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.source, text=True, stderr=subprocess.DEVNULL).strip()

    def commit(self):
        self.git('add', '.')
        self.git('-c', 'user.name=CI fixture', '-c', 'user.email=ci@example.invalid', 'commit', '--quiet', '-m', 'fixture')
        return self.git('rev-parse', 'HEAD')

    def test_control_snapshot_survives_historical_checkout(self):
        output = preserve(self.source, self.root / 'controls', self.current)
        self.git('checkout', '--quiet', '--detach', self.old)
        self.assertEqual((self.source / 'scripts/ci/security.py').read_text(), 'old source controls\n')
        self.assertEqual((output / 'scripts/ci/security.py').read_text(), 'current security controls\n')
        self.assertEqual((output / '.github/actions/openapi-document/action.yml').read_text(), 'current OpenAPI controls\n')
        self.assertEqual((self.source / '.github/actions/openapi-document/action.yml').read_text(), 'old source controls\n')

    def test_mutable_and_untracked_checkout_files_are_not_control_authority(self):
        (self.source / 'scripts/ci/security.py').write_text('uncommitted replacement\n')
        (self.source / 'scripts/ci/untracked.py').write_text('untracked replacement\n')
        output = preserve(self.source, self.root / 'controls', self.current)
        self.assertEqual((output / 'scripts/ci/security.py').read_text(), 'current security controls\n')
        self.assertFalse((output / 'scripts/ci/untracked.py').exists())

    def test_wrong_controller_and_existing_destination_fail(self):
        with self.assertRaisesRegex(PublicationError, 'differs'):
            preserve(self.source, self.root / 'controls', self.old)
        output = self.root / 'existing'
        output.mkdir()
        with self.assertRaises(FileExistsError):
            preserve(self.source, output, self.current)

    def test_link_in_tracked_controls_is_rejected_before_creating_snapshot(self):
        (self.source / 'scripts/ci/link.py').symlink_to('/tmp/outside-controls')
        revision = self.commit()
        output = self.root / 'controls'
        with self.assertRaisesRegex(PublicationError, 'regular tracked files'):
            preserve(self.source, output, revision)
        self.assertFalse(output.exists())


if __name__ == '__main__':
    unittest.main()
