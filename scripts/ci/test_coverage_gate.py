from pathlib import Path
import tempfile
import unittest
from coverage_gate import main


class CoverageGateTests(unittest.TestCase):
    def check(self, document):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'coverage.xml'
            path.write_text(document)
            return main(path)

    def test_aggregate_rate_with_many_nested_rates(self):
        self.assertEqual(self.check('<coverage line-rate="0.90">' + '<class line-rate="0"/>' * 10000 + '</coverage>'), 0)

    def test_below_threshold_fails_without_rounding_up(self):
        self.assertEqual(self.check('<coverage line-rate="0.89999"/>'), 1)

    def test_invalid_and_missing_reports_fail(self):
        for document in ['', '<coverage/>', '<coverage line-rate="NaN"/>', '<coverage line-rate="1.1"/>', '<other line-rate="1"/>']:
            with self.subTest(document=document):
                self.assertEqual(self.check(document), 1)
        with tempfile.TemporaryDirectory() as directory:
            self.assertEqual(main(Path(directory) / 'missing.xml'), 1)


if __name__ == '__main__':
    unittest.main()
