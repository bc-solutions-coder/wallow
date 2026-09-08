import copy
from dataclasses import asdict
from pathlib import Path
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from publication import PublicationError
from publication_alias_pipeline import prepare, promote
from publication_alias_registry import plan_aliases, registry_source
import test_publication_npm as package_fixtures
import test_publication_release_image_writer as image_fixtures


class Registry:
    def __init__(self):
        self.tags, self.events = {}, []
        self.fail = None
        self.packages = self
    def __enter__(self): return self
    def __exit__(self, *args): pass
    def current(self, output, alias): return self.tags.get(alias)
    def verify(self, record, fresh=False): self.events.append(('verify', record['release']['id']))
    def read(self, package): return True
    def versions(self, name): return ['1.0.0']
    def promote(self, output, alias, candidate, previous):
        self.events.append(('write', alias))
        if self.fail == alias: raise PublicationError('Registry mutation failed')
        self.tags[alias] = output['version']
        return 'verified'


class PipelineTests(unittest.TestCase):
    def setUp(self):
        fixture = package_fixtures.NpmTests()
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        self.record = {'release': {'id': 1, 'component': 'sdk', 'version': '1.0.0', 'commit_sha': 'a' * 40, 'prerelease': False},
                       'receipt': {'asset_id': 3}, 'outputs': {'package': asdict(fixture.package)}}
        self.client = SimpleNamespace(repository='example/repo', get=lambda path: {})
        self.catalog = {'components': [{'package': {'name': '@example/sdk'}}]}
        self.registry = Registry()

    def plan(self):
        return {'records': [self.record], 'unrelated': False, 'entries': plan_aliases(self.client, [self.record], 'package', self.registry), 'scans': {'1': {'current_policy': 'verified'}}, 'dependencies': []}

    def run_writer(self, plan, progress):
        with patch('publication_alias_pipeline.frame', return_value=({'job_id': 2}, {})), \
             patch('publication_alias_pipeline.authorize_plan', return_value=plan), \
             patch('publication_alias_pipeline.Registry', return_value=self.registry):
            return promote(self.client, {}, 1, 1, 2, 1, 'package', self.catalog, '.', 'fixture', 'example', progress, lambda: None)

    def test_all_identity_checks_precede_alias_writes_and_partial_retry_converges(self):
        plan = self.plan()
        self.registry.events.clear()
        self.registry.fail = 'major-1'
        progress = {'entries': []}
        with self.assertRaises(PublicationError): self.run_writer(plan, progress)
        self.assertEqual(self.registry.events[0], ('verify', 1))
        self.assertEqual(self.registry.tags, {'latest': '1.0.0'})
        self.assertEqual(len(progress['entries']), 1)
        self.registry.fail = None
        retry = self.plan()
        result = self.run_writer(retry, {'entries': []})
        self.assertEqual(self.registry.tags, {'latest': '1.0.0', 'major-1': '1.0.0', 'minor-1.0': '1.0.0'})
        self.assertEqual(result['entries'][0]['outcome'], 'identical')

    def test_changed_alias_or_missing_fresh_scan_prevents_every_write(self):
        for change in ('state', 'scan'):
            self.registry = Registry()
            plan = self.plan()
            self.registry.events.clear()
            if change == 'state': self.registry.tags['latest'] = '9.9.9'
            else: plan['scans'] = {}
            with self.subTest(change=change), self.assertRaises(PublicationError):
                self.run_writer(plan, {'entries': []})
            self.assertFalse(any(event == 'write' for event, _ in self.registry.events))

    def test_prerelease_has_no_stable_alias_mutation(self):
        self.record['release']['prerelease'] = True
        plan = self.plan()
        plan['scans'] = {}
        self.run_writer(plan, {'entries': []})
        self.assertEqual(self.registry.tags, {})

    def test_changing_package_target_requires_fresh_selected_inputs(self):
        self.record['selected'] = {'producer': {'run_id': 7, 'run_attempt': 1}}
        with patch('publication_alias_pipeline.authorize_preparation'), patch('publication_alias_pipeline.frame', return_value=({}, {})), \
             patch('publication_alias_pipeline.publication_records', return_value=([self.record], False)), \
             patch('publication_alias_pipeline.configuration'), patch('publication_alias_pipeline.policy_digest', return_value='current'), \
             patch('publication_alias_pipeline.Registry', return_value=self.registry), \
             patch('publication_alias_pipeline.resolve', side_effect=PublicationError('Expired selected lock artifact')) as resolve:
            with self.assertRaisesRegex(PublicationError, 'explicit recovery'):
                prepare(self.client, {}, 1, 1, 2, 1, 'package', self.catalog, '.', '/unused', 'fixture', 'example')
            self.registry.tags = {name: '1.0.0' for name in ('latest', 'major-1', 'minor-1.0')}
            resolve.reset_mock()
            unchanged = prepare(self.client, {}, 1, 1, 2, 1, 'package', self.catalog, '.', '/unused', 'fixture', 'example')
            self.assertEqual(unchanged['scans'], {})
            resolve.assert_not_called()
        self.assertFalse(any(event == 'write' for event, _ in self.registry.events))

    def test_old_run_cannot_move_current_alias_backward(self):
        old = copy.deepcopy(self.record)
        old['release'].update(id=2, version='0.9.0', commit_sha='b' * 40)
        old['outputs']['package']['version'] = '0.9.0'
        self.registry.tags['latest'] = '1.0.0'
        self.client.get = lambda path: {'status': 'behind', 'base_commit': {'sha': 'a' * 40}, 'merge_base_commit': {'sha': 'b' * 40}}
        entries = plan_aliases(self.client, [self.record, old], 'package', self.registry, 2)
        self.assertEqual(next(entry for entry in entries if entry['alias'] == 'latest')['action'], 'skip-older')


class RegistryImageTests(unittest.TestCase):
    def test_real_retained_layers_match_receipt_and_tampered_bytes_fail(self):
        fixture = image_fixtures.ReleaseImageTests()
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        fixture.publish()
        client = fixture.fixture
        image = fixture.plan['source']['verified_images']['docs']['prepared']['inventory']['images'][0]
        directory = client.root / 'readback-fixture'
        directory.mkdir()
        for name, value in client.blobs[image['prepared']['manifest_digest']].items(): (directory / name).write_bytes(value)
        result = registry_source(directory, image['prepared'])
        self.assertEqual(result['config_digest'], image['source']['config_digest'])
        self.assertEqual(result['layers'], [{'digest': layer['digest'], 'size': layer['size']} for layer in image['source']['layers']])
        (directory / image['prepared']['config_digest'][7:]).write_bytes(b'{}')
        with self.assertRaises(PublicationError): registry_source(directory, image['prepared'])


if __name__ == '__main__': unittest.main()
