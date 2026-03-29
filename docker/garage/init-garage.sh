#!/bin/sh
set -e

# Start Garage in the background
garage server &
GARAGE_PID=$!

echo "Waiting for Garage to start..."
for i in $(seq 1 30); do
    if garage status 2>/dev/null | grep -q "ID"; then
        echo "Garage is ready."
        break
    fi
    sleep 1
done

# Get the node ID
NODE_ID=$(garage node id 2>/dev/null | head -1 | cut -d'@' -f1)
if [ -z "$NODE_ID" ]; then
    echo "ERROR: Could not get node ID"
    wait $GARAGE_PID
    exit 1
fi

echo "Node ID: $NODE_ID"

# Check if layout is already applied
LAYOUT_STATUS=$(garage layout show 2>/dev/null || true)
if echo "$LAYOUT_STATUS" | grep -q "No nodes"; then
    echo "Applying initial layout..."
    garage layout assign -z dc1 -c 1G "$NODE_ID"
    garage layout apply --version 1
else
    echo "Layout already configured, skipping."
fi

# Create or import access key
KEY_NAME="${GARAGE_KEY_NAME:-wallow-dev}"
ACCESS_KEY="${GARAGE_ACCESS_KEY:-}"
SECRET_KEY="${GARAGE_SECRET_KEY:-}"
KEY_EXISTS=$(garage key info "$KEY_NAME" 2>/dev/null || true)
if [ -z "$KEY_EXISTS" ]; then
    if [ -n "$ACCESS_KEY" ] && [ -n "$SECRET_KEY" ]; then
        echo "Importing access key '$KEY_NAME' with provided credentials..."
        garage key import -n "$KEY_NAME" --yes "$ACCESS_KEY" "$SECRET_KEY"
    else
        echo "Creating access key '$KEY_NAME' (auto-generated)..."
        KEY_OUTPUT=$(garage key create "$KEY_NAME")
        echo "$KEY_OUTPUT"
        ACCESS_KEY=$(echo "$KEY_OUTPUT" | grep "Key ID:" | awk '{print $NF}')
        SECRET_KEY=$(echo "$KEY_OUTPUT" | grep "Secret key:" | awk '{print $NF}')
    fi

    echo "GARAGE_ACCESS_KEY=$ACCESS_KEY" > /var/lib/garage/credentials
    echo "GARAGE_SECRET_KEY=$SECRET_KEY" >> /var/lib/garage/credentials
    echo "Credentials written to /var/lib/garage/credentials"
else
    echo "Key '$KEY_NAME' already exists, skipping."
fi

# Create bucket if it doesn't exist
BUCKET_NAME="${GARAGE_BUCKET:-wallow-files}"
BUCKET_EXISTS=$(garage bucket info "$BUCKET_NAME" 2>/dev/null || true)
if [ -z "$BUCKET_EXISTS" ]; then
    echo "Creating bucket '$BUCKET_NAME'..."
    garage bucket create "$BUCKET_NAME"
    garage bucket allow --read --write --owner "$BUCKET_NAME" \
        --key "$KEY_NAME"
else
    echo "Bucket '$BUCKET_NAME' already exists, skipping."
fi

echo "GarageHQ initialization complete."
echo "  S3 endpoint: http://localhost:3900"
if [ -f /var/lib/garage/credentials ]; then
    cat /var/lib/garage/credentials
fi
echo "  Bucket:      $BUCKET_NAME"

# Wait for Garage to keep running
wait $GARAGE_PID
