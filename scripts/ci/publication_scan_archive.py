"""Serialize one verified image as regular Docker-save files for vulnerability scanning."""

import gzip
import io
import json
from pathlib import Path
import tarfile

from publication import PublicationError, matches
from publication_images import inspect_images
from publication_prepared_images import inspect_prepared_image


def write_scan_archive(prepared, expected, tag, destination):
    if not matches(r'[a-z0-9][a-z0-9_.-]*:[a-zA-Z0-9_.-]+', tag):
        raise PublicationError('An exact catalog image tag is required')
    prepared, destination = Path(prepared), Path(destination)
    verified = inspect_prepared_image(prepared, expected)
    config_name = verified['config_digest'][7:] + '.json'
    layer_names = [layer['digest'][7:] + '.tar' for layer in expected['layers']]
    manifest = json.dumps([{'Config': config_name, 'RepoTags': [tag], 'Layers': layer_names}], separators=(',', ':')).encode()
    created = False
    try:
        with destination.open('xb') as raw:
            created = True
            with gzip.GzipFile(fileobj=raw, mode='wb', mtime=0) as compressed, tarfile.open(fileobj=compressed, mode='w|', format=tarfile.USTAR_FORMAT) as archive:
                def add(name, size, stream):
                    member = tarfile.TarInfo(name)
                    member.size, member.mode = size, 0o644
                    archive.addfile(member, stream)

                add('manifest.json', len(manifest), io.BytesIO(manifest))
                config = (prepared / verified['config_digest'][7:]).read_bytes()
                add(config_name, len(config), io.BytesIO(config))
                written = set()
                for name, original, descriptor in zip(layer_names, expected['layers'], verified['layers'], strict=True):
                    if name in written:
                        continue
                    with gzip.open(prepared / descriptor['digest'][7:], 'rb') as stream:
                        add(name, original['size'], stream)
                    written.add(name)
        actual = inspect_images(destination, {tag: expected['platform']})[tag]
        if actual['config_digest'] != expected['config_digest'] or [(layer['digest'], layer['size']) for layer in actual['layers']] != [(layer['digest'], layer['size']) for layer in expected['layers']]:
            raise PublicationError('Scanner archive differs from the validated image')
        return destination
    except BaseException:
        if created:
            destination.unlink(missing_ok=True)
        raise
