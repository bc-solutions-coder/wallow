"""Publish immutable package versions from sealed preparation, never rebuilding or packing source."""

import argparse
from dataclasses import asdict
import hashlib
import json
import os
from pathlib import Path
import tempfile

from publication import PublicationError, load_catalog, matches
from publication_artifacts import unpack_payload
from publication_npm import PackageRegistry
from publication_package_preparation import authorize_packages, dependency_preflight
from publication_package_records import WRITER_JOB
from publication_packages import inspect_package
from publication_release_github import ReleaseGitHub
from publication_release_origin import frame
from publication_selection import recovery_selector
from publication_verify_packages import extract_prepared_candidates


def publish(client, context, producer_run, producer_attempt, run_id, attempt, catalog, root, token, result, release_id=None, *, recovery=None):
    invocation, _ = frame(client, context, run_id, attempt, WRITER_JOB)
    result['invocation'] = invocation
    plan, preparation, artifact, evidence = authorize_packages(client, context, producer_run, producer_attempt, run_id, attempt, catalog, root, release_id, recovery=recovery)
    result['preparation'] = evidence
    with tempfile.TemporaryDirectory(prefix='wallow-package-writer-') as directory:
        temporary = Path(directory)
        archive = client.download(artifact, temporary / 'prepared.zip')
        payload = unpack_payload(archive, temporary / 'verified', artifact, preparation, 'packages.tar', 'prepared-packages', 'release', 1024 * 1024 * 1024)
        with payload.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        if digest != plan['prepared']['archive']['sha256'] or payload.stat().st_size != plan['prepared']['archive']['size']:
            raise PublicationError('Prepared package bytes differ from the authorized plan')
        packed = temporary / 'packages'
        packed.mkdir()
        expected = {item['file'] for item in plan['prepared']['candidates']}
        extract_prepared_candidates(payload, packed, expected)
        for item in plan['prepared']['candidates']:
            package = inspect_package(packed / item['file'], item['package']['name'], item['release']['version'], catalog['package_registry'], client.repository)
            if asdict(package) != item['package'] or (packed / item['file']).stat().st_size != item['size']:
                raise PublicationError('Prepared package manifest or bytes differ from exact release authorization')
        with PackageRegistry(catalog['package_scope'], token) as registry:
            order = dependency_preflight(plan, client.repository, registry)
            if order['dependencies'] != plan['dependency_readiness'] or [item['candidate']['release']['id'] for item in order['ordered']] != plan['ordered_release_ids']:
                raise PublicationError('Internal package dependency resolution changed after preparation')
            completed = {}
            for item in order['ordered']:
                candidate = item['candidate']
                readback = registry.publish(packed / candidate['file'], candidate['package'])
                completed[candidate['release']['id']] = readback
                result['entries'].append(entry(candidate, readback))
            for candidate in order['verified']:
                if candidate['release']['id'] in completed:
                    continue
                package = candidate['package']
                readback = {'name': package.name, 'version': package.version, 'sha256': package.sha256, 'integrity': package.integrity,
                            'state': 'already-published', 'registry_bytes_verified': True}
                result['entries'].append(entry(candidate, readback))
    return result


def entry(candidate, readback):
    result = {key: candidate[key] for key in ('release', 'origin', 'selection')} | {'package': asdict(candidate['package']), 'readback': readback}
    if 'recovery' in candidate:
        result['recovery'] = candidate['recovery']
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--producer-run', required=True)
    parser.add_argument('--producer-attempt', required=True)
    parser.add_argument('--release-id', default='')
    parser.add_argument('--output', required=True)
    parser.add_argument('--recovery-run', default='')
    parser.add_argument('--recovery-attempt', default='')
    args = parser.parse_args()
    result = {'schema': 1, 'scope': 'immutable-package-versions', 'entries': []}
    error = None
    try:
        recovery = recovery_selector(args.recovery_run, args.recovery_attempt, args.release_id)
        values = (args.producer_run, args.producer_attempt, os.environ.get('GITHUB_RUN_ID'), os.environ.get('GITHUB_RUN_ATTEMPT'))
        if os.environ.get('ENABLE_PACKAGE_PUBLISH') != 'true' or not all(matches(r'[1-9][0-9]*', value) for value in values) or (args.release_id and not matches(r'[1-9][0-9]*', args.release_id)):
            raise PublicationError('Package writer requires literal enablement and exact invocation identities')
        context = {key: os.environ.get('GITHUB_' + key.upper(), '') for key in ('repository', 'ref', 'workflow_ref', 'workflow_sha', 'event_name')}
        root = Path(__file__).resolve().parents[2]
        token = os.environ.get('GH_TOKEN')
        client = ReleaseGitHub(context['repository'], token)
        publish(client, context, *(int(value) for value in values), load_catalog(root), root, token, result, int(args.release_id) if args.release_id else None, recovery=recovery)
    except (PublicationError, OSError, ValueError, TypeError, KeyError, RecursionError):
        error = 'Immutable package publication failed; preserved progress must be verified before retry.'
        result['error'] = error
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open('x') as stream:
        json.dump(result, stream, indent=2)
        stream.write('\n')
    if error:
        parser.exit(1, error + '\n')


if __name__ == '__main__':
    main()
