import copy
import unittest

from publication import PublicationError, authorize_controller, authorize_main_producer, image_repository, validate_catalog


class AuthorizationTests(unittest.TestCase):
    def setUp(self):
        self.sha = 'a' * 40
        self.repository = {'id': 1, 'full_name': 'example/fork', 'default_branch': 'main'}
        self.workflow = {'id': 10, 'path': '.github/workflows/ci.yml'}
        identity = {'id': 1, 'full_name': 'example/fork'}
        self.run = {'id': 20, 'run_attempt': 2, 'workflow_id': 10, 'path': '.github/workflows/ci.yml', 'event': 'push', 'head_branch': 'main', 'head_sha': self.sha, 'repository': identity, 'head_repository': identity, 'status': 'completed', 'conclusion': 'success'}
        self.gate = {'id': 30, 'run_id': 20, 'run_attempt': 2, 'head_sha': self.sha, 'name': 'CI / required', 'status': 'completed', 'conclusion': 'success', 'check_run_url': 'https://api.github.com/repos/example/fork/check-runs/40'}
        self.check = {'id': 40, 'name': 'CI / required', 'head_sha': self.sha, 'status': 'completed', 'conclusion': 'success', 'app': {'id': 15368, 'slug': 'github-actions'}}
        self.comparison = {'status': 'ahead', 'base_commit': {'sha': self.sha}, 'merge_base_commit': {'sha': self.sha}}

    def authorize(self, **changes):
        values = dict(repository=self.repository, workflow=self.workflow, run=self.run, run_id=20, attempt=2, jobs=[self.gate], required_check=self.check, comparison=self.comparison)
        return authorize_main_producer(**(values | changes))

    def test_binds_successful_main_source_run_attempt_and_workflow(self):
        producer = self.authorize()
        self.assertEqual((producer.source_sha, producer.run_id, producer.run_attempt, producer.workflow_id), (self.sha, 20, 2, 10))
        self.assertEqual(producer.workflow_ref, 'example/fork/.github/workflows/ci.yml@refs/heads/main')

    def test_rejects_pr_foreign_unsuccessful_and_unexpected_workflows(self):
        cases = [
            {'event': 'pull_request'}, {'event': 'pull_request_target'}, {'event': 'workflow_dispatch'},
            {'head_branch': 'release-please--branches--main'}, {'head_branch': 'feature'},
            {'conclusion': 'failure'}, {'conclusion': 'cancelled'}, {'status': 'in_progress'},
            {'workflow_id': 11}, {'path': '.github/workflows/other.yml'},
            {'head_repository': {'id': 2, 'full_name': 'foreign/fork'}},
            {'repository': {'id': 2, 'full_name': 'foreign/fork'}},
        ]
        for change in cases:
            with self.subTest(change=change), self.assertRaises(PublicationError):
                self.authorize(run=self.run | change)

    def test_rejects_replaced_run_attempt_or_source_evidence(self):
        for changes in [
            {'run_id': 21}, {'attempt': 1}, {'attempt': True},
            {'run': self.run | {'head_sha': 'b' * 40}},
            {'comparison': self.comparison | {'status': 'diverged'}},
            {'comparison': self.comparison | {'merge_base_commit': {'sha': 'b' * 40}}},
        ]:
            with self.subTest(changes=changes), self.assertRaises(PublicationError):
                self.authorize(**changes)

    def test_requires_exact_successful_aggregate_from_this_attempt(self):
        for jobs in [[], [self.gate, self.gate], [self.gate, None]]:
            with self.assertRaises(PublicationError):
                self.authorize(jobs=jobs)
        for change in [{'conclusion': 'failure'}, {'conclusion': 'skipped'}, {'run_attempt': 1}, {'run_id': 21}, {'head_sha': 'b' * 40}, {'check_run_url': 'https://api.github.com/repos/example/fork/check-runs/41'}]:
            with self.subTest(change=change), self.assertRaises(PublicationError):
                self.authorize(jobs=[self.gate | change])

    def test_rejects_a_same_name_check_from_another_app_or_revision(self):
        for change in [{'app': {'id': 12, 'slug': 'github-actions'}}, {'app': {'id': 15368.0, 'slug': 'github-actions'}}, {'app': {'id': 15368, 'slug': 'other-app'}}, {'head_sha': 'b' * 40}, {'conclusion': 'failure'}, {'id': 41}]:
            with self.subTest(change=change), self.assertRaises(PublicationError):
                self.authorize(required_check=self.check | change)

    def test_controller_revision_is_separate_from_source_and_must_be_on_main(self):
        controller_sha = 'c' * 40
        context = {'repository': 'example/fork', 'ref': 'refs/heads/main', 'workflow_ref': 'example/fork/.github/workflows/publish.yml@refs/heads/main', 'workflow_sha': controller_sha, 'event_name': 'workflow_run'}
        comparison = {'status': 'ahead', 'base_commit': {'sha': controller_sha}, 'merge_base_commit': {'sha': controller_sha}}
        self.assertEqual(authorize_controller(context, self.repository, comparison), controller_sha)
        self.assertNotEqual(controller_sha, self.authorize().source_sha)
        for change in [{'ref': 'refs/heads/feature'}, {'workflow_ref': 'example/fork/.github/workflows/publish.yml@refs/pull/1/merge'}, {'workflow_sha': self.sha}, {'repository': 'foreign/fork'}, {'event_name': 'pull_request'}]:
            with self.subTest(change=change), self.assertRaises(PublicationError):
                authorize_controller(context | change, self.repository, comparison)


class CatalogTests(unittest.TestCase):
    def setUp(self):
        self.catalog = {
            'schema': 1,
            'default_branch': 'main',
            'producer_workflow': '.github/workflows/ci.yml',
            'package_registry': 'https://npm.pkg.github.com',
            'package_scope': '@example',
            'components': [
                {'id': 'platform', 'path': '.', 'tag_prefix': 'v'},
                {'id': 'sdk', 'path': 'packages/sdk', 'tag_prefix': 'sdk-v', 'package': {'name': '@example/sdk', 'tarball': 'sdk.tgz'}},
            ],
            'images': [{'id': 'api', 'repository_suffix': '', 'bundle': 'app', 'component': 'platform', 'tags': {'linux/amd64': 'candidate:test', 'linux/arm64': 'candidate:test-arm64'}, 'build_args': {}}],
            'docs': {'bundle': 'docs', 'payload': 'site.tar.gz', 'variant': 'docfx'},
        }
        self.release = {'release-type': 'simple', 'packages': {'.': {}, 'packages/sdk': {'release-type': 'node', 'package-name': '@example/sdk', 'component': 'sdk', 'include-component-in-tag': True}}}
        self.manifests = {'packages/sdk': {'name': '@example/sdk', 'publishConfig': {'registry': 'https://npm.pkg.github.com'}}}

    def test_fork_catalog_resolves_only_fork_owned_registry_names(self):
        catalog = validate_catalog(self.catalog, self.release, self.manifests)
        self.assertEqual(image_repository('Example/My-Fork', catalog['images'][0]), 'ghcr.io/example/my-fork')
        image = {**catalog['images'][0], 'repository_suffix': '-auth'}
        self.assertEqual(image_repository('Example/My-Fork', image), 'ghcr.io/example/my-fork-auth')

    def test_rejects_unconfirmed_scope_private_package_and_other_registry(self):
        for change in [{'name': '@upstream/sdk'}, {'private': True}, {'publishConfig': {'registry': 'https://registry.npmjs.org'}}]:
            with self.subTest(change=change):
                manifests = copy.deepcopy(self.manifests)
                manifests['packages/sdk'].update(change)
                with self.assertRaises(PublicationError):
                    validate_catalog(self.catalog, self.release, manifests)

    def test_rejects_ambiguous_or_unmapped_release_identity(self):
        for key, value in [('component', 'other'), ('package-name', '@example/other'), ('include-component-in-tag', False), ('include-v-in-tag', False)]:
            with self.subTest(key=key):
                release = copy.deepcopy(self.release)
                release['packages']['packages/sdk'][key] = value
                with self.assertRaises(PublicationError):
                    validate_catalog(self.catalog, release, self.manifests)
        release = copy.deepcopy(self.release)
        release['packages']['packages/new'] = {}
        with self.assertRaises(PublicationError):
            validate_catalog(self.catalog, release, self.manifests)

    def test_rejects_missing_platform_and_unknown_component(self):
        for change in [{'tags': {'linux/amd64': 'candidate:test'}}, {'component': 'unknown'}, {'tags': {'linux/amd64': 'candidate:test', 'linux/arm64': 'candidate:test'}}, {'repository_suffix': '/upstream'}]:
            with self.subTest(change=change):
                catalog = copy.deepcopy(self.catalog)
                catalog['images'][0].update(change)
                with self.assertRaises(PublicationError):
                    validate_catalog(catalog, self.release, self.manifests)

    def test_rejects_duplicates_and_unknown_configuration(self):
        for field in ['components', 'images']:
            catalog = copy.deepcopy(self.catalog)
            catalog[field].append(copy.deepcopy(catalog[field][0]))
            with self.assertRaises(PublicationError):
                validate_catalog(catalog, self.release, self.manifests)
        for change in [{'schema': True}, {'default_branch': 'feature'}, {'producer_workflow': '.github/workflows/foreign.yml'}, {'unknown': 'value'}]:
            with self.subTest(change=change), self.assertRaises(PublicationError):
                validate_catalog({**self.catalog, **change}, self.release, self.manifests)


if __name__ == '__main__':
    unittest.main()
