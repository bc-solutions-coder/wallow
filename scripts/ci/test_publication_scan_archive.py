import gzip
import hashlib
import json
from pathlib import Path
import tarfile
import tempfile
import unittest

from publication import PublicationError
from publication_images import inspect_images
from publication_scan_archive import write_scan_archive


class ScanArchiveTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.prepared = self.root / 'prepared'
        self.prepared.mkdir()
        self.body = b'validated layer payload'
        self.diff = 'sha256:' + hashlib.sha256(self.body).hexdigest()
        config = json.dumps({'os': 'linux', 'architecture': 'arm64', 'rootfs': {'type': 'layers', 'diff_ids': [self.diff, self.diff]}}).encode()
        self.config = self.blob(config, 'application/vnd.docker.container.image.v1+json')
        self.layer = self.blob(gzip.compress(self.body, mtime=0), 'application/vnd.docker.image.rootfs.diff.tar.gzip')
        (self.prepared / 'manifest.json').write_text(json.dumps({'schemaVersion': 2, 'mediaType': 'application/vnd.docker.distribution.manifest.v2+json', 'config': self.config, 'layers': [self.layer, self.layer]}))
        self.expected = {'platform': 'linux/arm64', 'config_digest': self.config['digest'], 'layers': [{'digest': self.diff, 'size': len(self.body)}] * 2}
        self.output = self.root / 'scan.tar.gz'

    def blob(self, body, media_type):
        digest = hashlib.sha256(body).hexdigest()
        (self.prepared / digest).write_bytes(body)
        return {'digest': 'sha256:' + digest, 'size': len(body), 'mediaType': media_type}

    def test_exact_config_and_repeated_layer_sequence_survive_regular_archive(self):
        write_scan_archive(self.prepared, self.expected, 'wallow:test', self.output)
        actual = inspect_images(self.output, {'wallow:test': 'linux/arm64'})['wallow:test']
        self.assertEqual(actual['config_digest'], self.config['digest'])
        self.assertEqual(actual['diff_ids'], [self.diff, self.diff])
        with tarfile.open(self.output, 'r:gz') as archive:
            members = archive.getmembers()
            self.assertTrue(all(member.isfile() for member in members))
            self.assertEqual(len(members), 3)

    def test_tampered_prepared_bytes_never_produce_an_archive(self):
        (self.prepared / self.layer['digest'][7:]).write_bytes(b'tampered')
        with self.assertRaises(PublicationError):
            write_scan_archive(self.prepared, self.expected, 'wallow:test', self.output)
        self.assertFalse(self.output.exists())

    def test_reinspection_failure_cleans_only_new_output(self):
        with self.assertRaises(PublicationError):
            write_scan_archive(self.prepared, self.expected | {'platform': 'linux/amd64'}, 'wallow:test', self.output)
        self.assertFalse(self.output.exists())
        self.output.write_bytes(b'keep existing')
        with self.assertRaises(FileExistsError):
            write_scan_archive(self.prepared, self.expected, 'wallow:test', self.output)
        self.assertEqual(self.output.read_bytes(), b'keep existing')
