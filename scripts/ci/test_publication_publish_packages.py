from dataclasses import asdict
import hashlib
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from publication import PublicationError
from publication_artifacts import Artifact
from publication_packages import inspect_package
from publication_publish_packages import publish
import test_publication_verify_packages as fixtures


class Registry:
    def __init__(self):
        self.present = {}
        self.operations = []

    def __enter__(self):
        return self

    def __exit__(self, *args):
        pass

    def versions(self, name):
        return [version for (package, version) in self.present if package == name]

    def read(self, package):
        self.operations.append(('read', package.name))
        return (package.name, package.version) in self.present

    def publish(self, path, package):
        self.operations.append(('write', package.name))
        if hashlib.sha256(path.read_bytes()).hexdigest() != package.sha256:
            raise PublicationError('Wrong actual tarball')
        self.present[package.name, package.version] = package.sha256
        return {'name': package.name, 'version': package.version, 'sha256': package.sha256, 'integrity': package.integrity,
                'state': 'published', 'registry_bytes_verified': True}


class WriterTests(unittest.TestCase):
    def setUp(self):
        fixture = fixtures.VerifyPackagesTests()
        fixture.setUp()
        self.fixture = fixture
        _, _, self.packed = fixture.fixture()
        self.catalog = fixture.catalog | {'package_scope': '@example'}
        self.registry = Registry()
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def prepared(self, missing=False, bad_seal=False):
        candidates, content = [], io.BytesIO()
        with tarfile.open(fileobj=content, mode='w', format=tarfile.USTAR_FORMAT) as archive:
            for release_id, name in ((1, 'sdk'), (2, 'base')):
                if missing and name == 'base':
                    continue
                data = self.packed[name + '.tgz']
                path = self.root / (name + '.tgz')
                path.write_bytes(data)
                package = inspect_package(path, '@example/' + name, '1.0.0', self.catalog['package_registry'], 'example/repo')
                filename = f'release-{release_id}.tgz'
                member = tarfile.TarInfo(filename)
                member.size = len(data)
                archive.addfile(member, io.BytesIO(data))
                candidates.append({'release': {'id': release_id, 'version': '1.0.0'}, 'origin': {'asset_id': 10}, 'selection': {'asset_id': 20},
                                   'package': asdict(package), 'file': filename, 'size': len(data)})
        payload = content.getvalue()
        producer = self.fixture.producer
        seal = {'schema': 1, 'repository': producer.repository, 'sha': producer.source_sha, 'run_id': str(producer.run_id),
                'run_attempt': '1' if bad_seal else str(producer.run_attempt), 'workflow_ref': producer.workflow_ref,
                'kind': 'prepared-packages', 'variant': 'release', 'file': 'packages.tar', 'sha256': hashlib.sha256(payload).hexdigest()}
        output = io.BytesIO()
        with zipfile.ZipFile(output, 'w') as archive:
            archive.writestr('packages.tar', payload)
            archive.writestr('packages.tar.json', json.dumps(seal))
        data = output.getvalue()
        artifact = Artifact(99, 'prepared-packages-20-2', 'sha256:' + hashlib.sha256(data).hexdigest(), len(data))
        plan = {'catalog': self.catalog, 'prepared': {'candidates': candidates, 'published': [], 'target_release_id': None,
                'archive': {'sha256': hashlib.sha256(payload).hexdigest(), 'size': len(payload)}},
                'dependency_readiness': [{'name': '@example/sdk', 'version': '1.0.0', 'dependency': '@example/base', 'range': '^1.0.0', 'dependency_version': '1.0.0', 'dependency_sha256': candidates[-1]['package']['sha256']}],
                'ordered_release_ids': [2, 1]}
        client = fixtures.Transport(data)
        client.repository = 'example/repo'
        return client, plan, producer, artifact

    def run_writer(self, fixture):
        client, plan, producer, artifact = fixture
        with patch('publication_publish_packages.frame', return_value=({'job_id': 100}, {})), patch('publication_publish_packages.authorize_packages', return_value=(plan, producer, artifact, {})), patch('publication_publish_packages.PackageRegistry', return_value=self.registry):
            return publish(client, {}, 1, 1, 2, 1, self.catalog, self.root, 'fixture', {'entries': []})

    def test_real_sealed_tarballs_are_preflighted_then_published_in_dependency_order(self):
        fixture = self.prepared()
        from publication_package_preparation import dependency_preflight
        order = dependency_preflight(fixture[1], 'example/repo', self.registry)
        fixture[1]['dependency_readiness'] = order['dependencies']
        self.registry.operations.clear()
        result = self.run_writer(fixture)
        self.assertEqual([entry['release']['id'] for entry in result['entries']], [2, 1])
        self.assertEqual(self.registry.operations, [('read', '@example/base'), ('read', '@example/sdk'), ('write', '@example/base'), ('write', '@example/sdk')])
        self.assertTrue(all(not path.parent.exists() for path in fixture[0].paths))

    def test_missing_internal_release_or_altered_seal_prevents_every_write(self):
        for options in ({'missing': True}, {'bad_seal': True}):
            self.registry.operations.clear()
            with self.subTest(options=options), self.assertRaises(PublicationError):
                self.run_writer(self.prepared(**options))
            self.assertFalse(any(operation == 'write' for operation, _ in self.registry.operations))

    def test_plan_package_digest_mismatch_prevents_registry_access(self):
        fixture = self.prepared()
        fixture[1]['prepared']['candidates'][0]['package']['sha256'] = '0' * 64
        with self.assertRaises(PublicationError):
            self.run_writer(fixture)
        self.assertEqual(self.registry.operations, [])


if __name__ == '__main__':
    unittest.main()
