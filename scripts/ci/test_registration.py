import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from publication import PublicationError
from registration import source_inputs


class RegistrationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'packages/sdk').mkdir(parents=True)
        self.catalog = {'components': [{'id': 'platform', 'path': '.'}, {'id': 'sdk', 'path': 'packages/sdk', 'package': {'name': '@example/sdk'}}]}
        (self.root / '.release-please-manifest.json').write_text(json.dumps({'.': '1.0.0', 'packages/sdk': '2.0.0'}))
        (self.root / 'packages/sdk/package.json').write_text(json.dumps({'version': '2.0.0'}))
        (self.root / 'pnpm-lock.yaml').write_text('exact lockfile bytes')

    def test_records_versions_and_exact_input_fingerprints(self):
        result = source_inputs(self.root, self.catalog, ['pnpm-lock.yaml'])
        self.assertEqual(result['component_versions']['packages/sdk'], '2.0.0')
        self.assertEqual(result['input_sha256'], {'pnpm-lock.yaml': hashlib.sha256(b'exact lockfile bytes').hexdigest()})
        self.assertEqual(result['catalog'], self.catalog)

    def test_rejects_version_drift(self):
        (self.root / 'packages/sdk/package.json').write_text(json.dumps({'version': '2.0.1'}))
        with self.assertRaises(PublicationError):
            source_inputs(self.root, self.catalog, ['pnpm-lock.yaml'])

    def test_rejects_missing_unsafe_or_symlinked_inputs(self):
        (self.root / 'link').symlink_to(self.root / 'pnpm-lock.yaml')
        for paths in ([], ['missing'], ['../outside'], ['/absolute'], ['link']):
            with self.subTest(paths=paths), self.assertRaises(PublicationError):
                source_inputs(self.root, self.catalog, paths)


if __name__ == '__main__':
    unittest.main()
