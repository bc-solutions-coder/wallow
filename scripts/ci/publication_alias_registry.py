"""Verify registry bytes and plan exact observed-state alias transitions."""

import gzip
import hashlib
import json
from pathlib import Path
import tempfile

from publication import PublicationError
from publication_aliases import aliases, alias_action, stable_version
from publication_ghcr import GHCR
from publication_image_provenance import OCI_INDEX, main_index
from publication_npm import PackageRegistry
from publication_packages import Package
from publication_prepared_images import inspect_prepared_image
from publication_publish_images import SkopeoRegistry


def image_bytes(record, image, repository):
    data = main_index(repository, record['release']['commit_sha'], image['image'], image['platforms'])
    if 'sha256:' + hashlib.sha256(data).hexdigest() != image['index_digest']:
        raise PublicationError('Image receipt index digest differs from its exact child descriptors')
    return {'digest': image['index_digest'], 'media_type': OCI_INDEX, 'bytes': data}


def registry_source(directory, prepared):
    """Recover bounded scan input identities from exact retained compressed registry blobs."""
    directory = Path(directory)
    manifest = directory / 'manifest.json'
    if manifest.is_symlink() or not manifest.is_file() or manifest.stat().st_size != prepared['manifest_size'] or 'sha256:' + hashlib.sha256(manifest.read_bytes()).hexdigest() != prepared['manifest_digest']:
        raise PublicationError('Registry manifest differs from durable publication bytes')
    config = directory / prepared['config_digest'][7:]
    if config.is_symlink() or not config.is_file() or config.stat().st_size > 4 * 1024 * 1024 or 'sha256:' + hashlib.sha256(config.read_bytes()).hexdigest() != prepared['config_digest']:
        raise PublicationError('Registry configuration differs from durable publication bytes')
    value = json.loads(config.read_text())
    diff_ids = value.get('rootfs', {}).get('diff_ids')
    if value.get('os') + '/' + value.get('architecture') != prepared['platform'] or not isinstance(diff_ids, list) or len(diff_ids) != len(prepared['layers']):
        raise PublicationError('Registry platform or layer coverage differs from its receipt')
    layers, total, names = [], 0, {'manifest.json', config.name}
    for index, layer in enumerate(prepared['layers']):
        path = directory / layer['digest'][7:]
        names.add(path.name)
        if path.is_symlink() or not path.is_file() or path.stat().st_size != layer['size']:
            raise PublicationError('Registry layer is missing or changed')
        with path.open('rb') as stream:
            if 'sha256:' + hashlib.file_digest(stream, 'sha256').hexdigest() != layer['digest']:
                raise PublicationError('Registry layer bytes differ from durable publication')
        digest, size = hashlib.sha256(), 0
        with gzip.open(path, 'rb') as stream:
            while chunk := stream.read(1024 * 1024):
                size += len(chunk)
                total += len(chunk)
                if total > 16 * 1024**3:
                    raise PublicationError('Registry image exceeds bounded uncompressed scan size')
                digest.update(chunk)
        if 'sha256:' + digest.hexdigest() != diff_ids[index]:
            raise PublicationError('Registry layer differs from its configuration DiffID')
        layers.append({'digest': diff_ids[index], 'size': size})
    if {path.name for path in directory.iterdir()} != names:
        raise PublicationError('Registry image contains unexpected files')
    expected = {'config_digest': prepared['config_digest'], 'platform': prepared['platform'], 'diff_ids': diff_ids, 'layers': layers}
    if inspect_prepared_image(directory, expected) != prepared:
        raise PublicationError('Registry image differs from its durable prepared identity')
    return expected


class Registry:
    def __init__(self, kind, repository, catalog, token, username, scanner=None, access='read'):
        self.access = access
        self.kind, self.repository, self.catalog = kind, repository, catalog
        self.token, self.username, self.scanner = token, username, scanner
        self.verified = set()

    def __enter__(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='wallow-alias-credentials-')
        self.root = Path(self.temporary.name)
        self.transport = None
        self.packages = PackageRegistry(self.catalog['package_scope'], self.token).__enter__() if self.kind == 'package' else None
        return self

    def __exit__(self, *args):
        if self.packages:
            self.packages.__exit__(*args)
        self.temporary.cleanup()

    def image_registry(self, repository):
        # A fresh short-lived client avoids retaining a bearer across image scans.
        return GHCR(repository, self.username, self.token, access=self.access)

    def current(self, output, alias):
        if self.kind == 'package':
            return self.packages.dist_tags(output['name'], output['version']).get(alias)
        value = self.image_registry(output['repository']).read_manifest(alias)
        return None if value is None else value['digest']

    def verify(self, record, fresh=False):
        key = record['release']['id'], fresh
        if key in self.verified:
            return
        if self.kind == 'package':
            if not self.packages.read(Package(**record['outputs']['package'])):
                raise PublicationError('Immutable alias target package is absent from the registry')
        else:
            if self.transport is None:
                self.transport = SkopeoRegistry(self.username, self.token, self.root / 'auth.json')
            for image in record['outputs']['images']:
                registry = self.image_registry(image['repository'])
                expected = image_bytes(record, image, self.repository)
                for reference in (record['release']['version'], 'sha-' + record['release']['commit_sha'], expected['digest']):
                    if registry.read_manifest(reference) != expected:
                        raise PublicationError('Immutable image alias target differs from its authenticated receipt')
                for platform, prepared in image['platforms'].items():
                    with tempfile.TemporaryDirectory(prefix='wallow-alias-image-readback-') as directory:
                        path = Path(directory)
                        self.transport.copy(image['repository'], prepared['manifest_digest'], path, False)
                        source = registry_source(path, prepared)
                        if fresh and self.scanner:
                            self.scanner(record, image, platform, path, source)
        self.verified.add(key)

    def promote(self, output, alias, candidate, previous):
        if self.kind == 'package':
            return self.packages.replace_dist_tag(Package(**output), alias, previous)
        registry = self.image_registry(output['repository'])
        data = image_bytes(candidate, output, self.repository)
        observed = registry.read_manifest(alias)
        if (None if observed is None else observed['digest']) != previous:
            raise PublicationError('Image alias changed after its authorized preparation')
        registry.replace_release_alias(alias, data['bytes'], data['digest'], observed)
        return 'verified'


def plan_aliases(client, records, kind, registry, target=None, *, migration_authorizer=None):
    choices = {}
    for record in records:
        if target is not None and record['release']['id'] != target:
            continue
        for alias in aliases(record['release'], kind):
            key = record['release']['component'], alias
            if key not in choices or stable_version(record['release']) > stable_version(choices[key]['release']):
                choices[key] = record
    result, checked = [], set()
    for (_, alias), candidate in sorted(choices.items()):
        outputs = candidate['outputs']['images'] if kind == 'image' else [candidate['outputs']['package']]
        for output in outputs:
            current = registry.current(output, alias)
            prior = []
            migration = None
            if current is not None:
                for record in records:
                    if record['release']['component'] != candidate['release']['component']:
                        continue
                    if kind == 'package' and record['outputs']['package']['version'] == current:
                        prior.append(record)
                    elif kind == 'image' and any(image['repository'] == output['repository'] and image['index_digest'] == current for image in record['outputs']['images']):
                        prior.append(record)
                if not prior and kind == 'package' and migration_authorizer is not None:
                    migration = migration_authorizer(candidate, alias, current)
                if len(prior) != 1 and migration is None:
                    identity = output['name'] if kind == 'package' else output['repository']
                    raise PublicationError(
                        f'Existing alias has unknown or ambiguous durable release provenance: '
                        f'{kind} {identity!r}, alias {alias!r}, target {current!r}, '
                        f'{len(prior)} matching release receipts'
                    )
            previous = prior[0] if prior else None
            comparison = client.get('/compare/' + previous['release']['commit_sha'] + '...' + candidate['release']['commit_sha']) if previous and previous != candidate else None
            action = 'advance' if migration is not None else alias_action(alias, kind, previous, candidate, comparison)
            for record in (previous, candidate):
                if record and record['release']['id'] not in checked:
                    registry.verify(record)
                    checked.add(record['release']['id'])
            result.append({'release_id': candidate['release']['id'], 'output': output, 'alias': alias, 'previous': current,
                           'previous_release_id': previous['release']['id'] if previous else None, 'action': action})
            if migration is not None:
                result[-1]['migration'] = migration
    return result
