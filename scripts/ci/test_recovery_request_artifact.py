"""Completed recovery reads the original sealed request rather than caller-supplied identity."""

import hashlib
import unittest
from unittest.mock import patch

from publication import PublicationError
from recovery_authorization import authenticate_request
import test_register_recovery


class RecoveryRequestArtifactTests(unittest.TestCase):
    def setUp(self):
        self.fixture = test_register_recovery.RecoveryRegistrationTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)

    def authenticate(self, expected=None):
        fixture = self.fixture
        with patch('recovery_authorization.request_record', return_value=expected or fixture.expected):
            return authenticate_request(fixture.client, fixture.controller, [fixture.metadata], fixture.catalog)

    def test_actual_archive_binds_source_controller_and_exact_request_digest(self):
        producer, artifact, record = self.authenticate()
        self.assertEqual(producer.source_sha, self.fixture.identity['source_sha'])
        self.assertEqual(producer.controller_sha, self.fixture.controller.source_sha)
        self.assertEqual(producer.recovery_request_sha256, 'sha256:' + hashlib.sha256(self.fixture.data).hexdigest())
        self.assertEqual(producer.release_id, 40)
        self.assertEqual(artifact.id, 50)
        self.assertEqual(record, self.fixture.expected)

    def test_rejects_changed_release_or_controller_record(self):
        for changed in (self.fixture.expected | {'release': {'id': 41}},
                        self.fixture.expected | {'controller_sha': 'd' * 40}):
            with self.subTest(changed=changed), self.assertRaises(PublicationError):
                self.authenticate(changed)

    def test_rejects_archive_attributed_to_historical_source_instead_of_controller(self):
        self.fixture.metadata['workflow_run']['head_sha'] = self.fixture.identity['source_sha']
        with self.assertRaises(PublicationError):
            self.authenticate()


if __name__ == '__main__':
    unittest.main()
