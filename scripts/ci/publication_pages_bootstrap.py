"""Bound the one-time transition from the retired Docs deployment to Pages intents."""

from datetime import datetime, timedelta, timezone
import json
from pathlib import Path

from publication import PublicationError, main_ancestor, matches
from publication_pages_history import TASK, inspect_intent
from publication_release_origin import timestamp


def pages_history(client, records, source, identity, site_url, bootstrap_path=None):
    latest = max(records, key=lambda record: record['id']) if records else None
    if latest is not None and latest.get('task') == TASK:
        intent = inspect_intent(client, latest)
        if any(intent['history_tail_id'] < record['id'] < latest['id'] or timestamp(record.get('created_at')) > timestamp(latest['created_at']) for record in records):
            raise PublicationError('Pages history changed between its protected check and intent creation')
        return [intent]
    if any(record.get('task') == TASK for record in records):
        raise PublicationError('Pages has an unrecognized deployment after the verified cutover')
    if not records:
        client.ensure_unpublished(site_url)
        return []
    path = Path(bootstrap_path) if bootstrap_path else None
    if path is None or not path.is_file():
        raise PublicationError('Existing Pages deployment requires a reviewed bootstrap')
    bootstrap = json.loads(path.read_text())
    now = datetime.now(timezone.utc)
    if bootstrap.get('schema') != 1 or bootstrap.get('repository') != client.repository or not timestamp(bootstrap.get('created_at')) <= now < timestamp(bootstrap.get('expires_at')) or timestamp(bootstrap['expires_at']) - timestamp(bootstrap['created_at']) > timedelta(days=30) or not matches(r'git:[a-f0-9]{40}', bootstrap.get('source_sha')) or not matches(r'sha256:[a-f0-9]{64}', bootstrap.get('index_sha256')) or bootstrap.get('site_url') != site_url:
        raise PublicationError('Pages bootstrap is expired or belongs to another target')
    previous = bootstrap['source_sha'][4:]
    if latest.get('id') != bootstrap.get('deployment_id') or latest.get('sha') != previous or latest.get('task') != 'deploy' or latest.get('environment') != 'github-pages' or latest.get('payload') != {}:
        raise PublicationError('Current Pages deployment differs from the reviewed bootstrap')
    if any(timestamp(record.get('created_at')) > timestamp(latest['created_at']) for record in records):
        raise PublicationError('Pages deployment history ordering is unresolved')
    run = client.get(f"/actions/runs/{bootstrap['run_id']}/attempts/{bootstrap['run_attempt']}")
    if run.get('id') != bootstrap['run_id'] or run.get('run_attempt') != bootstrap['run_attempt'] or run.get('workflow_id') != bootstrap['workflow_id'] or run.get('path') != '.github/workflows/docs.yml' or run.get('head_sha') != previous or run.get('event') != 'push' or run.get('head_branch') != 'main' or run.get('status') != 'completed' or run.get('conclusion') != 'success' or any(run.get(key, {}).get('full_name') != client.repository for key in ('repository', 'head_repository')):
        raise PublicationError('Historical Docs producer differs from the reviewed successful main run')
    jobs = client.list(f"/actions/runs/{bootstrap['run_id']}/attempts/{bootstrap['run_attempt']}/jobs", 'jobs')
    selected = [job for job in jobs if job.get('id') == bootstrap['job_id']]
    if len(selected) != 1 or selected[0].get('status') != 'completed' or selected[0].get('conclusion') != 'success' or selected[0].get('head_sha') != previous:
        raise PublicationError('Historical Docs deployment job did not succeed')
    statuses = client.array(f"/deployments/{latest['id']}/statuses")
    expected_log = f"https://github.com/{client.repository}/actions/runs/{bootstrap['run_id']}/job/{bootstrap['job_id']}"
    if not any(status.get('id') == bootstrap['status_id'] and status.get('state') == 'success' and status.get('log_url') == expected_log and status.get('environment_url') == site_url for status in statuses):
        raise PublicationError('Historical Pages deployment lacks its reviewed success status')
    if not main_ancestor(client.main_comparison(previous), previous):
        raise PublicationError('Historical Pages source is outside current main history')
    baseline_index = {'index_size': bootstrap['index_size'], 'index_sha256': bootstrap['index_sha256'][7:]}
    client.verify_index(site_url, baseline_index)
    if source == previous and (identity['index_size'] != bootstrap['index_size'] or identity['index_sha256'] != baseline_index['index_sha256']):
        raise PublicationError('Same-source bootstrap site conflicts with the existing Pages index')
    return [{'source_sha': previous, 'site': identity if source == previous else None}]
