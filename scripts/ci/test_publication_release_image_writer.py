import copy
import hashlib
import io
import json
from pathlib import Path
import zipfile
from publication_artifacts import Artifact
from publication_prepare_images import ImagePreparation
from publication_verify_images import verify_images
from unittest.mock import patch
import unittest

from publication import PublicationError
from publication_release_image_writer import expected_images, finalize, publish_release_images
import test_publication_publish_images as fixtures


class ReleaseImageTests(unittest.TestCase):
    def setUp(self):
        self.fixture = fixtures.WriterTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        source_plan, source = self.fixture.fixture.fixture.plan('full')
        source_plan['controller_sha'] = 'b' * 40
        preparer = ImagePreparation(source_plan, self.fixture.catalog, self.fixture.root / 'release-prepared', self.fixture.root / 'release-reports', controller=self.fixture.root, database=self.fixture.root / '.ci-reports/security/trivy-database.json', runner=self.fixture.fixture.runner)
        source_plan['verified_images'] = verify_images(source, source_plan, self.fixture.catalog, preparer)
        self.fixture.plan = source_plan
        self.fixture.artifacts = {}
        for number, bundle in enumerate(('app', 'infra', 'docs'), 50):
            path = self.fixture.root / 'release-prepared' / bundle / 'images.tar'
            output = io.BytesIO()
            with zipfile.ZipFile(output, 'w') as archive:
                archive.writestr('images.tar', path.read_bytes())
                archive.writestr('images.tar.json', Path(str(path) + '.json').read_bytes())
            data = output.getvalue()
            self.fixture.artifacts[bundle] = Artifact(number, f'release-images-5-{bundle}-30-1', 'sha256:' + hashlib.sha256(data).hexdigest(), len(data))
            self.fixture.archives[number] = data
        self.registries = {}
        def registry(repository, username, token):
            self.fixture.manifests = self.registries.setdefault(repository, {})
            return self.fixture
        self.fixture.registry = registry
        self.plan = {'source': self.fixture.plan, 'authority': {'release': {'id': 5, 'version': '1.2.3'}, 'origin': {'asset_id': 10}, 'selection': {'asset_id': 11}}}

    def publish(self):
        progress = {'images': []}
        publish_release_images(self.fixture, self.plan, self.fixture.preparation, self.fixture.artifacts, self.fixture.catalog,
                               'example', 'fixture', progress, lambda: None, self.fixture.transport, self.fixture.registry)
        return progress

    def test_real_two_platform_release_bytes_and_retry_never_move_aliases(self):
        result = self.publish()
        self.assertEqual(result['images'], expected_images(self.plan, self.fixture.catalog, self.fixture.repository))
        self.assertEqual(self.fixture.manifests['1.2.3'], self.fixture.manifests['sha-' + 'a' * 40])
        self.assertNotIn('nightly', self.fixture.manifests)
        self.assertNotIn('latest', self.fixture.manifests)
        before = copy.deepcopy(self.fixture.manifests)
        self.fixture.events.clear()
        self.publish()
        self.assertEqual(self.fixture.manifests, before)
        self.assertNotIn('upload-child', self.fixture.events)
        self.assertEqual(self.fixture.events.count('readback-child'), 6)

    def test_existing_different_version_bytes_fail_without_overwrite(self):
        self.registries['ghcr.io/example/repo-app'] = {'1.2.3': {'digest': 'sha256:' + 'f' * 64, 'bytes': b'wrong'}}
        with self.assertRaises(PublicationError):
            self.publish()
        self.assertEqual(self.fixture.manifests['1.2.3']['bytes'], b'wrong')
        self.assertNotIn('nightly', self.fixture.events)

    def test_changed_archive_fails_before_registry_credentials(self):
        self.fixture.archives[50] += b'tampered'
        with self.assertRaises(PublicationError):
            self.publish()
        self.assertEqual(self.fixture.credentials, [])

    def test_partial_receipt_adoption_preserves_original_successful_writer(self):
        images = expected_images(self.plan, self.fixture.catalog, self.fixture.repository)
        identity = self.plan['authority']
        old_reference = {'frame': {'job_id': 10}, 'artifact': {'id': 11}, 'sha256': 'sha256:' + '1' * 64}
        new_reference = {'frame': {'job_id': 20}, 'artifact': {'id': 21}, 'sha256': 'sha256:' + '2' * 64}
        payload = identity | {'images': images, 'writer': old_reference}
        previous = {'record': {'payload': payload}}
        document = {'authority': identity, 'images': images}
        retained = []
        with patch('publication_release_image_writer.frame', return_value=({'job_id': 20}, {})), \
             patch('publication_release_image_writer.writer_progress', side_effect=[(document, new_reference), (document, old_reference)]) as evidence, \
             patch('publication_release_image_writer.authorize_prepared', return_value=(self.plan, None, None, None)), \
             patch('publication_release_image_writer.inspect_receipt', return_value=previous), \
             patch('publication_release_image_writer.verify_frame'), patch('publication_release_image_writer.endorsed', return_value=False), \
             patch('publication_release_image_writer.retain', side_effect=lambda client, release_id, name, value, current: retained.append(value) or {'asset_id': 99, 'sha256': 'sha256:' + '3' * 64}):
            result = finalize(self.fixture, {}, 1, 1, 30, 1, 5, self.fixture.catalog, self.fixture.root)
        self.assertEqual(evidence.call_count, 2)
        self.assertEqual(retained, [payload])
        self.assertEqual(result['asset_id'], 99)

    def test_incomplete_writer_cannot_create_durable_receipt(self):
        with patch('publication_release_image_writer.frame', return_value=({}, {})), \
             patch('publication_release_image_writer.writer_progress', return_value=({'authority': self.plan['authority'], 'images': []}, {})), \
             patch('publication_release_image_writer.authorize_prepared', return_value=(self.plan, None, None, None)), \
             patch('publication_release_image_writer.retain') as retain:
            with self.assertRaises(PublicationError):
                finalize(self.fixture, {}, 1, 1, 30, 1, 5, self.fixture.catalog, self.fixture.root)
        retain.assert_not_called()


if __name__ == '__main__':
    unittest.main()
