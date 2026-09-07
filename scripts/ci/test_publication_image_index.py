import json
import unittest

from publication import PublicationError
from publication_image_index import image_index


class ImageIndexTests(unittest.TestCase):
    def setUp(self):
        self.variants = {platform: {'platform': platform, 'manifest_digest': 'sha256:' + digit * 64, 'manifest_size': 1234} for platform, digit in [('linux/amd64', 'a'), ('linux/arm64', 'b')]}

    def test_emits_stable_exact_manifest_list(self):
        result = image_index(self.variants)
        self.assertEqual(result, image_index(dict(reversed(list(self.variants.items())))))
        manifests = json.loads(result)['manifests']
        self.assertEqual([item['platform']['architecture'] for item in manifests], ['amd64', 'arm64'])
        self.assertEqual([item['digest'] for item in manifests], [item['manifest_digest'] for item in self.variants.values()])

    def test_rejects_missing_extra_or_mislabeled_platform(self):
        for variants in [{'linux/amd64': self.variants['linux/amd64']}, self.variants | {'linux/s390x': {}}, self.variants | {'linux/arm64': self.variants['linux/amd64']}]:
            with self.subTest(variants=variants), self.assertRaises(PublicationError):
                image_index(variants)

    def test_rejects_malformed_identity_and_same_manifest_for_both_platforms(self):
        for change in [{'manifest_size': True}, {'manifest_size': 0}, {'manifest_digest': 'bad'}, {'manifest_digest': self.variants['linux/amd64']['manifest_digest']}]:
            with self.subTest(change=change), self.assertRaises(PublicationError):
                image_index(self.variants | {'linux/arm64': self.variants['linux/arm64'] | change})
