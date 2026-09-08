"""Bind the Pages writer to a protected preparation and its single exact TAR."""

import hashlib
from pathlib import Path
import stat
import tempfile
import zipfile

from publication import PublicationError
from publication_artifacts import select_artifact
from publication_pages_history import site_identity
from publication_pages_github import validate_site_url
from publication_preparation_authorization import authorize_preparation


def pages_environment(client):
    environment = client.get('/environments/github-pages')
    if not isinstance(environment, dict) or environment.get('name') != 'github-pages' or environment.get('deployment_branch_policy') != {'protected_branches': False, 'custom_branch_policies': True}:
        raise PublicationError('Enabled Pages publication requires github-pages restricted to main')
    rules = client.list('/environments/github-pages/deployment-branch-policies', 'branch_policies')
    if len(rules) != 1 or rules[0].get('name') != 'main' or rules[0].get('type') != 'branch':
        raise PublicationError('github-pages must allow only the exact main branch')
    settings = client.get('/pages')
    if not isinstance(settings, dict) or settings.get('build_type') != 'workflow':
        raise PublicationError('Enabled Pages publication requires workflow-based Pages')
    validate_site_url(settings.get('html_url'))
    return settings


def verify_prepared_site(client, artifact, site):
    identity = site_identity(site)
    if artifact.size > 1024 * 1024 * 1024 + 1024 * 1024 or identity['tar_size'] > 1024 * 1024 * 1024:
        raise PublicationError('Prepared Pages artifact exceeds its size limit')
    with tempfile.TemporaryDirectory(prefix='wallow-pages-writer-') as directory:
        archive = client.download(artifact, Path(directory) / 'site.zip')
        try:
            with zipfile.ZipFile(archive) as bundle:
                members = bundle.infolist()
                if len(members) != 1 or members[0].filename != 'artifact.tar':
                    raise PublicationError('Prepared Pages upload must contain only artifact.tar')
                member = members[0]
                if member.is_dir() or stat.S_IFMT(member.external_attr >> 16) not in (0, stat.S_IFREG) or member.flag_bits & 1 or member.file_size != identity['tar_size']:
                    raise PublicationError('Prepared Pages archive member is unsafe or changed')
                digest, size = hashlib.sha256(), 0
                with bundle.open(member) as contents:
                    while chunk := contents.read(1024 * 1024):
                        size += len(chunk)
                        if size > identity['tar_size']:
                            raise PublicationError('Prepared Pages archive exceeds its exact size')
                        digest.update(chunk)
                if size != identity['tar_size'] or digest.hexdigest() != identity['tar_sha256']:
                    raise PublicationError('Prepared Pages bytes differ from the authorized plan')
        except (zipfile.BadZipFile, RuntimeError, NotImplementedError):
            raise PublicationError('Prepared Pages artifact is invalid') from None
    return identity


def authorize_pages(client, context, run_id, attempt, invocation_run, invocation_attempt):
    settings = pages_environment(client)
    plan, preparation, artifacts, evidence = authorize_preparation(client, context, run_id, attempt, invocation_run, invocation_attempt)
    site = plan.get('verified_site')
    candidates = [item for item in plan['artifacts'] if item.get('kind') == 'docs']
    if not isinstance(site, dict) or len(candidates) != 1 or site.get('source_artifact') != candidates[0]:
        raise PublicationError('Prepared Pages source differs from the authorized producer')
    artifact = select_artifact(artifacts, preparation, 'prepared-site')
    identity = verify_prepared_site(client, artifact, site)
    return plan, artifact, identity, settings, evidence
