import copy
from dataclasses import asdict
import hashlib
import io
import json
import tarfile
import unittest
import zipfile

from publication import Producer, PublicationError
from publication_verify_packages import verify_packages


class Transport:
    def __init__(self, archive):
        self.archive, self.paths = archive, []

    def download(self, artifact, destination):
        self.paths.append(destination)
        destination.write_bytes(self.archive)
        return destination


class VerifyPackagesTests(unittest.TestCase):
    def setUp(self):
        self.producer = Producer('example/repo', 1, 'a' * 40, 20, 2, 10, 'example/repo/.github/workflows/ci.yml@refs/heads/main')
        self.catalog = {'package_registry': 'https://npm.pkg.github.com', 'components': [
            {'id': 'sdk', 'path': 'packages/sdk', 'package': {'name': '@example/sdk', 'tarball': 'sdk.tgz'}},
            {'id': 'base', 'path': 'packages/base', 'package': {'name': '@example/base', 'tarball': 'base.tgz'}}],
            'validation_only_packages': [{'name': '@example/private', 'tarball': 'private.tgz'}]}

    def fixture(self, change=None):
        packed = {}
        for name in ('sdk', 'base', 'private'):
            manifest = {'name': '@example/' + name, 'version': '1.0.0', 'repository': {'type': 'git', 'url': 'https://github.com/example/repo.git'},
                        'publishConfig': {'registry': 'https://npm.pkg.github.com'}, 'scripts': {'prepublishOnly': 'exit 99'}}
            if name == 'sdk':
                manifest['dependencies'] = {'@example/base': '^1.0.0', 'external': '^2.0.0'}
                if change == 'version':
                    manifest['version'] = '2.0.0'
            if name == 'private':
                manifest['private'] = True
                del manifest['publishConfig']
                if change == 'companion_name':
                    manifest['name'] = '@example/unexpected'
            inner = io.BytesIO()
            with tarfile.open(fileobj=inner, mode='w:gz') as archive:
                data = json.dumps(manifest).encode()
                member = tarfile.TarInfo('package/package.json')
                member.size = len(data)
                archive.addfile(member, io.BytesIO(data))
                if change == 'unsafe_companion' and name == 'private':
                    member = tarfile.TarInfo('package/link')
                    member.type = tarfile.SYMTYPE
                    member.linkname = '/tmp/escape'
                    archive.addfile(member)
            packed[name + '.tgz'] = inner.getvalue()
        outer = io.BytesIO()
        with tarfile.open(fileobj=outer, mode='w:gz') as archive:
            member = tarfile.TarInfo('./')
            member.type = tarfile.DIRTYPE
            archive.addfile(member)
            for name, data in packed.items():
                if change == 'missing' and name == 'base.tgz':
                    continue
                member = tarfile.TarInfo('./' + name)
                member.size = len(data)
                archive.addfile(member, io.BytesIO(data))
            if change in ('extra', 'traversal', 'duplicate', 'symlink'):
                member = tarfile.TarInfo({'extra': 'unknown.tgz', 'traversal': '../escape', 'duplicate': 'sdk.tgz', 'symlink': 'link.tgz'}[change])
                if change == 'symlink':
                    member.type, member.linkname = tarfile.SYMTYPE, '/tmp/escape'
                    archive.addfile(member)
                else:
                    member.size = 1
                    archive.addfile(member, io.BytesIO(b'x'))
        payload = outer.getvalue()
        seal = {'schema': 1, 'repository': self.producer.repository, 'sha': self.producer.source_sha, 'run_id': '20', 'run_attempt': '2',
                'workflow_ref': self.producer.workflow_ref, 'kind': 'packages', 'variant': 'pnpm', 'file': 'packages.tar.gz', 'sha256': hashlib.sha256(payload).hexdigest()}
        if change == 'seal':
            seal['run_attempt'] = '1'
        output = io.BytesIO()
        with zipfile.ZipFile(output, 'w') as archive:
            archive.writestr('packages.tar.gz', payload)
            archive.writestr('packages.tar.gz.json', json.dumps(seal))
        data = output.getvalue()
        artifact = {'id': 50, 'name': 'js-packages-20-2', 'digest': 'sha256:' + hashlib.sha256(data).hexdigest(), 'size': len(data),
                    'payload': 'packages.tar.gz', 'kind': 'packages', 'variant': 'pnpm'}
        plan = {'producer': asdict(self.producer), 'route': 'full', 'artifacts': [artifact], 'inputs': {'catalog': copy.deepcopy(self.catalog),
                'component_versions': {'packages/sdk': '1.0.0', 'packages/base': '1.0.0'}}}
        return plan, Transport(data), packed

    def test_inspects_candidates_orders_dependencies_and_excludes_private_companion(self):
        plan, client, packed = self.fixture()
        result = verify_packages(client, plan, self.catalog)
        self.assertEqual(result['artifact_id'], 50)
        self.assertEqual([package['name'] for package in result['packages']], ['@example/base', '@example/sdk'])
        self.assertEqual(result['packages'][1]['sha256'], hashlib.sha256(packed['sdk.tgz']).hexdigest())
        self.assertEqual(result['packages'][1]['dependencies'], {'@example/base': '^1.0.0', 'external': '^2.0.0'})
        self.assertTrue(all(not path.parent.exists() for path in client.paths))

    def test_fork_candidate_inspection_records_upstream_metadata_without_destination_authorization(self):
        plan, client, _ = self.fixture()
        plan['producer']['repository'] = 'fork-owner/unconfigured-fork'
        plan['producer']['workflow_ref'] = 'fork-owner/unconfigured-fork/.github/workflows/ci.yml@refs/heads/main'
        with zipfile.ZipFile(io.BytesIO(client.archive)) as archive:
            payload = archive.read('packages.tar.gz')
            seal = json.loads(archive.read('packages.tar.gz.json'))
        seal['repository'] = plan['producer']['repository']
        seal['workflow_ref'] = plan['producer']['workflow_ref']
        output = io.BytesIO()
        with zipfile.ZipFile(output, 'w') as archive:
            archive.writestr('packages.tar.gz', payload)
            archive.writestr('packages.tar.gz.json', json.dumps(seal))
        client.archive = output.getvalue()
        plan['artifacts'][0]['size'] = len(client.archive)
        plan['artifacts'][0]['digest'] = 'sha256:' + hashlib.sha256(client.archive).hexdigest()
        result = verify_packages(client, plan, self.catalog)
        self.assertEqual(result['packages'][0]['name'], '@example/base')
        self.assertEqual(result['packages'][0]['repository']['url'], 'https://github.com/example/repo.git')

    def test_rejects_unsafe_missing_extra_identity_or_seal_and_cleans(self):
        for change in ('missing', 'extra', 'traversal', 'duplicate', 'symlink', 'version', 'companion_name', 'unsafe_companion', 'seal'):
            plan, client, _ = self.fixture(change)
            with self.subTest(change=change), self.assertRaises(PublicationError):
                verify_packages(client, plan, self.catalog)
            self.assertTrue(all(not path.parent.exists() for path in client.paths))

    def test_docs_route_has_no_packages_and_rejects_unexpected_package_artifact(self):
        plan, client, _ = self.fixture()
        plan['route'] = 'docs'
        with self.assertRaises(PublicationError):
            verify_packages(client, plan, self.catalog)
        plan['artifacts'] = []
        self.assertIsNone(verify_packages(client, plan, self.catalog))
        self.assertEqual(client.paths, [])

    def test_incomplete_versions_catalog_drift_and_missing_or_duplicate_artifacts_fail_before_download(self):
        for change in ('versions', 'catalog', 'missing', 'duplicate'):
            plan, client, _ = self.fixture()
            if change == 'versions':
                del plan['inputs']['component_versions']['packages/sdk']
            elif change == 'catalog':
                plan['inputs']['catalog']['package_registry'] = 'https://other.example'
            elif change == 'missing':
                plan['artifacts'] = []
            else:
                plan['artifacts'] *= 2
            with self.subTest(change=change), self.assertRaises(PublicationError):
                verify_packages(client, plan, self.catalog)
            self.assertEqual(client.paths, [])


if __name__ == '__main__':
    unittest.main()
