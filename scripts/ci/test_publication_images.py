import hashlib
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest

from publication import PublicationError
from publication_images import inspect_images


class ImageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / 'images.tar.gz'
        self.layer = b'verified uncompressed layer'
        self.layer_hash = hashlib.sha256(self.layer).hexdigest()
        self.layer_path = 'blobs/sha256/' + self.layer_hash
        self.expected = {'example:test': 'linux/amd64'}

    def pack(self, architecture='amd64', tag='example:test', layer=None, reference=None, extra=None, config_name=None):
        config = json.dumps({'os': 'linux', 'architecture': architecture, 'rootfs': {'type': 'layers', 'diff_ids': ['sha256:' + self.layer_hash]}}).encode()
        config_path = config_name or 'blobs/sha256/' + hashlib.sha256(config).hexdigest()
        manifest = [{'Config': config_path, 'RepoTags': [tag], 'Layers': [self.layer_path if reference is None else reference]}]
        entries = [('manifest.json', json.dumps(manifest).encode()), (config_path, config), (self.layer_path, self.layer if layer is None else layer)]
        if extra:
            entries.append(extra)
        with tarfile.open(self.path, 'w:gz') as archive:
            for name, body in entries:
                member = tarfile.TarInfo(name)
                member.size = len(body)
                archive.addfile(member, io.BytesIO(body))

    def test_verifies_config_platform_and_uncompressed_layer(self):
        self.pack()
        result = inspect_images(self.path, self.expected)['example:test']
        self.assertEqual(result['platform'], 'linux/amd64')
        self.assertEqual(result['diff_ids'], ['sha256:' + self.layer_hash])
        self.assertEqual(result['layers'][0]['size'], len(self.layer))

    def test_rejects_wrong_platform_tag_tampered_or_missing_layer(self):
        for changes in [{'architecture': 'arm64'}, {'tag': 'unexpected:test'}, {'layer': b'tampered'}, {'reference': 'missing'}, {'reference': []}]:
            with self.subTest(changes=changes):
                self.pack(**changes)
                with self.assertRaises(PublicationError):
                    inspect_images(self.path, self.expected)

    def test_rejects_unsafe_duplicate_and_oversized_archives(self):
        for name in ['../escape', '/absolute', 'manifest.json']:
            with self.subTest(name=name):
                self.pack(extra=(name, b'bad'))
                with self.assertRaises(PublicationError):
                    inspect_images(self.path, self.expected)
        self.pack()
        with self.assertRaises(PublicationError):
            inspect_images(self.path, self.expected, max_bytes=100)

    def test_rejects_missing_required_variant(self):
        self.pack()
        with self.assertRaises(PublicationError):
            inspect_images(self.path, self.expected | {'example:test-arm64': 'linux/arm64'})

    def test_rejects_missing_or_mismatched_config_content_address(self):
        for name in ['config.json', 'blobs/sha256/not-a-digest', 'blobs/sha256/' + '0' * 64]:
            self.pack(config_name=name)
            with self.subTest(name=name), self.assertRaises(PublicationError):
                inspect_images(self.path, self.expected)

    def test_rejects_extended_headers_before_parsing_payloads(self):
        for kind in [tarfile.XHDTYPE, tarfile.XGLTYPE, tarfile.GNUTYPE_LONGNAME, tarfile.GNUTYPE_LONGLINK]:
            with tarfile.open(self.path, 'w:gz') as archive:
                member = tarfile.TarInfo('extension')
                member.type = kind
                member.size = 1024
                archive.addfile(member, io.BytesIO(b'x' * 1024))
            with self.subTest(kind=kind), self.assertRaisesRegex(PublicationError, 'plain regular-file'):
                inspect_images(self.path, self.expected)

    def test_rejects_directory_size_that_can_hide_extended_headers(self):
        with tarfile.open(self.path, 'w:gz') as archive:
            member = tarfile.TarInfo('directory')
            member.type = tarfile.DIRTYPE
            member.size = 512
            archive.addfile(member, io.BytesIO(bytes(512)))
        with self.assertRaisesRegex(PublicationError, 'zero size'):
            inspect_images(self.path, self.expected)
