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
    announcements apikeys branding inquiries audit authaudit
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
# 2. Generate OpenIddict certificates if missing
# -------------------------------------------------------
SIGNING_CERT_PATH="${OPENIDDICT_SIGNING_CERT_PATH:-${OpenIddict__SigningCertPath:-/app/certs/signing.pfx}}"
SIGNING_CERT_PASSWORD="${OPENIDDICT_SIGNING_CERT_PASSWORD:-${OpenIddict__SigningCertPassword:-changeit}}"
ENCRYPTION_CERT_PATH="${OPENIDDICT_ENCRYPTION_CERT_PATH:-${OpenIddict__EncryptionCertPath:-/app/certs/encryption.pfx}}"
ENCRYPTION_CERT_PASSWORD="${OPENIDDICT_ENCRYPTION_CERT_PASSWORD:-${OpenIddict__EncryptionCertPassword:-changeit}}"

mkdir -p "$(dirname "${SIGNING_CERT_PATH}")"
mkdir -p "$(dirname "${ENCRYPTION_CERT_PATH}")"

if [ ! -f "${SIGNING_CERT_PATH}" ]; then
    echo "Generating OpenIddict signing certificate at ${SIGNING_CERT_PATH}..."
    openssl req -x509 -newkey rsa:2048 -keyout /tmp/key.pem -out /tmp/cert.pem -days 3650 -nodes -subj "/CN=Wallow OpenIddict Signing"
    openssl pkcs12 -export -in /tmp/cert.pem -inkey /tmp/key.pem -out "${SIGNING_CERT_PATH}" -password "pass:${SIGNING_CERT_PASSWORD}"
    rm -f /tmp/key.pem /tmp/cert.pem
    echo "Signing certificate generated."
else
    echo "Signing certificate already exists at ${SIGNING_CERT_PATH}, skipping."
fi

if [ ! -f "${ENCRYPTION_CERT_PATH}" ]; then
    echo "Generating OpenIddict encryption certificate at ${ENCRYPTION_CERT_PATH}..."
    openssl req -x509 -newkey rsa:2048 -keyout /tmp/key.pem -out /tmp/cert.pem -days 3650 -nodes -subj "/CN=Wallow OpenIddict Encryption"
    openssl pkcs12 -export -in /tmp/cert.pem -inkey /tmp/key.pem -out "${ENCRYPTION_CERT_PATH}" -password "pass:${ENCRYPTION_CERT_PASSWORD}"
    rm -f /tmp/key.pem /tmp/cert.pem
    echo "Encryption certificate generated."
else
    echo "Encryption certificate already exists at ${ENCRYPTION_CERT_PATH}, skipping."
fi

# -------------------------------------------------------
# 3. Start the API
# -------------------------------------------------------
echo "Starting Wallow API..."
exec dotnet Wallow.Api.dll
