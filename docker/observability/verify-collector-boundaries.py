#!/usr/bin/env python3
"""Verify private collector authentication and fail-closed attribution using HTTP."""
import json
import os
import time
import urllib.error
import urllib.request

for endpoint in ("http://alloy:4318/v1/logs", "http://alloy:12347/collect"):
    try:
        urllib.request.urlopen(urllib.request.Request(endpoint, b"{}", {"Content-Type": "application/json"}), timeout=10)
        raise AssertionError("Collector accepted an unauthenticated request")
    except urllib.error.HTTPError as error:
        assert error.code == 401, (endpoint, error.code)


def filtered_logs():
    with urllib.request.urlopen("http://alloy:12345/metrics", timeout=10) as response:
        lines = response.read().decode().splitlines()
    return sum(float(line.rsplit(" ", 1)[1]) for line in lines
               if line.startswith("otelcol_processor_filter_logs_filtered_total{"))


before = filtered_logs()
body = {"resourceLogs": [{"resource": {"attributes": [
    {"key": "service.name", "value": {"stringValue": "missing-attribution"}}]},
    "scopeLogs": [{"logRecords": [{"timeUnixNano": str(time.time_ns()),
                                   "body": {"stringValue": "partial-metadata-proof"}}]}]}]}
request = urllib.request.Request("http://alloy:4318/v1/logs", json.dumps(body).encode(), {
    "Content-Type": "application/json",
    "Authorization": "Bearer " + os.environ["GATEWAY_COLLECTOR_SECRET"],
    "X-Wallow-Registration": "partial",
})
with urllib.request.urlopen(request, timeout=10) as response:
    assert response.status == 200
assert filtered_logs() >= before + 1, "Incomplete trusted metadata was not counted as dropped"
print("PASS: collector rejects unauthenticated requests and counts incomplete attribution as dropped")
