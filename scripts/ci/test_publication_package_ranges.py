import unittest

from publication import PublicationError
from publication_package_ranges import satisfying_versions


class RangeTests(unittest.TestCase):
    def test_actual_npm_semver_caret_prerelease_and_union_behavior(self):
        versions = ['1.0.0', '1.9.0', '2.0.0', '1.10.0-beta.1', '0.2.5', '0.3.0']
        self.assertEqual(satisfying_versions(versions, '^1.0.0'), ['1.9.0', '1.0.0'])
        self.assertEqual(satisfying_versions(versions, '^0.2.0'), ['0.2.5'])
        self.assertEqual(satisfying_versions(versions, '^1.10.0-beta.0'), ['1.10.0-beta.1'])
        self.assertEqual(satisfying_versions(versions, '1.0.0 || >=2.0.0'), ['2.0.0', '1.0.0'])

    def test_missing_match_and_malformed_range_are_distinct(self):
        self.assertEqual(satisfying_versions(['1.0.0'], '^2.0.0'), [])
        with self.assertRaises(PublicationError):
            satisfying_versions(['1.0.0'], 'not-a-range')

    def test_duplicate_or_unbounded_input_fails_without_runner(self):
        def forbidden(*args, **kwargs):
            self.fail('invalid range input reached the tool')
        for versions, requirement in [(['1.0.0'] * 2, '*'), (['1.0.0'], 'x' * 1001)]:
            with self.assertRaises(PublicationError):
                satisfying_versions(versions, requirement, runner=forbidden)


if __name__ == '__main__':
    unittest.main()
