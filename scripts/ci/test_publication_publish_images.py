import hashlib
import io
import json
from pathlib import Path
import tarfile
import unittest
import zipfile

from publication import Producer, PublicationError
from publication_artifacts import Artifact
from publication_image_provenance import OCI_INDEX, main_index
from publication_prepare_images import ImagePreparation
from publication_publish_images import publish_images
from publication_verify_images import verify_images
import test_publication_prepare_images as fixtures


class WriterTests(unittest.TestCase):
    def setUp(self):
        self.fixture = fixtures.PreparationTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.root = self.fixture.root
        for image in self.fixture.fixture.catalog['images']:
            image['repository_suffix'] = '-' + image['id']
        self.catalog = self.fixture.fixture.catalog
        self.plan, source = self.fixture.fixture.plan('docs')
        self.plan['controller_sha'] = 'b' * 40
        prepare = ImagePreparation(self.plan, self.catalog, self.root / 'prepared', self.root / 'reports', controller=self.root, runner=self.fixture.runner)
        self.plan['verified_images'] = verify_images(source, self.plan, self.catalog, prepare)
        self.preparation = Producer('example/repo', 1, 'b' * 40, 30, 1, 12, 'example/repo/.github/workflows/publish.yml@refs/heads/main')
        self.repository = 'example/repo'
        self.archives, self.downloads, self.events, self.credentials = {}, [], [], []
        self.manifests, self.blobs = {}, {}
        self.comparison = None
        self.fail_copy = False
        self.make_artifact()

    def make_artifact(self):
        payload = self.root / 'prepared/docs/images.tar'
        data = payload.read_bytes()
        seal = json.loads(Path(str(payload) + '.json').read_text())
        seal['sha256'] = hashlib.sha256(data).hexdigest()
        archive = io.BytesIO()
        with zipfile.ZipFile(archive, 'w') as content:
            content.writestr('images.tar', data)
            content.writestr('images.tar.json', json.dumps(seal))
        data = archive.getvalue()
        self.artifacts = {'docs': Artifact(50, 'prepared-images-docs-30-1', 'sha256:' + hashlib.sha256(data).hexdigest(), len(data))}
        self.archives[50] = data

    def download(self, artifact, destination):
        destination.write_bytes(self.archives[artifact.id])
        self.downloads.append(destination)
        return destination

    def get(self, path):
        if self.comparison:
            return self.comparison
        base = path.split('/compare/')[1].split('...')[0]
        return {'status': 'identical', 'base_commit': {'sha': base}, 'merge_base_commit': {'sha': base}}

    def main_comparison(self, sha):
        return {'status': 'ahead', 'base_commit': {'sha': sha}, 'merge_base_commit': {'sha': sha}}

    def transport(self, username, credential, authfile):
        self.events.append('credentials')
        authfile.write_text('disposable credential file')
        self.credentials.append(authfile)
        return self

    def registry(self, repository, username, credential):
        self.assertEqual(repository, 'ghcr.io/example/repo-docs')
        return self

    def copy(self, repository, digest, directory, upload):
        self.events.append('upload-child' if upload else 'readback-child')
        if self.fail_copy:
            raise PublicationError('Copy failed')
        if upload:
            files = {path.name: path.read_bytes() for path in directory.iterdir()}
            self.blobs[digest] = files
            self.manifests[digest] = {'digest': digest, 'media_type': 'application/vnd.docker.distribution.manifest.v2+json', 'bytes': files['manifest.json']}
        else:
            for name, data in self.blobs[digest].items():
                (directory / name).write_bytes(data)

    def read_manifest(self, reference):
        return self.manifests.get(reference)

    def write_manifest(self, reference, data, media, digest):
        expected = {'digest': digest, 'media_type': media, 'bytes': data}
        if reference in self.manifests and self.manifests[reference] != expected:
            raise PublicationError('Immutable conflict')
        self.manifests[reference] = self.manifests[digest] = expected
        self.events.append('immutable')
        return digest

    def replace_nightly(self, data, digest, previous):
        self.assertEqual(self.manifests.get('nightly'), previous)
        self.manifests['nightly'] = {'digest': digest, 'media_type': OCI_INDEX, 'bytes': data}
        self.events.append('nightly')

    def publish(self):
        return publish_images(self, self.plan, self.preparation, self.artifacts, self.catalog, 'example', 'job-credential', self.root / 'progress.json', self.transport, self.registry)

    def test_exact_artifacts_to_registry_and_idempotent_retry_preserve_both_configurations(self):
        first = self.publish()
        self.assertEqual(first['images'][0]['nightly'], 'verified')
        self.assertEqual(self.events, ['credentials', 'upload-child', 'readback-child', 'upload-child', 'readback-child', 'immutable', 'nightly'])
        self.assertEqual(self.manifests['nightly'], self.manifests['sha-' + 'a' * 40])
        before = dict(self.manifests)
        self.events.clear()
        self.publish()
        self.assertNotIn('upload-child', self.events)
        self.assertEqual(self.manifests, before)
        self.assertTrue(all(not path.parent.exists() for path in self.downloads + self.credentials))

    def test_older_retry_completes_missing_immutable_without_rolling_nightly_back(self):
        inventory = self.plan['verified_images']['docs']['prepared']['inventory']
        variants = {item['platform']: item['prepared'] for item in inventory['images']}
        data = main_index(self.repository, 'f' * 40, 'docs', variants)
        digest = 'sha256:' + hashlib.sha256(data).hexdigest()
        previous = {'digest': digest, 'media_type': OCI_INDEX, 'bytes': data}
        self.manifests['nightly'] = self.manifests['sha-' + 'f' * 40] = previous
        self.comparison = {'status': 'behind', 'base_commit': {'sha': 'f' * 40}, 'merge_base_commit': {'sha': 'a' * 40}}
        result = self.publish()
        self.assertEqual(result['images'][0]['nightly'], 'skip-older')
        self.assertIn('sha-' + 'a' * 40, self.manifests)
        self.assertEqual(self.manifests['nightly'], previous)
        self.assertNotIn('nightly', self.events)

    def test_unknown_alias_blocks_after_immutable_without_overwriting_alias(self):
        previous = {'digest': 'sha256:' + 'f' * 64, 'media_type': OCI_INDEX, 'bytes': b'{}'}
        self.manifests['nightly'] = previous
        with self.assertRaises(PublicationError):
            self.publish()
        self.assertIn('sha-' + 'a' * 40, self.manifests)
        self.assertEqual(self.manifests['nightly'], previous)

    def test_unsafe_extra_or_changed_prepared_files_fail_before_registry_credentials(self):
        payload = self.root / 'prepared/docs/images.tar'
        original = payload.read_bytes()
        for change in ('extra', 'symlink', 'config', 'inventory'):
            with tarfile.open(fileobj=io.BytesIO(original)) as archive:
                files = {item.name: archive.extractfile(item).read() for item in archive}
            if change == 'config':
                item = self.plan['verified_images']['docs']['prepared']['inventory']['images'][0]
                files[item['directory'] + item['prepared']['config_digest'][7:]] = b'{}'
            if change == 'inventory':
                files['inventory.json'] = b'{}'
            if change == 'extra':
                files['unknown'] = b'extra'
            with tarfile.open(payload, 'w', format=tarfile.USTAR_FORMAT) as archive:
                for name, data in files.items():
                    member = tarfile.TarInfo(name)
                    member.size = len(data)
                    archive.addfile(member, io.BytesIO(data))
                if change == 'symlink':
                    member = tarfile.TarInfo('linked')
                    member.type, member.linkname = tarfile.SYMTYPE, '/tmp/outside'
                    archive.addfile(member)
            self.plan['verified_images']['docs']['prepared']['size'] = payload.stat().st_size
            self.plan['verified_images']['docs']['prepared']['digest'] = 'sha256:' + hashlib.sha256(payload.read_bytes()).hexdigest()
            self.make_artifact()
            with self.subTest(change=change), self.assertRaises(PublicationError):
                self.publish()
            self.assertEqual(self.events, [])
            self.assertTrue(all(not path.parent.exists() for path in self.downloads))

    def test_registry_failure_retains_partial_progress_and_cleans_credentials(self):
        self.fail_copy = True
        with self.assertRaises(PublicationError):
            self.publish()
        progress = json.loads((self.root / 'progress.json').read_text())
        self.assertEqual(progress['images'][0]['immutable'], 'pending')
        self.assertFalse(progress['release_authorized'])
        self.assertNotIn('nightly', self.manifests)
        self.assertTrue(all(not path.parent.exists() for path in self.downloads + self.credentials))
