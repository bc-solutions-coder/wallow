"""Evaluate packed internal ranges with the SemVer library bundled in the npm toolchain."""

import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

from publication import PublicationError


SCRIPT = '''const fs = require('node:fs');
const semver = require(process.argv[1]);
const input = JSON.parse(fs.readFileSync(0, 'utf8'));
if (!semver.validRange(input.range) || input.versions.some(value => !semver.valid(value))) process.exit(2);
process.stdout.write(JSON.stringify(semver.rsort(input.versions.filter(value => semver.satisfies(value, input.range)))));
'''


def satisfying_versions(versions, requirement, runner=subprocess.run):
    if not isinstance(versions, list) or len(versions) > 1000 or any(not isinstance(version, str) or len(version) > 200 for version in versions) or len(set(versions)) != len(versions) or not isinstance(requirement, str) or not 0 < len(requirement) <= 1000:
        raise PublicationError('Internal package dependency range input exceeds its bounded contract')
    npm, node = shutil.which('npm'), shutil.which('node')
    if not npm or not node:
        raise PublicationError('Package publication requires the configured Node/npm toolchain')
    installation = Path(npm).resolve().parent.parent
    module = installation / 'node_modules/semver'
    try:
        manifest = json.loads((installation / 'package.json').read_text())
        semver = json.loads((module / 'package.json').read_text())
        if manifest.get('name') != 'npm' or semver.get('name') != 'semver' or not (module / 'index.js').is_file():
            raise PublicationError('SemVer must come from the configured npm installation')
        environment = {key: value for key, value in os.environ.items() if key in ('PATH', 'SYSTEMROOT', 'TMPDIR')}
        with tempfile.TemporaryDirectory(prefix='wallow-package-ranges-') as directory:
            result = runner([node, '-e', SCRIPT, str(module)], input=json.dumps({'versions': versions, 'range': requirement}), cwd=directory,
                            env=environment, capture_output=True, text=True, timeout=30)
        if result.returncode != 0 or not isinstance(result.stdout, str) or len(result.stdout.encode()) > 256 * 1024:
            raise PublicationError('Internal package dependency range could not be resolved')
        selected = json.loads(result.stdout)
        if not isinstance(selected, list) or len(selected) != len(set(selected)) or any(version not in versions for version in selected):
            raise PublicationError('SemVer returned unexpected dependency versions')
        return selected
    except (OSError, ValueError, TypeError, subprocess.TimeoutExpired):
        raise PublicationError('Configured npm SemVer could not evaluate package dependencies') from None
