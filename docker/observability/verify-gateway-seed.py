#!/usr/bin/env python3
"""Disposable private-network gateway verification; see README.md."""
import datetime, hashlib, json, os, secrets, struct, sys, time, urllib.request, urllib.parse, urllib.error, uuid

def call(url, payload=None, token=None, method=None, headers=None):
    h = {'Content-Type': 'application/json', **(headers or {})}
    if token:
        h['Authorization'] = 'Bearer ' + token
    data = payload if isinstance(payload, bytes) else json.dumps(payload).encode() if payload is not None else None
    with urllib.request.urlopen(urllib.request.Request(url, data, h, method=method), timeout=10) as response:
        body = response.read()
        return json.loads(body) if body and response.headers.get('Content-Type', '').startswith('application/json') else None

def encode_varint(n):
    out = b''
    while n > 127:
        out += bytes([n & 127 | 128])
        n >>= 7
    return out + bytes([n])

def protobuf_message(n, b):
    return encode_varint(n * 8 + 2) + encode_varint(len(b)) + b

def protobuf_text(n, s):
    return protobuf_message(n, s.encode())

def protobuf_fixed64(n, b):
    return encode_varint(n * 8 + 1) + b

def attributes(values):
    return [{'key': k, 'value': {'stringValue': v}} for k, v in values.items()]
results = []
credentials = []
for slot in ['a', 'b']:
    registration = str(uuid.uuid4())
    credential = uuid.uuid4().hex
    secret = secrets.token_hex(32)
    desired = {'revision': 1, 'clientId': 'client-' + slot, 'applicationId': 'application-' + slot, 'browserService': 'browser-' + slot, 'serverService': 'server-' + slot, 'environments': ['proof'], 'state': 'Enabled', 'credentials': [{'id': credential, 'verifier': hashlib.sha256(secret.encode()).hexdigest()}], 'rotation': None}
    ack = call('http://gateway:8081/control/v1/registrations/' + registration, desired, os.environ['GATEWAY_MANAGEMENT_SECRET'], 'PUT')
    token = credential + '.' + secret
    credentials.append(token)
    trustedHeaders = {'X-Wallow-Environment': 'proof', 'X-Wallow-Release': 'proof-245', 'X-Wallow-Application': 'forged', 'X-Wallow-Service': 'forged', 'X-Wallow-Registration': 'forged'}
    forged = {'service.name': 'forged', 'service.namespace': 'forged', 'wallow.application_id': 'forged', 'wallow.registration_id': 'forged', 'deployment.environment.name': 'forged', 'service.version': 'forged', '__trusted_registration': 'forged'}
    now = time.time_ns()
    marker = 'gateway-proof-' + slot + '-' + uuid.uuid4().hex
    trace = uuid.uuid4().hex
    call('http://gateway:8080/v1/logs', {'resourceLogs': [{'resource': {'attributes': attributes(forged)}, 'scopeLogs': [{'logRecords': [{'timeUnixNano': str(now), 'body': {'stringValue': marker}, 'attributes': attributes(forged)}]}]}]}, token, headers=trustedHeaders)
    call('http://gateway:8080/v1/traces', {'resourceSpans': [{'resource': {'attributes': attributes(forged)}, 'scopeSpans': [{'spans': [{'traceId': trace, 'spanId': '2450000000000001', 'name': marker, 'kind': 2, 'startTimeUnixNano': str(now), 'endTimeUnixNano': str(now + 1000000), 'attributes': attributes(forged)}]}]}]}, token, headers=trustedHeaders)
    call('http://gateway:8080/v1/metrics', {'resourceMetrics': [{'resource': {'attributes': attributes(forged)}, 'scopeMetrics': [{'metrics': [{'name': 'gateway_proof_value', 'gauge': {'dataPoints': [{'timeUnixNano': str(now), 'asDouble': 42, 'attributes': attributes(forged)}]}}]}]}]}, token, headers=trustedHeaders)
    call('http://gateway:8080/faro', {'meta': {'app': {'name': 'forged', 'namespace': 'forged', 'version': 'forged', 'environment': 'forged'}}, 'logs': [{'message': marker, 'level': 'error', 'timestamp': datetime.datetime.now(datetime.timezone.utc).isoformat()}]}, token, headers=trustedHeaders)
    resource = b''.join((protobuf_message(1, protobuf_text(1, k) + protobuf_message(2, protobuf_text(1, v))) for k, v in forged.items()))
    log = protobuf_fixed64(1, struct.pack('<Q', now)) + protobuf_message(5, protobuf_text(1, marker + '-protobuf'))
    protobuf = protobuf_message(1, protobuf_message(1, resource) + protobuf_message(2, protobuf_message(2, log)))
    call('http://gateway:8080/v1/logs', protobuf, token, headers={**trustedHeaders, 'Content-Type': 'application/x-protobuf'})
    proto_trace = uuid.uuid4().hex
    span = protobuf_message(1, bytes.fromhex(proto_trace)) + protobuf_message(2, bytes.fromhex('2450000000000002')) + protobuf_text(5, marker + '-protobuf') + encode_varint(6 * 8) + encode_varint(2) + protobuf_fixed64(7, struct.pack('<Q', now)) + protobuf_fixed64(8, struct.pack('<Q', now + 1000000))
    call('http://gateway:8080/v1/traces', protobuf_message(1, protobuf_message(1, resource) + protobuf_message(2, protobuf_message(2, span))), token, headers={**trustedHeaders, 'Content-Type': 'application/x-protobuf'})
    point = protobuf_fixed64(3, struct.pack('<Q', now)) + protobuf_fixed64(4, struct.pack('<d', 43))
    metric = protobuf_text(1, 'gateway_proof_proto') + protobuf_message(5, protobuf_message(1, point))
    call('http://gateway:8080/v1/metrics', protobuf_message(1, protobuf_message(1, resource) + protobuf_message(2, protobuf_message(2, metric))), token, headers={**trustedHeaders, 'Content-Type': 'application/x-protobuf'})
    results.append({'registration': registration, 'marker': marker, 'trace': trace, 'proto_trace': proto_trace, 'application': 'application-' + slot, 'server': 'server-' + slot, 'browser': 'browser-' + slot, 'timestamp': now})
print(json.dumps(results), flush=True)
if '--wait-for-restart' in sys.argv:
    print('Ready for gateway recreation; send continue afterward.', flush=True)
    if input() != 'continue':
        raise RuntimeError('Restart verification cancelled')
    for record, token in zip(results, credentials):
        call('http://gateway:8080/v1/logs', {'resourceLogs': [{'scopeLogs': [{'logRecords': [{'timeUnixNano': str(time.time_ns()), 'body': {'stringValue': record['marker'] + '-after-restart'}}]}]}]}, token, headers={'X-Wallow-Environment': 'proof', 'X-Wallow-Release': 'proof-245'})
    print('PASS: existing credentials accepted after gateway recreation', flush=True)
