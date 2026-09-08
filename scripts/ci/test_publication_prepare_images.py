import gzip
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest
from unittest.mock import patch

from publication import PublicationError
from publication_images import inspect_images
from publication_prepared_images import inspect_prepared_image
from publication_prepare_images import ImagePreparation, SKOPEO
from publication_verify_images import verify_images
import test_publication_verify_images as fixtures


class PreparationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.fixture = fixtures.VerifyImagesTests()
        self.fixture.setUp()
        for image in self.fixture.catalog['images']:
            image['id'] = image['bundle']
        self.commands, self.directories = [], []
        self.change = None
        policy = self.root / '.github/ci/security-exceptions.json'
        policy.parent.mkdir(parents=True)
        policy.write_text('{"exceptions":[]}')
        self.environment = {'GITHUB_REPOSITORY': 'example/repo', 'GITHUB_SHA': 'b' * 40, 'GITHUB_RUN_ID': '30',
                            'GITHUB_RUN_ATTEMPT': '1', 'GITHUB_WORKFLOW_REF': 'example/repo/.github/workflows/publish.yml@refs/heads/main'}
        patcher = patch.dict(os.environ, self.environment)
        patcher.start()
        self.addCleanup(patcher.stop)

    def runner(self, command, **options):
        self.commands.append(command)
        if command[:2] == ['docker', 'rm']:
            return
        self.assertTrue(options['check'])
        self.assertEqual(options['timeout'], 900)
        if command[-1] == 'install-trivy':
            database = self.root / '.ci-reports/security/trivy-database.json'
            database.parent.mkdir(parents=True)
            database.write_text('{"Version":"0.74.0","VulnerabilityDB":{"Version":2}}')
            return
        if command[:2] == ['docker', 'pull']:
            self.assertEqual(command[2], SKOPEO)
            return
        if command[:2] == ['docker', 'run']:
            self.assertEqual(command[command.index('--network') + 1], 'none')
            self.assertIn(SKOPEO, command)
            mounts = {dict(part.split('=', 1) for part in command[i + 1].split(',') if '=' in part)['dst']: dict(part.split('=', 1) for part in command[i + 1].split(',') if '=' in part)['src'] for i, value in enumerate(command) if value == '--mount'}
            destination = Path(mounts['/output'])
            self.directories.append(destination.parent)
            if self.change == 'conversion':
                raise subprocess.TimeoutExpired(command, 900)
            tag = command[-2].removeprefix('docker-archive:/input/images.tar.gz:')
            with tarfile.open(mounts['/input/images.tar.gz']) as archive:
                selected = next(item for item in json.load(archive.extractfile('manifest.json')) if tag in item['RepoTags'])
                config = archive.extractfile(selected['Config']).read()
                layers = [archive.extractfile(name).read() for name in selected['Layers']]
            def blob(body, media):
                digest = hashlib.sha256(body).hexdigest()
                (destination / digest).write_bytes(body)
                return {'digest': 'sha256:' + digest, 'size': len(body), 'mediaType': media}
            manifest = {'schemaVersion': 2, 'mediaType': 'application/vnd.docker.distribution.manifest.v2+json',
                        'config': blob(config, 'application/vnd.docker.container.image.v1+json'),
                        'layers': [blob(gzip.compress(body, mtime=0), 'application/vnd.docker.image.rootfs.diff.tar.gzip') for body in layers]}
            (destination / 'manifest.json').write_text(json.dumps(manifest))
            (destination / 'unexpected-tool-file').write_text('not a publication input')
            if self.change == 'prepared':
                (destination / manifest['config']['digest'][7:]).write_bytes(b'tampered')
            return
        self.assertIn('--skip-db-update', command)
        self.assertEqual(command[command.index('--scanners') + 1], 'vuln')
        self.assertEqual(command[command.index('--ignorefile') + 1], '/dev/null')
        source = Path(command[command.index('--input') + 1])
        with tarfile.open(source) as archive:
            manifest = json.load(archive.extractfile('manifest.json'))[0]
            config = json.load(archive.extractfile(manifest['Config']))
        tag = manifest['RepoTags'][0]
        expected = inspect_images(source, {tag: 'linux/' + config['architecture']})[tag]
        report = {'SchemaVersion': 2, 'ArtifactType': 'container_image',
                  'Metadata': {'ImageID': expected['config_digest'], 'DiffIDs': expected['diff_ids'], 'ImageConfig': config, 'RepoTags': [tag]},
                  'Results': [{'Target': str(source) + ' (alpine 3.21)', 'Class': 'os-pkgs', 'Packages': [{'Name': 'library', 'Version': '1'}]}]}
        if self.change == 'architecture':
            report['Metadata']['ImageConfig']['architecture'] = 'other'
        if self.change == 'imageid':
            report['Metadata']['ImageID'] = 'sha256:' + 'f' * 64
        if self.change == 'rootfs':
            report['Metadata']['ImageConfig']['rootfs'] = []
        if self.change == 'tag':
            report['Metadata']['RepoTags'] = ['other:tag']
        if self.change == 'layers':
            report['Metadata']['DiffIDs'] = []
        if self.change == 'coverage':
            report['Results'] = []
        if self.change == 'packages':
            report['Results'][0]['Packages'] = []
        if self.change == 'blocking':
            report['Results'][0]['Vulnerabilities'] = [{'VulnerabilityID': 'CVE-EXAMPLE', 'Severity': 'HIGH', 'PkgName': 'library', 'InstalledVersion': '1', 'FixedVersion': '2'}]
        Path(command[command.index('--output') + 1]).write_text(json.dumps(report))

    def prepare(self, route='full', database=None):
        plan, client = self.fixture.plan(route)
        plan['controller_sha'] = 'b' * 40
        preparation = ImagePreparation(plan, self.fixture.catalog, self.root / 'prepared', self.root / 'reports', database, self.root, self.runner)
        result = verify_images(client, plan, self.fixture.catalog, preparation)
        self.fixture.assert_cleaned(client)
        return result

    def test_full_pipeline_prepares_every_platform_seals_exact_bytes_and_excludes_companion(self):
        result = self.prepare()
        self.assertEqual(set(result), {'app', 'infra', 'docs'})
        for bundle, value in result.items():
            prepared = value['prepared']
            archive = self.root / 'prepared' / prepared['file']
            self.assertEqual(prepared['digest'], 'sha256:' + hashlib.sha256(archive.read_bytes()).hexdigest())
            seal = json.loads(Path(str(archive) + '.json').read_text())
            self.assertEqual(seal['sha'], 'b' * 40)
            self.assertEqual(seal['run_id'], '30')
            self.assertEqual(seal['kind'], 'prepared-images')
            with tarfile.open(archive) as content:
                names = content.getnames()
                inventory = json.load(content.extractfile('inventory.json'))
                self.assertFalse(inventory['publication_authorized'])
                self.assertEqual({item['tag'] for item in inventory['images']}, {bundle + ':amd64', bundle + ':arm64'})
                self.assertNotIn('wallow-bff-example:test', {item['tag'] for item in inventory['images']})
                self.assertFalse(any('unexpected-tool-file' in name for name in names))
                index = json.load(content.extractfile(bundle + '/index.json'))
                self.assertEqual([item['platform']['architecture'] for item in index['manifests']], ['amd64', 'arm64'])
                for item in inventory['images']:
                    with tempfile.TemporaryDirectory() as directory:
                        destination = Path(directory)
                        for member in content:
                            if member.name.startswith(item['directory']):
                                (destination / Path(member.name).name).write_bytes(content.extractfile(member).read())
                        self.assertEqual(inspect_prepared_image(destination, item['source']), item['prepared'])
        self.assertEqual(sum(command[-1] == 'install-trivy' for command in self.commands), 1)
        self.assertEqual(sum(command[:2] == ['docker', 'run'] for command in self.commands), 6)
        self.assertTrue(all(not directory.exists() for directory in self.directories))

    def test_docs_route_reuses_database_and_prepares_only_docs(self):
        database = self.root / 'database.json'
        database.write_text('{"Version":"0.74.0","VulnerabilityDB":{"Version":2}}')
        self.assertEqual(list(self.prepare('docs', database)), ['docs'])
        self.assertFalse(any(command[-1] == 'install-trivy' for command in self.commands))
        self.assertEqual(sum(command[:2] == ['docker', 'run'] for command in self.commands), 2)
        self.assertEqual((self.root / 'reports/trivy-database.json').read_bytes(), database.read_bytes())

    def test_wrong_controller_seal_fails_before_tools_and_outputs(self):
        plan, client = self.fixture.plan('docs')
        plan['controller_sha'] = 'c' * 40
        preparation = ImagePreparation(plan, self.fixture.catalog, self.root / 'prepared', self.root / 'reports', controller=self.root, runner=self.runner)
        with self.assertRaises(PublicationError):
            verify_images(client, plan, self.fixture.catalog, preparation)
        self.assertEqual(self.commands, [])
        self.assertFalse((self.root / 'prepared').exists())
        self.fixture.assert_cleaned(client)

    def test_conversion_tamper_identity_coverage_and_current_blocker_reject_and_clean(self):
        for change in ('conversion', 'prepared', 'architecture', 'imageid', 'rootfs', 'tag', 'layers', 'coverage', 'packages', 'blocking'):
            with self.subTest(change=change), tempfile.TemporaryDirectory() as directory:
                original = self.root
                self.root = Path(directory)
                policy = self.root / '.github/ci/security-exceptions.json'
                policy.parent.mkdir(parents=True)
                policy.write_text('{"exceptions":[]}')
                self.change = change
                with self.assertRaises(PublicationError):
                    self.prepare('docs')
                self.assertFalse((self.root / 'prepared/docs').exists())
                self.assertTrue(all(not path.exists() for path in self.directories))
                self.assertTrue(any(command[:2] == ['docker', 'rm'] for command in self.commands))
                if change == 'blocking':
                    gate = json.loads((self.root / 'reports/docs-amd64-gate.json').read_text())
                    self.assertEqual(gate['blocking_count'], 1)
                    self.assertTrue(gate['findings'][0]['scope'].startswith('docs:amd64::docs:amd64 (alpine'))
                self.root = original


if __name__ == '__main__':
    unittest.main()
