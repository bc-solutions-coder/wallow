import copy
from dataclasses import asdict
import hashlib
import io
import json
from pathlib import Path
import tarfile
import unittest
import zipfile

from publication import Producer, PublicationError
from publication_verify_images import verify_images


class Transport:
    def __init__(self, archives):
        self.archives = archives
        self.downloads = []

    def download(self, artifact, destination):
        if any(path.parent.exists() for _, path in self.downloads):
            raise AssertionError('Previous bundle was not cleaned before the next download')
        self.downloads.append((artifact.id, destination))
        destination.write_bytes(self.archives[artifact.id])
        return destination


class VerifyImagesTests(unittest.TestCase):
    def setUp(self):
        self.producer = Producer('example/repo', 1, 'a' * 40, 20, 2, 10, 'example/repo/.github/workflows/ci.yml@refs/heads/main')
        self.catalog = {'schema': 1, 'validation_only_images': [{'bundle': 'app', 'platform': 'linux/amd64', 'tag': 'wallow-bff-example:test'}], 'images': [{'bundle': bundle, 'tags': {f'linux/{arch}': f'{bundle}:{arch}' for arch in ('amd64', 'arm64')}} for bundle in ('app', 'infra', 'docs')]}

    def plan(self, route='full', altered_seal=False, wrong_tag=False, companion_tag='wallow-bff-example:test', companion_arm64=False):
        artifacts, archives = [], {}
        bundles = ('app', 'infra', 'docs') if route == 'full' else ('docs',)
        for ident, bundle in enumerate(bundles, 1):
            content = io.BytesIO()
            entries, manifest = {}, []
            for arch in ('amd64', 'arm64'):
                layer = f'{bundle}-{arch}-layer'.encode()
                layer_digest = hashlib.sha256(layer).hexdigest()
                layer_path = 'blobs/sha256/' + layer_digest
                config = json.dumps({'os': 'linux', 'architecture': arch, 'rootfs': {'type': 'layers', 'diff_ids': ['sha256:' + layer_digest]}}).encode()
                config_path = 'blobs/sha256/' + hashlib.sha256(config).hexdigest()
                entries[config_path], entries[layer_path] = config, layer
                tag = 'unexpected:tag' if wrong_tag and ident == 1 and arch == 'amd64' else f'{bundle}:{arch}'
                manifest.append({'Config': config_path, 'RepoTags': [tag], 'Layers': [layer_path]})
            if bundle == 'app':
                manifest.append(dict(manifest[1 if companion_arm64 else 0], RepoTags=[companion_tag]))
            entries['manifest.json'] = json.dumps(manifest).encode()
            with tarfile.open(fileobj=content, mode='w:gz') as archive:
                for name, body in entries.items():
                    member = tarfile.TarInfo(name)
                    member.size = len(body)
                    archive.addfile(member, io.BytesIO(body))
            payload = content.getvalue()
            seal = {'schema': 1, 'repository': self.producer.repository, 'sha': self.producer.source_sha,
                    'run_id': str(self.producer.run_id), 'run_attempt': str(self.producer.run_attempt),
                    'workflow_ref': self.producer.workflow_ref, 'kind': 'images', 'variant': bundle + '-amd64-arm64',
                    'file': 'images.tar.gz', 'sha256': hashlib.sha256(payload).hexdigest()}
            if altered_seal and ident == 1:
                seal['sha'] = 'b' * 40
            output = io.BytesIO()
            with zipfile.ZipFile(output, 'w') as archive:
                archive.writestr('images.tar.gz', payload)
                archive.writestr('images.tar.gz.json', json.dumps(seal))
            archives[ident] = output.getvalue()
            artifacts.append({'id': ident, 'name': f'images-{bundle}-20-2', 'digest': 'sha256:' + hashlib.sha256(archives[ident]).hexdigest(),
                              'size': len(archives[ident]), 'payload': 'images.tar.gz', 'kind': 'images', 'variant': bundle + '-amd64-arm64'})
        return {'producer': asdict(self.producer), 'route': route, 'artifacts': artifacts, 'inputs': {'catalog': copy.deepcopy(self.catalog)}}, Transport(archives)

    def assert_cleaned(self, client):
        self.assertTrue(all(not path.parent.exists() for _, path in client.downloads))

    def test_inspects_exact_full_and_docs_routes_sequentially(self):
        for route, expected in [('full', ['app', 'infra', 'docs']), ('docs', ['docs'])]:
            plan, client = self.plan(route)
            result = verify_images(client, plan, self.catalog)
            self.assertEqual(list(result), expected)
            self.assertEqual([ident for ident, _ in client.downloads], list(range(1, len(expected) + 1)))
            for bundle in expected:
                self.assertNotIn('wallow-bff-example:test', result[bundle]['images'])
                self.assertEqual(set(result[bundle]['images']), {f'{bundle}:amd64', f'{bundle}:arm64'})
                self.assertEqual(result[bundle]['images'][f'{bundle}:arm64']['platform'], 'linux/arm64')
            self.assert_cleaned(client)

    def test_rejects_altered_seal_wrong_tag_or_download_and_cleans(self):
        for options in [{'altered_seal': True}, {'wrong_tag': True}, {'companion_tag': 'unexpected:test'}, {'companion_arm64': True}, {}]:
            plan, client = self.plan(**options)
            if not options:
                client.archives[1] += b'tampered'
            with self.subTest(options=options), self.assertRaises(PublicationError):
                verify_images(client, plan, self.catalog)
            self.assertEqual(len(client.downloads), 1)
            self.assert_cleaned(client)

    def test_rejects_incomplete_extra_duplicate_bundles_and_catalog_drift_before_download(self):
        for change in ('missing', 'extra', 'duplicate', 'catalog', 'catalog_type'):
            plan, client = self.plan()
            if change == 'missing':
                plan['artifacts'].pop()
            elif change == 'extra':
                plan['artifacts'].append(plan['artifacts'][0] | {'variant': 'other-amd64-arm64'})
            elif change == 'duplicate':
                plan['artifacts'].append(plan['artifacts'][0])
            elif change == 'catalog':
                plan['inputs']['catalog']['images'][0]['tags']['linux/amd64'] = 'other:amd64'
            else:
                plan['inputs']['catalog']['schema'] = True
            with self.subTest(change=change), self.assertRaises(PublicationError):
                verify_images(client, plan, self.catalog)
            self.assertEqual(client.downloads, [])

    def test_download_failure_also_cleans_temporary_directory(self):
        plan, client = self.plan()
        def fail_download(artifact, destination):
            client.downloads.append((artifact.id, destination))
            destination.write_bytes(b'partial')
            raise PublicationError('Transport failed')
        client.download = fail_download
        with self.assertRaises(PublicationError):
            verify_images(client, plan, self.catalog)
        self.assert_cleaned(client)


if __name__ == '__main__':
    unittest.main()
