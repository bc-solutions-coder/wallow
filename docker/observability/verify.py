#!/usr/bin/env python3
"""Exercise storage HTTP boundaries from a container on the private storage network."""
import argparse
import json
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid


def request(url, payload=None):
    body = None if payload is None else json.dumps(payload).encode()
    query = urllib.request.Request(url, body, {"Content-Type": "application/json"})
    with urllib.request.urlopen(query, timeout=10) as response:
        data = response.read()
        return json.loads(data) if data else None


def seed():
    timestamp = time.time_ns()
    trace_id = uuid.uuid4().hex
    marker = f"storage-proof-{trace_id}"
    request("http://loki:3100/loki/api/v1/push", {
        "streams": [{"stream": {"service_name": "storage-proof"},
                     "values": [[str(timestamp), marker]]}]})
    request("http://tempo:4318/v1/traces", {"resourceSpans": [{
        "resource": {"attributes": [{"key": "service.name", "value": {
            "stringValue": "storage-proof"}}]},
        "scopeSpans": [{"spans": [{"traceId": trace_id, "spanId": "2450000000000001",
                                  "name": marker, "kind": 2,
                                  "startTimeUnixNano": str(timestamp),
                                  "endTimeUnixNano": str(timestamp + 1000000),
                                  "status": {"code": 2}}]}]}]})
    metric = request("http://prometheus:9090/api/v1/query?" + urllib.parse.urlencode({
        "query": 'up{job="prometheus"}'}))
    if not metric["data"]["result"] or metric["data"]["result"][0]["value"][1] != "1":
        raise RuntimeError("Prometheus has not scraped a successful sample")
    request("http://loki:3100/flush", {})
    request("http://tempo:3200/flush", {})
    print(json.dumps({"timestamp": timestamp, "trace_id": trace_id, "marker": marker,
                      "metric_time": metric["data"]["result"][0]["value"][0]}))


def verify(evidence):
    params = urllib.parse.urlencode({"query": '{service_name="storage-proof"}',
                                    "start": str(evidence["timestamp"] - 1000000),
                                    "end": str(evidence["timestamp"] + 1000000)})
    log = request("http://loki:3100/loki/api/v1/query_range?" + params)
    if not any(value[1] == evidence["marker"] for stream in log["data"]["result"]
               for value in stream["values"]):
        raise RuntimeError("Persisted log missing")
    trace = request(f'http://tempo:3200/api/traces/{evidence["trace_id"]}?mode=blocks')
    if not any(span["name"] == evidence["marker"] for batch in trace["batches"]
               for scope in batch["scopeSpans"] for span in scope["spans"]):
        raise RuntimeError("Object-store trace missing")
    metric = request("http://prometheus:9090/api/v1/query?" + urllib.parse.urlencode({
        "query": 'up{job="prometheus"}', "time": evidence["metric_time"]}))
    if not metric["data"]["result"] or metric["data"]["result"][0]["value"][1] != "1":
        raise RuntimeError("Historical metric missing")
    return {"persisted_log": True, "object_store_trace": True, "historical_metric": True,
            "trace_id": evidence["trace_id"]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["seed", "query"])
    parser.add_argument("--evidence", help="JSON from seed")
    args = parser.parse_args()
    if args.action == "seed":
        seed()
        return
    if not args.evidence:
        parser.error("query requires --evidence")
    evidence = json.loads(args.evidence)
    deadline = time.monotonic() + 120
    while True:
        try:
            print(json.dumps(verify(evidence)))
            return
        except (urllib.error.URLError, TimeoutError, RuntimeError, KeyError) as error:
            if time.monotonic() >= deadline:
                raise RuntimeError("Persistence verification timed out") from error
            time.sleep(2)


if __name__ == "__main__":
    main()
