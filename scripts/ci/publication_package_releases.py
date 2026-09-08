"""Resolve package release authority from durable origins and pinned producer receipts."""

from dataclasses import asdict

from publication import PublicationError, positive_integer
from publication_plan import resolve
from publication_release_authorization import release_identity
from publication_release_candidates import authorized_selection
from publication_release_receipts import ORIGIN, PACKAGE, SELECTION, endorsed, find_receipt, inspect_receipt
from publication_release_selection import validate_candidate
from publication_package_records import published_package
from publication_package_progress import revalidate_partial_receipt


def package_releases(client, context, catalog, release_id=None, explicit=None):
    """Read-only discovery; absent authority stays pending and cannot invent a release."""
    client.controller(context)
    components = [component for component in catalog['components'] if 'package' in component]
    if release_id is not None and (not positive_integer(release_id) or context.get('event_name') != 'workflow_dispatch'):
        raise PublicationError('An explicit package release requires a protected manual retry')
    releases = client.array('/releases')
    if release_id is not None and not any(item.get('id') == release_id for item in releases):
        raise PublicationError('Explicit package release is absent from complete GitHub enumeration')
    ready, published, pending, versions = [], [], [], set()
    for api_release in releases:
        component = next((item for item in components if isinstance(api_release.get('tag_name'), str) and api_release['tag_name'].startswith(item['tag_prefix'])), None)
        if component is None or api_release.get('draft') is not False:
            if api_release.get('id') == release_id:
                raise PublicationError('Requested release is not a published package component')
            continue
        release = release_identity(client, api_release, catalog)
        origin = find_receipt(client, release['id'], ORIGIN)
        selection = find_receipt(client, release['id'], SELECTION)
        if selection is not None and origin is None:
            raise PublicationError('Package selection has no immutable release origin')
        if origin is None or selection is None:
            pending.append({'release_id': release['id'], 'state': 'pending-origin' if origin is None else 'pending-producer'})
            continue
        pinned = authorized_selection(client, release, origin, selection, explicit if release['id'] == release_id else None)
        producer = pinned.get('producer', {})
        pair = producer.get('run_id'), producer.get('run_attempt')
        key = (component['package']['name'], release['version'])
        if key in versions:
            raise PublicationError('Multiple releases claim the same package version')
        versions.add(key)
        completed = inspect_receipt(client, release['id'], PACKAGE)
        if completed is not None:
            approved = endorsed(client, release['id'], completed)
            if not approved:
                revalidate_partial_receipt(client, completed)
            actual, _ = client.producer(*pair)
            if asdict(actual) != producer or producer.get('source_sha') != release['commit_sha'] or pinned.get('component_versions', {}).get(component['path']) != release['version']:
                raise PublicationError('Published package receipt differs from its exact successful producer')
            published.append(published_package(client, release, component, origin, selection, completed) | {'needs_endorsement': not approved})
            continue
        plan = resolve(client, context, *pair)
        if validate_candidate(plan, release, catalog) != pinned:
            raise PublicationError('Package producer differs from its immutable release receipt')
        ready.append({'release': release, 'component': component, 'origin': {'asset_id': origin['asset_id'], 'sha256': origin['sha256']},
                      'selection': {'asset_id': selection['asset_id'], 'sha256': selection['sha256']}, 'plan': plan})
    return {'ready': sorted(ready, key=lambda item: item['release']['id']), 'published': published, 'pending': pending, 'target_release_id': release_id}
