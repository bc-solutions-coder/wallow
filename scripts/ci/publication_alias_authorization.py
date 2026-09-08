"""Authenticate durable immutable publications without depending on expired build artifacts."""

from dataclasses import asdict

from publication import PublicationError, image_repository
from publication_image_provenance import main_index
from publication_package_records import published_package
from publication_release_authorization import release_identity
from publication_release_candidates import authorized_selection
from publication_release_image_authorization import WRITER_JOB, job_name
from publication_release_origin import verify_frame
from publication_release_receipts import IMAGE, ORIGIN, PACKAGE, SELECTION, endorsed, find_receipt, inspect_receipt


def publication_records(client, context, catalog, kind, target=None, explicit=None):
    client.controller(context)
    components = {item['component'] for item in catalog['images']} if kind == 'image' else {item['id'] for item in catalog['components'] if 'package' in item}
    if kind not in ('image', 'package'):
        raise PublicationError('Unknown alias publication capability')
    if target is not None:
        if context.get('event_name') != 'workflow_dispatch':
            raise PublicationError('Explicit alias retry requires protected manual dispatch')
        release = release_identity(client, client.get('/releases/' + str(target)), catalog)
        if release['id'] != target:
            raise PublicationError('Explicit alias release differs from the API identity')
        if release['component'] not in components:
            return [], True
    result = []
    for value in client.array('/releases'):
        component = next((item for item in catalog['components'] if isinstance(value.get('tag_name'), str) and value['tag_name'].startswith(item['tag_prefix'])), None)
        if component is None or component['id'] not in components or value.get('draft') is not False:
            continue
        release = release_identity(client, value, catalog)
        receipt = inspect_receipt(client, release['id'], IMAGE if kind == 'image' else PACKAGE)
        if receipt is None or not endorsed(client, release['id'], receipt):
            continue
        origin, selection = find_receipt(client, release['id'], ORIGIN), find_receipt(client, release['id'], SELECTION)
        if origin is None or selection is None:
            raise PublicationError('Immutable publication has no authenticated origin and producer selection')
        pinned = authorized_selection(client, release, origin, selection, explicit if release['id'] == target else None)
        producer = pinned['producer']
        actual, _ = client.producer(producer['run_id'], producer['run_attempt'])
        if asdict(actual) != producer or producer['source_sha'] != release['commit_sha'] or pinned['component_versions'].get(component['path']) != release['version']:
            raise PublicationError('Immutable publication differs from its successful selected producer')
        if kind == 'package':
            package = published_package(client, release, component, origin, selection, receipt)['package']
            outputs = {'package': asdict(package)}
        else:
            payload = receipt['record']['payload']
            if set(payload) != {'release', 'origin', 'selection', 'images', 'writer'} or payload['release'] != release or payload['origin'] != {'asset_id': origin['asset_id'], 'sha256': origin['sha256']} or payload['selection'] != {'asset_id': selection['asset_id'], 'sha256': selection['sha256']}:
                raise PublicationError('Image publication differs from exact release receipt identities')
            verify_frame(client, payload['writer']['frame'], job_name(WRITER_JOB, release['id']))
            images = {image['id']: image for image in catalog['images'] if image['component'] == release['component']}
            if not isinstance(payload['images'], list) or len(payload['images']) != len(images) or {item.get('image') for item in payload['images']} != images.keys():
                raise PublicationError('Durable image receipt has incomplete component coverage')
            for item in payload['images']:
                image = images[item['image']]
                if set(item) != {'image', 'repository', 'version', 'source_sha', 'index_digest', 'platforms', 'readback'} or item['repository'] != image_repository(client.repository, image) or item['version'] != release['version'] or item['source_sha'] != release['commit_sha'] or item['readback'] != 'verified' or set(item['platforms']) != set(image['tags']):
                    raise PublicationError('Durable image identity differs from the protected catalog')
                main_index(client.repository, release['commit_sha'], image['id'], item['platforms'])
            outputs = {'images': payload['images']}
        result.append({'release': release, 'receipt': {'asset_id': receipt['asset_id'], 'sha256': receipt['sha256']},
                       'origin': {'asset_id': origin['asset_id'], 'sha256': origin['sha256']}, 'selection': {'asset_id': selection['asset_id'], 'sha256': selection['sha256']},
                       'selected': pinned, 'outputs': outputs})
    identities = [(item['release']['component'], item['release']['version']) for item in result]
    if len(identities) != len(set(identities)) or len(result) > 1000:
        raise PublicationError('Durable release publication identities are ambiguous or unbounded')
    if target is not None and not any(item['release']['id'] == target for item in result):
        raise PublicationError('Requested alias target is pending a successful immutable publication receipt')
    return sorted(result, key=lambda item: item['release']['id']), False
