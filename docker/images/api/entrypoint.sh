#!/bin/bash
set -euo pipefail

BUNDLE_DIR="${BUNDLE_DIR:-/app/bundles}"

# -------------------------------------------------------
# 1. Run EF Core migrations
# -------------------------------------------------------
if [ -z "${CONNECTION_STRING:-}" ]; then
    echo "ERROR: CONNECTION_STRING environment variable is not set."
    exit 1
fi

MODULES=(
    identity billing storage notifications messaging
    announcements apikeys branding inquiries audit
)

for module in "${MODULES[@]}"; do
    bundle="${BUNDLE_DIR}/efbundle-${module}"
    echo "Applying migrations for ${module}..."

    if [ ! -f "${bundle}" ]; then
        echo "ERROR: Bundle not found: ${bundle}"
        exit 1
    fi

    if "${bundle}" --connection "${CONNECTION_STRING}"; then
        echo "Migrations for ${module} succeeded."
    else
        echo "ERROR: Migrations for ${module} failed."
        exit 1
    fi
done

echo "All migrations applied successfully."

# -------------------------------------------------------
# 2. Start the API
# -------------------------------------------------------
echo "Starting Wallow API..."
exec dotnet Wallow.Api.dll
