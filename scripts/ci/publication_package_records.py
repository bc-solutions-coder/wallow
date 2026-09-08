"""Validate durable published tarball identities without requiring expired producer archives."""

import base64

from publication import PublicationError, matches
from publication_packages import Package
from publication_release_origin import verify_frame
from recovery_reference import verify_reference

WRITER_JOB = 'Publish release packages'
PREPARE_JOB = 'Prepare release packages'


def package_record(value, name, version, repository):
    fields = {'name', 'version', 'sha256', 'integrity', 'dependencies', 'repository', 'peer_dependencies'}
    if not isinstance(value, dict) or set(value) != fields or value.get('name') != name or value.get('version') != version or not matches(r'[a-f0-9]{64}', value.get('sha256')):
        raise PublicationError('Published package receipt differs from its exact component identity')
    integrity = value.get('integrity')
    try:
        valid = isinstance(integrity, str) and integrity.startswith('sha512-') and len(base64.b64decode(integrity[7:], validate=True)) == 64
    except ValueError:
        valid = False
    if not valid or name.split('/')[0] != '@' + repository.split('/')[0].lower():
        raise PublicationError('Published package receipt lacks valid owner scope or exact SHA512')
    source = value.get('repository')
    if not isinstance(source, dict) or source.get('type') != 'git' or source.get('url') != f'https://github.com/{repository}.git':
        raise PublicationError('Published package receipt is linked to another repository')
    for field in ('dependencies', 'peer_dependencies'):
        values = value[field]
        if not isinstance(values, dict):
            raise PublicationError('Published package dependency metadata is malformed')
        for dependency, requirement in values.items():
            if not matches(r'(?:@[a-z0-9][a-z0-9-]*/)?[a-z0-9][a-z0-9_.-]*', dependency) or not isinstance(requirement, str) or not 0 < len(requirement) <= 1000 or any(marker in requirement for marker in (':', '/', '\\', '\n', '\r')):
                raise PublicationError('Published package dependency is not a bounded registry range')
    return Package(**value)


def validate_readback(readback, package):
    if not isinstance(readback, dict) or readback.get('name') != package.name or readback.get('version') != package.version or readback.get('sha256') != package.sha256 or readback.get('integrity') != package.integrity or readback.get('registry_bytes_verified') is not True or readback.get('state') not in ('published', 'already-published'):
        raise PublicationError('Package publication receipt lacks exact registry byte readback')


def published_package(client, release, component, origin, selection, receipt):
    payload = receipt['record']['payload']
    expected = {'release', 'origin', 'selection', 'package', 'writer', 'readback'}
    if not isinstance(payload, dict) or set(payload) not in (expected, expected | {'recovery'}) or payload.get('release') != release or payload.get('origin') != {'asset_id': origin['asset_id'], 'sha256': origin['sha256']} or payload.get('selection') != {'asset_id': selection['asset_id'], 'sha256': selection['sha256']}:
        raise PublicationError('Published package receipt differs from its durable release selection')
    writer = payload.get('writer', {})
    verify_frame(client, writer.get('frame'), WRITER_JOB)
    package = package_record(payload.get('package'), component['package']['name'], release['version'], client.repository)
    validate_readback(payload.get('readback'), package)
    result = {'release': release, 'package': package, 'receipt': {'asset_id': receipt['asset_id'], 'sha256': receipt['sha256']},
              'origin': payload['origin'], 'selection': payload['selection']}
    if 'recovery' in payload:
        binding = payload['recovery']
        if not isinstance(binding, dict) or set(binding) != {'receipt', 'producer'}:
            raise PublicationError('Published package recovery lineage is malformed')
        recovered = verify_reference(client, binding['receipt'], release, payload['origin'], payload['selection'])
        if binding['producer'] != recovered['record']['payload']['recovery']['producer']:
            raise PublicationError('Published package producer differs from its durable recovery receipt')
        result['recovery'] = binding
    return result
