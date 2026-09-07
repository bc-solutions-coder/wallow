#!/usr/bin/env python3
"""Start and initialize independent observability storage and query services."""
import argparse
import os
from pathlib import Path
import re
import secrets
import subprocess
import sys

ROOT = Path(__file__).resolve().parent


def write_private(target, lines):
    temporary = target.with_suffix(target.suffix + ".tmp")
    with temporary.open("w") as stream:
        os.chmod(temporary, 0o600)
        stream.write("\n".join(lines) + "\n")
    temporary.replace(target)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", default="wallow-observability")
    args = parser.parse_args()
    os.umask(0o077)
    env = ROOT / ".env"
    if not env.exists():
        with env.open("x") as stream:
            stream.write("OBSERVABILITY_DATA_DIR=/srv/observability\n")
            stream.write(f"GARAGE_RPC_SECRET={secrets.token_hex(32)}\n")
    compose = ["docker", "compose", "--project-name", args.project,
               "--env-file", str(env), "-f", str(ROOT / "compose.yml")]

    def garage(*arguments, optional=False):
        result = subprocess.run(compose + ["exec", "-T", "garage", "/garage", *arguments],
                                cwd=ROOT, text=True, capture_output=True)
        if result.returncode and not optional:
            # Key commands can return secrets. Never forward captured output.
            raise RuntimeError(f"Garage {arguments[0]} command failed; inspect service health")
        return result.stdout if result.returncode == 0 else None

    subprocess.run(compose + ["up", "-d", "--wait", "--wait-timeout", "90", "garage"],
                   cwd=ROOT, check=True)
    layout = garage("layout", "show")
    if "No nodes" in layout:
        node = garage("node", "id").strip().split("@")[0]
        garage("layout", "assign", "-z", "observability", "-c", "32G", node)
        garage("layout", "apply", "--version", "1")

    credentials = ["# Server-only S3 credentials. Never commit or expose to browsers.",
                   "S3_ENDPOINT=http://garage:3900", "S3_REGION=us-east-1"]
    for service in ("loki", "tempo"):
        bucket = f"observability-{service}"
        if garage("bucket", "info", bucket, optional=True) is None:
            garage("bucket", "create", bucket)
        if garage("key", "info", service, optional=True) is None:
            garage("key", "create", service)
        info = garage("key", "info", "--show-secret", service)
        key = re.search(r"Key ID:\s*(GK[0-9a-f]+)", info)
        secret = re.search(r"Secret key:\s*(\S+)", info)
        if not key or not secret:
            raise RuntimeError("Could not read Garage credentials; output withheld")
        garage("bucket", "allow", "--read", "--write", bucket, "--key", service)
        prefix = service.upper()
        credentials.extend([f"{prefix}_S3_BUCKET={bucket}",
                            f"{prefix}_S3_ACCESS_KEY={key[1]}",
                            f"{prefix}_S3_SECRET_KEY={secret[1]}"])
    for service in ("loki", "tempo"):
        prefix = service.upper() + "_"
        write_private(ROOT / f"{service}.local", [line for line in credentials if line.startswith(prefix)])
    grafana = ROOT / "grafana.local"
    if not grafana.exists():
        write_private(grafana, ["GF_AUTH_ANONYMOUS_ENABLED=false",
                                "GF_USERS_ALLOW_SIGN_UP=false",
                                "GF_ANALYTICS_REPORTING_ENABLED=false",
                                "GF_SECURITY_ADMIN_USER=operator",
                                f"GF_SECURITY_ADMIN_PASSWORD={secrets.token_urlsafe(32)}"])
    target = ROOT / "credentials.local"
    write_private(target, credentials)
    subprocess.run(compose + ["--profile", "query", "up", "-d"], cwd=ROOT, check=True)
    print(f"Storage and query services started in Compose project {args.project}.")
    print(f"Private Loki/Tempo credentials saved to {target} (mode 600).")


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, subprocess.CalledProcessError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
