"""Freshly prepare stable alias transitions, then reauthorize them in isolated writers."""

import json
from pathlib import Path
import tempfile

from publication import PublicationError
from publication_alias_authorization import publication_records
from publication_alias_registry import Registry, plan_aliases
from publication_artifacts import select_artifact, unpack_payload
from publication_image_authorization import image_environment
from publication_package_configuration import package_configuration, package_environment
from publication_package_dependencies import publication_order
from publication_package_records import package_record
from publication_plan import resolve
from publication_preparation_authorization import authorize_preparation
from publication_prepare_images import ImagePreparation
from publication_release_image_authorization import policy_digest
from publication_release_origin import frame
from publication_release_selection import validate_candidate
from publication_verify_dependencies import verify_dependencies


def job(kind, phase):
    if kind not in ('image', 'package') or phase not in ('Prepare', 'Promote', 'Record'):
        raise PublicationError('Unknown protected alias job identity')
    return f'{phase} {kind} aliases'


def configuration(client, catalog, root, kind):
    if kind == 'image':
        image_environment(client)
    else:
        package_configuration(client.repository, catalog, root)
        package_environment(client)


def dependencies(client, records, registry, catalog, targets):
    published = [{**record, 'package': package_record(record['outputs']['package'], record['outputs']['package']['name'], record['release']['version'], client.repository)} for record in records]
    selected = [(item['package'].name, item['package'].version) for item in published if item['release']['id'] in targets]
    return publication_order([], published, catalog, registry.packages, selected)['dependencies']


def prepare(client, context, producer_run, producer_attempt, run_id, attempt, kind, catalog, root, output, token, username, target=None):
    authorize_preparation(client, context, producer_run, producer_attempt, run_id, attempt)
    invocation, _ = frame(client, context, run_id, attempt, job(kind, 'Prepare'))
    records, unrelated = publication_records(client, context, catalog, kind, target, (producer_run, producer_attempt) if target else None)
    plan = {'schema': 1, 'kind': kind, 'invocation': invocation, 'records': records, 'target': target, 'unrelated': unrelated,
            'policy_sha256': policy_digest(root), 'entries': [], 'scans': {}, 'dependencies': [], 'publication_authorized': False}
    if unrelated:
        return plan
    configuration(client, catalog, root, kind)
    output = Path(output)
    reports = output / 'scans'
    with Registry(kind, client.repository, catalog, token, username) as registry:
        plan['entries'] = plan_aliases(client, records, kind, registry, target)
        targets = {entry['release_id'] for entry in plan['entries'] if entry['action'] == 'advance'}
        if kind == 'package':
            plan['dependencies'] = dependencies(client, records, registry, catalog, targets)
            for record in records:
                if record['release']['id'] not in targets:
                    continue
                identity = record['selected']['producer']
                try:
                    source = resolve(client, context, identity['run_id'], identity['run_attempt'])
                    if validate_candidate(source, record['release'], catalog) != record['selected']:
                        raise PublicationError('Selected alias producer changed')
                except PublicationError:
                    raise PublicationError('Package alias target inputs are unavailable or changed; explicit recovery of the selected source is required') from None
                reports.mkdir(parents=True, exist_ok=True)
                plan['scans'][str(record['release']['id'])] = verify_dependencies(client, source, reports / str(record['release']['id']))
        elif targets:
            reports.parent.mkdir(parents=True, exist_ok=True)
            scanner = ImagePreparation({}, catalog, output / 'unused', reports, controller=root)
            scanner.start()
            def scan(record, image, platform, directory, source):
                release_id = str(record['release']['id'])
                name = release_id + '-' + image['image'] + '-' + platform.split('/')[1]
                tag = next(item for item in catalog['images'] if item['id'] == image['image'])['tags'][platform]
                with tempfile.TemporaryDirectory(prefix='wallow-alias-scan-input-') as temporary:
                    reports_ = scanner.scan(directory, source, tag, name, Path(temporary))
                plan['scans'].setdefault(release_id, {}).update(reports_)
            registry.scanner = scan
            for record in records:
                if record['release']['id'] in targets:
                    registry.verify(record, fresh=True)
    return plan


def authorize_plan(client, context, producer_run, producer_attempt, run_id, attempt, kind, catalog, root, target=None):
    _, producer, artifacts, _ = authorize_preparation(client, context, producer_run, producer_attempt, run_id, attempt)
    invocation, _ = frame(client, context, run_id, attempt, job(kind, 'Prepare'), 'success')
    selected = select_artifact(artifacts, producer, kind + '-alias-plan')
    if selected.size > 17 * 1024 * 1024:
        raise PublicationError('Alias plan artifact exceeds its bounded size')
    with tempfile.TemporaryDirectory(prefix='wallow-alias-plan-') as directory:
        temporary = Path(directory)
        archive = client.download(selected, temporary / 'plan.zip')
        path = unpack_payload(archive, temporary / 'verified', selected, producer, 'plan.json', 'alias-plan', kind, 16 * 1024 * 1024)
        plan = json.loads(path.read_text())
    records, unrelated = publication_records(client, context, catalog, kind, target, (producer_run, producer_attempt) if target else None)
    if not isinstance(plan, dict) or plan.get('schema') != 1 or plan.get('kind') != kind or plan.get('invocation') != invocation or plan.get('records') != records or plan.get('target') != target or plan.get('unrelated') != unrelated or plan.get('policy_sha256') != policy_digest(root) or plan.get('publication_authorized') is not False:
        raise PublicationError('Alias preparation differs from current exact release authority or policy')
    if not unrelated:
        configuration(client, catalog, root, kind)
    return plan


def promote(client, context, producer_run, producer_attempt, run_id, attempt, kind, catalog, root, token, username, progress, record, target=None):
    invocation, _ = frame(client, context, run_id, attempt, job(kind, 'Promote'))
    progress['invocation'] = invocation
    plan = authorize_plan(client, context, producer_run, producer_attempt, run_id, attempt, kind, catalog, root, target)
    progress['records'] = plan['records']
    if plan['unrelated']:
        return progress
    with Registry(kind, client.repository, catalog, token, username, access='write') as registry:
        entries = plan_aliases(client, plan['records'], kind, registry, target)
        if entries != plan['entries']:
            raise PublicationError('Alias state changed after its exact preparation')
        targets = {entry['release_id'] for entry in entries if entry['action'] == 'advance'}
        if set(plan['scans']) != {str(value) for value in targets} or any(not value for value in plan['scans'].values()):
            raise PublicationError('Alias promotion lacks fresh scan coverage for every changing target')
        if kind == 'package' and dependencies(client, plan['records'], registry, catalog, targets) != plan['dependencies']:
            raise PublicationError('Package dependency readiness changed before alias promotion')
        indexed = {item['release']['id']: item for item in plan['records']}
        for entry in entries:
            outcome = entry['action']
            if outcome == 'advance':
                outcome = registry.promote(entry['output'], entry['alias'], indexed[entry['release_id']], entry['previous'])
            progress['entries'].append(entry | {'outcome': outcome})
            record()
    return progress
