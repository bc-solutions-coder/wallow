"""Inspect sealed package candidates and validation companions without installing them."""

from dataclasses import asdict
import gzip
import json
from pathlib import Path
import shutil
import tarfile
import tempfile

from publication import Producer, PublicationError
from publication_archives import bounded_tar
from publication_artifacts import Artifact, unpack_payload
from publication_packages import dependency_order, inspect_candidate, inspect_validation_package


PACKAGE_LIMIT = 100 * 1024 * 1024


def extract_candidates(payload, destination, expected):
    seen = set()
    try:
        with bounded_tar(payload, len(expected) * PACKAGE_LIMIT + 1024 * 1024, plain_headers=True) as archive:
            for member in archive:
                if member.name in ('.', './') and member.isdir() and not member.size:
                    name = '.'
                else:
                    name = member.name.removeprefix('./')
                    if name not in expected or not member.isfile() or member.issparse() or not 0 < member.size <= PACKAGE_LIMIT:
                        raise PublicationError('Package bundle contains an unsafe, unexpected or oversized member')
                if name in seen:
                    raise PublicationError('Package bundle contains duplicate members')
                seen.add(name)
                if name != '.':
                    with archive.extractfile(member) as source, (destination / name).open('xb') as target:
                        shutil.copyfileobj(source, target, 1024 * 1024)
        if seen - {'.'} != set(expected):
            raise PublicationError('Package bundle is missing required candidates or companions')
    except (tarfile.TarError, gzip.BadGzipFile, EOFError):
        raise PublicationError('Package bundle could not be inspected') from None


def verify_packages(client, plan, catalog):
    """The caller provides an API-authorized plan and the validated controller catalog."""
    if json.dumps(catalog, sort_keys=True) != json.dumps(plan['inputs']['catalog'], sort_keys=True):
        raise PublicationError('Registered package catalog differs from the controller catalog')
    selected = [item for item in plan['artifacts'] if item['kind'] == 'packages']
    if plan['route'] == 'docs' and not selected:
        return None
    if plan['route'] != 'full' or len(selected) != 1:
        raise PublicationError('Package artifacts do not match the authorized route')
    item = selected[0]
    if item['payload'] != 'packages.tar.gz' or item['variant'] != 'pnpm':
        raise PublicationError('Unexpected authorized package artifact layout')
    versions = plan['inputs']['component_versions']
    if not isinstance(versions, dict) or set(versions) != {component['path'] for component in catalog['components']}:
        raise PublicationError('Registered component versions do not cover the trusted catalog')
    candidates = {component['package']['tarball']: component for component in catalog['components'] if 'package' in component}
    companions = {package['tarball']: package for package in catalog['validation_only_packages']}
    expected = candidates.keys() | companions.keys()
    artifact = Artifact(**{key: item[key] for key in ('id', 'name', 'digest', 'size')})
    producer = Producer(**plan['producer'])
    with tempfile.TemporaryDirectory(prefix='wallow-package-inspection-') as directory:
        root = Path(directory)
        archive = client.download(artifact, root / 'artifact.zip')
        payload = unpack_payload(archive, root / 'verified', artifact, producer, item['payload'], 'packages', 'pnpm', len(expected) * PACKAGE_LIMIT)
        packed = root / 'packages'
        packed.mkdir()
        extract_candidates(payload, packed, expected)
        packages = []
        for tarball, component in candidates.items():
            packages.append(inspect_candidate(packed / tarball, component['package']['name'], versions[component['path']], catalog['package_registry']))
        for tarball, package in companions.items():
            inspect_validation_package(packed / tarball, package['name'])
        return {'artifact_id': artifact.id, 'packages': [asdict(package) for package in dependency_order(packages)]}
