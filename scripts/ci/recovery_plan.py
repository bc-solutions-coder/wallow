"""Resolve an explicit recovered producer while preserving its original release selection."""

from dataclasses import asdict
import json
from pathlib import Path
import tempfile

from publication import PublicationError
from publication_artifacts import select_artifact, unpack_payload
from publication_plan import candidate_artifacts, validate_registered_inputs
from recovery_authorization import authenticate_request, completed_controller


def validate_registration(record, producer, artifacts, request_artifact, request, catalog):
    expected_keys = {'schema', 'producer', 'route', 'artifacts', 'component_versions', 'input_sha256', 'catalog',
                     'recovery_request'}
    if not isinstance(record, dict) or set(record) != expected_keys or type(record['schema']) is not int or record['schema'] != 2:
        raise PublicationError('Recovery requires an exact schema-2 registration')
    binding = {'artifact': asdict(request_artifact), 'sha256': producer.recovery_request_sha256, 'record': request}
    if record['producer'] != asdict(producer) or record['route'] != 'full' or record['artifacts'] != artifacts or record['recovery_request'] != binding:
        raise PublicationError('Recovery registration differs from authenticated invocation, request, or artifacts')
    validate_registered_inputs(record)
    release = request['release']
    if record['catalog'] != catalog or record['component_versions'].get(release['component_path']) != release['version']:
        raise PublicationError('Recovered catalog or release version differs from current publication authority')


def resolve(client, context, run_id, attempt, catalog):
    if not isinstance(context, dict) or context.get('event_name') != 'workflow_dispatch':
        raise PublicationError('Recovered publication requires explicit manual selection')
    controller_sha = client.controller(context)
    invocation, jobs = completed_controller(client, run_id, attempt)
    artifacts = client.list(f'/actions/runs/{run_id}/artifacts', 'artifacts')
    producer, request_artifact, request = authenticate_request(client, invocation, artifacts, catalog)
    route, selected = candidate_artifacts(producer, jobs, artifacts)
    if route != 'full':
        raise PublicationError('Recovery must have passed the complete validation route')
    registration = select_artifact(artifacts, producer, 'producer-registration')
    with tempfile.TemporaryDirectory(prefix='wallow-recovered-plan-') as directory:
        directory = Path(directory)
        archive = client.download(registration, directory / 'registration.zip')
        payload = unpack_payload(archive, directory / 'verified', registration, producer,
                                 'registration.json', 'registration', 'publication', 1024 * 1024)
        try:
            record = json.loads(payload.read_bytes())
        except (ValueError, TypeError, RecursionError):
            raise PublicationError('Recovery registration payload is malformed') from None
    validate_registration(record, producer, selected, request_artifact, request, catalog)
    return {'schema': 2, 'controller_sha': controller_sha, 'producer': asdict(producer), 'route': route,
            'artifacts': selected, 'registration': asdict(registration), 'inputs': record}
