"""Bind compressed registry inputs to an inspected Docker-save image."""

import gzip
import hashlib
import json
from pathlib import Path

from publication import PublicationError, matches


def inspect_prepared_image(directory, expected):
    directory = Path(directory)
    if directory.is_symlink() or not directory.is_dir():
        raise PublicationError('Prepared image must be a regular directory')
    manifest_path = directory / 'manifest.json'
    if manifest_path.is_symlink() or not manifest_path.is_file() or manifest_path.stat().st_size > 4 * 1024 * 1024:
        raise PublicationError('Prepared image manifest is missing or oversized')
    try:
        manifest_bytes = manifest_path.read_bytes()
        manifest = json.loads(manifest_bytes)
        if not isinstance(manifest, dict) or manifest.get('schemaVersion') != 2 or manifest.get('mediaType') != 'application/vnd.docker.distribution.manifest.v2+json':
            raise PublicationError('Prepared image requires a Docker v2 manifest')

        def verify_blob(descriptor, media_type, maximum):
            if not isinstance(descriptor, dict) or descriptor.get('mediaType') != media_type or not matches(r'sha256:[0-9a-f]{64}', descriptor.get('digest')) or type(descriptor.get('size')) is not int or not 0 <= descriptor['size'] <= maximum:
                raise PublicationError('Prepared image has an invalid blob descriptor')
            path = directory / descriptor['digest'][7:]
            if path.is_symlink() or not path.is_file() or path.stat().st_size != descriptor['size']:
                raise PublicationError('Prepared image blob is missing or has a different size')
            with path.open('rb') as stream:
                if 'sha256:' + hashlib.file_digest(stream, 'sha256').hexdigest() != descriptor['digest']:
                    raise PublicationError('Prepared image blob digest does not match')
            return path

        config = manifest.get('config')
        verify_blob(config, 'application/vnd.docker.container.image.v1+json', 4 * 1024 * 1024)
        if config['digest'] != expected['config_digest']:
            raise PublicationError('Prepared configuration differs from the validated image')
        layers = manifest.get('layers')
        if not isinstance(layers, list) or len(layers) != len(expected['layers']):
            raise PublicationError('Prepared image layers are incomplete')
        for descriptor, original in zip(layers, expected['layers'], strict=True):
            path = verify_blob(descriptor, 'application/vnd.docker.image.rootfs.diff.tar.gzip', original['size'] + 1024 * 1024)
            digest, size = hashlib.sha256(), 0
            with gzip.open(path, 'rb') as stream:
                while chunk := stream.read(1024 * 1024):
                    size += len(chunk)
                    if size > original['size']:
                        raise PublicationError('Prepared layer exceeds the validated uncompressed size')
                    digest.update(chunk)
            if size != original['size'] or 'sha256:' + digest.hexdigest() != original['digest']:
                raise PublicationError('Prepared layer differs from the validated image')
        return {'manifest_digest': 'sha256:' + hashlib.sha256(manifest_bytes).hexdigest(), 'manifest_size': len(manifest_bytes), 'config_digest': config['digest'], 'platform': expected['platform'], 'layers': layers}
    except (gzip.BadGzipFile, UnicodeDecodeError, json.JSONDecodeError, EOFError):
        raise PublicationError('Prepared image could not be inspected') from None
