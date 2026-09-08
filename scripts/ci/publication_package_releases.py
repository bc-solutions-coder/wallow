"""Resolve package release authority from durable origins and pinned producer receipts."""

from dataclasses import asdict

from publication import PublicationError, positive_integer
from publication_plan import resolve
from publication_selection import resolve_selection
from publication_release_authorization import release_identity
from publication_release_candidates import authorized_selection
from publication_release_receipts import ORIGIN, PACKAGE, SELECTION, endorsed, find_receipt, inspect_receipt
from publication_release_selection import validate_candidate
from publication_package_records import published_package
from publication_package_progress import revalidate_partial_receipt


def package_releases(client, context, catalog, release_id=None, explicit=None, *, recovery=None):
    """Read-only discovery; absent authority stays pending and cannot invent a release."""
    if recovery is not None and (not isinstance(recovery, dict) or set(recovery) != {'release_id', 'run_id', 'run_attempt'} or not all(positive_integer(value) for value in recovery.values()) or recovery['release_id'] != release_id or not isinstance(context, dict) or context.get('event_name') != 'workflow_dispatch' or not isinstance(explicit, tuple) or len(explicit) != 2 or not all(positive_integer(value) for value in explicit)):
        raise PublicationError('Package recovery requires an exact manual target and original producer selection')
    recovered_metadata = None
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
        identity = api_release.get('id')
        if not positive_integer(identity):
            raise PublicationError('Package release has no exact API identity')
        origin = find_receipt(client, identity, ORIGIN)
        selection = find_receipt(client, identity, SELECTION)
        if identity != release_id and origin is None and selection is None:
            pending.append({'release_id': identity, 'state': 'pending-origin'})
            continue
        release = release_identity(client, api_release, catalog)
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
        recovered_plan = None
        if recovery is not None and release['id'] == release_id:
            recovered_plan = resolve_selection(client, context, *pair, catalog, recovery=recovery)
            if recovered_plan.get('recovery', {}).get('original') != pinned:
                raise PublicationError('Recovered package inputs differ from the original selected producer')
            validate_candidate(recovered_plan, release, catalog)
            recovered_metadata = recovered_plan['recovery']
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
        plan = recovered_plan if recovered_plan is not None else resolve(client, context, *pair)
        if recovered_plan is None and validate_candidate(plan, release, catalog) != pinned:
            raise PublicationError('Package producer differs from its immutable release receipt')
        ready.append({'release': release, 'component': component, 'origin': {'asset_id': origin['asset_id'], 'sha256': origin['sha256']},
                      'selection': {'asset_id': selection['asset_id'], 'sha256': selection['sha256']}, 'plan': plan})
    result = {'ready': sorted(ready, key=lambda item: item['release']['id']), 'published': published, 'pending': pending, 'target_release_id': release_id}
    if recovery is not None:
        if recovered_metadata is None:
            raise PublicationError('Requested package recovery is pending its original release authority')
        result['recovery'] = recovered_metadata
    return result
