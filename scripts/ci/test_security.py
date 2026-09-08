import copy
import hashlib
from datetime import datetime, timezone
import unittest

import security


class SecurityTests(unittest.TestCase):
    def test_profile_defaults_and_explicit_selection(self):
        self.assertEqual(security.profile('', 'bc-solutions-coder/wallow'), 'codeql')
        self.assertEqual(security.profile('', 'someone/fork'), 'portable')
        self.assertEqual(security.profile('codeql', 'someone/fork'), 'codeql')
        with self.assertRaises(ValueError):
            security.profile('disabled', 'someone/fork')

    def test_exception_expiry_scope_and_duration(self):
        now = datetime(2026, 9, 7, tzinfo=timezone.utc)
        item = dict(scanner='devskim', id='DS1', scope='api/A.cs', owner='maintainer',
                    reason='Protocol compatibility', tracking='https://github.com/a/b/issues/1',
                    created='2026-09-01T00:00:00Z', expires='2026-09-20T00:00:00Z')
        security.validate_exceptions({'exceptions': [item]}, now)
        for field, value in [('scope', '*'), ('expires', '2026-09-06T00:00:00Z'),
                             ('expires', '2026-11-01T00:00:00Z'), ('owner', '')]:
            bad = dict(item, **{field: value})
            with self.assertRaises(ValueError, msg=field):
                security.validate_exceptions({'exceptions': [bad]}, now)

    def test_native_devskim_severity_not_generic_sarif_level(self):
        report = {'version': '2.1.0', 'runs': [{'tool': {'driver': {'name': 'devskim', 'rules': []}}, 'results': [
            {'ruleId': 'DS1', 'level': 'warning', 'properties': {'DevSkimSeverity': 'Important', 'DevSkimConfidence': 'Medium'},
             'locations': [{'physicalLocation': {'artifactLocation': {'uri': 'api/A.cs'}}}]}]}]}
        self.assertTrue(security.devskim(report)[0]['blocking'])
        report['runs'][0]['results'][0]['properties']['DevSkimConfidence'] = 'Low'
        self.assertFalse(security.devskim(report)[0]['blocking'])
        del report['runs'][0]['results'][0]['properties']['DevSkimSeverity']
        with self.assertRaises(ValueError):
            security.devskim(report)

    def test_zizmor_native_threshold(self):
        finding = {'ident': 'injection', 'determinations': {'severity': 'High', 'confidence': 'Medium'},
                   'ignored': False, 'locations': [{'symbolic': {'key': {'Local': {'verbatim_path': '.github/workflows/a.yml'}}}}]}
        self.assertTrue(security.zizmor([finding])[0]['blocking'])
        finding['determinations']['confidence'] = 'Low'
        self.assertFalse(security.zizmor([finding])[0]['blocking'])
        finding['ignored'] = True
        with self.assertRaises(ValueError):
            security.zizmor([finding])

    def test_zizmor_exact_feature_exception_keeps_changed_or_missing_findings_blocking(self):
        feature = 'on:\n  workflow_run:\n    workflows: [CI]'
        result = {'ident': 'dangerous-triggers', 'ignored': False,
                  'determinations': {'severity': 'High', 'confidence': 'Medium'},
                  'locations': [{'symbolic': {'kind': 'Primary', 'key': {'Local': {'verbatim_path': '.github/workflows/publish.yml'}}},
                                 'concrete': {'feature': feature}}]}
        exception = {'scanner': 'zizmor', 'id': 'dangerous-triggers', 'scope': '.github/workflows/publish.yml',
                     'feature_sha256': 'sha256:' + hashlib.sha256(feature.encode()).hexdigest()}
        for change in ('feature', 'missing', 'path', 'id', 'secondary'):
            neighbor = copy.deepcopy(result)
            if change == 'feature':
                neighbor['locations'][0]['concrete']['feature'] += '\n  pull_request_target:'
            elif change == 'missing':
                del neighbor['locations'][0]['concrete']['feature']
            elif change == 'path':
                neighbor['locations'][0]['symbolic']['key']['Local']['verbatim_path'] = '.github/workflows/other.yml'
            elif change == 'id':
                neighbor['ident'] = 'other-audit'
            else:
                neighbor['locations'][0]['symbolic']['kind'] = 'Secondary'
            findings = security.apply_exceptions(security.zizmor([result, neighbor]), [exception])
            self.assertTrue(findings[0]['excepted'])
            self.assertTrue(findings[1]['blocking'])
            self.assertFalse(findings[1]['excepted'], change)
        reordered = copy.deepcopy(result)
        reordered['locations'].insert(0, {'symbolic': {'kind': 'Secondary'}, 'concrete': {'feature': 'unrelated'}})
        self.assertTrue(security.apply_exceptions(security.zizmor([reordered]), [exception])[0]['excepted'])

    def test_zizmor_exception_requires_exact_feature_selector_and_bounded_expiry(self):
        now = datetime(2026, 9, 7, tzinfo=timezone.utc)
        entry = dict(scanner='zizmor', id='dangerous-triggers', scope='.github/workflows/publish.yml',
                     owner='maintainer', reason='Reviewed protected controller', tracking='https://github.com/a/b/issues/1',
                     created='2026-09-01T00:00:00Z', expires='2026-10-01T00:00:00Z', feature_sha256='sha256:' + 'a' * 64)
        security.validate_exceptions({'exceptions': [entry]}, now)
        missing = dict(entry)
        del missing['feature_sha256']
        with self.assertRaises(ValueError):
            security.validate_exceptions({'exceptions': [missing]}, now)
        with self.assertRaises(ValueError):
            security.apply_exceptions([], [missing])
        for change in [{'feature_sha256': '*'}, {'feature_sha256': ''}, {'expires': '2026-10-01T00:00:01Z'}, {'expires': '2026-09-07T00:00:00Z'}]:
            with self.subTest(change=change), self.assertRaises(ValueError):
                security.validate_exceptions({'exceptions': [entry | change]}, now)
        with self.assertRaises(ValueError):
            security.validate_exceptions({'exceptions': [entry, entry]}, now)

    def test_codeql_security_score(self):
        report = {'version': '2.1.0', 'runs': [{'tool': {'driver': {'name': 'CodeQL', 'rules': [
            {'id': 'cs/injection', 'properties': {'security-severity': '8.1'}}]}}, 'results': [
            {'ruleId': 'cs/injection', 'locations': [{'physicalLocation': {'artifactLocation': {'uri': 'api/A.cs'}}}]}]}]}
        self.assertTrue(security.codeql(report)[0]['blocking'])
        report['runs'][0]['tool']['driver']['rules'][0]['properties']['security-severity'] = '5.0'
        self.assertFalse(security.codeql(report)[0]['blocking'])
        report['runs'][0]['invocations'] = [{'executionSuccessful': False}]
        with self.assertRaises(ValueError):
            security.codeql(report)

    def test_codeql_exact_selector_preserves_neighboring_findings(self):
        message = 'User-controlled consent guards an action.'
        result = {'ruleId': 'cs/bypass', 'message': {'text': message},
                  'partialFingerprints': {'primaryLocationLineHash': 'abc123:1'},
                  'locations': [{'physicalLocation': {'artifactLocation': {'uri': 'api/A.cs'}}}]}
        report = {'version': '2.1.0', 'runs': [{'tool': {'driver': {'name': 'CodeQL', 'rules': [
            {'id': 'cs/bypass', 'properties': {'security-severity': '8.1'}}]}}, 'results': [result]}]}
        exception = dict(scanner='codeql', id='cs/bypass', scope='api/A.cs',
                         primary_location_line_hash='abc123:1',
                         message_sha256='sha256:' + hashlib.sha256(message.encode()).hexdigest())
        for changed in ('fingerprint', 'message', 'missing_fingerprint', 'missing_message'):
            neighbor = copy.deepcopy(result)
            if changed == 'fingerprint':
                neighbor['partialFingerprints']['primaryLocationLineHash'] = 'def456:1'
            elif changed == 'message':
                neighbor['message']['text'] = 'User-controlled authentication guards an action.'
            elif changed == 'missing_fingerprint':
                del neighbor['partialFingerprints']
            else:
                del neighbor['message']
            report['runs'][0]['results'] = [result, neighbor]
            findings = security.apply_exceptions(security.codeql(report), [exception])
            self.assertTrue(findings[0]['excepted'])
            self.assertTrue(findings[1]['blocking'])
            self.assertFalse(findings[1]['excepted'], changed)

    def test_codeql_exception_requires_selectors_and_bounded_expiry(self):
        now = datetime(2026, 9, 7, tzinfo=timezone.utc)
        item = dict(scanner='codeql', id='cs/bypass', scope='api/A.cs', owner='maintainer',
                    reason='Reviewed consent condition', tracking='https://github.com/a/b/issues/1',
                    created='2026-09-01T00:00:00Z', expires='2026-10-01T00:00:00Z',
                    primary_location_line_hash='abc123:1', message_sha256='sha256:' + 'a' * 64)
        security.validate_exceptions({'exceptions': [item]}, now)
        for key in ('primary_location_line_hash', 'message_sha256'):
            bad = dict(item)
            del bad[key]
            with self.assertRaises(ValueError):
                security.validate_exceptions({'exceptions': [bad]}, now)
            with self.assertRaises(ValueError):
                security.apply_exceptions([], [bad])
        for field, value in [('primary_location_line_hash', '*'), ('message_sha256', 'invalid'),
                             ('expires', '2026-10-01T00:00:01Z'), ('expires', '2026-09-07T00:00:00Z')]:
            with self.assertRaises(ValueError, msg=field):
                security.validate_exceptions({'exceptions': [dict(item, **{field: value})]}, now)
        with self.assertRaises(ValueError):
            security.validate_exceptions({'exceptions': [item, item]}, now)

    def test_trivy_only_fixable_high_blocks(self):
        report = {'SchemaVersion': 2, 'Results': [{'Target': 'pnpm-lock.yaml', 'Packages': [{'Name': 'a'}], 'Vulnerabilities': [
            {'VulnerabilityID': 'CVE-1', 'PkgName': 'a', 'InstalledVersion': '1', 'FixedVersion': '2', 'Severity': 'HIGH'}]}]}
        self.assertTrue(security.trivy(report)[0]['blocking'])
        report['Results'][0]['Vulnerabilities'][0]['FixedVersion'] = ''
        self.assertFalse(security.trivy(report)[0]['blocking'])
        with self.assertRaises(ValueError):
            security.trivy({'SchemaVersion': 2, 'Results': []})

    def test_missing_reports_and_invalid_findings_fail(self):
        for parser in [security.devskim, security.codeql, security.trivy, security.zizmor]:
            with self.assertRaises(ValueError):
                parser({})

    def test_scoped_exception_does_not_hide_neighbor(self):
        findings = [{'scanner': 'devskim', 'id': 'DS1', 'scope': 'api/A.cs', 'blocking': True},
                    {'scanner': 'devskim', 'id': 'DS1', 'scope': 'api/B.cs', 'blocking': True}]
        result = security.apply_exceptions(copy.deepcopy(findings), [{'scanner': 'devskim', 'id': 'DS1', 'scope': 'api/A.cs'}])
        self.assertTrue(result[0]['excepted'])
        self.assertFalse(result[1]['excepted'])


if __name__ == '__main__':
    unittest.main()
