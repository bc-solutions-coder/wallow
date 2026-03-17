#!/bin/bash
set -euo pipefail

KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8080}"
ADMIN_USER="${KEYCLOAK_ADMIN:-admin}"
ADMIN_PASS="${KEYCLOAK_ADMIN_PASSWORD:-admin}"
REALM="foundry"

# Wait for Keycloak to be ready
echo "Waiting for Keycloak to be ready..."
until curl -sf "${KEYCLOAK_URL}/health/ready" > /dev/null 2>&1; do
  sleep 2
done
echo "Keycloak is ready."

# Obtain admin access token
TOKEN=$(curl -sf -X POST "${KEYCLOAK_URL}/realms/master/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "username=${ADMIN_USER}" \
  -d "password=${ADMIN_PASS}" \
  -d "grant_type=password" \
  -d "client_id=admin-cli" | jq -r '.access_token')

if [ -z "$TOKEN" ] || [ "$TOKEN" = "null" ]; then
  echo "ERROR: Failed to obtain admin access token."
  exit 1
fi

POLICIES_URL="${KEYCLOAK_URL}/admin/realms/${REALM}/client-registration-policy/providers"
COMPONENTS_URL="${KEYCLOAK_URL}/admin/realms/${REALM}/components"

# Helper: find existing component by name and provider id
find_component() {
  local name="$1"
  local provider_id="$2"
  curl -sf -X GET "${COMPONENTS_URL}?name=${name}&type=org.keycloak.services.clientregistration.policy.ClientRegistrationPolicy" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "Accept: application/json" | jq -r --arg pid "$provider_id" '.[] | select(.providerId == $pid) | .id'
}

# Helper: create or update a component
upsert_component() {
  local name="$1"
  local provider_id="$2"
  local config="$3"
  local sub_type="$4"

  local existing_id
  existing_id=$(find_component "$name" "$provider_id")

  local payload
  payload=$(jq -n \
    --arg name "$name" \
    --arg providerId "$provider_id" \
    --arg parentId "$REALM" \
    --arg subType "$sub_type" \
    --argjson config "$config" \
    '{
      name: $name,
      providerId: $providerId,
      providerType: "org.keycloak.services.clientregistration.policy.ClientRegistrationPolicy",
      parentId: $parentId,
      subType: $subType,
      config: $config
    }')

  if [ -n "$existing_id" ] && [ "$existing_id" != "null" ]; then
    echo "Updating existing component '${name}' (${existing_id})..."
    local update_payload
    update_payload=$(echo "$payload" | jq --arg id "$existing_id" '. + {id: $id}')
    curl -sf -X PUT "${COMPONENTS_URL}/${existing_id}" \
      -H "Authorization: Bearer ${TOKEN}" \
      -H "Content-Type: application/json" \
      -d "$update_payload"
    echo "Updated '${name}'."
  else
    echo "Creating component '${name}'..."
    curl -sf -X POST "${COMPONENTS_URL}" \
      -H "Authorization: Bearer ${TOKEN}" \
      -H "Content-Type: application/json" \
      -d "$payload"
    echo "Created '${name}'."
  fi
}

# Configure trusted-hosts policy
echo "Configuring trusted-hosts policy..."
upsert_component "Trusted Hosts" "trusted-hosts" \
  '{"host-sending-registration-request-must-match": ["true"], "client-uris-must-match": ["true"], "trusted-hosts": ["localhost", "127.0.0.1"]}' \
  "anonymous"

# Configure max-clients policy
echo "Configuring max-clients policy..."
upsert_component "Max Clients Limit" "max-clients" \
  '{"max-clients": ["200"]}' \
  "anonymous"

echo "DCR policy configuration complete."
