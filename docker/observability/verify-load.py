#!/usr/bin/env python3
"""Exercise only a disposable observability project on its private network."""
import concurrent.futures
import hashlib
import json
import os
import secrets
import time
import sys
import urllib.error
import urllib.request
import uuid
from collections import Counter

registration = str(uuid.uuid4())
key, secret = uuid.uuid4().hex, secrets.token_hex(32)
application = 'load-' + registration
management = os.environ['GATEWAY_MANAGEMENT_SECRET']
desired = dict(revision=1, clientId=application, applicationId=application,
               browserService=application+'-browser', serverService=application+'-server',
               environments=['test'], state='Enabled',
               credentials=[dict(id=key, verifier=hashlib.sha256(secret.encode()).hexdigest())], rotation=None)

def request(path, payload, control=False, extra=None):
    started = time.monotonic()
    headers = {'Content-Type': 'application/json', 'Authorization': 'Bearer '+(management if control else key+'.'+secret),
               'X-Wallow-Environment': 'test', 'X-Wallow-Release': 'load-1', **(extra or {})}
    body = payload if isinstance(payload, bytes) else json.dumps(payload).encode()
    try:
        with urllib.request.urlopen(urllib.request.Request(('http://gateway:8081' if control else 'http://gateway:8080')+path,
              body, headers, method='PUT' if control else 'POST'), timeout=7) as response:
            response.read()
            status = response.status
    except urllib.error.HTTPError as error:
        status = error.code
    return status, round(time.monotonic()-started, 4)

control_path = '/control/v1/registrations/'+registration
assert request(control_path, desired, True)[0] == 200
payload = dict(resourceLogs=[dict(resource=dict(attributes=[]), scopeLogs=[dict(logRecords=[dict(
    timeUnixNano=str(time.time_ns()), severityNumber=9, body=dict(stringValue='load.proof'),
    attributes=[dict(key='data', value=dict(stringValue='x'*8192))])])])])
if '--outage' in sys.argv:
    status, elapsed = request('/v1/logs', payload)
    assert status == 503, status
    control_status, control_elapsed = request(control_path, desired, True)
    assert control_status == 200 and control_elapsed < 2
    print(json.dumps(dict(collector_stopped=True, ingestion_status=status, ingestion_seconds=elapsed,
                         management_status=control_status, management_seconds=control_elapsed)))
    raise SystemExit(0)
assert request('/v1/logs', payload)[0] == 200
assert request('/v1/logs', b'x'*(1024*1024+1))[0] == 413
assert request('/v1/logs', payload, extra={'Content-Encoding':'gzip'})[0] == 415
statuses = Counter()
latencies = []
started = time.monotonic()
with concurrent.futures.ThreadPoolExecutor(max_workers=32) as pool:
    requests = [pool.submit(request, '/v1/logs', payload) for _ in range(256)]
    controls = [pool.submit(request, control_path, desired, True) for _ in range(8)]
    for item in requests:
        status, elapsed = item.result()
        statuses[status] += 1
        latencies.append(elapsed)
    control_results = [item.result() for item in controls]
assert statuses[429] > 0, statuses
assert all(status == 200 for status, elapsed in control_results), control_results
assert max(elapsed for status, elapsed in control_results) < 2, control_results
with urllib.request.urlopen('http://gateway:8083/metrics') as response:
    metrics = response.read().decode()
assert 'wallow_gateway_requests_total{status="429"}' in metrics
print(json.dumps(dict(application=application, requests=256, payload_bytes=len(json.dumps(payload).encode()),
    elapsed_seconds=round(time.monotonic()-started,3), statuses=dict(statuses),
    max_ingestion_seconds=max(latencies), max_control_seconds=max(elapsed for status,elapsed in control_results),
    body_limit_status=413, compression_status=415, metrics_count_refusals=True)))
