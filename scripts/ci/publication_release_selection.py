"""Bind a release component to one exact successful immutable producer registration."""

import urllib.parse

from publication import PublicationError, matches, positive_integer
from publication_plan import resolve


def validate_candidate(plan, release, catalog):
    if plan.get('producer', {}).get('source_sha') != release['commit_sha'] or plan.get('inputs', {}).get('catalog') != catalog or plan.get('inputs', {}).get('component_versions', {}).get(release['component_path']) != release['version']:
        raise PublicationError('Release producer source, version, or catalog differs from the release')
    component = next((item for item in catalog['components'] if item['id'] == release['component']), None)
    if component is None or component['path'] != release['component_path'] or component['tag_prefix'] + release['version'] != release['tag_name']:
        raise PublicationError('Release does not match the trusted component catalog')
    if plan['route'] != 'full' and component['id'] != 'docs':
        raise PublicationError('Documentation-only validation cannot authorize another release component')
    return {'producer': plan['producer'], 'registration': plan['registration'], 'artifacts': plan['artifacts'], 'component_versions': plan['inputs']['component_versions'],
            'catalog': plan['inputs']['catalog'], 'input_sha256': plan['inputs']['input_sha256']}


def choose_producer(client, context, release, catalog, pinned=None, explicit=None, resolver=resolve):
    """A receipt pins identity permanently; unavailable old artifacts never select a newer run."""
    if pinned is not None:
        identity = pinned.get('producer', {})
        pair = (identity.get('run_id'), identity.get('run_attempt'))
        if explicit is not None and explicit != pair:
            raise PublicationError('Explicit retry conflicts with the durable selected producer')
        candidate = validate_candidate(resolver(client, context, *pair), release, catalog)
        if candidate != pinned:
            raise PublicationError('Selected producer differs from its immutable release receipt')
        return candidate
    if explicit is not None:
        if len(explicit) != 2 or not all(positive_integer(value) for value in explicit):
            raise PublicationError('Manual release retry requires an exact producer run and attempt')
        return validate_candidate(resolver(client, context, *explicit), release, catalog)
    if not matches(r'[a-f0-9]{40}', release.get('commit_sha')):
        raise PublicationError('Release has no exact source commit')
    query = urllib.parse.urlencode({'event': 'push', 'head_sha': release['commit_sha'], 'branch': 'main'})
    runs = client.collection('/actions/workflows/ci.yml/runs?' + query, 'workflow_runs')
    candidates = []
    for run in runs:
        if not positive_integer(run.get('id')) or not positive_integer(run.get('run_attempt')) or run['run_attempt'] > 100 or run.get('head_sha') != release['commit_sha'] or run.get('event') != 'push' or run.get('head_branch') != 'main':
            raise PublicationError('Release producer enumeration contains an unexpected identity')
        for attempt in range(1, run['run_attempt'] + 1):
            observed = client.get(f"/actions/runs/{run['id']}/attempts/{attempt}")
            if observed.get('id') != run['id'] or observed.get('run_attempt') != attempt or observed.get('head_sha') != release['commit_sha']:
                raise PublicationError('Release producer attempt enumeration changed')
            if observed.get('status') == 'completed' and observed.get('conclusion') == 'success':
                jobs = client.list(f"/actions/runs/{run['id']}/attempts/{attempt}/jobs", 'jobs')
                registration = [job for job in jobs if job.get('name') == 'register-publication']
                if len(registration) > 1:
                    raise PublicationError('Producer registration job identity is ambiguous')
                if registration and registration[0].get('conclusion') == 'success':
                    candidates.append((run['id'], attempt))
                elif registration and registration[0].get('conclusion') not in ('skipped', 'failure', 'cancelled'):
                    raise PublicationError('Producer registration job has an unexpected state')

    if not candidates:
        return None
    # Earlier registered successful runs must remain available. Do not silently
    # skip an expired or malformed candidate to promote newer bytes.
    pair = min(candidates)
    return validate_candidate(resolver(client, context, *pair), release, catalog)
