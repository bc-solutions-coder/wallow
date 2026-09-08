"""Validate immutable action references and supported workflow queue settings."""

from pathlib import Path
import re
import sys


def validate_reference(reference):
    if not isinstance(reference, str):
        raise ValueError('action reference must be a string')
    if reference.startswith('./'):
        parts = reference[2:].split('/')
        if not all(part and part not in ('.', '..') and '\\' not in part for part in parts):
            raise ValueError('local action path must stay inside the repository')
        return
    if reference.startswith('docker://'):
        if not re.fullmatch(r'docker://[a-zA-Z0-9][a-zA-Z0-9._:/-]*@sha256:[0-9a-fA-F]{64}', reference):
            raise ValueError('Docker actions must use a full SHA-256 digest')
        return
    if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)*@[0-9a-fA-F]{40}', reference):
        raise ValueError('external actions must use a full commit SHA')
    if any(part in ('.', '..') for part in reference.split('@')[0].split('/')):
        raise ValueError('external action path must not traverse directories')


def validate_document(document):
    if not isinstance(document, dict):
        raise ValueError('workflow or action must be a mapping')
    owners = [document]
    if isinstance(document.get('jobs'), dict):
        owners.extend(job for job in document['jobs'].values() if isinstance(job, dict))
    for owner in owners:
        concurrency = owner.get('concurrency')
        if isinstance(concurrency, dict) and 'queue' in concurrency:
            if concurrency['queue'] not in ('max', 'single'):
                raise ValueError('concurrency queue must be max or single')
            if concurrency['queue'] == 'max' and concurrency.get('cancel-in-progress', False) is not False:
                raise ValueError('queued workflows must not cancel runs in progress')

    def visit(value):
        if isinstance(value, dict):
            for key, child in value.items():
                if key == 'uses':
                    validate_reference(child)
                else:
                    visit(child)
        elif isinstance(value, list):
            for child in value:
                visit(child)

    try:
        visit(document)
    except RecursionError as error:
        raise ValueError('workflow document is recursive or too deeply nested') from error


def main():
    import yaml

    paths = sorted(set(Path('.github/workflows').glob('*.yml'))
                   | set(Path('.github/workflows').glob('*.yaml'))
                   | set(Path('.github/actions').glob('**/action.yml'))
                   | set(Path('.github/actions').glob('**/action.yaml')))
    if not paths:
        print('No workflow or action documents found.', file=sys.stderr)
        return 1
    failed = False
    for path in paths:
        try:
            validate_document(yaml.safe_load(path.read_text()))
        except (ValueError, OSError, yaml.YAMLError):
            print(f'{path}: invalid workflow/action policy configuration', file=sys.stderr)
            failed = True
    if failed:
        return 1
    print(f'Immutable action policy passed for {len(paths)} documents.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
