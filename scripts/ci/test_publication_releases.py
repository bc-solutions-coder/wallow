import unittest
from unittest.mock import Mock

from publication import PublicationError
from publication_releases import current_aliases, selected_releases


class ReleaseTests(unittest.TestCase):
    def test_explicit_release_must_match_validated_commit(self):
        client = Mock()
        client.get.side_effect = [[{'id': 1, 'tag_name': 'sdk-v1.0.0', 'draft': False}], {'object': {'type': 'commit', 'sha': 'a' * 40}}]
        with self.assertRaises(PublicationError):
            selected_releases(client, {'components': [{'id': 'sdk', 'tag_prefix': 'sdk-v'}]}, 'b' * 40, 1)

    def test_newer_release_prevents_older_alias_rollback(self):
        old = {'component': 'sdk', 'version': '1.1.0', 'prerelease': False}
        newer = old | {'version': '2.0.0'}
        self.assertEqual(current_aliases(old, [old, newer], 'package'), ['major-1', 'minor-1.1'])
        self.assertEqual(current_aliases(old, [old, old | {'version': '1.1.1'}], 'image'), [])

    def test_prerelease_has_no_stable_aliases(self):
        release = {'component': 'sdk', 'version': '2.0.0-beta.1', 'prerelease': True}
        self.assertEqual(current_aliases(release, [release], 'package'), [])
