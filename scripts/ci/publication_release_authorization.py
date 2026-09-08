"""Authenticate release origins without treating action output or authors as authority."""

import base64
import json

from publication import PublicationError, matches, positive_integer
from publication_release_github import ACTIONS_ACTOR
from publication_release_origin import ACTION_JOB, COMMENT_PREFIX, PR_JOB, action_evidence, comment_record, recorded_job, verify_frame, verify_pr_origin
from release_evidence import actor, release_commit


def release_identity(client, release, catalog):
    if not positive_integer(release.get('id')) or release.get('draft') is not False or type(release.get('prerelease')) is not bool:
        raise PublicationError('Release must be an exact published GitHub release')
    tag = release.get('tag_name')
    components = [item for item in catalog['components'] if isinstance(tag, str) and tag.startswith(item['tag_prefix'])]
    if len(components) != 1:
        raise PublicationError('Release tag does not identify one trusted component')
    component = components[0]
    version = tag[len(component['tag_prefix']):]
    if not matches(r'[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?', version):
        raise PublicationError('Release tag has no valid component version')
    commit, chain = release_commit(client, tag)
    comparison = client.main_comparison(commit)
    if comparison.get('status') not in ('ahead', 'identical') or comparison.get('merge_base_commit', {}).get('sha') != commit:
        raise PublicationError('Release tag is not on protected main ancestry')
    document = client.get('/contents/.release-please-manifest.json?ref=' + commit)
    if document.get('type') != 'file' or document.get('encoding') != 'base64' or type(document.get('size')) is not int or not 0 < document['size'] <= 65536:
        raise PublicationError('Release source version manifest is not bounded')
    try:
        raw = base64.b64decode(''.join(document['content'].split()), validate=True)
        versions = json.loads(raw)
    except (KeyError, ValueError, TypeError, RecursionError):
        raise PublicationError('Release source version manifest is malformed') from None
    if len(raw) != document['size'] or not isinstance(versions, dict) or versions.get(component['path']) != version:
        raise PublicationError('Release tag version differs from its exact source manifest')
    return {'id': release['id'], 'component': component['id'], 'component_path': component['path'], 'version': version, 'tag_name': tag,
            'commit_sha': commit, 'tag_object_chain': chain, 'author': actor(release.get('author')), 'draft': False, 'prerelease': release['prerelease']}


def merged_origin(client, release):
    pulls = client.array('/commits/' + release['commit_sha'] + '/pulls')
    matches_ = []
    for summary in pulls:
        number = summary.get('number')
        if not positive_integer(number):
            raise PublicationError('Release commit PR enumeration is malformed')
        pr = client.get('/pulls/' + str(number))
        if pr.get('merged') is not True or pr.get('merge_commit_sha') != release['commit_sha'] or pr.get('base', {}).get('ref') != 'main':
            continue
        for comment in client.array('/issues/' + str(number) + '/comments'):
            body = comment.get('body')
            if isinstance(body, str) and body.startswith(COMMENT_PREFIX) and comment.get('user', {}).get('id') == ACTIONS_ACTOR['id']:
                record = comment_record(comment)
                if record.get('pull_request', {}).get('head_sha') != pr.get('head', {}).get('sha') or recorded_job(client, record.get('recorder'), PR_JOB).get('conclusion') != 'success':
                    continue
                origin = verify_pr_origin(client, comment, pr)
                matches_.append((pr, origin))
    if not matches_:
        return None
    identities = {(pr['id'], origin['record']['pull_request']['head_sha']) for pr, origin in matches_}
    if len(identities) != 1:
        raise PublicationError('Release commit has ambiguous protected PR origins')
    pr, origin = min(matches_, key=lambda item: item[1]['comment_id'])
    return {'pull_request_id': pr['id'], 'pull_request_number': pr['number'], 'head_sha': pr['head']['sha'], 'merge_commit_sha': pr['merge_commit_sha'], 'origin': origin}


def evidence_matches(evidence, release):
    observed = [value for value in evidence['document'].get('releases', []) if isinstance(value, dict) and value.get('id') == release['id']]
    if not observed:
        return False
    if len(observed) != 1 or any(observed[0].get(key) != value for key, value in release.items()) or release['author'] != evidence['document']['invocation']['credential_actor']:
        raise PublicationError('Release differs from its protected action observation')
    return True


def discover_evidence(client, release):
    runs = client.collection('/actions/workflows/publish.yml/runs?branch=main', 'workflow_runs')
    found = []
    for run in runs:
        if not positive_integer(run.get('id')) or not positive_integer(run.get('run_attempt')) or run['run_attempt'] > 100:
            raise PublicationError('Release automation enumeration is malformed')
        if run.get('event') not in ('workflow_run', 'workflow_dispatch'):
            continue
        artifacts = client.list(f"/actions/runs/{run['id']}/artifacts", 'artifacts')
        for attempt in range(1, run['run_attempt'] + 1):
            name = f"release-evidence-{run['id']}-{attempt}"
            eligible = [item for item in artifacts if item.get('name') == name and item.get('expired') is False]
            if not eligible:
                continue
            jobs = client.list(f"/actions/runs/{run['id']}/attempts/{attempt}/jobs", 'jobs')
            action = [job for job in jobs if job.get('name') == ACTION_JOB]
            if len(action) != 1 or action[0].get('conclusion') != 'success':
                continue
            evidence = action_evidence(client, run['id'], attempt)
            if evidence_matches(evidence, release):
                found.append(evidence)
    return min(found, key=lambda item: (item['frame']['run_id'], item['frame']['run_attempt'])) if found else None


def authenticate_origin(client, release, existing=None):
    if existing is not None:
        payload = existing['record']['payload']
        if set(payload) != {'release', 'merged_pr', 'release_please'} or payload.get('release') != release:
            raise PublicationError('Release changed after its immutable origin receipt')
        merged = payload['merged_pr']
        pr = client.get('/pulls/' + str(merged.get('pull_request_number')))
        if pr.get('id') != merged.get('pull_request_id') or pr.get('merged') is not True or pr.get('merge_commit_sha') != release['commit_sha'] or pr.get('head', {}).get('sha') != merged.get('head_sha'):
            raise PublicationError('Durable release origin differs from its merged PR')
        # The immutable asset retains exact comment bytes, but its live identity
        # must remain unedited and attached to this PR.
        comment = client.get('/issues/comments/' + str(merged['origin']['comment_id']))
        if verify_pr_origin(client, comment, pr) != merged['origin']:
            raise PublicationError('Durable release PR origin changed')
        evidence = payload['release_please']
        verify_frame(client, evidence['frame'], ACTION_JOB)
        if not evidence_matches(evidence, release):
            raise PublicationError('Durable release action evidence no longer matches')
        return payload
    merged = merged_origin(client, release)
    if merged is None:
        return None
    evidence = discover_evidence(client, release)
    if evidence is None:
        return None
    return {'release': release, 'merged_pr': merged, 'release_please': evidence}
