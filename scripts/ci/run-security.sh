#!/usr/bin/env bash
set -euo pipefail

mkdir -p .ci-reports/security .ci-tools
export PATH="$PWD/.ci-tools/bin:$PWD/.ci-tools/python/bin:$PATH"
reports="$PWD/.ci-reports/security"
mode="${1:?security mode is required}"

install_trivy() {
    mkdir -p .ci-tools/bin
    curl --fail --silent --show-error --location --retry 3 \
      https://github.com/aquasecurity/trivy/releases/download/v0.74.0/trivy_0.74.0_Linux-64bit.tar.gz \
      --output .ci-tools/trivy.tar.gz
    echo '2ae6fe3ee734b7fdf11335663e18c75ea12dccc76062f09f164a3b0f8be4371a  .ci-tools/trivy.tar.gz' | sha256sum --check --strict
    tar -xzf .ci-tools/trivy.tar.gz -C .ci-tools/bin trivy
    printf '{}\n' > .ci-tools/trivy.yaml
    trivy --version > "$reports/trivy-version.txt"
    trivy --config .ci-tools/trivy.yaml image --download-db-only
    trivy --config .ci-tools/trivy.yaml version --format json > "$reports/trivy-database.json"
}

case "$mode" in
  install)
    mkdir -p .ci-tools/bin
    dotnet tool install Microsoft.CST.DevSkim.CLI --version 1.0.90 --tool-path .ci-tools/bin
    python3 -m venv .ci-tools/python
    .ci-tools/python/bin/pip install --disable-pip-version-check zizmor==1.30.0
    devskim --version > "$reports/devskim-version.txt"
    zizmor --version > "$reports/zizmor-version.txt"
    install_trivy
    ;;
  install-trivy)
    install_trivy
    ;;
  devskim)
    stage="${RUNNER_TEMP:-/tmp}/security-source-${GITHUB_RUN_ID:-local}-${GITHUB_RUN_ATTEMPT:-1}"
    python3 scripts/ci/security.py stage --destination "$stage" --inventory "$reports/source-inventory.json"
    devskim analyze -I "$stage" -O "$reports/devskim.sarif" --confidence High,Medium,Low \
      --disable-supression --disable-console -v Debug -l "$reports/devskim-execution.log"
    python3 - "$reports" <<'PY'
import json, pathlib, re, sys
root = pathlib.Path(sys.argv[1])
inventory = json.loads((root / 'source-inventory.json').read_text())['files']
log = (root / 'devskim-execution.log').read_text()
analyzed = re.findall(r'Files analyzed: (\d+)', log)
skipped = re.findall(r'Files skipped: (\d+)', log)
if analyzed != [str(len(inventory))] or skipped != ['0']:
    raise SystemExit('DevSkim did not analyze every staged source input; inspect inventory and debug log')
PY
    python3 scripts/ci/security.py gate --scanner devskim --report "$reports/devskim.sarif" \
      --inventory "$reports/source-inventory.json" --output "$reports/devskim-gate.json"
    ;;
  zizmor)
    zizmor --offline --strict-collection --no-ignores --no-config --no-exit-codes \
      --persona regular --collect workflows --collect actions --format json-v1 .github > "$reports/zizmor.json" 2> "$reports/zizmor-execution.log"
    python3 - "$reports" <<'PYCODE'
import pathlib, subprocess, sys
log = (pathlib.Path(sys.argv[1]) / 'zizmor-execution.log').read_text()
tracked = subprocess.check_output(['git', 'ls-files', '-z', '.github']).decode().split('\0')
expected = [p for p in tracked if (p.startswith('.github/workflows/') and p.endswith(('.yml', '.yaml'))) or (p.startswith('.github/actions/') and pathlib.Path(p).name in ('action.yml', 'action.yaml'))]
if not expected or any('completed ' + p not in log for p in expected):
    raise SystemExit('zizmor workflow/action collection was incomplete; inspect execution log')
PYCODE
    python3 scripts/ci/security.py gate --scanner zizmor --report "$reports/zizmor.json" --output "$reports/zizmor-gate.json"
    ;;
  dependencies)
    dotnet restore api/Wallow.slnx -p:RestorePackagesWithLockFile=true
    while IFS= read -r -d '' project; do
      if [[ ! -f "${project%/*}/packages.lock.json" ]]; then
        dotnet restore "$project" -p:RestorePackagesWithLockFile=true
      fi
    done < <(git ls-files -z -- 'api/**/*.csproj')
    python3 - <<'PY'
import json, pathlib, shutil, subprocess
root = pathlib.Path('.ci-tools/dependencies')
root.mkdir()
files = [pathlib.Path('pnpm-lock.yaml'), *pathlib.Path('api').rglob('packages.lock.json')]
projects = [pathlib.Path(p) for p in subprocess.check_output(['git', 'ls-files', '-z', '--', 'api/**/*.csproj']).decode().split('\0') if p]
missing = [str(p) for p in projects if not (p.parent / 'packages.lock.json').is_file()]
if missing:
    raise SystemExit(f'NuGet resolved dependency lockfiles missing: {missing}')
if not projects:
    raise SystemExit('No .NET dependency inputs found')
for path in files:
    target = root / path
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(path, target)
pathlib.Path('.ci-reports/security/dependency-inventory.json').write_text(json.dumps([str(p) for p in files], indent=2))
PY
    trivy --config .ci-tools/trivy.yaml fs --scanners vuln --include-dev-deps --list-all-pkgs \
      --ignorefile /dev/null --format json --output "$reports/dependencies.json" .ci-tools/dependencies
    python3 - "$reports" <<'PY'
import json, pathlib, sys
root = pathlib.Path(sys.argv[1])
expected = json.loads((root / 'dependency-inventory.json').read_text())
actual = [r['Target'] for r in json.loads((root / 'dependencies.json').read_text()).get('Results', [])]
missing = [p for p in expected if not any(t == p or t.endswith('/' + p) for t in actual)]
if missing:
    raise SystemExit(f'Trivy did not report all resolved dependency inputs: {missing}')
PY
    python3 scripts/ci/security.py gate --scanner trivy --report "$reports/dependencies.json" --output "$reports/dependencies-gate.json"
    ;;
  images)
    kind="${2:?image kind is required}"
    case "$kind" in
      app) tags=(wallow-api:test wallow-api:test-arm64 wallow-auth-react:test wallow-auth-react:test-arm64 wallow-web-react:test wallow-web-react:test-arm64 wallow-bff-example:test wallow-migrations:test wallow-migrations:test-arm64 wallow-seeder:test wallow-seeder:test-arm64) ;;
      infra) tags=(wallow-garage:test wallow-garage:test-arm64 wallow-postgres-replica:latest wallow-postgres-replica:test-arm64) ;;
      docs) tags=(wallow-docs:test wallow-docs:test-arm64) ;;
      *) echo "Unknown image kind: $kind" >&2; exit 2 ;;
    esac
    archive=".ci-artifacts/images-$kind/images.tar.gz"
    python3 scripts/ci/validation.py verify --file "$archive" --manifest "$archive.json" --kind images --variant "$kind-amd64-arm64"
    docker load --input "$archive"
    failed=0
    for tag in "${tags[@]}"; do
      platform=amd64
      [[ "$tag" != *-arm64 ]] || platform=arm64
      actual="$(docker image inspect --format '{{.Os}}/{{.Architecture}}' "$tag")"
      [[ "$actual" == "linux/$platform" ]] || { echo "Unexpected platform $actual for $tag" >&2; exit 2; }
      filename="${tag//:/-}"
      docker image inspect "$tag" > "$reports/$filename-image.json"
      trivy --config .ci-tools/trivy.yaml image --scanners vuln --list-all-pkgs --image-src docker \
        --ignorefile /dev/null --format json --output "$reports/$filename-trivy.json" "$tag"
      if ! python3 scripts/ci/security.py gate --scanner trivy --report "$reports/$filename-trivy.json" --scope-prefix "$tag" --output "$reports/$filename-gate.json"; then
        failed=1
      fi
    done
    exit "$failed"
    ;;
  *) echo "Unknown security mode: $mode" >&2; exit 2 ;;
esac
