"""Local security gates. JSON reports and the JSON-compatible YAML exception policy are authoritative."""

import argparse
from datetime import datetime, timedelta, timezone
import hashlib
import json
import math
import os
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys
from urllib.parse import unquote


SCANNERS = {'devskim', 'zizmor', 'codeql', 'trivy'}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def profile(value, repository):
    require(bool(repository), 'repository identity is missing')
    selected = value or ('codeql' if repository == 'bc-solutions-coder/wallow' else 'portable')
    require(selected in ('portable', 'codeql'), 'SECURITY_PROFILE must be portable or codeql')
    return selected


def validate_exceptions(document, now=None):
    now = now or datetime.now(timezone.utc)
    require(isinstance(document, dict) and set(document) == {'exceptions'}, 'invalid exception policy')
    entries = document['exceptions']
    require(isinstance(entries, list), 'exceptions must be a list')
    seen = set()
    fields = {'scanner', 'id', 'scope', 'owner', 'reason', 'tracking', 'created', 'expires'}
    for entry in entries:
        require(isinstance(entry, dict), 'exception must be an object')
        expected = fields | ({'feature_sha256'} if entry.get('scanner') == 'zizmor' else set())
        require(set(entry) == expected, 'exception fields must match policy')
        require(all(isinstance(v, str) and v.strip() == v and v for v in entry.values()), 'exception fields must be nonempty text')
        require(entry['scanner'] in SCANNERS, 'unknown exception scanner')
        require(not any(c in entry['scope'] for c in '*?[]\n\r') and '..' not in PurePosixPath(entry['scope']).parts, 'exception scope must be exact')
        require(entry['scope'] not in ('.', '/', 'all') and not entry['scope'].endswith('/'), 'exception scope must identify a file or package in an artifact')
        require(entry['tracking'].startswith('https://') and '/' in entry['tracking'][8:], 'exception needs an HTTPS tracking link')
        for key in ('created', 'expires'):
            require(bool(re.fullmatch(r'\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z', entry[key])), 'exception dates must be UTC timestamps')
        created = datetime.fromisoformat(entry['created'].replace('Z', '+00:00'))
        expires = datetime.fromisoformat(entry['expires'].replace('Z', '+00:00'))
        require(created <= now < expires and timedelta(0) < expires - created <= timedelta(days=30), 'exception is expired, future-dated, or longer than 30 days')
        if entry['scanner'] == 'zizmor':
            require(bool(re.fullmatch(r'sha256:[a-f0-9]{64}', entry['feature_sha256'])), 'Zizmor exception needs an exact feature SHA-256')
        identity = finding_identity(entry)
        require(identity not in seen, 'duplicate exception')
        seen.add(identity)
    return entries


def sarif_runs(document, scanner):
    require(isinstance(document, dict) and document.get('version') == '2.1.0', 'missing or invalid SARIF version')
    runs = document.get('runs')
    require(isinstance(runs, list) and runs, 'SARIF runs are missing')
    for run in runs:
        require(isinstance(run, dict) and isinstance(run.get('results'), list), 'SARIF results are missing')
        driver = run.get('tool', {}).get('driver', {})
        require(scanner in driver.get('name', '').lower(), 'unexpected SARIF scanner identity')
        for invocation in run.get('invocations', []):
            require(invocation.get('executionSuccessful') is not False, 'scanner invocation failed')
            for notification in invocation.get('toolExecutionNotifications', []):
                require(notification.get('level') != 'error', 'scanner execution notification reported an error')
    return runs


def location(result):
    locations = result.get('locations')
    require(isinstance(locations, list) and locations, 'finding has no exact location')
    uri = locations[0].get('physicalLocation', {}).get('artifactLocation', {}).get('uri')
    require(isinstance(uri, str) and uri and not uri.startswith('/') and '://' not in uri, 'finding location must be repository-relative')
    path = unquote(uri)
    require('..' not in PurePosixPath(path).parts, 'finding escapes source root')
    return path


def finding(scanner, ident, scope, blocking, **metadata):
    require(isinstance(ident, str) and bool(ident), 'finding ID missing')
    return dict(scanner=scanner, id=ident, scope=scope, blocking=blocking, **metadata)


def devskim(document):
    findings = []
    for run in sarif_runs(document, 'devskim'):
        rules = {r['id']: r for r in run['tool']['driver'].get('rules', [])}
        for result in run['results']:
            require(not result.get('suppressions'), 'scanner-local suppressions are forbidden')
            props = result.get('properties', {})
            rule_props = rules.get(result.get('ruleId'), {}).get('properties', {})
            severity = props.get('DevSkimSeverity', rule_props.get('DevSkimSeverity'))
            confidence = props.get('DevSkimConfidence', rule_props.get('DevSkimConfidence'))
            require(severity in ('Critical', 'Important', 'Moderate', 'BestPractice', 'ManualReview'), 'DevSkim native severity is missing or invalid')
            require(confidence in ('High', 'Medium', 'Low'), 'DevSkim confidence is missing or invalid')
            findings.append(finding('devskim', result.get('ruleId'), location(result), severity in ('Critical', 'Important') and confidence in ('Medium', 'High'), severity=severity, confidence=confidence))
    return findings


def zizmor(document):
    require(isinstance(document, list), 'zizmor report must be a JSON-v1 array')
    findings = []
    for result in document:
        require(isinstance(result, dict) and result.get('ignored') is False, 'invalid or suppressed zizmor finding')
        determination = result.get('determinations', {})
        severity, confidence = determination.get('severity'), determination.get('confidence')
        require(severity in ('Informational', 'Low', 'Medium', 'High'), 'invalid zizmor severity')
        require(confidence in ('Low', 'Medium', 'High'), 'invalid zizmor confidence')
        locations = result.get('locations')
        require(isinstance(locations, list) and locations, 'zizmor location missing')
        primary = [item for item in locations if item.get('symbolic', {}).get('kind') == 'Primary']
        selected = primary[0] if len(primary) == 1 else locations[0]
        path = selected.get('symbolic', {}).get('key', {}).get('Local', {}).get('verbatim_path')
        feature = selected.get('concrete', {}).get('feature') if len(primary) == 1 else None
        require(isinstance(path, str) and path and not path.startswith('/') and '..' not in PurePosixPath(path).parts, 'zizmor location must be local and relative')
        findings.append(finding('zizmor', result.get('ident'), path, severity == 'High' and confidence in ('Medium', 'High'), severity=severity, confidence=confidence,
                                feature_sha256='sha256:' + hashlib.sha256(feature.encode()).hexdigest() if isinstance(feature, str) and feature else None))
    return findings


def codeql(document):
    findings = []
    for run in sarif_runs(document, 'codeql'):
        components = [run['tool']['driver']] + run['tool'].get('extensions', [])
        rules = {rule['id']: rule for component in components for rule in component.get('rules', [])}
        for result in run['results']:
            require(not result.get('suppressions'), 'scanner-local suppressions are forbidden')
            ident = result.get('ruleId', result.get('rule', {}).get('id'))
            require(ident in rules, 'CodeQL finding rule metadata missing')
            props = rules[ident].get('properties', {})
            score = props.get('security-severity')
            if score is None:
                require('security' not in props.get('tags', []), 'security rule is missing its severity')
                blocking = False
            else:
                try:
                    score = float(score)
                except (ValueError, TypeError) as exc:
                    raise ValueError('invalid CodeQL security severity') from exc
                require(math.isfinite(score) and 0 <= score <= 10, 'invalid CodeQL security severity')
                blocking = score >= 7
            findings.append(finding('codeql', ident, location(result), blocking, security_severity=score))
    return findings


def trivy(document):
    require(isinstance(document, dict) and document.get('SchemaVersion') == 2, 'invalid Trivy schema')
    results = document.get('Results')
    require(isinstance(results, list) and results, 'Trivy did not report dependency coverage')
    require(any(isinstance(r.get('Packages'), list) and r['Packages'] for r in results), 'Trivy package inventory missing; use --list-all-pkgs')
    findings = []
    for result in results:
        target = result.get('Target')
        require(isinstance(target, str) and target, 'Trivy target missing')
        vulns = result.get('Vulnerabilities', [])
        require(isinstance(vulns, list), 'invalid Trivy vulnerabilities')
        for vuln in vulns:
            severity = vuln.get('Severity')
            require(severity in ('UNKNOWN', 'LOW', 'MEDIUM', 'HIGH', 'CRITICAL'), 'invalid vulnerability severity')
            package, version = vuln.get('PkgName'), vuln.get('InstalledVersion')
            require(isinstance(package, str) and package and isinstance(version, str) and version, 'vulnerable package identity missing')
            fixed = vuln.get('FixedVersion', '')
            require(isinstance(fixed, str), 'invalid vulnerability fix identity')
            findings.append(finding('trivy', vuln.get('VulnerabilityID'), f'{target}::{package}@{version}', severity in ('HIGH', 'CRITICAL') and bool(fixed.strip()), severity=severity, fixed_version=fixed))
    return findings


def finding_identity(item):
    identity = (item['scanner'], item['id'], item['scope'])
    if item['scanner'] == 'zizmor':
        identity += (item.get('feature_sha256'),)
    return identity


def apply_exceptions(findings, exceptions):
    for entry in exceptions:
        if entry['scanner'] == 'zizmor':
            require(bool(entry.get('feature_sha256')), 'Zizmor exceptions require an exact feature selector')
    allowed = {finding_identity(e) for e in exceptions}
    for result in findings:
        result['excepted'] = finding_identity(result) in allowed
    return findings


def stage_sources(destination, inventory):
    extensions = {'.cs', '.ts', '.tsx', '.js', '.jsx', '.mjs', '.cjs', '.json', '.yml', '.yaml', '.sh', '.py', '.csproj', '.props', '.targets', '.xml', '.config', '.sql'}
    files = subprocess.check_output(['git', 'ls-files', '-z']).decode().split('\0')
    destination = Path(destination)
    require(not destination.exists(), 'source staging directory already exists')
    entries = []
    for name in files:
        if not name:
            continue
        path = Path(name)
        if name.startswith(('docs/agents/', '.agents/', '.codex/', 'docfx/')) or path.name in ('pnpm-lock.yaml', 'package-lock.json', 'packages.lock.json', 'skills-lock.json', 'issues.jsonl'):
            continue
        if path.suffix not in extensions and not path.name.startswith('Dockerfile'):
            continue
        require(not path.is_symlink() and path.is_file(), f'unsupported/unreadable source input: {name}')
        data = path.read_bytes()
        data.decode('utf-8')
        alias = name + ('.js' if path.suffix in ('.mjs', '.cjs') else '.sh' if path.name.startswith('Dockerfile') else '.xml' if path.suffix in ('.props', '.targets') else '')
        target = destination / alias
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        entries.append({'path': name, 'scan_path': alias, 'sha256': hashlib.sha256(data).hexdigest()})
    require(entries, 'no source inputs selected')
    Path(inventory).write_text(json.dumps({'files': entries, 'scope': 'Tracked application, generated source, scripts and configuration; excludes agent archives, tool metadata, vendored DocFX, prose and dependency locks. Dependencies are scanned separately.'}, indent=2) + '\n')
    print(f'Staged {len(entries)} tracked source inputs')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    sub.add_parser('profile')
    policy = sub.add_parser('policy')
    policy.add_argument('--exceptions', default='.github/ci/security-exceptions.json')
    gate = sub.add_parser('gate')
    gate.add_argument('--scanner', choices=sorted(SCANNERS), required=True)
    gate.add_argument('--report', required=True, nargs='+')
    gate.add_argument('--exceptions', default='.github/ci/security-exceptions.json')
    gate.add_argument('--output', required=True)
    gate.add_argument('--inventory')
    gate.add_argument('--scope-prefix')
    stage = sub.add_parser('stage')
    stage.add_argument('--destination', required=True)
    stage.add_argument('--inventory', required=True)
    args = parser.parse_args()
    if args.command == 'profile':
        selected = profile(os.environ.get('SECURITY_PROFILE', ''), os.environ.get('GITHUB_REPOSITORY', ''))
        if os.environ.get('GITHUB_OUTPUT'):
            with open(os.environ['GITHUB_OUTPUT'], 'a') as handle:
                handle.write(f'profile={selected}\n')
        print(selected)
    elif args.command == 'policy':
        validate_exceptions(json.loads(Path(args.exceptions).read_text()))
        if os.environ.get('GITHUB_EVENT_NAME') == 'pull_request':
            require(bool(re.fullmatch(r'(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\([a-zA-Z0-9_./-]+\))?!?: [^\r\n]+', os.environ.get('PR_TITLE', ''))), 'PR title must be a Conventional Commit')
        print('Security exception policy and PR title passed')
    elif args.command == 'stage':
        stage_sources(args.destination, args.inventory)
    else:
        exceptions = validate_exceptions(json.loads(Path(args.exceptions).read_text()))
        findings = []
        for path in args.report:
            findings.extend(globals()[args.scanner](json.loads(Path(path).read_text())))
        if args.inventory:
            inventory = json.loads(Path(args.inventory).read_text())
            paths = {item['scan_path']: item['path'] for item in inventory['files']}
            for item in findings:
                require(item['scope'] in paths, 'finding is outside source inventory')
                item['scope'] = paths[item['scope']]
        if args.scope_prefix:
            require(args.scanner == 'trivy', 'artifact scope prefix is only valid for vulnerability scans')
            for item in findings:
                item['scope'] = args.scope_prefix + '::' + item['scope']
        apply_exceptions(findings, exceptions)
        blocking = [f for f in findings if f['blocking'] and not f['excepted']]
        result = {'scanner': args.scanner, 'reports': args.report, 'findings': findings, 'exceptions': exceptions, 'blocking_count': len(blocking), 'result': 'fail' if blocking else 'pass'}
        Path(args.output).write_text(json.dumps(result, indent=2) + '\n')
        print(f'{args.scanner}: {len(findings)} findings, {len(blocking)} unexcepted blocking findings')
        for item in blocking:
            print(f"  {item['id']} {item['scope']}")
        if os.environ.get('GITHUB_STEP_SUMMARY'):
            with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as handle:
                handle.write(f"\n{args.scanner}: {len(findings)} findings, {len(blocking)} blocking. Full reports and applied exceptions are in the security artifacts.\n")
        return 1 if blocking else 0
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (ValueError, KeyError, TypeError, OSError) as error:
        print(f'Security execution/coverage error: {error}', file=sys.stderr)
        sys.exit(2)
