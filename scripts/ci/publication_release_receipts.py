"""Immutable release origin and producer receipts, authenticated through protected jobs."""

from datetime import datetime, timezone
import hashlib
import json

from publication import PublicationError, matches, positive_integer
from publication_release_github import ACTIONS_ACTOR, RECORD_LIMIT
from publication_release_origin import RECEIPT_JOB, canonical, timestamp, recorded_job

ORIGIN = 'wallow-release-origin-v1.json'
SELECTION = 'wallow-release-selection-v1.json'


def digest(body):
    return 'sha256:' + hashlib.sha256(body).hexdigest()


def inspect_asset(client, asset, release_id, name, current=None):
    if not isinstance(asset, dict) or asset.get('name') != name or asset.get('state') != 'uploaded' or asset.get('content_type') != 'application/json' or {key: asset.get('uploader', {}).get(key) for key in ACTIONS_ACTOR} != ACTIONS_ACTOR:
        raise PublicationError('Release receipt is not an immutable GitHub Actions JSON asset')
    body = client.asset_bytes(asset)
    try:
        record = json.loads(body)
    except (ValueError, RecursionError):
        raise PublicationError('Release receipt is malformed') from None
    if not isinstance(record, dict) or set(record) != {'schema', 'type', 'publication_authorized', 'release_id', 'recorder', 'payload'} or record.get('schema') != 1 or record.get('type') != name or record.get('publication_authorized') is not False or record.get('release_id') != release_id or not isinstance(record.get('payload'), dict):
        raise PublicationError('Release receipt identity differs from its requested release')
    if name not in (ORIGIN, SELECTION) and (not matches(r'wallow-release-endorsement-v1-[1-9][0-9]*-[1-9][0-9]*-[1-9][0-9]*\.json', name) or name != f"wallow-release-endorsement-v1-{record['payload'].get('asset_id')}-{record['recorder'].get('run_id')}-{record['recorder'].get('run_attempt')}.json"):
        raise PublicationError('Release endorsement name differs from its exact invocation and asset')
    if canonical(record) != body:
        raise PublicationError('Release receipt bytes are not canonical')
    if current is None:
        job = recorded_job(client, record['recorder'], RECEIPT_JOB)
        end = timestamp(job.get('completed_at'))
    else:
        frame, job = current
        if record['recorder'] != frame:
            raise PublicationError('New release receipt differs from its active writer')
        end = datetime.now(timezone.utc)
    if not timestamp(job.get('started_at')) <= timestamp(asset.get('created_at')) <= timestamp(asset.get('updated_at')) <= end:
        raise PublicationError('Release receipt was not uploaded during its recorded protected job')
    return {'asset_id': asset['id'], 'name': name, 'sha256': digest(body), 'body': body.decode(), 'record': record, 'originating_job_success': job.get('conclusion') == 'success'}


def inspect_receipt(client, release_id, name):
    assets = client.array(f'/releases/{release_id}/assets')
    found = [asset for asset in assets if asset.get('name') == name]
    if len(found) > 1:
        raise PublicationError('Release receipt identity is ambiguous')
    return inspect_asset(client, found[0], release_id, name) if found else None


def retain(client, release_id, name, payload, current):
    if not positive_integer(release_id) or name not in (ORIGIN, SELECTION) or not isinstance(payload, dict):
        raise PublicationError('Invalid immutable release receipt request')
    existing = inspect_receipt(client, release_id, name)
    if existing:
        if existing['record']['payload'] != payload:
            raise PublicationError('Release receipt conflicts with its immutable prior selection')
        if not endorsed(client, release_id, existing):
            endorsement = f"wallow-release-endorsement-v1-{existing['asset_id']}-{current[0]['run_id']}-{current[0]['run_attempt']}.json"
            write_receipt(client, release_id, endorsement, {'asset_id': existing['asset_id'], 'sha256': existing['sha256']}, current)
        return existing
    return write_receipt(client, release_id, name, payload, current)


def write_receipt(client, release_id, name, payload, current):
    frame, _ = current
    record = {'schema': 1, 'type': name, 'publication_authorized': False, 'release_id': release_id, 'recorder': frame, 'payload': payload}
    body = canonical(record)
    if len(body) > RECORD_LIMIT:
        raise PublicationError('Release receipt exceeds its bounded size')
    asset = client.upload(release_id, name, body)
    if not positive_integer(asset.get('id')):
        raise PublicationError('Release upload did not return an exact asset identity')
    readback = client.get('/releases/assets/' + str(asset['id']))
    for key in ('id', 'name', 'size', 'digest', 'uploader', 'state', 'content_type', 'created_at', 'updated_at'):
        if readback.get(key) != asset.get(key):
            raise PublicationError('Release receipt metadata changed after upload')
    verified = inspect_asset(client, readback, release_id, name, current)
    if verified['body'].encode() != body:
        raise PublicationError('Release receipt readback differs from its exact written bytes')
    return verified


def endorsed(client, release_id, receipt):
    if receipt['originating_job_success']:
        return True
    prefix = f"wallow-release-endorsement-v1-{receipt['asset_id']}-"
    for asset in client.array(f'/releases/{release_id}/assets'):
        if isinstance(asset.get('name'), str) and asset['name'].startswith(prefix):
            endorsement = inspect_asset(client, asset, release_id, asset['name'])
            if endorsement['originating_job_success'] and endorsement['record']['payload'] == {'asset_id': receipt['asset_id'], 'sha256': receipt['sha256']}:
                return True
    return False


def find_receipt(client, release_id, name):
    receipt = inspect_receipt(client, release_id, name)
    if receipt is not None and not endorsed(client, release_id, receipt):
        raise PublicationError('Release receipt still requires full revalidation by a successful recorder')
    return receipt
