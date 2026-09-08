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
from publication_verify_site import verify_site


class VerifySiteTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.destination = self.root / 'prepared'
        self.producer = Producer('example/repo', 1, 'a' * 40, 20, 2, 10,
                                 'example/repo/.github/workflows/ci.yml@refs/heads/main')
        self.downloads = []

    def fixture(self, bad_seal=False, unsafe=False):
        packed = io.BytesIO()
        with tarfile.open(fileobj=packed, mode='w:gz', format=tarfile.GNU_FORMAT) as archive:
            member = tarfile.TarInfo('../escape' if unsafe else './index.html')
            member.size = 9
            archive.addfile(member, io.BytesIO(b'validated'))
        body = packed.getvalue()
        seal = {'schema': 1, 'repository': self.producer.repository,
                'sha': 'b' * 40 if bad_seal else self.producer.source_sha,
                'run_id': '20', 'run_attempt': '2', 'workflow_ref': self.producer.workflow_ref,
                'kind': 'docs', 'variant': 'docfx', 'file': 'site.tar.gz',
                'sha256': hashlib.sha256(body).hexdigest()}
        packed = io.BytesIO()
        with zipfile.ZipFile(packed, 'w') as archive:
            archive.writestr('site.tar.gz', body)
            archive.writestr('site.tar.gz.json', json.dumps(seal))
        self.archive = packed.getvalue()
        artifact = {'id': 30, 'name': 'docfx-site-20-2', 'size': len(self.archive),
                    'digest': 'sha256:' + hashlib.sha256(self.archive).hexdigest(),
                    'payload': 'site.tar.gz', 'kind': 'docs', 'variant': 'docfx'}
        return {'producer': asdict(self.producer), 'artifacts': [artifact]}

    def download(self, artifact, destination):
        self.downloads.append(destination)
        destination.write_bytes(self.archive)
        return destination

    def test_verified_source_identity_and_exact_bytes_are_bound_to_preparation(self):
        plan = self.fixture()
        result = verify_site(self, plan, self.destination)
        self.assertEqual(result['source_artifact'], plan['artifacts'][0])
        self.assertEqual(result['files'], [{'path': 'index.html', 'size': 9,
                                          'sha256': hashlib.sha256(b'validated').hexdigest()}])
        with tarfile.open(self.destination / 'artifact.tar') as archive:
            self.assertEqual(archive.extractfile('./index.html').read(), b'validated')
        self.assertTrue(all(not path.parent.exists() for path in self.downloads))

    def test_wrong_seal_or_unsafe_site_leaves_no_prepared_archive(self):
        for options in ({'bad_seal': True}, {'unsafe': True}):
            with self.subTest(options=options), self.assertRaises(PublicationError):
                verify_site(self, self.fixture(**options), self.destination)
            self.assertFalse(self.destination.exists())

    def test_missing_or_duplicate_site_fails_before_download(self):
        plan = self.fixture()
        for artifacts in ([], plan['artifacts'] * 2):
            with self.assertRaises(PublicationError):
                verify_site(self, plan | {'artifacts': artifacts}, self.destination)
        self.assertEqual(self.downloads, [])
