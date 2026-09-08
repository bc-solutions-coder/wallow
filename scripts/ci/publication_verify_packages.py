"""Inspect sealed package candidates and validation companions without installing them."""

from dataclasses import asdict
import gzip
import json
from pathlib import Path
import shutil
import tarfile
import tempfile

from publication import PublicationError
from publication_identity import producer_from_record
from publication_archives import bounded_plain_tar, bounded_tar
from publication_artifacts import Artifact, unpack_payload
from publication_packages import dependency_order, inspect_candidate, inspect_validation_package, packed_manifest


PACKAGE_LIMIT = 100 * 1024 * 1024


def extract_candidates(payload, destination, expected):
    _read_candidates(payload, destination, expected, lambda path, limit: bounded_tar(path, limit, plain_headers=True))


def extract_prepared_candidates(payload, destination, expected):
    _read_candidates(payload, destination, expected, bounded_plain_tar)


def _read_candidates(payload, destination, expected, reader):
    seen = set()
    try:
        with reader(payload, len(expected) * PACKAGE_LIMIT + 1024 * 1024) as archive:
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


def verify_packages(client, plan, catalog, prepare=None):
    """The caller provides an API-authorized plan and the validated controller catalog."""
    selected = [item for item in plan['artifacts'] if item['kind'] == 'packages']
    if plan['route'] == 'docs' and not selected:
        return None
    if plan['route'] != 'full' or len(selected) != 1:
        raise PublicationError('Package artifacts do not match the authorized route')
    item = selected[0]
    if item['payload'] != 'packages.tar.gz' or item['variant'] != 'pnpm':
        raise PublicationError('Unexpected authorized package artifact layout')
    candidates = {component['package']['tarball']: component for component in catalog['components'] if 'package' in component}
    companions = {package['tarball']: package for package in catalog['validation_only_packages']}
    expected = candidates.keys() | companions.keys()
    artifact = Artifact(**{key: item[key] for key in ('id', 'name', 'digest', 'size')})
    if artifact.size > len(expected) * PACKAGE_LIMIT + 1024 * 1024:
        raise PublicationError('Package artifact exceeds its bounded download size')
    producer = producer_from_record(plan['producer'])
    with tempfile.TemporaryDirectory(prefix='wallow-package-inspection-') as directory:
        root = Path(directory)
        archive = client.download(artifact, root / 'artifact.zip')
        payload = unpack_payload(archive, root / 'verified', artifact, producer, item['payload'], 'packages', 'pnpm', len(expected) * PACKAGE_LIMIT)
        packed = root / 'packages'
        packed.mkdir()
        extract_candidates(payload, packed, expected)
        packages = []
        for tarball, component in candidates.items():
            packages.append(inspect_candidate(packed / tarball, component['package']['name'], packed_manifest(packed / tarball).get('version'), catalog['package_registry']))
        for tarball, package in companions.items():
            inspect_validation_package(packed / tarball, package['name'])
        ordered = dependency_order(packages)
        if prepare is not None:
            prepare(packed, candidates, ordered)
        return {'artifact_id': artifact.id, 'packages': [asdict(package) for package in ordered]}
