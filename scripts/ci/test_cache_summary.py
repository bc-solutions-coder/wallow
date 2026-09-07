import unittest

from cache_summary import cache_counts


class CacheSummaryTests(unittest.TestCase):
    def test_distinguishes_remote_local_and_missed_tasks_without_metadata(self):
        summary = {'environmentVariables': {'secret': 'private'}, 'tasks': [
            {'taskId': 'private-task', 'cache': {'status': 'HIT', 'source': 'REMOTE'}},
            {'cache': {'status': 'HIT', 'source': 'LOCAL'}},
            {'cache': {'status': 'MISS'}},
        ]}
        self.assertEqual(cache_counts(summary), {'remote_hits': 1, 'local_hits': 1, 'misses': 1})

    def test_does_not_infer_remote_hits_from_ambiguous_results(self):
        for summary in [{}, {'tasks': []}, {'tasks': [{}]}, {'tasks': [{'cache': {'status': 'HIT'}}]}, {'tasks': [{'cache': {'status': 'UNKNOWN'}}]}]:
            with self.subTest(summary=summary), self.assertRaises(ValueError):
                cache_counts(summary)
