import base64
import hashlib
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest

from publication import PublicationError
from publication_packages import Package, bounded_tar, dependency_order, inspect_package


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / 'sdk.tgz'
        self.manifest = {'name': '@example/sdk', 'version': '2.0.0', 'publishConfig': {'registry': 'https://npm.pkg.github.com'}, 'dependencies': {'@example/errors': '^1.0.0'}, 'scripts': {'prepublishOnly': 'exit 99'}}

    def pack(self, changes=None, extra=None, link=False):
        with tarfile.open(self.path, 'w:gz') as bundle:
            data = json.dumps(self.manifest | (changes or {})).encode()
            member = tarfile.TarInfo('package/package.json')
            member.size = len(data)
            bundle.addfile(member, io.BytesIO(data))
            if extra:
                member = tarfile.TarInfo(extra)
                if link:
                    member.type = tarfile.SYMTYPE
                    member.linkname = '/tmp/escape'
                bundle.addfile(member, io.BytesIO(b''))

    def inspect(self):
        return inspect_package(self.path, '@example/sdk', '2.0.0', 'https://npm.pkg.github.com')

    def test_inspects_exact_tarball_without_running_lifecycle_scripts(self):
        self.pack()
        package = self.inspect()
        self.assertEqual(package.dependencies, {'@example/errors': '^1.0.0'})
        self.assertEqual(package.sha256, hashlib.sha256(self.path.read_bytes()).hexdigest())
        self.assertEqual(package.integrity, 'sha512-' + base64.b64encode(hashlib.sha512(self.path.read_bytes()).digest()).decode())

    def test_rejects_wrong_name_version_privacy_and_registry(self):
        for change in [{'name': '@foreign/sdk'}, {'version': '2.1.0'}, {'private': True}, {'publishConfig': {'registry': 'https://registry.npmjs.org'}}]:
            self.pack(change)
            with self.subTest(change=change), self.assertRaises(PublicationError):
                self.inspect()

    def test_rejects_unresolved_or_nonregistry_runtime_and_peer_dependencies(self):
        for field in ('dependencies', 'optionalDependencies', 'peerDependencies'):
            for requirement in ('workspace:^', 'catalog:react', 'file:../other', 'git+https://example.test/repo', ''):
                self.pack({field: {'@example/errors': requirement}})
                with self.subTest(field=field, requirement=requirement), self.assertRaises(PublicationError):
                    self.inspect()

    def test_rejects_traversal_links_and_duplicate_manifest(self):
        for name, link in [('../escaped', False), ('/absolute', False), ('package/../escaped', False), ('package/link', True), ('package/package.json', False)]:
            self.pack(extra=name, link=link)
            with self.subTest(name=name), self.assertRaises(PublicationError):
                self.inspect()

    def test_orders_authorized_dependencies_before_dependents(self):
        errors = Package('@example/errors', '1.0.0', '', '', {})
        sdk = Package('@example/sdk', '2.0.0', '', '', {'@example/errors': '^1.0.0', 'external': '^3.0.0'})
        self.assertEqual(dependency_order([sdk, errors]), [errors, sdk])
        self.assertEqual(dependency_order([sdk]), [sdk])

    def test_rejects_cycles_and_duplicate_candidates(self):
        first = Package('@example/first', '1.0.0', '', '', {'@example/second': '^1.0.0'})
        second = Package('@example/second', '1.0.0', '', '', {'@example/first': '^1.0.0'})
        for candidates in ([first, second], [first, first]):
            with self.assertRaises(PublicationError):
                dependency_order(candidates)

    def test_decompressed_limit_includes_pax_headers(self):
        with tarfile.open(self.path, 'w:gz', format=tarfile.PAX_FORMAT) as bundle:
            member = tarfile.TarInfo('package/package.json')
            member.pax_headers = {'comment': 'x' * 20000}
            bundle.addfile(member, io.BytesIO(b''))
        self.assertLess(self.path.stat().st_size, 1000)
        with self.assertRaises(PublicationError):
            with bounded_tar(self.path, limit=10000):
                self.fail('Oversized metadata must be rejected before parsing TAR')


if __name__ == '__main__':
    unittest.main()
