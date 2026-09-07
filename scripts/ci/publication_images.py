"""Verify Docker-save image identities without loading or executing containers."""

import gzip
import hashlib
import json
from pathlib import Path
import re
import tarfile

from publication import PublicationError, matches
from publication_archives import bounded_tar


def inspect_images(path, expected, max_bytes=8 * 1024**3):
    """Expected maps exact local tags to linux/amd64 or linux/arm64."""
    if not isinstance(expected, dict) or not expected or any(not matches(r'[a-z0-9][a-z0-9_.-]*:[a-zA-Z0-9_.-]+', tag) or platform not in ('linux/amd64', 'linux/arm64') for tag, platform in expected.items()):
        raise PublicationError('Expected image tags and platforms are required')
    path = Path(path)
    if path.is_symlink() or not path.is_file():
        raise PublicationError('Image export must be a regular file')
    try:
        with bounded_tar(path, max_bytes, plain_headers=True) as archive:
            members = {}
            for member in archive:
                if member.name in members or member.name.startswith('/') or any(part in ('', '.', '..') for part in member.name.split('/')) or '\\' in member.name or member.issparse() or not (member.isfile() or member.isdir()):
                    raise PublicationError('Image export contains duplicate or unsafe archive members')
                members[member.name] = member
                if len(members) > 100000:
                    raise PublicationError('Image export contains too many archive members')

            def file(name):
                member = members.get(name) if isinstance(name, str) else None
                if member is None or not member.isfile():
                    raise PublicationError('Image export references a missing regular file')
                return member

            def read_json(name):
                member = file(name)
                if member.size > 4 * 1024 * 1024:
                    raise PublicationError('Image metadata exceeds its size limit')
                with archive.extractfile(member) as stream:
                    return json.load(stream)

            digests = {}

            def digest(name):
                if not isinstance(name, str):
                    raise PublicationError('Image layer reference must be a file name')
                if name not in digests:
                    with archive.extractfile(file(name)) as stream:
                        value = hashlib.file_digest(stream, 'sha256').hexdigest()
                    address = re.fullmatch(r'blobs/sha256/([0-9a-f]{64})|([0-9a-f]{64})\.json', name)
                    if address and value != (address[1] or address[2]):
                        raise PublicationError('Image content differs from its digest address')
                    digests[name] = 'sha256:' + value
                return digests[name]

            manifest = read_json('manifest.json')
            if not isinstance(manifest, list) or not manifest or len(manifest) > len(expected):
                raise PublicationError('Invalid Docker image manifest')
            images = {}
            for item in manifest:
                if not isinstance(item, dict) or not isinstance(item.get('RepoTags'), list) or not item['RepoTags'] or not isinstance(item.get('Layers'), list):
                    raise PublicationError('Invalid tagged image descriptor')
                config_path = item.get('Config')
                if not matches(r'blobs/sha256/[0-9a-f]{64}|[0-9a-f]{64}\.json', config_path):
                    raise PublicationError('Image configuration requires a SHA256 content address')
                config = read_json(config_path)
                if not isinstance(config, dict) or not isinstance(config.get('rootfs'), dict) or config['rootfs'].get('type') != 'layers' or not isinstance(config['rootfs'].get('diff_ids'), list):
                    raise PublicationError('Invalid image configuration')
                layers = item['Layers']
                if any(not isinstance(layer, str) or (layer.startswith('blobs/') and not matches(r'blobs/sha256/[0-9a-f]{64}', layer)) for layer in layers):
                    raise PublicationError('Image layer has an invalid content address')
                diff_ids = config['rootfs']['diff_ids']
                if len(layers) != len(diff_ids) or any(not matches(r'sha256:[0-9a-f]{64}', value) for value in diff_ids):
                    raise PublicationError('Image layer identities are incomplete')
                actual_layers = [digest(layer) for layer in layers]
                if actual_layers != diff_ids:
                    raise PublicationError('Exported uncompressed image layers differ from configuration')
                platform = str(config.get('os')) + '/' + str(config.get('architecture'))
                for tag in item['RepoTags']:
                    if not isinstance(tag, str) or tag in images or expected.get(tag) != platform:
                        raise PublicationError('Image tag or platform differs from the publication catalog')
                    images[tag] = {'platform': platform, 'config_digest': digest(config_path), 'diff_ids': diff_ids, 'layers': [{'path': layer, 'digest': value, 'size': file(layer).size} for layer, value in zip(layers, actual_layers)]}
            if images.keys() != expected.keys():
                raise PublicationError('Image export is missing required publication variants')
            return images
    except (tarfile.TarError, gzip.BadGzipFile, UnicodeDecodeError, json.JSONDecodeError, EOFError):
        raise PublicationError('Image export could not be inspected') from None
