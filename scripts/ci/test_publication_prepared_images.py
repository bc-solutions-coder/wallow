import gzip
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from publication import PublicationError
from publication_prepared_images import inspect_prepared_image


class PreparedImageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.body = b'validated layer bytes'
        self.config = self.blob(b'{"os":"linux","architecture":"amd64"}', 'application/vnd.docker.container.image.v1+json')
        self.layer = self.blob(gzip.compress(self.body, mtime=0), 'application/vnd.docker.image.rootfs.diff.tar.gzip')
        self.expected = {'platform': 'linux/amd64', 'config_digest': self.config['digest'], 'layers': [{'size': len(self.body), 'digest': 'sha256:' + hashlib.sha256(self.body).hexdigest()}]}
        self.manifest = {'schemaVersion': 2, 'mediaType': 'application/vnd.docker.distribution.manifest.v2+json', 'config': self.config, 'layers': [self.layer]}

    def blob(self, body, media_type):
        digest = hashlib.sha256(body).hexdigest()
        (self.root / digest).write_bytes(body)
        return {'mediaType': media_type, 'digest': 'sha256:' + digest, 'size': len(body)}

    def inspect(self):
        (self.root / 'manifest.json').write_text(json.dumps(self.manifest))
        return inspect_prepared_image(self.root, self.expected)

    def test_preserves_original_config_and_layers_while_verifying_compression(self):
        result = self.inspect()
        self.assertEqual(result['config_digest'], self.expected['config_digest'])
        self.assertEqual(result['layers'], [self.layer])
        self.assertEqual(result['manifest_digest'], 'sha256:' + hashlib.sha256((self.root / 'manifest.json').read_bytes()).hexdigest())

    def test_rejects_changed_config_even_when_its_own_digest_is_correct(self):
        self.manifest['config'] = self.blob(b'{}', self.config['mediaType'])
        with self.assertRaises(PublicationError):
            self.inspect()

    def test_rejects_changed_uncompressed_layer_or_expansion(self):
        for body in [b'changed', self.body * 10000]:
            self.manifest['layers'] = [self.blob(gzip.compress(body), self.layer['mediaType'])]
            with self.subTest(size=len(body)), self.assertRaises(PublicationError):
                self.inspect()

    def test_rejects_tampered_missing_and_symlinked_blobs(self):
        path = self.root / self.layer['digest'][7:]
        path.write_bytes(b'x' * self.layer['size'])
        with self.assertRaises(PublicationError):
            self.inspect()
        path.unlink()
        with self.assertRaises(PublicationError):
            self.inspect()
        path.symlink_to(self.root / self.config['digest'][7:])
        with self.assertRaises(PublicationError):
            self.inspect()

    def test_rejects_missing_layer_and_unsafe_descriptor(self):
        for layers in [[], [{'digest': '../outside', 'mediaType': self.layer['mediaType'], 'size': 1}]]:
            self.manifest['layers'] = layers
            with self.subTest(layers=layers), self.assertRaises(PublicationError):
                self.inspect()
