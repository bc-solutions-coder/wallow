"""Reauthorize current protected preparation before a main image writer uses credentials."""

from dataclasses import asdict
import json
from pathlib import Path
import tempfile

from publication import Producer, PublicationError, positive_integer
from publication_artifacts import select_artifact, unpack_payload


def image_environment(client):
    environment = client.get('/environments/image-publish')
    if not isinstance(environment, dict) or environment.get('name') != 'image-publish' or environment.get('deployment_branch_policy') != {'protected_branches': False, 'custom_branch_policies': True}:
        raise PublicationError('Enabled image publication requires image-publish restricted to the main branch')
    rules = client.list('/environments/image-publish/deployment-branch-policies', 'branch_policies')
    if len(rules) != 1 or rules[0].get('name') != 'main' or rules[0].get('type') != 'branch':
        raise PublicationError('image-publish must allow only the exact main branch')


def authorize_images(client, context, run_id, attempt, invocation_run, invocation_attempt):
    from publication_plan import resolve

    image_environment(client)
    fresh = resolve(client, context, run_id, attempt)
    run = client.get(f'/actions/runs/{invocation_run}/attempts/{invocation_attempt}')
    workflow = client.get('/actions/workflows/publish.yml')
    source = fresh['producer']
    if not positive_integer(invocation_run) or not positive_integer(invocation_attempt) or not isinstance(run, dict) or not isinstance(workflow, dict):
        raise PublicationError('Image writer invocation identity is missing')
    if run.get('id') != invocation_run or run.get('run_attempt') != invocation_attempt or not positive_integer(workflow.get('id')) or run.get('workflow_id') != workflow['id'] or workflow.get('path') != '.github/workflows/publish.yml' or run.get('path') != workflow['path'] or run.get('head_sha') != fresh['controller_sha'] or run.get('head_branch') != 'main' or run.get('event') != context['event_name']:
        raise PublicationError('Image writer is not the authorized protected main invocation')
    for key in ('repository', 'head_repository'):
        repository = run.get(key, {})
        if repository.get('id') != source['repository_id'] or repository.get('full_name') != source['repository']:
            raise PublicationError('Image writer belongs to another repository')
    jobs = client.list(f'/actions/runs/{invocation_run}/attempts/{invocation_attempt}/jobs', 'jobs')
    gates = [job for job in jobs if job.get('name') == 'authorize']
    if len(gates) != 1 or not positive_integer(gates[0].get('id')) or gates[0].get('status') != 'completed' or gates[0].get('conclusion') != 'success' or gates[0].get('head_sha') != fresh['controller_sha'] or gates[0].get('run_id') != invocation_run or gates[0].get('run_attempt') != invocation_attempt:
        raise PublicationError('This exact preparation job did not complete successfully')
    preparation = Producer(source['repository'], source['repository_id'], fresh['controller_sha'], invocation_run, invocation_attempt, workflow['id'], context['workflow_ref'])
    artifacts = client.list(f'/actions/runs/{invocation_run}/artifacts', 'artifacts')
    selected = select_artifact(artifacts, preparation, 'publication-plan')
    with tempfile.TemporaryDirectory(prefix='wallow-writer-plan-') as directory:
        root = Path(directory)
        archive = client.download(selected, root / 'plan.zip')
        payload = unpack_payload(archive, root / 'verified', selected, preparation, 'plan.json', 'publication-plan', 'authorized', 16 * 1024 * 1024)
        plan = json.loads(payload.read_text())
    if not isinstance(plan, dict) or {key: plan.get(key) for key in fresh} != fresh:
        raise PublicationError('Sealed publication plan differs from fresh producer authorization')
    required = ('app', 'infra', 'docs') if plan['route'] == 'full' else ('docs',)
    if set(plan.get('verified_images', {})) != set(required):
        raise PublicationError('Prepared image bundle coverage differs from the authorized route')
    prepared = {bundle: select_artifact(artifacts, preparation, 'prepared-images-' + bundle) for bundle in required}
    return plan, preparation, prepared, {'plan_artifact': asdict(selected), 'authorize_job_id': gates[0]['id']}
