from dataclasses import asdict
import hashlib
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest
import zipfile

from publication import Producer, PublicationError
from publication_verify_dependencies import expected_inputs, verify_dependencies, verify_coverage


class Transport:
    def __init__(self, data):
        self.data, self.destinations = data, []

    def download(self, artifact, destination):
        self.destinations.append(destination)
        destination.write_bytes(self.data)
        return destination


class DependencyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        policy = self.root / '.github/ci/security-exceptions.json'
        policy.parent.mkdir(parents=True)
        policy.write_text('{"exceptions":[]}')
        self.producer = Producer('example/repo', 1, 'a' * 40, 20, 2, 10, 'example/repo/.github/workflows/ci.yml@refs/heads/main')
        self.files = {'pnpm-lock.yaml': b'lockfileVersion: 9.0\n', 'api/src/App/packages.lock.json': b'{"version":1,"dependencies":{}}'}
        self.fingerprints = {'pnpm-lock.yaml': hashlib.sha256(self.files['pnpm-lock.yaml']).hexdigest(), 'api/src/App/App.csproj': 'b' * 64}
        self.commands, self.scan_roots = [], []
        self.scan_change = None

    def fixture(self, change=None):
        files = dict(self.files)
        inventory = list(files)
        if change == 'pnpm':
            files['pnpm-lock.yaml'] += b'changed'
        if change == 'missing':
            del files['api/src/App/packages.lock.json']
        if change == 'extra':
            files['api/Other/packages.lock.json'] = b'{}'
        if change == 'inventory':
            inventory = ['pnpm-lock.yaml']
        if change == 'duplicate_inventory':
            inventory.append('pnpm-lock.yaml')
        payload = io.BytesIO()
        with tarfile.open(fileobj=payload, mode='w:gz') as archive:
            for name in ('.', './api', './api/src', './api/src/App'):
                member = tarfile.TarInfo(name)
                member.type = tarfile.DIRTYPE
                archive.addfile(member)
            entries = list(files.items()) + [('inventory.json', json.dumps(inventory).encode())]
            if change == 'duplicate_file':
                entries.append(('pnpm-lock.yaml', b'duplicate'))
            if change == 'traversal':
                entries.append(('../escape', b'bad'))
            for name, data in entries:
                member = tarfile.TarInfo('./' + name)
                member.size = len(data)
                archive.addfile(member, io.BytesIO(data))
            if change == 'symlink':
                member = tarfile.TarInfo('linked')
                member.type, member.linkname = tarfile.SYMTYPE, '/tmp/escape'
                archive.addfile(member)
        data = payload.getvalue()
        seal = {'schema': 1, 'repository': self.producer.repository, 'sha': self.producer.source_sha, 'run_id': '20', 'run_attempt': '2',
                'workflow_ref': self.producer.workflow_ref, 'kind': 'dependencies', 'variant': 'resolved-locks', 'file': 'dependencies.tar.gz', 'sha256': hashlib.sha256(data).hexdigest()}
        if change == 'seal':
            seal['run_attempt'] = '1'
        output = io.BytesIO()
        with zipfile.ZipFile(output, 'w') as archive:
            archive.writestr('dependencies.tar.gz', data)
            archive.writestr('dependencies.tar.gz.json', json.dumps(seal))
        data = output.getvalue()
        item = {'id': 50, 'name': 'dependency-inputs-20-2', 'digest': 'sha256:' + hashlib.sha256(data).hexdigest(), 'size': len(data),
                'payload': 'dependencies.tar.gz', 'kind': 'dependencies', 'variant': 'resolved-locks'}
        return {'producer': asdict(self.producer), 'route': 'full', 'artifacts': [item], 'inputs': {'input_sha256': self.fingerprints}}, Transport(data)

    def runner(self, command, **options):
        self.commands.append(command)
        self.assertTrue(options['check'])
        self.assertEqual(options['timeout'], 900)
        if command[-1] == 'install-trivy':
            database = self.root / '.ci-reports/security/trivy-database.json'
            database.parent.mkdir(parents=True, exist_ok=True)
            database.write_text('{"Version":"0.74.0","VulnerabilityDB":{"Version":2}}')
            return
        scan_root = Path(command[-1])
        self.scan_roots.append(scan_root)
        self.assertIn('--include-dev-deps', command)
        self.assertIn('--skip-db-update', command)
        self.assertEqual(command[command.index('--scanners') + 1], 'vuln')
        self.assertEqual(command[command.index('--ignorefile') + 1], '/dev/null')
        results = [{'Target': name, 'Class': 'lang-pkgs', 'Type': 'pnpm' if name == 'pnpm-lock.yaml' else 'nuget',
                    'Packages': [{'Name': 'library', 'Version': '1.0.0'}]} for name in self.files]
        if self.scan_change == 'missing':
            results.pop()
        if self.scan_change == 'blocking':
            results[0]['Vulnerabilities'] = [{'VulnerabilityID': 'CVE-EXAMPLE', 'Severity': 'HIGH', 'PkgName': 'library', 'InstalledVersion': '1.0.0', 'FixedVersion': '2.0.0'}]
        Path(command[command.index('--output') + 1]).write_text(json.dumps({'SchemaVersion': 2, 'Results': results}))

    def test_verifies_exact_inputs_and_runs_only_fresh_trivy_then_cleans(self):
        plan, client = self.fixture()
        result = verify_dependencies(client, plan, self.root / 'reports', self.root, self.runner)
        self.assertEqual({item['path'] for item in result['inventory']}, set(self.files))
        self.assertEqual(result['blocking_count'], 0)
        self.assertEqual(len(self.commands), 2)
        self.assertEqual(self.commands[0][-1], 'install-trivy')
        self.assertEqual(len(result['reports']), 4)
        self.assertTrue(all(not path.parent.exists() for path in client.destinations))
        self.assertTrue(all(not path.exists() for path in self.scan_roots))

    def test_archive_or_seal_mismatch_fails_before_scanner_and_cleans(self):
        for change in ('pnpm', 'missing', 'extra', 'inventory', 'duplicate_inventory', 'duplicate_file', 'traversal', 'symlink', 'seal'):
            plan, client = self.fixture(change)
            with self.subTest(change=change), self.assertRaises(PublicationError):
                verify_dependencies(client, plan, self.root / ('reports-' + change), self.root, self.runner)
            self.assertTrue(all(not path.parent.exists() for path in client.destinations))
        self.assertEqual(self.commands, [])

    def test_missing_scanner_coverage_or_new_fixable_high_finding_blocks_and_retains_reports(self):
        for change in ('missing', 'blocking'):
            self.scan_change = change
            plan, client = self.fixture()
            reports = self.root / ('reports-' + change)
            with self.subTest(change=change), self.assertRaises(PublicationError):
                verify_dependencies(client, plan, reports, self.root, self.runner)
            self.assertTrue((reports / 'trivy.json').is_file())
            self.assertTrue(all(not path.parent.exists() for path in client.destinations))
        self.assertEqual(json.loads((self.root / 'reports-blocking/gate.json').read_text())['blocking_count'], 1)

    def test_coverage_rejects_extra_duplicate_wrong_type_and_empty_packages(self):
        inventory = [{'path': 'pnpm-lock.yaml'}]
        result = {'Target': 'pnpm-lock.yaml', 'Class': 'lang-pkgs', 'Type': 'pnpm', 'Packages': [{'Name': 'a'}]}
        self.assertEqual(verify_coverage({'Results': [result | {'Target': '/scan/pnpm-lock.yaml'}]}, inventory, Path('/scan'))['Results'][0]['Target'], 'pnpm-lock.yaml')
        for results in ([result, result], [result | {'Target': 'other/pnpm-lock.yaml'}], [result | {'Type': 'nuget'}], [result | {'Packages': []}]):
            with self.assertRaises(PublicationError):
                verify_coverage({'Results': results}, inventory, Path('/scan'))

    def test_docs_route_is_noop_and_missing_project_identity_or_duplicate_artifact_fails(self):
        plan, client = self.fixture()
        plan['route'], plan['artifacts'] = 'docs', []
        self.assertIsNone(verify_dependencies(client, plan, self.root / 'reports', self.root, self.runner))
        self.assertEqual(client.destinations, [])
        with self.assertRaises(PublicationError):
            expected_inputs({'pnpm-lock.yaml': self.fingerprints['pnpm-lock.yaml']})
        plan, client = self.fixture()
        plan['artifacts'] *= 2
        with self.assertRaises(PublicationError):
            verify_dependencies(client, plan, self.root / 'reports', self.root, self.runner)
        self.assertEqual(client.destinations, [])


if __name__ == '__main__':
    unittest.main()
