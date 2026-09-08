import copy
import unittest

from publication import PublicationError
from publication_aliases import aliases, alias_action


class AliasTests(unittest.TestCase):
    def record(self, version='1.2.3', source='a', ident=1):
        return {'release': {'id': ident, 'component': 'sdk', 'version': version, 'commit_sha': source * 40, 'prerelease': False}, 'receipt': ident}

    def compare(self, old, new, ahead=True):
        return {'status': 'ahead' if ahead else 'behind', 'base_commit': {'sha': old['release']['commit_sha']}, 'merge_base_commit': {'sha': old['release']['commit_sha'] if ahead else new['release']['commit_sha']}}

    def test_stable_names_and_prerelease_exclusion(self):
        self.assertEqual(aliases(self.record()['release'], 'image'), ['latest', '1', '1.2'])
        self.assertEqual(aliases(self.record()['release'], 'package'), ['latest', 'major-1', 'minor-1.2'])
        self.assertEqual(aliases(self.record('1.2.3-rc.1')['release'], 'package'), [])
        self.assertEqual(aliases(self.record()['release'] | {'prerelease': True}, 'image'), [])
        with self.assertRaises(PublicationError): aliases(self.record('1.2.3+build')['release'], 'image')

    def test_missing_identical_newer_and_older(self):
        old, new = self.record(), self.record('1.3.0', 'b', 2)
        self.assertEqual(alias_action('latest', 'image', None, new), 'advance')
        self.assertEqual(alias_action('latest', 'image', old, old), 'identical')
        self.assertEqual(alias_action('latest', 'image', old, new, self.compare(old, new)), 'advance')
        self.assertEqual(alias_action('latest', 'image', new, old, self.compare(new, old, False)), 'skip-older')

    def test_wrong_scope_unknown_history_and_same_version_conflict_fail(self):
        old, new = self.record(), self.record('2.0.0', 'b', 2)
        for alias, previous, candidate, comparison in [('1', old, new, self.compare(old, new)), ('latest', old, new, {}),
                ('latest', old, self.record('1.2.3', 'b', 2), None), ('latest', old, new, self.compare(old, new, False))]:
            with self.subTest(alias=alias), self.assertRaises(PublicationError):
                alias_action(alias, 'image', previous, candidate, comparison)


if __name__ == '__main__': unittest.main()
