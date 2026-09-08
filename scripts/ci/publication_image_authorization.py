"""Reauthorize current protected preparation before a main image writer uses credentials."""

from publication import PublicationError
from publication_artifacts import select_artifact
from publication_preparation_authorization import authorize_preparation


def image_environment(client):
    environment = client.get('/environments/image-publish')
    if not isinstance(environment, dict) or environment.get('name') != 'image-publish' or environment.get('deployment_branch_policy') != {'protected_branches': False, 'custom_branch_policies': True}:
        raise PublicationError('Enabled image publication requires image-publish restricted to the main branch')
    rules = client.list('/environments/image-publish/deployment-branch-policies', 'branch_policies')
    if len(rules) != 1 or rules[0].get('name') != 'main' or rules[0].get('type') != 'branch':
        raise PublicationError('image-publish must allow only the exact main branch')


def authorize_images(client, context, run_id, attempt, invocation_run, invocation_attempt):
    image_environment(client)
    plan, preparation, artifacts, evidence = authorize_preparation(client, context, run_id, attempt, invocation_run, invocation_attempt)
    required = ('app', 'infra', 'docs') if plan['route'] == 'full' else ('docs',)
    if set(plan.get('verified_images', {})) != set(required):
        raise PublicationError('Prepared image bundle coverage differs from the authorized route')
    prepared = {bundle: select_artifact(artifacts, preparation, 'prepared-images-' + bundle) for bundle in required}
    return plan, preparation, prepared, evidence
