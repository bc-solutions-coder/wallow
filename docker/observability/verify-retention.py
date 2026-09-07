#!/usr/bin/env python3
"""Accelerated Tempo retention on disposable tmpfs, never the observability data directory."""
import json
from pathlib import Path
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid

root = Path(__file__).resolve().parent.parent
name = 'wallow-retention-' + uuid.uuid4().hex[:12]
image = 'grafana/tempo:2.10.8@sha256:f0561deb1c68ec44d6e6e7e4487f30106c4e5e768642077695b37958b105812a'
configuration = '''server:
  http_listen_port: 3200
distributor:
  receivers:
    otlp:
      protocols:
        http:
          endpoint: 0.0.0.0:4318
ingester:
  max_block_duration: 1s
  trace_idle_period: 1s
  flush_check_period: 1s
  complete_block_timeout: 1s
compactor:
  compaction:
    block_retention: 45s
    compacted_block_retention: 1s
    compaction_cycle: 1s
storage:
  trace:
    backend: local
    blocklist_poll: 1s
    wal:
      path: /data/wal
    local:
      path: /data/blocks
usage_report:
  reporting_enabled: false
'''

def docker(*args):
    return subprocess.run(['docker', *args], cwd=root, check=True, capture_output=True, text=True).stdout.strip()

def get(url):
    try:
        with urllib.request.urlopen(url, timeout=3) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()

with tempfile.TemporaryDirectory() as directory:
    config = Path(directory) / 'tempo.yml'
    config.write_text(configuration)
    config.chmod(0o644)
    try:
        docker('run', '-d', '--name', name, '--memory', '512m', '--tmpfs', '/data:rw,size=64m,mode=1777',
               '-p', '127.0.0.1::3200', '-p', '127.0.0.1::4318', '-v', str(config)+':/etc/tempo-proof.yml:ro',
               image, '-config.file=/etc/tempo-proof.yml')
        query = 'http://' + docker('port', name, '3200/tcp')
        ingest = 'http://' + docker('port', name, '4318/tcp')
        for _ in range(45):
            try:
                if get(query+'/ready')[0] == 200: break
            except (urllib.error.URLError, ConnectionError): pass
            time.sleep(1)
        else: raise RuntimeError('Disposable Tempo did not become ready: '+docker('logs', '--tail', '8', name))
        trace = uuid.uuid4().hex
        now = time.time_ns()
        payload = {'resourceSpans':[{'resource':{},'scopeSpans':[{'spans':[{'traceId':trace,'spanId':'1234567890abcdef','name':'retention.proof','startTimeUnixNano':str(now),'endTimeUnixNano':str(now+1000000)}]}]}]}
        with urllib.request.urlopen(urllib.request.Request(ingest+'/v1/traces', json.dumps(payload).encode(), {'Content-Type':'application/json'}), timeout=5) as response:
            assert response.status == 200
        path = query+'/api/traces/'+trace+'?mode=blocks'
        started = time.monotonic()
        while get(path)[0] != 200:
            assert time.monotonic()-started < 40, 'Trace did not reach local backend'
            time.sleep(1)
        readable = round(time.monotonic()-started, 2)
        while get(path)[0] != 404:
            assert time.monotonic()-started < 150, 'Trace did not expire from backend'
            time.sleep(1)
        print(json.dumps(dict(trace=trace, backend='disposable-local-tmpfs', retention_seconds=45,
                              backend_readable_seconds=readable, expired_seconds=round(time.monotonic()-started,2),
                              memory_limit_bytes=512*1024*1024, storage_limit_bytes=64*1024*1024)))
    finally:
        subprocess.run(['docker','rm','-f',name], cwd=root, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
