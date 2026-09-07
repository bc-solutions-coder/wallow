#!/usr/bin/env python3
"""Read-only mount/quota audit. An existing directory is not a storage quota."""
import argparse
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--data-dir", default=os.environ.get("OBSERVABILITY_DATA_DIR", "/srv/observability"))
args = parser.parse_args()
root = Path(args.data_dir).resolve()
report = {"path": str(root), "platform": platform.system(), "ready": False, "quota_verified": False}
if not root.is_dir():
    report["reason"] = "Prepared data directory is missing"
else:
    stats = os.statvfs(root)
    report.update(filesystem_bytes=stats.f_blocks * stats.f_frsize,
                  available_bytes=stats.f_bavail * stats.f_frsize,
                  directories={name: (root / name).is_dir() for name in ("garage", "loki", "tempo", "prometheus", "alloy", "grafana", "gateway")})
    if platform.system() != "Linux" or not shutil.which("findmnt"):
        report["reason"] = "Debian mount and quota enforcement cannot be verified on this host"
    else:
        mount = subprocess.run(["findmnt", "--json", "--target", str(root), "--output", "TARGET,SOURCE,FSTYPE,OPTIONS"], check=True, text=True, capture_output=True)
        report["mount"] = json.loads(mount.stdout)
        if shutil.which("xfs_quota"):
            quota = subprocess.run(["xfs_quota", "-x", "-c", "report -p -n -b -N", str(root)], text=True, capture_output=True)
            report["project_quota_report"] = quota.stdout
            report["quota_command_succeeded"] = quota.returncode == 0
        report["reason"] = "Compare actual project hard limits and /etc/projects mappings with the allocation contract; directory size and Garage layout weights do not enforce quotas"
print(json.dumps(report, indent=2))
raise SystemExit(0 if report["ready"] else 2)
