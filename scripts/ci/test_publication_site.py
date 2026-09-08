import hashlib
import io
from pathlib import Path
import tarfile
import tempfile
import unittest

from publication import PublicationError
from publication_site import prepare_site


class SiteTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.source = self.root / 'site.tar.gz'
        self.destination = self.root / 'artifact.tar'

    def write_site(self, entries):
        with tarfile.open(self.source, 'w:gz', format=tarfile.GNU_FORMAT) as archive:
            for name, body, kind in entries:
                member = tarfile.TarInfo(name)
                member.type = kind
                member.size = len(body)
                if kind in (tarfile.SYMTYPE, tarfile.LNKTYPE):
                    member.linkname = 'index.html'
                archive.addfile(member, io.BytesIO(body))

    def test_long_docfx_names_and_file_bytes_survive_without_metadata(self):
        long_name = 'api/' + 'VeryLongTypeName.' * 15 + 'html'
        self.write_site([('./index.html', b'<html>validated</html>', tarfile.REGTYPE),
                         ('./' + long_name, b'long file', tarfile.REGTYPE)])
        result = prepare_site(self.source, self.destination)
        self.assertEqual(result['sha256'], hashlib.sha256(self.destination.read_bytes()).hexdigest())
        with tarfile.open(self.destination) as archive:
            self.assertEqual(archive.extractfile('index.html').read(), b'<html>validated</html>')
            self.assertEqual(archive.extractfile(long_name).read(), b'long file')
            self.assertTrue(all(member.mode == 0o644 and member.uid == 0 for member in archive))

    def test_unsafe_names_links_duplicates_and_file_parents_are_rejected(self):
        cases = [('../escape', tarfile.REGTYPE), ('/absolute', tarfile.REGTYPE),
                 ('a\\b', tarfile.REGTYPE), ('a//b', tarfile.REGTYPE),
                 ('index.html', tarfile.REGTYPE), ('link', tarfile.SYMTYPE),
                 ('link', tarfile.LNKTYPE), ('device', tarfile.CHRTYPE),
                 ('index.html/child', tarfile.REGTYPE)]
        for name, kind in cases:
            with self.subTest(name=name, kind=kind):
                self.write_site([('./index.html', b'ok', tarfile.REGTYPE), (name, b'', kind)])
                with self.assertRaises(PublicationError):
                    prepare_site(self.source, self.destination)
                self.assertFalse(self.destination.exists())

    def test_missing_index_and_decompression_limit_fail_before_output(self):
        self.write_site([('other.html', b'other', tarfile.REGTYPE)])
        with self.assertRaises(PublicationError):
            prepare_site(self.source, self.destination)
        self.write_site([('index.html', b'x' * 10000, tarfile.REGTYPE)])
        with self.assertRaises(PublicationError):
            prepare_site(self.source, self.destination, limit=512)
        self.assertFalse(self.destination.exists())

    def test_existing_output_is_preserved(self):
        self.write_site([('index.html', b'ok', tarfile.REGTYPE)])
        self.destination.write_bytes(b'existing')
        with self.assertRaises(PublicationError):
            prepare_site(self.source, self.destination)
        self.assertEqual(self.destination.read_bytes(), b'existing')

    def test_duplicate_or_nonempty_root_directory_is_rejected(self):
        for roots in [[('./', b'', tarfile.DIRTYPE), ('.', b'', tarfile.DIRTYPE)],
                      [('./', b'invalid', tarfile.DIRTYPE)]]:
            with self.subTest(roots=roots):
                self.write_site(roots + [('index.html', b'ok', tarfile.REGTYPE)])
                with self.assertRaises(PublicationError):
                    prepare_site(self.source, self.destination)
                self.assertFalse(self.destination.exists())

    def test_oversized_longname_metadata_is_rejected(self):
        self.write_site([('index.html', b'ok', tarfile.REGTYPE),
                         ('x' * 5000, b'long name', tarfile.REGTYPE)])
        with self.assertRaises(PublicationError):
            prepare_site(self.source, self.destination)
        self.assertFalse(self.destination.exists())


if __name__ == '__main__':
    unittest.main()
