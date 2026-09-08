"""Prepare and reauthorize a sealed package-release plan under the protected controller."""

import argparse
from dataclasses import asdict
import hashlib
import json
import os
from pathlib import Path
import tempfile

from publication import PublicationError, load_catalog, matches, positive_integer
from publication_artifacts import select_artifact, unpack_payload
from publication_npm import PackageRegistry
from publication_package_configuration import package_configuration, package_environment
from publication_package_dependencies import publication_order
from publication_package_records import PREPARE_JOB, package_record
from publication_package_releases import package_releases
from publication_preparation_authorization import authorize_preparation
from publication_prepare_packages import prepare_packages
from publication_release_github import ReleaseGitHub
from publication_release_origin import frame
from publication_release_authorization import release_identity
from publication_selection import recovery_selector


def snapshot(authority):
    return {**authority, 'published': [{**record, 'package': asdict(record['package'])} for record in authority['published']]}


def policy_digest(root):
    return 'sha256:' + hashlib.sha256((Path(root) / '.github/ci/security-exceptions.json').read_bytes()).hexdigest()


def batch(plan, repository):
    prepared = plan['prepared']
    candidates = [{**item, 'package': package_record(item['package'], item['package']['name'], item['release']['version'], repository)} for item in prepared['candidates']]
    published = [{**item, 'package': package_record(item['package'], item['package']['name'], item['release']['version'], repository)} for item in prepared['published']]
    target_id = prepared['target_release_id']
    targets = [(item['package'].name, item['package'].version) for item in candidates + published if item in candidates or item.get('needs_endorsement')]
    if target_id is not None:
        matching = [item for item in candidates + published if item['release']['id'] == target_id]
        if len(matching) != 1:
            raise PublicationError('Requested package release is still pending its exact origin or producer')
        targets = [(matching[0]['package'].name, matching[0]['package'].version)]
    return candidates, published, targets


def dependency_preflight(plan, repository, registry):
    candidates, published, targets = batch(plan, repository)
    ordered = publication_order(candidates, published, plan['catalog'], registry, targets)
    return ordered


def prepare(client, context, producer_run, producer_attempt, run_id, attempt, catalog, root, destination, token, release_id=None, *, recovery=None):
    if release_id is not None:
        client.controller(context)
        target = release_identity(client, client.get('/releases/' + str(release_id)), catalog)
        if target['id'] != release_id:
            raise PublicationError('Requested release API identity differs from its exact target')
        component = next(item for item in catalog['components'] if item['id'] == target['component'])
        if 'package' not in component:
            return {'schema': 1, 'unrelated_release_id': release_id, 'publication_authorized': False}
    package_configuration(client.repository, catalog, root)
    package_environment(client)
    invocation, _ = frame(client, context, run_id, attempt, PREPARE_JOB)
    trigger, _, _, _ = authorize_preparation(client, context, producer_run, producer_attempt, run_id, attempt, recovery=recovery, catalog=catalog)
    authority = package_releases(client, context, catalog, release_id, (producer_run, producer_attempt) if release_id else None, recovery=recovery)
    destination = Path(destination)
    destination.mkdir(parents=True)
    prepared = prepare_packages(client, authority, catalog, destination / 'bytes', destination / 'scans')
    plan = {'schema': 1, 'invocation': invocation, 'trigger_producer': trigger['producer'], 'catalog': catalog, 'authority': snapshot(authority),
            'policy_sha256': policy_digest(root), 'prepared': prepared, 'publication_authorized': False}
    with PackageRegistry(catalog['package_scope'], token) as registry:
        order = dependency_preflight(plan, client.repository, registry)
    plan['dependency_readiness'] = order['dependencies']
    plan['ordered_release_ids'] = [item['candidate']['release']['id'] for item in order['ordered']]
    return plan


def validate_prepared(plan, authority, repository):
    prepared = plan.get('prepared')
    if not isinstance(prepared, dict) or prepared.get('published') != snapshot(authority)['published'] or prepared.get('pending') != authority['pending'] or prepared.get('target_release_id') != authority['target_release_id'] or prepared.get('publication_authorized') is not False:
        raise PublicationError('Prepared package coverage differs from authorized release state')
    candidates = prepared.get('candidates')
    expected = {item['release']['id']: item for item in authority['ready']}
    if not isinstance(candidates, list) or len(candidates) != len(expected):
        raise PublicationError('Prepared package candidates are incomplete or duplicated')
    seen = set()
    for item in candidates:
        release_id = item.get('release', {}).get('id') if isinstance(item, dict) else None
        if release_id not in expected or release_id in seen:
            raise PublicationError('Prepared package candidate has an unexpected release identity')
        seen.add(release_id)
        source = expected[release_id]
        recovery = {'receipt': source['plan']['recovery']['receipt'], 'producer': source['plan']['producer']} if 'recovery' in source['plan'] else None
        if item.get('recovery') != recovery or ('recovery' in item and recovery is None):
            raise PublicationError('Prepared package recovery differs from its authenticated producer and receipt')
        for key in ('release', 'origin', 'selection'):
            if item.get(key) != source[key]:
                raise PublicationError('Prepared package differs from its exact durable release receipt')
        if item.get('producer') != source['plan']['producer'] or item.get('registration') != source['plan']['registration'] or item.get('file') != f'release-{release_id}.tgz' or not positive_integer(item.get('size')) or item['size'] > 100 * 1024 * 1024:
            raise PublicationError('Prepared tarball differs from its exact producer or archive identity')
        package_record(item.get('package'), source['component']['package']['name'], source['release']['version'], repository)
        dependency = [artifact for artifact in source['plan']['artifacts'] if artifact['kind'] == 'dependencies']
        scan = item.get('dependency_scan', {})
        if len(dependency) != 1 or scan.get('artifact_id') != dependency[0]['id'] or scan.get('blocking_count') != 0 or not isinstance(scan.get('reports'), list) or not scan['reports']:
            raise PublicationError('Prepared package lacks its exact fresh dependency scan')
    archive = prepared.get('archive', {})
    if archive.get('file') != 'packages.tar' or not positive_integer(archive.get('size')) or archive['size'] > 1024 * 1024 * 1024 or not matches(r'[a-f0-9]{64}', archive.get('sha256')):
        raise PublicationError('Prepared package archive lacks exact bounded identity')


def authorize_packages(client, context, producer_run, producer_attempt, run_id, attempt, catalog, root, release_id=None, *, recovery=None):
    package_configuration(client.repository, catalog, root)
    package_environment(client)
    trigger, producer, artifacts, _ = authorize_preparation(client, context, producer_run, producer_attempt, run_id, attempt, recovery=recovery, catalog=catalog)
    invocation, _ = frame(client, context, run_id, attempt, PREPARE_JOB, 'success')
    selected = select_artifact(artifacts, producer, 'package-plan')
    if selected.size > 17 * 1024 * 1024:
        raise PublicationError('Package plan artifact exceeds its bounded download size')
    with tempfile.TemporaryDirectory(prefix='wallow-package-plan-') as directory:
        temporary = Path(directory)
        archive = client.download(selected, temporary / 'plan.zip')
        path = unpack_payload(archive, temporary / 'verified', selected, producer, 'plan.json', 'package-plan', 'release', 16 * 1024 * 1024)
        plan = json.loads(path.read_text())
    authority = package_releases(client, context, catalog, release_id, (producer_run, producer_attempt) if release_id else None, recovery=recovery)
    if not isinstance(plan, dict) or plan.get('schema') != 1 or plan.get('invocation') != invocation or plan.get('trigger_producer') != trigger['producer'] or plan.get('catalog') != catalog or plan.get('authority') != snapshot(authority) or plan.get('policy_sha256') != policy_digest(root) or plan.get('publication_authorized') is not False:
        raise PublicationError('Prepared package plan differs from current exact release authorization and policy')
    validate_prepared(plan, authority, client.repository)
    artifact = select_artifact(artifacts, producer, 'prepared-packages')
    if artifact.size > 1025 * 1024 * 1024:
        raise PublicationError('Prepared package artifact exceeds its bounded download size')
    return plan, producer, artifact, {'plan_artifact': asdict(selected), 'prepare_job_id': invocation['job_id']}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--producer-run', required=True)
    parser.add_argument('--producer-attempt', required=True)
    parser.add_argument('--release-id', default='')
    parser.add_argument('--output', required=True)
    parser.add_argument('--recovery-run', default='')
    parser.add_argument('--recovery-attempt', default='')
    args = parser.parse_args()
    try:
        recovery = recovery_selector(args.recovery_run, args.recovery_attempt, args.release_id)
        values = (args.producer_run, args.producer_attempt, os.environ.get('GITHUB_RUN_ID'), os.environ.get('GITHUB_RUN_ATTEMPT'))
        if os.environ.get('ENABLE_PACKAGE_PUBLISH') != 'true' or not all(matches(r'[1-9][0-9]*', value) for value in values) or (args.release_id and not matches(r'[1-9][0-9]*', args.release_id)):
            raise PublicationError('Package preparation requires literal enablement and exact invocation identities')
        context = {key: os.environ.get('GITHUB_' + key.upper(), '') for key in ('repository', 'ref', 'workflow_ref', 'workflow_sha', 'event_name')}
        root = Path(__file__).resolve().parents[2]
        token = os.environ.get('GH_TOKEN')
        client = ReleaseGitHub(context['repository'], token)
        output = Path(args.output)
        plan = prepare(client, context, *(int(value) for value in values), load_catalog(root), root, output.parent, token, int(args.release_id) if args.release_id else None, recovery=recovery)
        output.parent.mkdir(parents=True, exist_ok=True)
        with open(os.environ['GITHUB_OUTPUT'], 'a') as stream:
            stream.write('eligible=' + ('false' if 'unrelated_release_id' in plan else 'true') + '\n')
        with output.open('x') as stream:
            json.dump(plan, stream, indent=2)
            stream.write('\n')
    except PublicationError as error:
        parser.exit(1, str(error) + '\n')
    except (OSError, ValueError, TypeError, KeyError, RecursionError):
        parser.exit(1, 'Package release preparation failed.\n')


if __name__ == '__main__':
    main()
