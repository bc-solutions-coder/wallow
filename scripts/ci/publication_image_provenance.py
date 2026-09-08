"""Stable main-image identity and fail-closed nightly ordering."""

import hashlib
import json

from publication import PublicationError, main_ancestor, matches
from publication_image_index import image_index
from publication_legacy_nightly import legacy_source


OCI_INDEX = 'application/vnd.oci.image.index.v1+json'


def main_index(repository, source, image_id, variants):
    if not matches(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repository) or not matches(r'[a-f0-9]{40}', source) or not matches(r'[a-z][a-z0-9-]*', image_id):
        raise PublicationError('Main image provenance identity is invalid')
    prepared = image_index(variants)
    value = json.loads(prepared)
    value['mediaType'] = OCI_INDEX
    value['annotations'] = {
        'org.opencontainers.image.source': 'https://github.com/' + repository,
        'org.opencontainers.image.revision': source,
        'io.wallow.image.id': image_id,
        'io.wallow.image.content-digest': 'sha256:' + hashlib.sha256(prepared).hexdigest(),
        **{'io.wallow.image.' + platform.split('/')[1] + '.digest': image['manifest_digest'] for platform, image in variants.items()},
    }
    return json.dumps(value, sort_keys=True, separators=(',', ':')).encode()


def prior_source(value, repository, image_id):
    try:
        document = json.loads(value['bytes'])
        annotations = document['annotations']
        source = annotations['org.opencontainers.image.revision']
        variants = {}
        for item in document['manifests']:
            platform = item['platform']['os'] + '/' + item['platform']['architecture']
            if platform in variants or item['mediaType'] != 'application/vnd.docker.distribution.manifest.v2+json':
                raise ValueError()
            variants[platform] = {'platform': platform, 'manifest_digest': item['digest'], 'manifest_size': item['size']}
        expected = main_index(repository, source, image_id, variants)
        if value['media_type'] != OCI_INDEX or value['bytes'] != expected or value['digest'] != 'sha256:' + hashlib.sha256(expected).hexdigest():
            raise ValueError()
        return source
    except (KeyError, TypeError, ValueError, UnicodeDecodeError):
        raise PublicationError('Nightly has unknown or invalid main image provenance') from None


def nightly_action(registry, client, current, repository, image_id, source, legacy=None):
    if current is None:
        return 'advance'
    previous = legacy_source(client, registry, current, repository, image_id, legacy)
    if previous is None:
        previous = prior_source(current, repository, image_id)
        if registry.read_manifest('sha-' + previous) != current:
            raise PublicationError('Nightly does not match its exact immutable main image')
    if not main_ancestor(client.main_comparison(previous), previous):
        raise PublicationError('Nightly source is not on current main history')
    comparison = client.get(f'/compare/{previous}...{source}')
    if main_ancestor(comparison, previous):
        return 'advance'
    if isinstance(comparison, dict) and comparison.get('status') == 'behind' and comparison.get('base_commit', {}).get('sha') == previous and comparison.get('merge_base_commit', {}).get('sha') == source:
        return 'skip-older'
    raise PublicationError('Nightly source history is incomparable or unresolved')
