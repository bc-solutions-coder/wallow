"""Recognize only the reviewed, expiring legacy nightly cutover snapshot."""

from datetime import datetime, timedelta, timezone

from publication import PublicationError, matches, positive_integer


def legacy_source(client, registry, current, repository, image_id, record, now=None):
    if record is None or record.get('repository') != repository:
        return None
    fields = {'schema', 'repository', 'source_sha', 'run_id', 'run_attempt', 'workflow_id', 'tag', 'created', 'expires', 'evidence', 'images'}
    if set(record) != fields or type(record['schema']) is not int or record['schema'] != 1 or not matches(r'sha1:[a-f0-9]{40}', record['source_sha']):
        raise PublicationError('Legacy nightly cutover snapshot is invalid')
    source = record['source_sha'].removeprefix('sha1:')
    images = record['images']
    if not isinstance(images, dict) or not images or any(not matches(r'[a-z][a-z0-9-]*', key) or not matches(r'sha256:[a-f0-9]{64}', value) for key, value in images.items()):
        raise PublicationError('Legacy nightly cutover image identities are invalid')
    if current['digest'] != images.get(image_id):
        return None
    if not all(positive_integer(record[key]) for key in ('run_id', 'run_attempt', 'workflow_id')) or record['tag'] != source[:7]:
        raise PublicationError('Legacy nightly deployment identity is invalid')
    try:
        expires = datetime.strptime(record['expires'], '%Y-%m-%dT%H:%M:%SZ').replace(tzinfo=timezone.utc)
        created = datetime.strptime(record['created'], '%Y-%m-%dT%H:%M:%SZ').replace(tzinfo=timezone.utc)
    except (TypeError, ValueError):
        raise PublicationError('Legacy nightly cutover expiry is invalid') from None
    current_time = now or datetime.now(timezone.utc)
    if not created <= current_time < expires or not timedelta(0) < expires - created <= timedelta(days=30):
        raise PublicationError('Legacy nightly cutover approval expired')
    run = client.get(f"/actions/runs/{record['run_id']}/attempts/{record['run_attempt']}")
    expected = {'id': record['run_id'], 'run_attempt': record['run_attempt'], 'workflow_id': record['workflow_id'],
                'path': '.github/workflows/deploy.yml', 'head_sha': source, 'head_branch': 'main',
                'event': 'push', 'status': 'completed', 'conclusion': 'success'}
    if not isinstance(run, dict) or any(run.get(key) != value for key, value in expected.items()):
        raise PublicationError('Reviewed legacy deployment metadata no longer matches')
    for key in ('repository', 'head_repository'):
        if run.get(key, {}).get('full_name') != repository:
            raise PublicationError('Legacy deployment belongs to another repository')
    if registry.read_manifest(record['tag']) != current:
        raise PublicationError('Legacy nightly differs from its reviewed deployment tag')
    return source
