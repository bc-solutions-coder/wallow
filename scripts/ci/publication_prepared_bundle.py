"""Extract only the exact prepared files recorded by the authorized read-only job."""

import hashlib
import json
import shutil
import tarfile

from publication import PublicationError, matches, positive_integer
from publication_image_index import image_index
from publication_prepared_images import inspect_prepared_image


def extract_prepared(payload, destination, bundle, plan, catalog, preparation):
    record = plan['verified_images'][bundle]['prepared']
    inventory = record['inventory']
    identity = {'schema': 1, 'repository': preparation.repository, 'sha': preparation.source_sha, 'run_id': str(preparation.run_id), 'run_attempt': str(preparation.run_attempt), 'workflow_ref': preparation.workflow_ref, 'kind': 'prepared-images', 'variant': bundle + '-amd64-arm64'}
    source_artifact = next(item for item in plan['artifacts'] if item['kind'] == 'images' and item['variant'] == bundle + '-amd64-arm64')
    if record.get('file') != bundle + '/images.tar' or inventory.get('schema') != 1 or inventory.get('preparation') != identity or inventory.get('source_artifact') != source_artifact:
        raise PublicationError('Prepared artifact provenance differs from the exact authorized invocation')
    images = [item for item in catalog['images'] if item['bundle'] == bundle]
    expected = {(item['id'], platform, tag) for item in images for platform, tag in item['tags'].items()}
    if inventory.get('producer') != plan['producer'] or inventory.get('controller_sha') != plan['controller_sha'] or inventory.get('bundle') != bundle or inventory.get('publication_authorized') is not False or inventory.get('catalog_sha256') != 'sha256:' + hashlib.sha256(json.dumps(catalog, sort_keys=True, separators=(',', ':')).encode()).hexdigest():
        raise PublicationError('Prepared provenance differs from authorized source and catalog')
    if not isinstance(inventory.get('images'), list) or len(inventory['images']) != len(expected) or {(item.get('id'), item.get('platform'), item.get('tag')) for item in inventory['images']} != expected:
        raise PublicationError('Prepared image inventory is incomplete or unexpected')
    if not positive_integer(record.get('size')) or record['size'] > 16 * 1024**3 or payload.stat().st_size != record['size']:
        raise PublicationError('Prepared archive has an invalid size')
    with payload.open('rb') as stream:
        if 'sha256:' + hashlib.file_digest(stream, 'sha256').hexdigest() != record['digest']:
            raise PublicationError('Prepared archive differs from the authorized plan digest')
    expected_files = {'inventory.json': (16 * 1024 * 1024, None)}
    for item in inventory['images']:
        prefix = item['id'] + '/' + item['platform'].split('/')[1] + '/'
        prepared = item['prepared']
        if item['directory'] != prefix or item['source'] != plan['verified_images'][bundle]['images'][item['tag']]:
            raise PublicationError('Prepared platform identity differs from original inspected image')
        expected_files[prefix + 'manifest.json'] = (prepared['manifest_size'], prepared['manifest_digest'])
        if not matches(r'sha256:[a-f0-9]{64}', prepared.get('config_digest')):
            raise PublicationError('Prepared configuration digest is invalid')
        expected_files[prefix + prepared['config_digest'][7:]] = (4 * 1024 * 1024, prepared['config_digest'])
        for layer in prepared['layers']:
            if not matches(r'sha256:[a-f0-9]{64}', layer.get('digest')) or not positive_integer(layer.get('size')):
                raise PublicationError('Prepared layer identity is invalid')
            expected_files[prefix + layer['digest'][7:]] = (layer['size'], layer['digest'])
    if set(inventory.get('indexes', {})) != {item['id'] for item in images}:
        raise PublicationError('Prepared indexes are incomplete')
    for image in images:
        index = inventory['indexes'][image['id']]
        if index['file'] != image['id'] + '/index.json':
            raise PublicationError('Prepared index path is invalid')
        expected_files[index['file']] = (index['size'], index['digest'])
    seen = set()
    try:
        with payload.open('rb') as stream:
            while header := stream.read(512):
                if header == bytes(512):
                    if any(any(chunk) for chunk in iter(lambda: stream.read(1024 * 1024), b'')):
                        raise PublicationError('Prepared archive has unexpected trailing data')
                    break
                member = tarfile.TarInfo.frombuf(header, 'utf-8', 'strict')
                if member.type not in (tarfile.REGTYPE, tarfile.AREGTYPE) or member.name not in expected_files or member.name in seen or not 0 < member.size <= expected_files[member.name][0] or stream.tell() + member.size > record['size']:
                    raise PublicationError('Prepared archive contains unsafe, duplicate or unexpected members')
                seen.add(member.name)
                stream.seek((member.size + 511) // 512 * 512, 1)
        if seen != set(expected_files):
            raise PublicationError('Prepared archive is missing required files')
        destination.mkdir()
        with tarfile.open(payload, 'r:') as archive:
            for member in archive:
                target = destination / member.name
                target.parent.mkdir(parents=True, exist_ok=True)
                with archive.extractfile(member) as source, target.open('xb') as output:
                    shutil.copyfileobj(source, output, 1024 * 1024)
                with target.open('rb') as stream:
                    expected_digest = expected_files[member.name][1]
                    if expected_digest and 'sha256:' + hashlib.file_digest(stream, 'sha256').hexdigest() != expected_digest:
                        raise PublicationError('Prepared file differs from its recorded digest')
        if json.loads((destination / 'inventory.json').read_text()) != inventory:
            raise PublicationError('Prepared inventory differs from the sealed plan')
        result = {}
        for image in images:
            variants = {}
            for item in inventory['images']:
                if item['id'] != image['id']:
                    continue
                directory = destination / item['directory']
                verified = inspect_prepared_image(directory, item['source'])
                if verified != item['prepared']:
                    raise PublicationError('Prepared image bytes differ from the authorized identity')
                variants[item['platform']] = verified
            if (destination / (image['id'] + '/index.json')).read_bytes() != image_index(variants):
                raise PublicationError('Prepared index differs from its exact image variants')
            result[image['id']] = [item for item in inventory['images'] if item['id'] == image['id']]
        return result
    except (tarfile.TarError, UnicodeDecodeError, json.JSONDecodeError, EOFError, RecursionError):
        raise PublicationError('Prepared archive could not be inspected') from None
