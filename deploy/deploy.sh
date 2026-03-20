#!/usr/bin/env bash
# deploy.sh — Deploy the Wallow application
# Called by CI/CD via SSH. Restarts only the app container;
# infrastructure (postgres, rabbitmq) stays running.
#
# Usage: deploy.sh <environment> [image-tag]
#   environment: dev | staging | prod
#   image-tag:   Docker image tag (optional; defaults per environment)

set -euo pipefail

# ============================================
# COLORS & LOGGING
# ============================================
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log() {
    echo -e "[$(date '+%Y-%m-%d %H:%M:%S')] $1"
}

log_success() {
    log "${GREEN}$1${NC}"
}

log_warn() {
    log "${YELLOW}$1${NC}"
}

log_error() {
    log "${RED}$1${NC}"
}

# ============================================
# ENVIRONMENT MAPPINGS
# ============================================
declare -A ENV_PORTS=(
    [dev]=8081
    [staging]=8082
    [prod]=8080
)

declare -A ENV_DEFAULT_TAGS=(
    [dev]=dev
    [staging]=staging
    [prod]=latest
)

# ============================================
# STEP 1: VALIDATE ARGUMENTS
# ============================================
ENVIRONMENT="${1:-}"

if [[ -z "$ENVIRONMENT" ]]; then
    log_error "Usage: deploy.sh <environment> [image-tag]"
    log_error "  environment: dev | staging | prod"
    exit 1
fi

if [[ "$ENVIRONMENT" != "dev" && "$ENVIRONMENT" != "staging" && "$ENVIRONMENT" != "prod" ]]; then
    log_error "Invalid environment: '$ENVIRONMENT'. Must be one of: dev, staging, prod"
    exit 1
fi

DEPLOY_DIR="/opt/wallow/${ENVIRONMENT}"

if [[ ! -d "$DEPLOY_DIR" ]]; then
    log_error "Deploy directory does not exist: $DEPLOY_DIR"
    exit 1
fi

if [[ ! -f "$DEPLOY_DIR/docker-compose.base.yml" ]]; then
    log_error "Missing docker-compose.base.yml in $DEPLOY_DIR"
    exit 1
fi

# Resolve image tag: use argument if provided, otherwise environment default
NEW_TAG="${2:-${ENV_DEFAULT_TAGS[$ENVIRONMENT]}}"

log "Deploying to ${ENVIRONMENT} environment with tag: ${NEW_TAG}"

# ============================================
# STEP 2: COMPOSE FILE VARIABLES
# ============================================
BASE_COMPOSE="docker-compose.base.yml"
ENV_COMPOSE="docker-compose.${ENVIRONMENT}.yml"
COMPOSE_CMD="docker compose -f $BASE_COMPOSE -f $ENV_COMPOSE --env-file .env"

cd "$DEPLOY_DIR"

# Verify environment-specific compose file exists
if [[ ! -f "$ENV_COMPOSE" ]]; then
    log_error "Missing environment compose file: $DEPLOY_DIR/$ENV_COMPOSE"
    exit 1
fi

# Verify .env file exists
if [[ ! -f ".env" ]]; then
    log_error "Missing .env file in $DEPLOY_DIR"
    log_error "Copy .env.example to .env and configure it before deploying."
    exit 1
fi

# ============================================
# STEP 3: PRE-FLIGHT — VERIFY INFRASTRUCTURE
# ============================================
log "Pre-flight: checking infrastructure health..."

check_container_running() {
    local service="$1"
    local status
    # Use docker compose ps with JSON format to check container state
    status=$($COMPOSE_CMD ps "$service" --format json 2>/dev/null | head -1)

    if [[ -z "$status" ]]; then
        return 1
    fi

    # Check if the container is running
    echo "$status" | grep -qi '"running"'
}

infra_healthy=true

if ! check_container_running "postgres"; then
    log_warn "Postgres is not running"
    infra_healthy=false
fi

if ! check_container_running "rabbitmq"; then
    log_warn "RabbitMQ is not running"
    infra_healthy=false
fi

if [[ "$infra_healthy" == "false" ]]; then
    log "Starting infrastructure services..."
    $COMPOSE_CMD up -d postgres rabbitmq

    # Wait up to 30s for infrastructure to become healthy
    log "Waiting for infrastructure to become healthy (up to 30s)..."
    infra_retries=6
    infra_interval=5
    infra_ready=false

    for ((i = 1; i <= infra_retries; i++)); do
        sleep "$infra_interval"
        pg_healthy=false
        rmq_healthy=false

        if check_container_running "postgres"; then
            pg_healthy=true
        fi
        if check_container_running "rabbitmq"; then
            rmq_healthy=true
        fi

        if [[ "$pg_healthy" == "true" && "$rmq_healthy" == "true" ]]; then
            infra_ready=true
            break
        fi
        log "Infrastructure check $i/$infra_retries: waiting..."
    done

    if [[ "$infra_ready" == "false" ]]; then
        log_error "Infrastructure failed to become healthy within 30 seconds."
        log_error "Check postgres and rabbitmq logs: $COMPOSE_CMD logs postgres rabbitmq"
        exit 1
    fi
fi

log_success "Infrastructure is healthy."

# ============================================
# STEP 4: SAVE CURRENT STATE FOR ROLLBACK
# ============================================
PREV_TAG=$(grep '^APP_TAG=' .env | cut -d= -f2 || echo "")

if [[ -n "$PREV_TAG" ]]; then
    log "Saving rollback point: $PREV_TAG"
else
    log_warn "No previous APP_TAG found in .env (first deploy?)"
    PREV_TAG=""
fi

# Read APP_IMAGE from .env for logging
APP_IMAGE=$(grep '^APP_IMAGE=' .env | cut -d= -f2 || echo "unknown")

# ============================================
# STEP 5: UPDATE IMAGE TAG AND PULL
# ============================================
if [[ -n "$PREV_TAG" ]]; then
    sed -i'' -e "s/^APP_TAG=.*/APP_TAG=$NEW_TAG/" .env
else
    # First deploy or missing APP_TAG — append it
    echo "APP_TAG=$NEW_TAG" >> .env
fi

log "Pulling image ${APP_IMAGE}:${NEW_TAG}..."

if ! $COMPOSE_CMD pull app; then
    log_error "Failed to pull image ${APP_IMAGE}:${NEW_TAG}"
    # Restore previous tag
    if [[ -n "$PREV_TAG" ]]; then
        sed -i'' -e "s/^APP_TAG=.*/APP_TAG=$PREV_TAG/" .env
        log "Restored APP_TAG to $PREV_TAG"
    fi
    exit 1
fi

log_success "Image pulled successfully."

# ============================================
# STEP 6: RESTART APP CONTAINER ONLY
# ============================================
log "Restarting app container..."
$COMPOSE_CMD up -d --no-deps app

# ============================================
# STEP 7: HEALTH CHECK LOOP
# ============================================
PORT="${ENV_PORTS[$ENVIRONMENT]}"
MAX_RETRIES=12
RETRY_INTERVAL=5

log "Running health checks on http://localhost:${PORT}/health/ready (${MAX_RETRIES} attempts, ${RETRY_INTERVAL}s interval)..."

for ((i = 1; i <= MAX_RETRIES; i++)); do
    if curl -sf "http://localhost:${PORT}/health/ready" > /dev/null 2>&1; then
        log_success "Health check passed!"
        log_success "Deployment to ${ENVIRONMENT} successful. Image: ${APP_IMAGE}:${NEW_TAG}"
        exit 0
    fi
    log "Health check $i/$MAX_RETRIES: waiting..."
    sleep "$RETRY_INTERVAL"
done

# ============================================
# STEP 8: ROLLBACK ON FAILURE
# ============================================
log_error "Health check failed after ${MAX_RETRIES} attempts. Rolling back..."

if [[ -z "$PREV_TAG" ]]; then
    log_error "No previous tag to roll back to. This may have been a first deploy."
    log_error "Manual intervention required. Check: $COMPOSE_CMD logs app"
    exit 1
fi

# Restore previous tag
sed -i'' -e "s/^APP_TAG=.*/APP_TAG=$PREV_TAG/" .env
log "Restored APP_TAG to $PREV_TAG"

log "Pulling previous image ${APP_IMAGE}:${PREV_TAG}..."
if ! $COMPOSE_CMD pull app; then
    log_error "Failed to pull rollback image ${APP_IMAGE}:${PREV_TAG}. Manual intervention required."
    exit 1
fi

$COMPOSE_CMD up -d --no-deps app

# Wait for rollback to become healthy (30s)
log "Waiting for rollback to become healthy (up to 30s)..."
ROLLBACK_RETRIES=6
ROLLBACK_INTERVAL=5

for ((i = 1; i <= ROLLBACK_RETRIES; i++)); do
    if curl -sf "http://localhost:${PORT}/health/ready" > /dev/null 2>&1; then
        log_warn "Rolled back to $PREV_TAG"
        exit 1
    fi
    log "Rollback health check $i/$ROLLBACK_RETRIES: waiting..."
    sleep "$ROLLBACK_INTERVAL"
done

log_error "Rollback health check also failed. Manual intervention required."
log_error "Check logs: $COMPOSE_CMD logs app"
exit 1
