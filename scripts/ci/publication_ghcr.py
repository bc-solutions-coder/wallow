"""Fixed-host GHCR manifest transport; authorization and writer locking belong to callers."""

import base64
import hashlib
import http.client
import json
import urllib.error
import urllib.parse
import urllib.request

from publication import PublicationError, matches


MEDIA_TYPES = ('application/vnd.docker.distribution.manifest.v2+json',
               'application/vnd.docker.distribution.manifest.list.v2+json',
               'application/vnd.oci.image.index.v1+json')
MANIFEST_LIMIT = 4 * 1024 * 1024
JSON_LIMIT = 64 * 1024


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, response, code, message, headers, new_url):
        return None


def parse_json(data):
    try:
        value = json.loads(data)
    except (UnicodeDecodeError, json.JSONDecodeError, RecursionError):
        raise PublicationError('GHCR returned invalid JSON') from None
    if not isinstance(value, dict):
        raise PublicationError('GHCR returned an invalid response object')
    return value


def manifest_identity(data, media_type):
    if not isinstance(data, bytes) or not 0 < len(data) <= MANIFEST_LIMIT or media_type not in MEDIA_TYPES:
        raise PublicationError('Invalid manifest bytes or media type')
    document = parse_json(data)
    if type(document.get('schemaVersion')) is not int or document['schemaVersion'] != 2 or document.get('mediaType') != media_type:
        raise PublicationError('Manifest schema or media type differs from its declaration')
    return 'sha256:' + hashlib.sha256(data).hexdigest()


class GHCR:
    def __init__(self, repository, username, credential, opener=None):
        if not matches(r'ghcr\.io/[a-z0-9][a-z0-9-]*/[a-z0-9]+(?:[._-][a-z0-9]+)*', repository) or len(repository) > 255:
            raise PublicationError('An exact authorized GHCR repository is required')
        if not matches(r'[A-Za-z0-9_.\[\]-]{1,100}', username) or not matches(r'[\x21-\x7e]{1,8192}', credential):
            raise PublicationError('Valid GitHub registry credentials are required')
        self.repository = repository.removeprefix('ghcr.io/')
        self.opener = opener or urllib.request.build_opener(NoRedirect())
        basic = base64.b64encode((username + ':' + credential).encode()).decode()
        query = urllib.parse.urlencode({'service': 'ghcr.io', 'scope': f'repository:{self.repository}:pull,push'})
        status, _, body = self._request('GET', '/token?' + query, 'Basic ' + basic, limit=JSON_LIMIT)
        if status != 200:
            raise PublicationError('GHCR authentication failed')
        document = parse_json(body)
        token = document.get('token', document.get('access_token'))
        if not matches(r'[A-Za-z0-9._~+/=-]{1,16384}', token) or ('access_token' in document and document['access_token'] != token):
            raise PublicationError('GHCR authentication returned an invalid token')
        self.authorization = 'Bearer ' + token

    def _request(self, method, path, authorization, data=None, media_type=None, limit=MANIFEST_LIMIT):
        headers = {'Authorization': authorization, 'Accept': ', '.join(MEDIA_TYPES), 'User-Agent': 'wallow-publication'}
        if media_type:
            headers['Content-Type'] = media_type
        request = urllib.request.Request('https://ghcr.io' + path, data=data, headers=headers, method=method)
        try:
            try:
                response = self.opener.open(request, timeout=30)
            except urllib.error.HTTPError as error:
                response = error
            with response:
                body = response.read(limit + 1)
                if len(body) > limit:
                    raise PublicationError('GHCR response exceeds its size limit')
                return response.status, response.headers, body
        except (urllib.error.URLError, OSError, http.client.HTTPException):
            raise PublicationError('GHCR request failed') from None

    def _manifest_path(self, reference):
        if not (matches(r'sha256:[0-9a-f]{64}', reference) or matches(r'[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}', reference)):
            raise PublicationError('Invalid exact manifest reference')
        return f'/v2/{self.repository}/manifests/{reference}'

    def read_manifest(self, reference):
        path = self._manifest_path(reference)
        status, headers, body = self._request('GET', path, self.authorization)
        if status == 404:
            if len(body) > JSON_LIMIT:
                raise PublicationError('GHCR error response exceeds its size limit')
            errors = parse_json(body).get('errors')
            if isinstance(errors, list) and errors and all(isinstance(error, dict) and error.get('code') == 'MANIFEST_UNKNOWN' for error in errors):
                return None
            raise PublicationError('GHCR did not confirm manifest absence')
        if status != 200:
            raise PublicationError('GHCR manifest could not be read')
        media_type = headers.get('Content-Type', '').split(';', 1)[0].strip()
        digest = manifest_identity(body, media_type)
        if headers.get('Docker-Content-Digest') != digest or (reference.startswith('sha256:') and reference != digest):
            raise PublicationError('GHCR manifest digest does not match its bytes or reference')
        return {'digest': digest, 'media_type': media_type, 'bytes': body}

    def write_manifest(self, reference, data, media_type, expected_digest):
        path = self._manifest_path(reference)
        digest = manifest_identity(data, media_type)
        if expected_digest != digest or (reference.startswith('sha256:') and reference != digest):
            raise PublicationError('Manifest bytes differ from the expected digest')
        expected = {'digest': digest, 'media_type': media_type, 'bytes': data}
        existing = self.read_manifest(reference)
        if existing is not None:
            if existing != expected:
                raise PublicationError('Existing manifest conflicts with the authorized bytes')
            return digest
        status, headers, _ = self._request('PUT', path, self.authorization, data, media_type, JSON_LIMIT)
        if status != 201 or headers.get('Docker-Content-Digest') != digest:
            raise PublicationError('GHCR did not acknowledge the exact manifest digest')
        if self.read_manifest(reference) != expected:
            raise PublicationError('GHCR manifest readback differs from the authorized bytes')
        return digest


    def replace_nightly(self, data, expected_digest, previous):
        """Caller proves ancestry under the workflow queue; this is not registry CAS."""
        media_type = 'application/vnd.oci.image.index.v1+json'
        digest = manifest_identity(data, media_type)
        if digest != expected_digest:
            raise PublicationError('Nightly bytes differ from the authorized digest')
        expected = {'digest': digest, 'media_type': media_type, 'bytes': data}
        observed = self.read_manifest('nightly')
        if observed != previous:
            raise PublicationError('Nightly changed after its provenance was checked')
        if observed == expected:
            return digest
        status, headers, _ = self._request('PUT', self._manifest_path('nightly'), self.authorization, data, media_type, JSON_LIMIT)
        if status != 201 or headers.get('Docker-Content-Digest') != digest or self.read_manifest('nightly') != expected:
            raise PublicationError('Nightly write or exact readback failed')
        return digest
