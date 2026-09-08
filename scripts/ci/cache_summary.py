"""Report cache counts without emitting Turbo environment or task metadata."""

import json
import os
from pathlib import Path
import sys


def cache_counts(summary):
    if not isinstance(summary, dict) or not isinstance(summary.get('tasks'), list) or not summary['tasks']:
        raise ValueError('Turbo summary has no tasks')
    counts = {'remote_hits': 0, 'local_hits': 0, 'misses': 0}
    for task in summary['tasks']:
        cache = task.get('cache') if isinstance(task, dict) else None
        if not isinstance(cache, dict):
            raise ValueError('Turbo task has no cache result')
        if cache.get('status') == 'MISS':
            counts['misses'] += 1
        elif cache.get('status') == 'HIT' and cache.get('source') in ('REMOTE', 'LOCAL'):
            counts['remote_hits' if cache['source'] == 'REMOTE' else 'local_hits'] += 1
        else:
            raise ValueError('Turbo task has an unsupported cache result')
    return counts


def main():
    try:
        files = list(Path('.turbo/runs').glob('*.json'))
        if len(files) != 1 or files[0].is_symlink() or files[0].stat().st_size > 32 * 1024 * 1024:
            raise ValueError('Expected one bounded summary from the current build')
        counts = cache_counts(json.loads(files[0].read_text()))
    except (OSError, ValueError):
        sys.exit('Current Turbo cache evidence could not be read')
    message = 'Turbo build cache: ' + ', '.join(f'{key}={value}' for key, value in counts.items())
    print(message)
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as output:
        output.write(message + '\n')


if __name__ == '__main__':
    main()
