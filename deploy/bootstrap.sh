#!/usr/bin/env bash
# ==============================================================================
# bootstrap.sh — One-time setup script for a fresh Ubuntu 22.04+ server
#
# Sets up Docker, deploy user, directory structure, environment files,
# firewall rules, and starts infrastructure containers for Wallow.
#
# Usage:
#   sudo ./bootstrap.sh [--ssh-key "ssh-rsa AAAA..."]
#
# This script is idempotent — safe to re-run.
# ==============================================================================
set -euo pipefail

# ==============================================================================
# COLORS AND LOGGING
# ==============================================================================
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

log()   { echo -e "[$(date '+%Y-%m-%d %H:%M:%S')] ${CYAN}INFO${NC}  $*"; }
ok()    { echo -e "[$(date '+%Y-%m-%d %H:%M:%S')] ${GREEN}OK${NC}    $*"; }
skip()  { echo -e "[$(date '+%Y-%m-%d %H:%M:%S')] ${YELLOW}SKIP${NC}  $*"; }
err()   { echo -e "[$(date '+%Y-%m-%d %H:%M:%S')] ${RED}ERROR${NC} $*" >&2; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# ==============================================================================
# 1. CHECK PREREQUISITES
# ==============================================================================
usage() {
    echo "Usage: sudo $0 [--ssh-key \"ssh-rsa AAAA...\"]"
    echo ""
    echo "One-time bootstrap for a fresh Ubuntu 22.04+ server."
    echo ""
    echo "Options:"
    echo "  --ssh-key KEY   Public SSH key to install for the deploy user"
    echo "  --help          Show this message"
    exit 1
}

if [[ $EUID -ne 0 ]]; then
    err "This script must be run as root (use sudo)."
    usage
fi

# ==============================================================================
# 2. PARSE ARGUMENTS
# ==============================================================================
SSH_KEY=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --ssh-key)
            if [[ -z "${2:-}" ]]; then
                err "--ssh-key requires a value."
                usage
            fi
            SSH_KEY="$2"
            shift 2
            ;;
        --help|-h)
            usage
            ;;
        *)
            err "Unknown argument: $1"
            usage
            ;;
    esac
done

log "Starting Wallow server bootstrap..."

# ==============================================================================
# 3. INSTALL DOCKER ENGINE + COMPOSE V2
# ==============================================================================
install_docker() {
    log "Installing Docker Engine + Compose v2..."

    if command -v docker &>/dev/null; then
        skip "Docker is already installed: $(docker --version)"
        return
    fi

    # Prerequisites
    apt-get update -qq
    apt-get install -y -qq ca-certificates curl gnupg lsb-release

    # Add Docker's official GPG key
    install -m 0755 -d /etc/apt/keyrings
    if [[ ! -f /etc/apt/keyrings/docker.gpg ]]; then
        curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
        chmod a+r /etc/apt/keyrings/docker.gpg
    fi

    # Add Docker apt repository
    if [[ ! -f /etc/apt/sources.list.d/docker.list ]]; then
        echo \
            "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
            https://download.docker.com/linux/ubuntu \
            $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null
    fi

    apt-get update -qq
    apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-compose-plugin

    systemctl enable docker
    systemctl start docker

    ok "Docker installed: $(docker --version)"
}

install_docker

# ==============================================================================
# 4. CREATE DEPLOY USER
# ==============================================================================
create_deploy_user() {
    log "Setting up deploy user..."

    if id "deploy" &>/dev/null; then
        skip "User 'deploy' already exists."
    else
        useradd --create-home --shell /bin/bash deploy
        ok "User 'deploy' created."
    fi

    # Ensure deploy is in docker group
    if id -nG deploy | grep -qw docker; then
        skip "User 'deploy' is already in docker group."
    else
        usermod -aG docker deploy
        ok "Added 'deploy' to docker group."
    fi

    # Set up SSH key if provided
    if [[ -n "$SSH_KEY" ]]; then
        local ssh_dir="/home/deploy/.ssh"
        local auth_keys="$ssh_dir/authorized_keys"

        mkdir -p "$ssh_dir"

        # Only add key if not already present
        if [[ -f "$auth_keys" ]] && grep -qF "$SSH_KEY" "$auth_keys"; then
            skip "SSH key already in authorized_keys."
        else
            echo "$SSH_KEY" >> "$auth_keys"
            ok "SSH key added to authorized_keys."
        fi

        chmod 700 "$ssh_dir"
        chmod 600 "$auth_keys"
        chown -R deploy:deploy "$ssh_dir"
    else
        skip "No --ssh-key provided; skipping SSH key setup."
    fi
}

create_deploy_user

# ==============================================================================
# 5. CREATE DIRECTORY STRUCTURE
# ==============================================================================
create_directories() {
    log "Creating directory structure under /opt/wallow..."

    local dirs=(
        /opt/wallow/dev
        /opt/wallow/staging
        /opt/wallow/prod
        /opt/wallow/scripts
    )

    for dir in "${dirs[@]}"; do
        if [[ -d "$dir" ]]; then
            skip "Directory $dir already exists."
        else
            mkdir -p "$dir"
            ok "Created $dir"
        fi
    done

    chown -R deploy:deploy /opt/wallow
    ok "Ownership set to deploy:deploy on /opt/wallow"
}

create_directories

# ==============================================================================
# 6. COPY FILES FROM REPO DEPLOY DIRECTORY
# ==============================================================================
copy_deploy_files() {
    log "Copying deploy files from $SCRIPT_DIR..."

    local envs=(dev staging prod)

    # Copy docker-compose.base.yml + init-db.sql to each env directory
    for env in "${envs[@]}"; do
        cp "$SCRIPT_DIR/docker-compose.base.yml" "/opt/wallow/$env/docker-compose.base.yml"
        cp "$SCRIPT_DIR/init-db.sql" "/opt/wallow/$env/init-db.sql"
        ok "Copied base compose + init-db.sql to /opt/wallow/$env/"
    done

    # Copy environment-specific compose files
    cp "$SCRIPT_DIR/docker-compose.dev.yml"     "/opt/wallow/dev/docker-compose.dev.yml"
    cp "$SCRIPT_DIR/docker-compose.staging.yml" "/opt/wallow/staging/docker-compose.staging.yml"
    cp "$SCRIPT_DIR/docker-compose.prod.yml"    "/opt/wallow/prod/docker-compose.prod.yml"
    ok "Copied environment-specific compose files."

    # Copy deploy.sh to scripts (if it exists)
    if [[ -f "$SCRIPT_DIR/deploy.sh" ]]; then
        cp "$SCRIPT_DIR/deploy.sh" "/opt/wallow/scripts/deploy.sh"
        chmod +x "/opt/wallow/scripts/deploy.sh"
        ok "Copied deploy.sh to /opt/wallow/scripts/"
    else
        skip "deploy.sh not found in $SCRIPT_DIR; skipping."
    fi

    chown -R deploy:deploy /opt/wallow
}

copy_deploy_files

# ==============================================================================
# 7. GENERATE .ENV FILES FOR EACH ENVIRONMENT
# ==============================================================================
generate_env_file() {
    local env_name="$1"
    local compose_project="$2"
    local aspnet_env="$3"
    local app_tag="$4"
    local env_file="/opt/wallow/$env_name/.env"

    if [[ -f "$env_file" ]]; then
        skip ".env already exists at $env_file (not overwriting existing secrets)."
        return
    fi

    local pg_pass
    local rmq_pass
    pg_pass="$(openssl rand -base64 32)"
    rmq_pass="$(openssl rand -base64 32)"

    cat > "$env_file" <<EOF
# Auto-generated by bootstrap.sh on $(date -Iseconds)
# Environment: ${env_name}

# Compose
COMPOSE_PROJECT_NAME=${compose_project}
ASPNETCORE_ENVIRONMENT=${aspnet_env}

# App Image
APP_IMAGE=ghcr.io/bc-solutions-coder/wallow
APP_TAG=${app_tag}

# PostgreSQL
POSTGRES_USER=wallow
POSTGRES_PASSWORD=${pg_pass}
POSTGRES_DB=wallow

# RabbitMQ
RABBITMQ_USER=wallow
RABBITMQ_PASSWORD=${rmq_pass}
EOF

    chmod 600 "$env_file"
    chown deploy:deploy "$env_file"
    ok "Generated $env_file with unique secrets."
}

generate_env_files() {
    log "Generating .env files..."

    generate_env_file "dev"     "wallow-dev"     "Development" "dev"
    generate_env_file "staging" "wallow-staging" "Staging"     "staging"
    generate_env_file "prod"    "wallow-prod"    "Production"  "latest"
}

generate_env_files

# ==============================================================================
# 8. CONFIGURE FIREWALL (UFW)
# ==============================================================================
configure_firewall() {
    log "Configuring firewall..."

    if ! command -v ufw &>/dev/null; then
        skip "ufw is not installed; skipping firewall configuration."
        return
    fi

    ufw allow ssh          >/dev/null 2>&1 && ok "Allowed SSH (22/tcp)"
    ufw allow 8080/tcp     >/dev/null 2>&1 && ok "Allowed 8080/tcp (prod app)"
    ufw allow 8081/tcp     >/dev/null 2>&1 && ok "Allowed 8081/tcp (dev app)"
    ufw allow 8082/tcp     >/dev/null 2>&1 && ok "Allowed 8082/tcp (staging app)"

    if ufw status | grep -q "Status: active"; then
        skip "ufw is already active."
    else
        ufw --force enable
        ok "Firewall enabled."
    fi
}

configure_firewall

# ==============================================================================
# 9. START INFRASTRUCTURE CONTAINERS
# ==============================================================================
start_infrastructure() {
    log "Starting infrastructure containers (postgres + rabbitmq)..."

    local envs=(dev staging prod)

    for env in "${envs[@]}"; do
        local env_dir="/opt/wallow/$env"
        local compose_base="$env_dir/docker-compose.base.yml"
        local compose_env="$env_dir/docker-compose.${env}.yml"
        local env_file="$env_dir/.env"

        if [[ ! -f "$env_file" ]]; then
            err "No .env file found for $env; skipping container start."
            continue
        fi

        log "Starting $env infrastructure..."
        (
            cd "$env_dir"
            docker compose \
                -f "$compose_base" \
                -f "$compose_env" \
                --env-file "$env_file" \
                up -d postgres rabbitmq
        )
        ok "Started $env postgres + rabbitmq containers."
    done

    # Wait for health checks
    log "Waiting for health checks (up to 60s)..."
    local timeout=60
    local elapsed=0
    local interval=5
    local all_healthy=false

    while [[ $elapsed -lt $timeout ]]; do
        all_healthy=true
        for env in "${envs[@]}"; do
            local project_name
            project_name=$(grep '^COMPOSE_PROJECT_NAME=' "/opt/wallow/$env/.env" | cut -d= -f2)

            for svc in postgres rabbitmq; do
                local container="${project_name}-${svc}"
                local health
                health=$(docker inspect --format='{{.State.Health.Status}}' "$container" 2>/dev/null || echo "missing")
                if [[ "$health" != "healthy" ]]; then
                    all_healthy=false
                fi
            done
        done

        if $all_healthy; then
            break
        fi

        sleep "$interval"
        elapsed=$((elapsed + interval))
        log "Waiting... (${elapsed}s / ${timeout}s)"
    done

    if $all_healthy; then
        ok "All infrastructure containers are healthy."
    else
        err "Some containers did not become healthy within ${timeout}s. Check with: docker ps"
    fi
}

start_infrastructure

# ==============================================================================
# 10. PRINT SUMMARY
# ==============================================================================
print_summary() {
    echo ""
    echo -e "${GREEN}============================================${NC}"
    echo -e "${GREEN}  Wallow Bootstrap Complete${NC}"
    echo -e "${GREEN}============================================${NC}"
    echo ""
    echo -e "${CYAN}Directory structure:${NC}"
    echo "  /opt/wallow/"
    echo "  |-- dev/          (Development environment)"
    echo "  |   |-- docker-compose.base.yml"
    echo "  |   |-- docker-compose.dev.yml"
    echo "  |   |-- init-db.sql"
    echo "  |   \`-- .env"
    echo "  |-- staging/      (Staging environment)"
    echo "  |   |-- docker-compose.base.yml"
    echo "  |   |-- docker-compose.staging.yml"
    echo "  |   |-- init-db.sql"
    echo "  |   \`-- .env"
    echo "  |-- prod/         (Production environment)"
    echo "  |   |-- docker-compose.base.yml"
    echo "  |   |-- docker-compose.prod.yml"
    echo "  |   |-- init-db.sql"
    echo "  |   \`-- .env"
    echo "  \`-- scripts/"
    echo "      \`-- deploy.sh"
    echo ""
    echo -e "${CYAN}Open ports:${NC}"
    echo "  22/tcp    SSH"
    echo "  8080/tcp  Production app"
    echo "  8081/tcp  Development app"
    echo "  8082/tcp  Staging app"
    echo ""
    echo -e "${CYAN}Running infrastructure:${NC}"
    docker ps --format "  {{.Names}}\t{{.Status}}" 2>/dev/null | grep -E "wallow-(dev|staging|prod)" || echo "  (check with: docker ps)"
    echo ""
    echo -e "${CYAN}Next steps:${NC}"
    echo "  1. Configure GitHub secrets for CI/CD:"
    echo "     - DEPLOY_SSH_KEY    (private key matching --ssh-key)"
    echo "     - DEPLOY_HOST       (this server's IP or hostname)"
    echo "     - DEPLOY_USER       (deploy)"
    echo "  2. Push to dev/main or create a version tag to deploy:"
    echo "     git push origin dev          # deploys to dev"
    echo "     git push origin main         # deploys to staging"
    echo "     git tag v0.1.0 && git push origin v0.1.0  # deploys to prod"
    echo ""
    echo -e "${GREEN}Bootstrap complete. Server is ready for deployments.${NC}"
}

print_summary
