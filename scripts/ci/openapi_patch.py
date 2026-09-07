"""Prepare a git-apply patch for the backend-emitted contract snapshot."""
import json
from pathlib import Path
import subprocess
import sys

snapshot = Path('packages/sdk/openapi/v1.json')
document = json.loads(Path(sys.argv[1]).read_text())
document['servers'] = [{'url': 'http://localhost:5001/'}]
snapshot.write_text(json.dumps(document, indent=2) + '\n')
Path('.ci-artifacts').mkdir(exist_ok=True)
with open('.ci-artifacts/openapi.patch', 'wb') as patch:
    subprocess.run(['git', 'diff', '--binary', '--', str(snapshot)], stdout=patch, check=True)
