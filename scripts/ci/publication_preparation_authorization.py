"""Reauthorize an exact protected preparation before any publication writer."""

from dataclasses import asdict
import json
from pathlib import Path
import tempfile

from publication import Producer, PublicationError, positive_integer
from publication_artifacts import select_artifact, unpack_payload


def authorize_preparation(client, context, run_id, attempt, invocation_run, invocation_attempt, *, recovery=None, catalog=None):
    from publication_plan import resolve

    if recovery is None:
        fresh = resolve(client, context, run_id, attempt)
    else:
        from publication_selection import resolve_selection

        if not isinstance(catalog, dict):
            raise PublicationError('Recovery preparation requires the current publication catalog')
        fresh = resolve_selection(client, context, run_id, attempt, catalog, recovery)
    run = client.get(f'/actions/runs/{invocation_run}/attempts/{invocation_attempt}')
    workflow = client.get('/actions/workflows/publish.yml')
    source = fresh['producer']
    if not positive_integer(invocation_run) or not positive_integer(invocation_attempt) or not isinstance(run, dict) or not isinstance(workflow, dict):
        raise PublicationError('Publication writer invocation identity is missing')
    if run.get('id') != invocation_run or run.get('run_attempt') != invocation_attempt or not positive_integer(workflow.get('id')) or run.get('workflow_id') != workflow['id'] or workflow.get('path') != '.github/workflows/publish.yml' or run.get('path') != workflow['path'] or run.get('head_sha') != fresh['controller_sha'] or run.get('head_branch') != 'main' or run.get('event') != context['event_name']:
        raise PublicationError('Publication writer is not the authorized protected main invocation')
    for key in ('repository', 'head_repository'):
        repository = run.get(key, {})
        if repository.get('id') != source['repository_id'] or repository.get('full_name') != source['repository']:
            raise PublicationError('Publication writer belongs to another repository')
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
    if not isinstance(plan, dict) or (recovery is None and 'recovery' in plan) or {key: plan.get(key) for key in fresh} != fresh:
        raise PublicationError('Sealed publication plan differs from fresh producer authorization')
    return plan, preparation, artifacts, {'plan_artifact': asdict(selected), 'authorize_job_id': gates[0]['id']}
