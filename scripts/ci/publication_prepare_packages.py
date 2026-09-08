"""Freshly scan selected release inputs and retain exact packed bytes without publishing."""

from dataclasses import asdict
import hashlib
import json
from pathlib import Path
import shutil
import tarfile
import tempfile

from publication import PublicationError
from publication_packages import inspect_package
from publication_verify_dependencies import verify_dependencies
from publication_verify_packages import verify_packages


def prepare_packages(client, authority, catalog, destination, reports, scan=verify_dependencies):
    destination, reports = Path(destination), Path(reports)
    if destination.exists() or destination.is_symlink():
        raise PublicationError('Prepared package destination must be new')
    destination.mkdir(parents=True)
    reports.mkdir(parents=True, exist_ok=True)
    candidates, scans = [], {}
    created = False
    try:
        if len(authority['ready']) > 100:
            raise PublicationError('Package preparation exceeds its bounded release batch')
        with tempfile.TemporaryDirectory(prefix='wallow-release-package-preparation-') as directory:
            root = Path(directory)
            for release in authority['ready']:
                plan = release['plan']
                producer = plan['producer']
                key = producer['run_id'], producer['run_attempt']
                if key not in scans:
                    scans[key] = scan(client, plan, reports / f'dependencies-{key[0]}-{key[1]}')
                    if scans[key] is None or scans[key].get('blocking_count') != 0:
                        raise PublicationError('Package release lacks complete fresh dependency scan evidence')
                component = release['component']
                target = root / f"release-{release['release']['id']}.tgz"
                captured = []

                def retain(packed, components, packages):
                    source = packed / component['package']['tarball']
                    package = inspect_package(source, component['package']['name'], release['release']['version'], catalog['package_registry'], client.repository)
                    shutil.copyfile(source, target)
                    with target.open('rb') as stream:
                        if hashlib.file_digest(stream, 'sha256').hexdigest() != package.sha256:
                            raise PublicationError('Packed release changed during preparation')
                    captured.append(package)

                verify_packages(client, plan, catalog, prepare=retain)
                if len(captured) != 1:
                    raise PublicationError('Expected one exact prepared package per authorized release')
                candidates.append({'release': release['release'], 'origin': release['origin'], 'selection': release['selection'],
                                   'producer': producer, 'registration': plan['registration'], 'package': asdict(captured[0]),
                                   'file': target.name, 'size': target.stat().st_size, 'dependency_scan': scans[key]})
                if 'recovery' in plan:
                    candidates[-1]['recovery'] = {'receipt': plan['recovery']['receipt'], 'producer': producer}
                if sum(item['size'] + 1024 for item in candidates) > 1024 * 1024 * 1024 - 10240:
                    raise PublicationError('Prepared package batch exceeds its archive size limit')
            archive_path = destination / 'packages.tar'
            with archive_path.open('xb') as output:
                created = True
                with tarfile.open(fileobj=output, mode='w', format=tarfile.USTAR_FORMAT) as archive:
                    for candidate in sorted(candidates, key=lambda item: item['file']):
                        member = tarfile.TarInfo(candidate['file'])
                        member.size, member.mode = candidate['size'], 0o644
                        with (root / candidate['file']).open('rb') as source:
                            archive.addfile(member, source)
        with archive_path.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        return {'candidates': candidates, 'published': [{**item, 'package': asdict(item['package'])} for item in authority['published']],
                'pending': authority['pending'], 'target_release_id': authority['target_release_id'],
                'archive': {'file': archive_path.name, 'size': archive_path.stat().st_size, 'sha256': digest}, 'publication_authorized': False}
    except BaseException:
        if created:
            (destination / 'packages.tar').unlink(missing_ok=True)
        destination.rmdir()
        raise
