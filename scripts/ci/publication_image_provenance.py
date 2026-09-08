"""Stable main-image identity and fail-closed nightly ordering."""

import hashlib
import json

from publication import PublicationError, matches
from publication_image_index import image_index


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
