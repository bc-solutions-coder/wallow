"""Verify sealed dependency inputs and scan them under current policy without restoring code."""

from dataclasses import asdict
import gzip
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import tarfile
import tempfile

from publication import PublicationError, matches
from publication_identity import producer_from_record
from publication_archives import bounded_tar
from publication_artifacts import Artifact, unpack_payload
import security


ARCHIVE_LIMIT = 256 * 1024 * 1024
FILE_LIMIT = 16 * 1024 * 1024


def expected_inputs(fingerprints):
    if not isinstance(fingerprints, dict) or not matches(r'[a-f0-9]{64}', fingerprints.get('pnpm-lock.yaml')):
        raise PublicationError('Registered pnpm dependency identity is missing')
    projects = []
    for name, digest in fingerprints.items():
        if not isinstance(name, str) or name.startswith('/') or '\\' in name or any(part in ('', '.', '..') for part in name.split('/')) or not matches(r'[a-f0-9]{64}', digest):
            raise PublicationError('Registered dependency source identity is invalid')
        if name.startswith('api/') and name.endswith('.csproj'):
            projects.append(name)
    if not projects:
        raise PublicationError('Registered .NET project inventory is missing')
    return {'pnpm-lock.yaml'} | {str(PurePosixPath(project).parent / 'packages.lock.json') for project in projects}


def extract_dependencies(payload, destination, fingerprints):
    expected = expected_inputs(fingerprints)
    directories = {str(parent) for name in expected for parent in PurePosixPath(name).parents}
    seen, files, inventory = set(), [], None
    try:
        with bounded_tar(payload, ARCHIVE_LIMIT, plain_headers=True) as archive:
            for member in archive:
                name = member.name.removeprefix('./')
                if member.isdir():
                    name = name.removesuffix('/') or '.'
                    if name not in directories or member.size:
                        raise PublicationError('Dependency archive contains an unexpected directory')
                elif not member.isfile() or member.issparse() or name not in expected | {'inventory.json'} or not 0 < member.size <= (1024 * 1024 if name == 'inventory.json' else FILE_LIMIT):
                    raise PublicationError('Dependency archive contains an unsafe or unexpected file')
                if name in seen:
                    raise PublicationError('Dependency archive contains duplicate entries')
                seen.add(name)
                if member.isdir():
                    continue
                with archive.extractfile(member) as source:
                    if name == 'inventory.json':
                        inventory = json.load(source)
                    else:
                        target = destination / name
                        target.parent.mkdir(parents=True, exist_ok=True)
                        with target.open('xb') as output:
                            shutil.copyfileobj(source, output, 1024 * 1024)
                        with target.open('rb') as stream:
                            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
                        if name == 'pnpm-lock.yaml' and digest != fingerprints[name]:
                            raise PublicationError('pnpm lockfile bytes differ from the registered source')
                        files.append({'path': name, 'size': target.stat().st_size, 'sha256': 'sha256:' + digest})
        if not isinstance(inventory, list) or any(not isinstance(name, str) for name in inventory) or len(inventory) != len(set(inventory)) or set(inventory) != expected or {item['path'] for item in files} != expected:
            raise PublicationError('Dependency inventory differs from registered pnpm and .NET project inputs')
    except (tarfile.TarError, gzip.BadGzipFile, UnicodeDecodeError, json.JSONDecodeError, EOFError, RecursionError):
        raise PublicationError('Dependency archive could not be inspected') from None
    return sorted(files, key=lambda item: item['path'])


def verify_coverage(document, inventory, scan_root):
    expected = {item['path'] for item in inventory}
    results = document.get('Results') if isinstance(document, dict) else None
    if not isinstance(results, list):
        raise PublicationError('Trivy dependency coverage is missing')
    seen = set()
    normalized = []
    for result in results:
        target = result.get('Target') if isinstance(result, dict) else None
        if not isinstance(target, str):
            raise PublicationError('Trivy dependency target is missing')
        path = target.removeprefix(str(scan_root) + '/')
        if path not in expected or path in seen or result.get('Class') != 'lang-pkgs' or result.get('Type') != ('pnpm' if path == 'pnpm-lock.yaml' else 'nuget') or not isinstance(result.get('Packages'), list) or not result['Packages']:
            raise PublicationError('Trivy dependency coverage is incomplete, ambiguous or unexpected')
        seen.add(path)
        normalized.append(result | {'Target': path})
    if seen != expected:
        raise PublicationError('Trivy did not scan every registered dependency input')
    return document | {'Results': normalized}


def verify_dependencies(client, plan, reports, controller=None, runner=subprocess.run):
    selected = [item for item in plan['artifacts'] if item['kind'] == 'dependencies']
    if plan['route'] == 'docs' and not selected:
        return None
    if plan['route'] != 'full' or len(selected) != 1:
        raise PublicationError('Dependency artifacts do not match the authorized route')
    item = selected[0]
    if item['payload'] != 'dependencies.tar.gz' or item['variant'] != 'resolved-locks':
        raise PublicationError('Unexpected dependency artifact layout')
    fingerprints = plan['inputs']['input_sha256']
    expected_inputs(fingerprints)
    artifact = Artifact(**{key: item[key] for key in ('id', 'name', 'digest', 'size')})
    producer = producer_from_record(plan['producer'])
    controller = Path(controller or Path(__file__).resolve().parents[2])
    reports = Path(reports).resolve()
    reports.mkdir()
    with tempfile.TemporaryDirectory(prefix='wallow-dependency-inspection-') as directory:
        root = Path(directory)
        archive = client.download(artifact, root / 'artifact.zip')
        payload = unpack_payload(archive, root / 'verified', artifact, producer, item['payload'], 'dependencies', item['variant'], ARCHIVE_LIMIT)
        scan_root = root / 'inputs'
        scan_root.mkdir()
        inventory = extract_dependencies(payload, scan_root, fingerprints)
        receipt = {'producer': asdict(producer), 'artifact': asdict(artifact), 'inventory': inventory, 'publication_authorized': False}
        (reports / 'inputs.json').write_text(json.dumps(receipt, indent=2) + '\n')
        try:
            runner(['bash', str(controller / 'scripts/ci/run-security.sh'), 'install-trivy'], cwd=controller, check=True, timeout=900)
            shutil.copyfile(controller / '.ci-reports/security/trivy-database.json', reports / 'trivy-database.json')
            runner([str(controller / '.ci-tools/bin/trivy'), '--config', str(controller / '.ci-tools/trivy.yaml'), 'fs', '--skip-db-update', '--scanners', 'vuln',
                    '--include-dev-deps', '--list-all-pkgs', '--ignorefile', '/dev/null', '--format', 'json', '--output', str(reports / 'trivy.json'), str(scan_root)],
                   cwd=controller, check=True, timeout=900)
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
            raise PublicationError('Fresh dependency scanner execution failed') from None
        report_path = reports / 'trivy.json'
        if not report_path.is_file() or report_path.stat().st_size > 64 * 1024 * 1024:
            raise PublicationError('Trivy dependency report is missing or oversized')
        try:
            report = verify_coverage(json.loads(report_path.read_text()), inventory, scan_root)
        except (UnicodeDecodeError, json.JSONDecodeError, RecursionError):
            raise PublicationError('Trivy dependency report is malformed') from None
        exceptions = security.validate_exceptions(json.loads((controller / '.github/ci/security-exceptions.json').read_text()))
        findings = security.apply_exceptions(security.trivy(report), exceptions)
        blocking = sum(item['blocking'] and not item['excepted'] for item in findings)
        gate = {'scanner': 'trivy', 'findings': findings, 'exceptions': exceptions, 'blocking_count': blocking, 'result': 'fail' if blocking else 'pass'}
        (reports / 'gate.json').write_text(json.dumps(gate, indent=2) + '\n')
        if blocking:
            raise PublicationError('Fresh dependency scan has unexcepted blocking findings')
        return {'artifact_id': artifact.id, 'inventory': inventory, 'blocking_count': 0,
                'reports': [{'file': reports.name + '/' + path.name, 'sha256': 'sha256:' + hashlib.sha256(path.read_bytes()).hexdigest()} for path in sorted(reports.iterdir())]}
