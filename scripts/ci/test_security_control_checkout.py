"""Execute scanner orchestration from current controls against another source checkout."""

import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


class SecurityControlCheckoutTests(unittest.TestCase):
    def test_source_checkout_cannot_replace_control_helper_or_exception_policy(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            control = root / 'controller'
            source = root / 'historical-source'
            scripts = control / 'scripts/ci'
            scripts.mkdir(parents=True)
            (control / '.github/ci').mkdir(parents=True)
            (control / '.github/ci/security-exceptions.json').write_text('{"exceptions": []}')
            for name in ('run-security.sh', 'security.py'):
                shutil.copyfile(Path(__file__).with_name(name), scripts / name)
            (source / '.github/workflows').mkdir(parents=True)
            (source / '.github/workflows/ci.yml').write_text('name: historical CI\n')
            (source / '.github/ci').mkdir()
            (source / '.github/ci/security-exceptions.json').write_text('historical policy must not be read')
            (source / 'scripts/ci').mkdir(parents=True)
            (source / 'scripts/ci/security.py').write_text("from pathlib import Path\nPath('wrong-control-ran').touch()\nraise SystemExit(9)\n")
            subprocess.run(['git', 'init', '--quiet', str(source)], check=True)
            subprocess.run(['git', 'add', '.'], cwd=source, check=True)
            binaries = source / '.ci-tools/bin'
            binaries.mkdir(parents=True)
            scanner = binaries / 'zizmor'
            scanner.write_text("#!/bin/sh\nprintf '[]\\n'\nprintf 'completed .github/workflows/ci.yml\\n' >&2\n")
            scanner.chmod(0o755)
            result = subprocess.run(['bash', str(scripts / 'run-security.sh'), 'zizmor'], cwd=source,
                                    env={key: value for key, value in os.environ.items() if key in ('PATH', 'HOME', 'TMPDIR')},
                                    capture_output=True, text=True, timeout=30)
            self.assertEqual(result.returncode, 0, result.stderr)
            report = json.loads((source / '.ci-reports/security/zizmor-gate.json').read_text())
            self.assertEqual(report['exceptions'], [])
            self.assertEqual(report['result'], 'pass')
            self.assertFalse((source / 'wrong-control-ran').exists())


if __name__ == '__main__':
    unittest.main()
