import unittest
from dataclasses import asdict

from publication import Producer, PublicationError
from publication_plan import candidate_artifacts, validate_registration


class PlanTests(unittest.TestCase):
    def setUp(self):
        self.producer = Producer('example/repo', 1, 'a' * 40, 20, 2, 10, 'example/repo/.github/workflows/ci.yml@refs/heads/main')
        self.artifacts = []
        for number, prefix in enumerate(('docfx-site', 'images-docs', 'js-packages', 'images-app', 'images-infra', 'dependency-inputs'), start=1):
            self.artifacts.append({'id': number, 'name': prefix + '-20-2', 'size_in_bytes': 100, 'digest': 'sha256:' + 'b' * 64, 'expired': False, 'expires_at': '2099-01-01T00:00:00Z', 'workflow_run': {'id': 20, 'repository_id': 1, 'head_repository_id': 1, 'head_sha': 'a' * 40, 'head_branch': 'main'}})

    def test_full_route_requires_every_publication_bundle(self):
        route, artifacts = candidate_artifacts(self.producer, [{'name': 'build', 'conclusion': 'success'}], self.artifacts)
        self.assertEqual(route, 'full')
        self.assertEqual(len(artifacts), 6)
        self.assertEqual([item['id'] for item in artifacts], [1, 2, 3, 4, 5, 6])
        self.assertEqual((artifacts[-1]['kind'], artifacts[-1]['variant'], artifacts[-1]['payload']), ('dependencies', 'resolved-locks', 'dependencies.tar.gz'))
        with self.assertRaises(PublicationError):
            candidate_artifacts(self.producer, [{'name': 'build', 'conclusion': 'success'}], self.artifacts[:-1])

    def test_docs_route_cannot_create_application_or_package_candidates(self):
        route, artifacts = candidate_artifacts(self.producer, [{'name': 'build', 'conclusion': 'skipped'}], self.artifacts)
        self.assertEqual(route, 'docs')
        self.assertEqual([item['name'] for item in artifacts], ['docfx-site-20-2', 'images-docs-20-2'])

    def test_rejects_missing_failed_or_ambiguous_route_evidence(self):
        for jobs in ([], [{'name': 'build', 'conclusion': 'failure'}], [{'name': 'build', 'conclusion': 'success'}] * 2):
            with self.assertRaises(PublicationError):
                candidate_artifacts(self.producer, jobs, self.artifacts)

    def test_registration_binds_exact_artifact_ids_and_digests(self):
        route, artifacts = candidate_artifacts(self.producer, [{'name': 'build', 'conclusion': 'success'}], self.artifacts)
        record = {'schema': 1, 'producer': asdict(self.producer), 'route': route, 'artifacts': artifacts, 'component_versions': {'.': '1.0.0'}, 'input_sha256': {'pnpm-lock.yaml': 'a' * 64}, 'catalog': {}}
        validate_registration(record, self.producer, route, artifacts)
        for changes in ({'artifacts': artifacts[:-1]}, {'route': 'docs'}, {'producer': asdict(self.producer) | {'run_attempt': 1}}, {'input_sha256': {'../escape': 'a' * 64}}):
            with self.subTest(changes=changes), self.assertRaises(PublicationError):
                validate_registration(record | changes, self.producer, route, artifacts)


if __name__ == '__main__':
    unittest.main()
