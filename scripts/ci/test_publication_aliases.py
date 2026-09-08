import unittest

from publication import PublicationError
from publication_aliases import aliases


class AliasTests(unittest.TestCase):
    def record(self, version='1.2.3', source='a', ident=1):
        return {'release': {'id': ident, 'component': 'sdk', 'version': version, 'commit_sha': source * 40, 'prerelease': False}, 'receipt': ident}

    def test_stable_names_and_prerelease_exclusion(self):
        self.assertEqual(aliases(self.record()['release'], 'image'), ['latest', '1', '1.2'])
        self.assertEqual(aliases(self.record()['release'], 'package'), ['latest', 'major-1', 'minor-1.2'])
        self.assertEqual(aliases(self.record('1.2.3-rc.1')['release'], 'package'), [])
        self.assertEqual(aliases(self.record()['release'] | {'prerelease': True}, 'image'), [])
        with self.assertRaises(PublicationError): aliases(self.record('1.2.3+build')['release'], 'image')
