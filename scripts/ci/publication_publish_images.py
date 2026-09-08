"""Publish reauthorized main images, then advance nightly without rolling it backward."""

import argparse
import base64
from dataclasses import asdict
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import uuid

from publication import PublicationError, image_repository, load_catalog, matches
from publication_artifacts import unpack_payload
from publication_github import GitHub
from publication_ghcr import GHCR
from publication_image_authorization import authorize_images
from publication_image_provenance import OCI_INDEX, main_index, nightly_action
from publication_prepare_images import SKOPEO
from publication_prepared_bundle import extract_prepared
from publication_prepared_images import inspect_prepared_image


class SkopeoRegistry:
    def __init__(self, username, credential, authfile, runner=subprocess.run):
        if not matches(r'[A-Za-z0-9_.\[\]-]{1,100}', username) or not matches(r'[\x21-\x7e]{1,8192}', credential):
            raise PublicationError('Valid job-scoped registry credentials are required')
        self.authfile, self.runner = authfile, runner
        with authfile.open('x') as output:
            os.chmod(authfile, 0o600)
            json.dump({'auths': {'ghcr.io': {'auth': base64.b64encode((username + ':' + credential).encode()).decode()}}}, output)
        self.run(['docker', 'pull', SKOPEO])

    def run(self, command):
        try:
            self.runner(command, check=True, timeout=900)
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
            raise PublicationError('Exact image registry copy failed') from None

    def copy(self, repository, digest, directory, upload):
        if not matches(r'ghcr\.io/[a-z0-9][a-z0-9-]*/[a-z0-9]+(?:[._-][a-z0-9]+)*', repository) or not matches(r'sha256:[a-f0-9]{64}', digest):
            raise PublicationError('Registry copy target is not an exact authorized GHCR image')
        name = 'wallow-image-copy-' + uuid.uuid4().hex
        source, target = ('dir:/image', f'docker://{repository}@{digest}') if upload else (f'docker://{repository}@{digest}', 'dir:/image')
        command = ['docker', 'run', '--rm', '--name', name, '--read-only', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
                   '--user', f'{os.getuid()}:{os.getgid()}', '--mount', f'type=bind,src={self.authfile},dst=/run/auth.json,readonly',
                   '--mount', f'type=bind,src={directory},dst=/image' + (',readonly' if upload else ''),
                   '--tmpfs', '/tmp:rw,noexec,nosuid,size=64m', '--tmpfs', '/var/tmp:rw,noexec,nosuid,size=64m',
                   SKOPEO, 'copy', '--preserve-digests', '--authfile', '/run/auth.json', source, target]
        try:
            self.run(command)
        finally:
            self.runner(['docker', 'rm', '--force', name], check=False, timeout=30, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def publish_images(client, plan, preparation, artifacts, catalog, username, credential, output, transport_factory=SkopeoRegistry, registry_factory=GHCR, legacy=None):
    progress = {'schema': 1, 'mode': 'main-images', 'producer': plan['producer'], 'controller_sha': plan['controller_sha'],
                'preparation': asdict(preparation), 'artifacts': {key: asdict(value) for key, value in artifacts.items()}, 'images': [],
                'release_authorized': False, 'durable_release_receipt': False}
    output = Path(output)

    def record():
        output.write_text(json.dumps(progress, indent=2) + '\n')

    record()
    aliases = []
    source = plan['producer']['source_sha']
    with tempfile.TemporaryDirectory(prefix='wallow-image-writer-credentials-') as credentials:
        transport = None
        for bundle, artifact in artifacts.items():
            with tempfile.TemporaryDirectory(prefix='wallow-image-writer-inputs-') as directory:
                root = Path(directory)
                archive = client.download(artifact, root / 'prepared.zip')
                payload = unpack_payload(archive, root / 'sealed', artifact, preparation, 'images.tar', 'prepared-images', bundle + '-amd64-arm64', 16 * 1024**3)
                extracted = root / 'inputs'
                images = extract_prepared(payload, extracted, bundle, plan, catalog, preparation)
                if transport is None:
                    transport = transport_factory(username, credential, Path(credentials) / 'auth.json')
                for image in (item for item in catalog['images'] if item['bundle'] == bundle):
                    repository = image_repository(client.repository, image)
                    registry = registry_factory(repository, username, credential)
                    entry = {'image': image['id'], 'repository': repository, 'source_sha': source, 'children': [], 'immutable': 'pending', 'nightly': 'pending'}
                    progress['images'].append(entry)
                    record()
                    variants = {}
                    for item in images[image['id']]:
                        prepared = item['prepared']
                        local = extracted / item['directory']
                        data = (local / 'manifest.json').read_bytes()
                        expected = {'digest': prepared['manifest_digest'], 'media_type': 'application/vnd.docker.distribution.manifest.v2+json', 'bytes': data}
                        existing = registry.read_manifest(prepared['manifest_digest'])
                        if existing is not None and existing != expected:
                            raise PublicationError('Existing child manifest conflicts with prepared bytes')
                        if existing is None:
                            transport.copy(repository, prepared['manifest_digest'], local, True)
                        if registry.read_manifest(prepared['manifest_digest']) != expected:
                            raise PublicationError('Registry child manifest readback differs from prepared bytes')
                        with tempfile.TemporaryDirectory(prefix='wallow-image-readback-') as readback:
                            target = Path(readback)
                            transport.copy(repository, prepared['manifest_digest'], target, False)
                            if inspect_prepared_image(target, item['source']) != prepared:
                                raise PublicationError('Registry image readback differs from original configuration or layers')
                        variants[item['platform']] = prepared
                        entry['children'].append({'platform': item['platform'], 'digest': prepared['manifest_digest'], 'readback': 'verified'})
                        record()
                    data = main_index(client.repository, source, image['id'], variants)
                    digest = 'sha256:' + hashlib.sha256(data).hexdigest()
                    registry.write_manifest('sha-' + source, data, OCI_INDEX, digest)
                    if registry.read_manifest(digest) != {'digest': digest, 'media_type': OCI_INDEX, 'bytes': data}:
                        raise PublicationError('Immutable index digest readback differs from authorized bytes')
                    entry['immutable'], entry['digest'] = 'verified', digest
                    record()
                    aliases.append((registry, image['id'], data, digest, entry))
        for registry, image_id, data, digest, entry in aliases:
            previous = registry.read_manifest('nightly')
            action = nightly_action(registry, client, previous, client.repository, image_id, source, legacy=legacy)
            if legacy and previous and previous['digest'] == legacy.get('images', {}).get(image_id) and legacy.get('repository') == client.repository:
                entry['legacy_cutover'] = {'previous_digest': previous['digest'], 'source_sha': legacy['source_sha'], 'evidence': legacy['evidence']}
            if action == 'advance':
                registry.replace_nightly(data, digest, previous)
                entry['nightly'] = 'verified'
            else:
                entry['nightly'] = action
            record()
    return progress


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--producer-run', required=True)
    parser.add_argument('--producer-attempt', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    if os.environ.get('ENABLE_IMAGE_PUBLISH') != 'true':
        parser.exit(1, 'Image publication requires the literal ENABLE_IMAGE_PUBLISH=true.\n')
    values = (args.producer_run, args.producer_attempt, os.environ.get('GITHUB_RUN_ID'), os.environ.get('GITHUB_RUN_ATTEMPT'))
    if not all(matches(r'[1-9][0-9]*', value) for value in values):
        parser.exit(1, 'Image publication requires exact producer and invocation run/attempt IDs.\n')
    context = {key: os.environ.get('GITHUB_' + key.upper(), '') for key in ('repository', 'ref', 'workflow_ref', 'workflow_sha', 'event_name')}
    try:
        client = GitHub(context['repository'], os.environ.get('GH_TOKEN'))
        plan, preparation, artifacts, evidence = authorize_images(client, context, *map(int, values))
        catalog = load_catalog(Path(__file__).resolve().parents[2])
        if catalog != plan['inputs']['catalog']:
            raise PublicationError('Current catalog differs from authorized prepared inputs')
        Path(args.output).parent.mkdir()
        Path(args.output).with_name('authorization.json').write_text(json.dumps(evidence, indent=2) + '\n')
        legacy_path = Path(__file__).resolve().parents[2] / '.github/ci/legacy-nightly.json'
        legacy = json.loads(legacy_path.read_text()) if legacy_path.is_file() else None
        if legacy is not None and not isinstance(legacy, dict):
            raise PublicationError('Legacy nightly cutover snapshot must be an object')
        publish_images(client, plan, preparation, artifacts, catalog, os.environ.get('GITHUB_ACTOR'), os.environ.get('GH_TOKEN'), Path(args.output), legacy=legacy)
    except (ValueError, OSError) as error:
        parser.exit(1, f'Image publication failed: {error}\n')


if __name__ == '__main__':
    main()
