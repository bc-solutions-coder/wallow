"""Select remote Turbo only for a configured main push; otherwise build locally."""
import os
import urllib.error
import urllib.request


def select_cache(env, probe):
    if env.get('GITHUB_EVENT_NAME') != 'push' or env.get('GITHUB_REF') != 'refs/heads/main':
        return 'local:rw', 'PR and non-push runs use local/GitHub caches'
    if env.get('MAIN_CACHE') != 'true' or env.get('TAILNET_RESULT') != 'success':
        return 'local:rw', 'private network unavailable; using local/GitHub caches'
    if not all(env.get(key) for key in ['TURBO_API', 'TURBO_TEAM', 'TURBO_TOKEN']):
        return 'local:rw', 'remote cache configuration absent; using local/GitHub caches'
    if not probe(env['TURBO_API']):
        return 'local:rw', 'remote cache unavailable; using local/GitHub caches'
    return 'local:rw,remote:rw', 'main remote cache enabled; cache misses rebuild'


def reachable(url):
    try:
        with urllib.request.urlopen(url.rstrip('/') + '/v8/artifacts/status', timeout=5) as response:
            return response.status == 200
    except urllib.error.HTTPError as error:
        return error.code in (401, 403)
    except (urllib.error.URLError, TimeoutError, ValueError):
        return False


if __name__ == '__main__':
    mode, reason = select_cache(os.environ, reachable)
    with open(os.environ['GITHUB_ENV'], 'a') as output:
        output.write(f'TURBO_CACHE={mode}\n')
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as output:
        output.write(f'Cache mode: {reason}.\n')
    print(reason)
