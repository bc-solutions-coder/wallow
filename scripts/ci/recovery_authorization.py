"""Authenticate a completed recovery controller without granting publication authority."""

import hashlib
import json
from pathlib import Path
import tempfile

from publication import PublicationError, _authorize_ci_invocation, matches, positive_integer
from publication_artifacts import select_artifact, unpack_payload
from publication_identity import RecoveryProducer
from recovery_request import request_record


def completed_controller(client, run_id, attempt):
    """Fetch one exact attempt and its Actions check from the fixed repository API."""
    if not all(positive_integer(value) for value in (run_id, attempt)):
        raise PublicationError('Recovery requires an explicit positive run and attempt')
    repository = client.get('')
    workflow = client.get('/actions/workflows/ci.yml')
    run = client.get(f'/actions/runs/{run_id}/attempts/{attempt}')
    jobs = client.list(f'/actions/runs/{run_id}/attempts/{attempt}/jobs', 'jobs')
    if not isinstance(repository, dict) or repository.get('full_name') != client.repository:
        raise PublicationError('Recovery repository differs from the requested API repository')
    if not isinstance(run, dict) or not matches(r'[0-9a-f]{40}', run.get('head_sha')) or not isinstance(jobs, list) or any(not isinstance(job, dict) for job in jobs):
        raise PublicationError('Recovery invocation metadata is malformed')
    gates = [job for job in jobs if job.get('name') == 'CI / required']
    if len(gates) != 1:
        raise PublicationError('Recovery requires one exact aggregate job')
    prefix = f'https://api.github.com/repos/{client.repository}/check-runs/'
    url = gates[0].get('check_run_url')
    if not isinstance(url, str) or not url.startswith(prefix) or not matches(r'[1-9][0-9]*', url[len(prefix):]):
        raise PublicationError('Recovery aggregate check belongs to an unexpected API location')
    check = client.get('/check-runs/' + url[len(prefix):])
    controller = authorize_completed_controller(repository, workflow, run, run_id, attempt, jobs, check,
                                                client.main_comparison(run['head_sha']))
    return controller, jobs


def authorize_completed_controller(repository, workflow, run, run_id, attempt, jobs, required_check, comparison):
    controller = _authorize_ci_invocation(repository, workflow, run, run_id, attempt, jobs, required_check, comparison,
                                          'workflow_dispatch')
    expected = {'build': 'success', 'js / local': 'success', 'js / main': 'skipped',
                'js / complete': 'success', 'register-publication': 'success'}
    for name, conclusion in expected.items():
        selected = [job for job in jobs if job.get('name') == name]
        if len(selected) != 1:
            raise PublicationError('Recovery invocation lacks exact full-route job evidence')
        job = selected[0]
        if not all(positive_integer(job.get(key)) for key in ('id', 'run_id', 'run_attempt')) or any(
            job.get(key) != value for key, value in {'run_id': run_id, 'run_attempt': attempt,
                                                   'head_sha': controller.source_sha, 'status': 'completed',
                                                   'conclusion': conclusion}.items()
        ):
            raise PublicationError('Recovery job does not prove the exact credential-free full validation attempt')
    return controller


def authenticate_request(client, controller, artifacts, catalog):
    """Read the controller-bound request and recheck its original release authority."""
    selected = select_artifact(artifacts, controller, 'recovery-request')
    with tempfile.TemporaryDirectory(prefix='wallow-recovery-request-') as directory:
        directory = Path(directory)
        archive = client.download(selected, directory / 'request.zip')
        payload = unpack_payload(archive, directory / 'verified', selected, controller,
                                 'request.json', 'recovery-request', 'source', 1024 * 1024)
        data = payload.read_bytes()
        try:
            record = json.loads(data)
            source, release_id = record['source_sha'], record['release']['id']
        except (ValueError, TypeError, KeyError, RecursionError):
            raise PublicationError('Recovery request is malformed') from None
        expected = request_record(client, controller.source_sha, source, controller.run_id, controller.run_attempt,
                                  controller.workflow_id, release_id, catalog)
        if record != expected:
            raise PublicationError('Sealed recovery request differs from its current release or invocation authority')
    producer = RecoveryProducer(controller.repository, controller.repository_id, source, controller.run_id,
                                controller.run_attempt, controller.workflow_id, controller.workflow_ref,
                                controller.source_sha, 'sha256:' + hashlib.sha256(data).hexdigest(), release_id)
    return producer, selected, record
