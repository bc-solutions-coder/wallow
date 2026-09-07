"""Select remote Turbo only for a configured main push; otherwise build locally."""
import os
import http.client
import urllib.error
import urllib.request
from urllib.parse import urlsplit


def select_cache(env, probe):
    if env.get('GITHUB_EVENT_NAME') != 'push' or env.get('GITHUB_REF') != 'refs/heads/main':
        return 'local:rw', 'PR and non-push runs use local/GitHub caches'
    if env.get('MAIN_CACHE') != 'true' or env.get('TAILNET_RESULT') != 'success':
        return 'local:rw', 'private network unavailable; using local/GitHub caches'
    if not all(env.get(key) for key in ['TURBO_API', 'TURBO_TEAM', 'TURBO_TOKEN', 'TURBO_REMOTE_CACHE_SIGNATURE_KEY']):
        return 'local:rw', 'remote cache configuration absent; using local/GitHub caches'
    if not probe(env['TURBO_API']):
        return 'local:rw', 'remote cache unavailable; using local/GitHub caches'
    return 'local:rw,remote:rw', 'main remote cache enabled; cache misses rebuild'


def reachable(url, report=print):
    try:
        parsed = urlsplit(url)
        if any(character.isspace() or ord(character) < 32 or ord(character) == 127 for character in url) or parsed.scheme not in ('http', 'https') or not parsed.hostname or parsed.username or parsed.password or parsed.query or parsed.fragment:
            report('Remote cache probe: invalid endpoint configuration')
            return False
    except ValueError:
        report('Remote cache probe: invalid endpoint configuration')
        return False
    try:
        with urllib.request.urlopen(url.rstrip('/') + '/v8/artifacts/status', timeout=5) as response:
            report(f'Remote cache probe: HTTP {response.status}')
            return response.status == 200
    except urllib.error.HTTPError as error:
        report(f'Remote cache probe: HTTP {error.code}')
        error.close()
        return error.code in (401, 403)
    except urllib.error.URLError as error:
        report(f'Remote cache probe: transport failure ({type(error.reason).__name__})')
        return False
    except (TimeoutError, ValueError, http.client.HTTPException) as error:
        report(f'Remote cache probe: {type(error).__name__}')
        return False


if __name__ == '__main__':
    mode, reason = select_cache(os.environ, reachable)
    with open(os.environ['GITHUB_ENV'], 'a') as output:
        output.write(f'TURBO_CACHE={mode}\n')
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as output:
        output.write(f'Cache mode: {reason}.\n')
    print(reason)
