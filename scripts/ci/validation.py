"""Fail-closed routing, aggregate results, and artifact identity for shared CI."""

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import subprocess


def docs_path(path):
    parts = PurePosixPath(path).parts
    if not parts or path.startswith('/') or any(p in ('.', '..') for p in path.split('/')):
        return False
    return path in {'README.md', 'CONTRIBUTING.md', 'CODE_OF_CONDUCT.md', 'SECURITY.md'} or (
        len(parts) > 1 and parts[0] == 'docs' and (path.endswith('.md') or parts[-1] == 'toc.yml')
    )


def classify_diff(data):
    if not data or not data.endswith(b'\0'):
        return 'full', 'empty or malformed diff'
    try:
        fields = data.decode('utf-8', errors='strict').split('\0')[:-1]
        paths = []
        while fields:
            status = fields.pop(0)
            if not re.fullmatch(r'[AMDT]|[RC][0-9]{1,3}', status):
                raise ValueError()
            count = 2 if status[0] in 'RC' else 1
            if len(fields) < count or any(not path for path in fields[:count]):
                raise ValueError()
            paths.extend(fields[:count])
            del fields[:count]
    except (UnicodeDecodeError, ValueError):
        return 'full', 'empty or malformed diff'
    if all(docs_path(path) for path in paths):
        return 'docs', 'all changed paths are in the documentation allowlist'
    return 'full', 'changed paths require full validation'


def classify(event, base, head):
    if event not in ('pull_request', 'push') or not all(re.fullmatch(r'[0-9a-fA-F]{40}', ref or '') and ref != '0' * 40 for ref in (base, head)):
        return 'full', 'missing or invalid comparison commits'
    comparison = f'{base}{"..." if event == "pull_request" else ".."}{head}'
    try:
        data = subprocess.check_output(['git', 'diff', '--name-status', '-z', '--find-renames', comparison, '--'], stderr=subprocess.DEVNULL)
    except subprocess.CalledProcessError:
        return 'full', 'comparison commits could not be resolved'
    return classify_diff(data)


def gate(route, needs, common, full, profile='codeql'):
    expected = common + full
    if profile not in ('portable', 'codeql') or route not in ('docs', 'full') or not common or not full or len(set(expected)) != len(expected):
        raise ValueError('invalid route or required job configuration')
    if not isinstance(needs, dict) or set(needs) != set(expected):
        raise ValueError('required job set is missing or unexpected')
    for job in expected:
        required = 'skipped' if (route == 'docs' and job in full) or (profile == 'portable' and job == 'codeql') else 'success'
        if not isinstance(needs[job], dict) or needs[job].get('result') != required:
            raise ValueError(f'required job {job} did not report {required}')


def artifact_identity(kind, variant):
    identity = {
        'schema': 1,
        'repository': os.environ.get('GITHUB_REPOSITORY', ''),
        'sha': os.environ.get('GITHUB_SHA', ''),
        'run_id': os.environ.get('GITHUB_RUN_ID', ''),
        'run_attempt': os.environ.get('GITHUB_RUN_ATTEMPT', ''),
        'workflow_ref': os.environ.get('GITHUB_WORKFLOW_REF', ''),
        'kind': kind,
        'variant': variant,
    }
    if not re.fullmatch(r'[^/\s]+/[^/\s]+', identity['repository']) or not re.fullmatch(r'[0-9a-f]{40}', identity['sha']):
        raise ValueError('invalid artifact repository or revision')
    workflow_prefix = identity['repository'] + '/.github/workflows/'
    if not identity['workflow_ref'].startswith(workflow_prefix) or not re.fullmatch(r'[^@\s]+@[^@\s]+', identity['workflow_ref'][len(workflow_prefix):]):
        raise ValueError('invalid artifact workflow reference')
    if any(not re.fullmatch(r'[1-9][0-9]*', identity[key]) for key in ('run_id', 'run_attempt')) or not kind or not variant:
        raise ValueError('invalid artifact run or variant')
    recovery = {key: os.environ.get(name, '') for key, name in (
        ('source_sha', 'CI_RECOVERY_SOURCE_SHA'), ('recovery_request_sha256', 'CI_RECOVERY_REQUEST_SHA256'),
        ('release_id', 'CI_RECOVERY_RELEASE_ID'))}
    if any(recovery.values()):
        if os.environ.get('GITHUB_EVENT_NAME') != 'workflow_dispatch' or os.environ.get('GITHUB_REF') != 'refs/heads/main' or identity['workflow_ref'] != identity['repository'] + '/.github/workflows/ci.yml@refs/heads/main' or identity['sha'] != os.environ.get('GITHUB_WORKFLOW_SHA'):
            raise ValueError('recovery artifacts require the exact main CI dispatch controller')
        if not re.fullmatch(r'[0-9a-f]{40}', recovery['source_sha']) or not re.fullmatch(r'sha256:[0-9a-f]{64}', recovery['recovery_request_sha256']) or not re.fullmatch(r'[1-9][0-9]*', recovery['release_id']):
            raise ValueError('recovery artifacts require complete exact source and request identity')
        identity.update(schema=2, **(recovery | {'release_id': int(recovery['release_id'])}))
    return identity


def artifact(command, file, manifest, kind, variant):
    identity = artifact_identity(kind, variant)
    payload = Path(file)
    if not payload.is_file() or payload.is_symlink():
        raise ValueError('artifact payload must be a regular file')
    with payload.open('rb') as stream:
        checksum = hashlib.file_digest(stream, 'sha256').hexdigest()
    expected = {**identity, 'file': payload.name, 'sha256': checksum}
    if command == 'seal':
        Path(manifest).write_text(json.dumps(expected, indent=2) + '\n')
    elif json.loads(Path(manifest).read_text()) != expected:
        raise ValueError('artifact identity or checksum does not match')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    route = commands.add_parser('classify')
    route.add_argument('--event', required=True)
    route.add_argument('--base', default='')
    route.add_argument('--head', default='')
    aggregate = commands.add_parser('gate')
    aggregate.add_argument('--route', required=True)
    aggregate.add_argument('--needs-json', required=True)
    aggregate.add_argument('--common', required=True)
    aggregate.add_argument('--full', required=True)
    aggregate.add_argument('--profile', choices=['portable', 'codeql'], default='codeql')
    for command in ('seal', 'verify'):
        bundle = commands.add_parser(command)
        for option in ('file', 'manifest', 'kind', 'variant'):
            bundle.add_argument('--' + option, required=True)
    args = parser.parse_args()
    if args.command in ('seal', 'verify'):
        try:
            artifact(args.command, args.file, args.manifest, args.kind, args.variant)
        except (ValueError, OSError):
            parser.exit(1, 'Artifact identity or checksum validation failed.\n')
        print('Artifact manifest ' + ('sealed.' if args.command == 'seal' else 'verified.'))
        return
    if args.command == 'gate':
        try:
            gate(args.route, json.loads(args.needs_json), args.common.split(','), args.full.split(','), args.profile)
        except (ValueError, TypeError):
            parser.exit(1, 'Required validation did not complete successfully.\n')
        print('All required validation completed successfully.')
        return
    route, reason = classify(args.event, args.base, args.head)
    output = f'route={route}\nreason={reason}\n'
    if os.environ.get('GITHUB_OUTPUT'):
        with open(os.environ['GITHUB_OUTPUT'], 'a') as stream:
            stream.write(output)
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as stream:
            stream.write(f'Validation route: **{route}** — {reason}.\n')
    print(output, end='')


if __name__ == '__main__':
    main()
