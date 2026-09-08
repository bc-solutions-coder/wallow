"""Register historical outputs against their sealed, reauthenticated recovery request."""

import argparse
from dataclasses import asdict
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile

from publication import Producer, PublicationError, load_catalog
from publication_artifacts import select_artifact, unpack_payload
from publication_identity import RecoveryProducer
from publication_plan import candidate_artifacts
from publication_release_github import ReleaseGitHub
from recovery_request import request
from registration import source_inputs, tracked_inputs
from validation import artifact_identity


def register(client, context, identity, root, controls):
    root, controls = Path(root), Path(controls)
    if identity.get('schema') != 2:
        raise PublicationError('Recovery registration requires a historical artifact identity')
    source = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
    if source != identity['source_sha']:
        raise PublicationError('Recovery checkout differs from the authorized historical source')
    catalog = load_catalog(root)
    current_catalog = json.loads((controls / '.github/ci/publication.json').read_text())
    if catalog != current_catalog:
        raise PublicationError('Historical publication catalog differs from current controls')
    run_id, attempt = int(identity['run_id']), int(identity['run_attempt'])
    expected = request(client, context, run_id, attempt, source, identity['release_id'], catalog)
    repository = client.get('')
    controller = Producer(client.repository, repository['id'], expected['controller_sha'], run_id, attempt,
                          expected['workflow_id'], identity['workflow_ref'])
    artifacts = client.list(f'/actions/runs/{run_id}/artifacts', 'artifacts')
    selected_request = select_artifact(artifacts, controller, 'recovery-request')
    with tempfile.TemporaryDirectory(prefix='wallow-recovery-registration-') as directory:
        directory = Path(directory)
        archive = client.download(selected_request, directory / 'request.zip')
        payload = unpack_payload(archive, directory / 'verified', selected_request, controller,
                                 'request.json', 'recovery-request', 'source', 1024 * 1024)
        data = payload.read_bytes()
        if 'sha256:' + hashlib.sha256(data).hexdigest() != identity['recovery_request_sha256'] or json.loads(data) != expected:
            raise PublicationError('Recovery request differs from current release authority or sealed digest')
    producer = RecoveryProducer(client.repository, repository['id'], source, run_id, attempt,
                                expected['workflow_id'], identity['workflow_ref'], expected['controller_sha'],
                                identity['recovery_request_sha256'], identity['release_id'])
    jobs = client.list(f'/actions/runs/{run_id}/attempts/{attempt}/jobs', 'jobs')
    route, selected = candidate_artifacts(producer, jobs, artifacts)
    gates = [job for job in jobs if job.get('name') == 'CI / required']
    if route != 'full' or len(gates) != 1 or gates[0].get('conclusion') != 'success':
        raise PublicationError('Recovery requires successful full validation before registration')
    return {'schema': 2, 'producer': asdict(producer), 'route': route, 'artifacts': selected,
            'recovery_request': {'artifact': asdict(selected_request), 'sha256': identity['recovery_request_sha256'],
                                 'record': expected},
            **source_inputs(root, catalog, tracked_inputs(root))}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    try:
        identity = artifact_identity('registration', 'publication')
        context = {key: os.environ.get('GITHUB_' + key.upper(), '') for key in
                   ('repository', 'ref', 'workflow_ref', 'workflow_sha', 'event_name')}
        client = ReleaseGitHub(context['repository'], os.environ.get('GH_TOKEN'))
        record = register(client, context, identity, Path.cwd(), Path(os.environ['CI_CONTROL_ROOT']))
        with Path(args.output).open('x') as output:
            json.dump(record, output, indent=2)
            output.write('\n')
    except PublicationError as error:
        parser.exit(1, f'Recovery registration failed: {error}\n')
    except (OSError, ValueError, TypeError, KeyError, subprocess.CalledProcessError):
        parser.exit(1, 'Recovery registration could not be validated.\n')


if __name__ == '__main__':
    main()
