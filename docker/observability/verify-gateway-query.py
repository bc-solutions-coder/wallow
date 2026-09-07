#!/usr/bin/env python3
"""Disposable private-network gateway verification; see README.md."""
import json, sys, time, urllib.request, urllib.parse, urllib.error

def get(url):
    with urllib.request.urlopen(url, timeout=10) as response:
        return json.load(response)

def logs(query, start):
    return get('http://loki:3100/loki/api/v1/query_range?' + urllib.parse.urlencode({'query': query, 'start': str(start - 1000000), 'end': str(time.time_ns())}))['data']['result']
records = json.loads(sys.argv[1])
for record in records:
    for attempt in range(45):
        try:
            entries = logs('{service_name="' + record['server'] + '"}', record['timestamp'])
            values = [value[1] for entry in entries for value in entry['values']]
            assert record['marker'] in values and record['marker'] + '-protobuf' in values, ('logs', values)
            if '--after-restart' in sys.argv:
                assert record['marker'] + '-after-restart' in values, ('post-restart logs', values)
            assert all((entry['stream']['service_namespace'] == record['application'] for entry in entries)), entries
            trace = get('http://tempo:3200/api/traces/' + record['trace'])
            resources = trace.get('batches', trace.get('resourceSpans', []))
            identity = {a['key']: a['value'].get('stringValue') for a in resources[0]['resource']['attributes']}
            assert identity['wallow.application_id'] == record['application'], identity
            assert identity['wallow.registration_id'] == record['registration'], identity
            assert identity['service.name'] == record['server'], identity
            metric = get('http://prometheus:9090/api/v1/query?' + urllib.parse.urlencode({'query': 'gateway_proof_value{wallow_application_id="' + record['application'] + '"}'}))
            assert metric['data']['result'], metric
            assert all((v['metric']['service_name'] == record['server'] and v['value'][1] == '42' for v in metric['data']['result'])), metric
            proto = get('http://tempo:3200/api/traces/' + record['proto_trace'])
            proto_identity = {a['key']: a['value'].get('stringValue') for a in proto['batches'][0]['resource']['attributes']}
            assert proto_identity['wallow.registration_id'] == record['registration'], proto_identity
            metric_proto = get('http://prometheus:9090/api/v1/query?' + urllib.parse.urlencode({'query': 'gateway_proof_proto{wallow_registration_id="' + record['registration'] + '"}'}))
            assert metric_proto['data']['result'][0]['value'][1] == '43', metric_proto
            browser = logs('{app_name="' + record['browser'] + '"}', record['timestamp'])
            assert any((record['marker'] in value[1] for stream in browser for value in stream['values'])), browser
            print(json.dumps({'application': record['application'], 'trace': record['trace'], 'json_and_protobuf_logs': True, 'attributed_trace': True, 'attributed_metric': True, 'normalized_faro': True}))
            break
        except (urllib.error.URLError, TimeoutError, AssertionError, KeyError, IndexError) as e:
            if attempt == 44:
                raise
            time.sleep(2)
