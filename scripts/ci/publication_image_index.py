"""Create an exact two-platform manifest list from verified prepared images."""

import json

from publication import PublicationError, matches, positive_integer


def image_index(variants):
    platforms = ('linux/amd64', 'linux/arm64')
    if not isinstance(variants, dict) or set(variants) != set(platforms):
        raise PublicationError('Image publication requires exactly AMD64 and ARM64 variants')
    manifests = []
    for platform in platforms:
        image = variants[platform]
        if not isinstance(image, dict) or image.get('platform') != platform or not matches(r'sha256:[0-9a-f]{64}', image.get('manifest_digest')) or not positive_integer(image.get('manifest_size')):
            raise PublicationError('Image variant has an invalid verified manifest identity')
        operating_system, architecture = platform.split('/')
        manifests.append({'mediaType': 'application/vnd.docker.distribution.manifest.v2+json', 'digest': image['manifest_digest'], 'size': image['manifest_size'], 'platform': {'os': operating_system, 'architecture': architecture}})
    if manifests[0]['digest'] == manifests[1]['digest']:
        raise PublicationError('Distinct image platforms cannot share one manifest')
    return json.dumps({'schemaVersion': 2, 'mediaType': 'application/vnd.docker.distribution.manifest.list.v2+json', 'manifests': manifests}, sort_keys=True, separators=(',', ':')).encode()
