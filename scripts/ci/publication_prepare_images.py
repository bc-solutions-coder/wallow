"""Prepare and freshly scan exact verified image bytes without registry credentials."""

import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import tarfile
import tempfile
import uuid

from publication import PublicationError
from publication_image_index import image_index
from publication_prepared_images import inspect_prepared_image
from publication_scan_archive import write_scan_archive
import security
from validation import artifact, artifact_identity


# v1.22.2-immutable retains its digest; the upstream stable stream is rebuilt daily.
SKOPEO = 'quay.io/containers/skopeo@sha256:ca4fd94dba8cab15cf79c4c156bfc26d28e2265411294e9bba87756942e739ad'


def inspect_scan(document, expected, tag, source):
    metadata = document.get('Metadata') if isinstance(document, dict) else None
    config = metadata.get('ImageConfig') if isinstance(metadata, dict) else None
    rootfs = config.get('rootfs') if isinstance(config, dict) else None
    if not isinstance(rootfs, dict) or document.get('ArtifactType') != 'container_image' or metadata.get('ImageID') != expected['config_digest'] or metadata.get('DiffIDs') != expected['diff_ids'] or rootfs.get('diff_ids') != expected['diff_ids'] or str(config.get('os')) + '/' + str(config.get('architecture')) != expected['platform'] or metadata.get('RepoTags') != [tag]:
        raise PublicationError('Image scanner report differs from the verified image identity')
    results = document.get('Results')
    if not isinstance(results, list) or not results:
        raise PublicationError('Image scanner coverage is missing')
    normalized = []
    for result in results:
        if not isinstance(result, dict) or not isinstance(result.get('Target'), str) or not result['Target'] or result.get('Class') not in ('os-pkgs', 'lang-pkgs'):
            raise PublicationError('Image scanner package target is invalid')
        target = result['Target']
        if target == str(source) or target.startswith(str(source) + ' ('):
            target = tag + target[len(str(source)):]
        normalized.append(result | {'Target': target})
    return document | {'Results': normalized}


class ImagePreparation:
    """Called only with an already verified bundle and trusted catalog entries."""

    def __init__(self, plan, catalog, output, reports, database=None, controller=None, runner=subprocess.run):
        self.plan, self.catalog = plan, catalog
        self.output, self.reports = Path(output).resolve(), Path(reports).resolve()
        self.database = database
        self.controller = Path(controller or Path(__file__).resolve().parents[2])
        self.runner, self.started = runner, False

    def start(self):
        self.reports.mkdir()
        if self.database is None:
            self.run(['bash', str(self.controller / 'scripts/ci/run-security.sh'), 'install-trivy'])
            self.database = self.controller / '.ci-reports/security/trivy-database.json'
        shutil.copyfile(self.database, self.reports / 'trivy-database.json')
        self.exceptions = security.validate_exceptions(json.loads((self.controller / '.github/ci/security-exceptions.json').read_text()))
        self.run(['docker', 'pull', SKOPEO])
        self.started = True

    def run(self, command):
        try:
            return self.runner(command, cwd=self.controller, check=True, timeout=900)
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
            raise PublicationError('Image preparation or scanner execution failed') from None

    def convert(self, payload, destination, scratch, platform, tag):
        name = 'wallow-prepare-' + uuid.uuid4().hex
        command = ['docker', 'run', '--rm', '--name', name, '--network', 'none', '--read-only', '--cap-drop', 'ALL',
                   '--security-opt', 'no-new-privileges', '--user', f'{os.getuid()}:{os.getgid()}',
                   '--mount', f'type=bind,src={payload},dst=/input/images.tar.gz,readonly',
                   '--mount', f'type=bind,src={destination},dst=/output', '--mount', f'type=bind,src={scratch},dst=/var/tmp',
                   '--tmpfs', '/tmp:rw,noexec,nosuid,size=64m', SKOPEO, '--override-os', 'linux', '--override-arch', platform.split('/')[1],
                   'copy', '--format', 'v2s2', '--dest-compress', f'docker-archive:/input/images.tar.gz:{tag}', 'dir:/output']
        try:
            self.run(command)
        finally:
            self.runner(['docker', 'rm', '--force', name], cwd=self.controller, check=False, timeout=30, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    def scan(self, prepared, expected, tag, name, root):
        source = write_scan_archive(prepared, expected, tag, root / 'scan.tar.gz')
        report = self.reports / (name + '-trivy.json')
        self.run([str(self.controller / '.ci-tools/bin/trivy'), '--config', str(self.controller / '.ci-tools/trivy.yaml'),
                  'image', '--skip-db-update', '--scanners', 'vuln', '--list-all-pkgs', '--ignorefile', '/dev/null',
                  '--format', 'json', '--output', str(report), '--input', str(source)])
        if not report.is_file() or report.stat().st_size > 64 * 1024 * 1024:
            raise PublicationError('Image scanner report is missing or oversized')
        try:
            document = inspect_scan(json.loads(report.read_text()), expected, tag, source)
        except (UnicodeDecodeError, json.JSONDecodeError, RecursionError):
            raise PublicationError('Image scanner report is malformed') from None
        try:
            findings = security.trivy(document)
            for item in findings:
                item['scope'] = tag + '::' + item['scope']
            findings = security.apply_exceptions(findings, self.exceptions)
        except ValueError:
            raise PublicationError('Image scanner findings or package coverage are invalid') from None
        blocking = sum(item['blocking'] and not item['excepted'] for item in findings)
        gate = {'scanner': 'trivy', 'image': tag, 'findings': findings, 'exceptions': self.exceptions, 'blocking_count': blocking, 'result': 'fail' if blocking else 'pass'}
        gate_path = self.reports / (name + '-gate.json')
        gate_path.write_text(json.dumps(gate, indent=2) + '\n')
        if blocking:
            raise PublicationError('Fresh image scan has unexcepted blocking findings')
        return {path.name: 'sha256:' + hashlib.sha256(path.read_bytes()).hexdigest() for path in (report, gate_path, self.reports / 'trivy-database.json')}

    def __call__(self, bundle, payload, inspected):
        identity = artifact_identity('prepared-images', bundle + '-amd64-arm64')
        if identity['sha'] != self.plan['controller_sha']:
            raise PublicationError('Preparation seal differs from the authorized controller revision')
        if not self.started:
            self.start()
        images = [image for image in self.catalog['images'] if image['bundle'] == bundle]
        output = self.output / bundle
        output.mkdir(parents=True)
        target = output / 'images.tar'
        inventory = {'schema': 1, 'producer': self.plan['producer'], 'controller_sha': self.plan['controller_sha'],
                     'preparation': identity,
                     'catalog_sha256': 'sha256:' + hashlib.sha256(json.dumps(self.catalog, sort_keys=True, separators=(',', ':')).encode()).hexdigest(),
                     'source_artifact': next(item for item in self.plan['artifacts'] if item['kind'] == 'images' and item['variant'] == bundle + '-amd64-arm64'),
                     'bundle': bundle, 'images': [], 'indexes': {}, 'skopeo': SKOPEO, 'publication_authorized': False}
        try:
            with target.open('xb') as raw, tarfile.open(fileobj=raw, mode='w', format=tarfile.USTAR_FORMAT) as archive:
                def add_bytes(name, body):
                    member = tarfile.TarInfo(name)
                    member.size, member.mode = len(body), 0o644
                    archive.addfile(member, io.BytesIO(body))

                for image in images:
                    variants = {}
                    for platform, tag in sorted(image['tags'].items()):
                        expected = inspected[tag]
                        arch = platform.split('/')[1]
                        name = image['id'] + '-' + arch
                        with tempfile.TemporaryDirectory(prefix='wallow-image-preparation-') as directory:
                            root = Path(directory)
                            prepared, scratch = root / 'prepared', root / 'scratch'
                            prepared.mkdir()
                            scratch.mkdir()
                            self.convert(payload, prepared, scratch, platform, tag)
                            verified = inspect_prepared_image(prepared, expected)
                            reports = self.scan(prepared, expected, tag, name, root)
                            if inspect_prepared_image(prepared, expected) != verified:
                                raise PublicationError('Prepared image changed during scanning')
                            variants[platform] = verified
                            prefix = image['id'] + '/' + arch + '/'
                            files = {'manifest.json', verified['config_digest'][7:], *(layer['digest'][7:] for layer in verified['layers'])}
                            for file in sorted(files):
                                path = prepared / file
                                member = tarfile.TarInfo(prefix + file)
                                member.size, member.mode = path.stat().st_size, 0o644
                                with path.open('rb') as stream:
                                    archive.addfile(member, stream)
                            inventory['images'].append({'id': image['id'], 'tag': tag, 'platform': platform, 'directory': prefix,
                                                        'source': expected, 'prepared': verified, 'reports': reports})
                    index = image_index(variants)
                    add_bytes(image['id'] + '/index.json', index)
                    inventory['indexes'][image['id']] = {'file': image['id'] + '/index.json', 'digest': 'sha256:' + hashlib.sha256(index).hexdigest(), 'size': len(index)}
                add_bytes('inventory.json', json.dumps(inventory, sort_keys=True, separators=(',', ':')).encode())
            artifact('seal', target, str(target) + '.json', 'prepared-images', bundle + '-amd64-arm64')
            with target.open('rb') as stream:
                digest = 'sha256:' + hashlib.file_digest(stream, 'sha256').hexdigest()
            return {'file': str(target.relative_to(self.output)), 'digest': digest, 'size': target.stat().st_size, 'inventory': inventory}
        except BaseException:
            shutil.rmtree(output)
            raise
