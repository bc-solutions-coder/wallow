"""Inspect authorized image bundles sequentially without executing their contents."""

import json
from pathlib import Path
import tempfile

from publication import PublicationError
from publication_identity import producer_from_record
from publication_artifacts import Artifact, unpack_payload
from publication_images import inspect_images


def verify_images(client, plan, catalog, prepare=None):
    """Inputs are the resolved API-authorized plan and the trusted loaded catalog."""
    required = {'full': ('app', 'infra', 'docs'), 'docs': ('docs',)}.get(plan['route'])
    if required is None:
        raise PublicationError('Unknown image publication route')
    bundles = {}
    for item in plan['artifacts']:
        if item['kind'] != 'images':
            continue
        bundle = item['variant'].removesuffix('-amd64-arm64')
        if bundle not in required or item['variant'] != bundle + '-amd64-arm64' or bundle in bundles or item['payload'] != 'images.tar.gz':
            raise PublicationError('Image artifact does not match an exact required bundle')
        bundles[bundle] = item
    if set(bundles) != set(required):
        raise PublicationError('Required image artifact bundles are incomplete')
    producer = producer_from_record(plan['producer'])
    verified = {}
    for bundle in required:
        expected = {tag: platform for image in catalog['images'] if image['bundle'] == bundle for platform, tag in image['tags'].items()}
        if not expected:
            raise PublicationError('Required image bundle has no catalog images')
        companions = {image['tag']: image['platform'] for image in catalog['validation_only_images'] if image['bundle'] == bundle}
        item = bundles[bundle]
        artifact = Artifact(**{key: item[key] for key in ('id', 'name', 'digest', 'size')})
        with tempfile.TemporaryDirectory(prefix='wallow-image-inspection-') as directory:
            root = Path(directory)
            archive = client.download(artifact, root / 'artifact.zip')
            payload = unpack_payload(archive, root / 'verified', artifact, producer, item['payload'], 'images', item['variant'], 8 * 1024**3)
            inspected = inspect_images(payload, expected | companions)
            verified[bundle] = {'artifact_id': artifact.id, 'images': {tag: inspected[tag] for tag in expected}}
            if prepare is not None:
                verified[bundle]['prepared'] = prepare(bundle, payload, verified[bundle]['images'])
    return verified
