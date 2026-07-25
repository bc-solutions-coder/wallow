#!/bin/sh
set -e

# Defaults (exported so envsubst and the garage CLI can see them)
export GARAGE_CONFIG_FILE="${GARAGE_CONFIG_FILE:-/etc/garage.toml}"
export GARAGE_REGION="${GARAGE_REGION:-us-east-1}"
export GARAGE_S3_PORT="${GARAGE_S3_PORT:-3900}"
export GARAGE_ADMIN_PORT="${GARAGE_ADMIN_PORT:-3903}"
export GARAGE_KEY_NAME="${GARAGE_KEY_NAME:-wallow}"
export GARAGE_BUCKET="${GARAGE_BUCKET:-wallow-files}"
export GARAGE_RPC_SECRET="${GARAGE_RPC_SECRET}"
export GARAGE_ADMIN_TOKEN="${GARAGE_ADMIN_TOKEN}"

ACCESS_KEY="${GARAGE_ACCESS_KEY:-}"
SECRET_KEY="${GARAGE_SECRET_KEY:-}"

# Generate config from template
envsubst < /etc/garage/garage.toml.template > "$GARAGE_CONFIG_FILE"

# Start Garage in the background
garage server &
GARAGE_PID=$!

echo "Waiting for Garage to start..."
for i in $(seq 1 60); do
    if garage status 2>/dev/null | grep -q "ID"; then
        echo "Garage is ready."
        break
    fi
    if [ "$i" = "60" ]; then echo "ERROR: Garage not ready after 60s"; exit 1; fi
    sleep 1
done

# Get the node ID
NODE_ID=$(garage node id 2>/dev/null | head -1 | cut -d'@' -f1)
if [ -z "$NODE_ID" ]; then
    echo "ERROR: Could not get node ID"
    kill $GARAGE_PID 2>/dev/null
    exit 1
fi
echo "Node ID: $NODE_ID"

# Apply layout if not already configured
LAYOUT_STATUS=$(garage layout show 2>/dev/null || true)
if echo "$LAYOUT_STATUS" | grep -q "No nodes"; then
    echo "Applying initial layout..."
    garage layout assign -z dc1 -c 1G "$NODE_ID"
    garage layout apply --version 1
else
    echo "Layout already configured, skipping."
fi

# Create or import access key
KEY_INFO=$(garage key info "$GARAGE_KEY_NAME" 2>/dev/null || true)

if [ -n "$KEY_INFO" ] && [ -n "$ACCESS_KEY" ]; then
    # Key exists — verify it matches the expected access key
    EXISTING_ID=$(echo "$KEY_INFO" | grep "Key ID:" | awk '{print $NF}')
    if [ "$EXISTING_ID" != "$ACCESS_KEY" ]; then
        echo "Key '$GARAGE_KEY_NAME' exists with ID '$EXISTING_ID' but expected '$ACCESS_KEY'. Deleting and reimporting..."
        garage key delete --yes "$GARAGE_KEY_NAME"
        KEY_INFO=""
    fi
fi

if [ -z "$KEY_INFO" ]; then
    if [ -n "$ACCESS_KEY" ] && [ -n "$SECRET_KEY" ]; then
        echo "Importing access key '$GARAGE_KEY_NAME' with provided credentials..."
        garage key import -n "$GARAGE_KEY_NAME" --yes "$ACCESS_KEY" "$SECRET_KEY"
    else
        echo "Creating access key '$GARAGE_KEY_NAME' (auto-generated)..."
        KEY_OUTPUT=$(garage key create "$GARAGE_KEY_NAME")
        echo "$KEY_OUTPUT"
        ACCESS_KEY=$(echo "$KEY_OUTPUT" | grep "Key ID:" | awk '{print $NF}')
        SECRET_KEY=$(echo "$KEY_OUTPUT" | grep "Secret key:" | awk '{print $NF}')
    fi

    echo "GARAGE_ACCESS_KEY=$ACCESS_KEY" > /var/lib/garage/credentials
    echo "GARAGE_SECRET_KEY=$SECRET_KEY" >> /var/lib/garage/credentials
    echo "Credentials written to /var/lib/garage/credentials"
else
    echo "Key '$GARAGE_KEY_NAME' already exists with correct ID, skipping."
fi

# Create bucket if it doesn't exist
BUCKET_EXISTS=$(garage bucket info "$GARAGE_BUCKET" 2>/dev/null || true)
if [ -z "$BUCKET_EXISTS" ]; then
    echo "Creating bucket '$GARAGE_BUCKET'..."
    garage bucket create "$GARAGE_BUCKET"
    garage bucket allow --read --write --owner "$GARAGE_BUCKET" --key "$GARAGE_KEY_NAME"
else
    echo "Bucket '$GARAGE_BUCKET' already exists, skipping."
fi

echo "GarageHQ initialization complete."
echo "  S3 endpoint: http://localhost:${GARAGE_S3_PORT}"
if [ -f /var/lib/garage/credentials ]; then
    cat /var/lib/garage/credentials
fi
echo "  Bucket:      $GARAGE_BUCKET"

# Keep Garage running in the foreground
wait $GARAGE_PID
