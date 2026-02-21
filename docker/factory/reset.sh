#!/usr/bin/env bash
# ============================================================================
# RecallDB Factory Reset Script
# Resets the Docker deployment to default factory configuration.
# ============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$(dirname "$SCRIPT_DIR")"

echo "========================================="
echo "  RecallDB Factory Reset"
echo "========================================="
echo ""
echo "WARNING: This will destroy ALL data including:"
echo "  - All tenants, users, credentials, and collections"
echo "  - All documents and vector embeddings"
echo "  - All log files"
echo "  - All configuration changes"
echo ""
echo "The deployment will be restored to factory defaults:"
echo "  - Default tenant: 'Default Tenant'"
echo "  - Default user: admin@recall / password"
echo "  - Default credential: bearer token 'default'"
echo "  - Default collection: 'Default Collection' (384 dimensions)"
echo ""
read -r -p "Type RESET to confirm: " confirmation

if [ "$confirmation" != "RESET" ]; then
    echo ""
    echo "Reset cancelled."
    exit 1
fi

echo ""
echo "[1/6] Stopping containers and removing volumes..."
cd "$DOCKER_DIR"
docker compose down -v 2>/dev/null || docker-compose down -v 2>/dev/null
echo "       Done."

echo ""
echo "[2/6] Restoring factory configuration..."
cp "$SCRIPT_DIR/recalldb.json" "$DOCKER_DIR/recalldb.json"
echo "       Copied recalldb.json"

echo ""
echo "[3/6] Cleaning log files..."
if [ -d "$DOCKER_DIR/logs" ]; then
    find "$DOCKER_DIR/logs" -type f -name "*.log" -delete
    find "$DOCKER_DIR/logs" -type f -name "*.log.*" -delete
    echo "       Removed log files from $DOCKER_DIR/logs/"
else
    echo "       No logs directory found, skipping."
fi

echo ""
echo "[4/6] Starting PostgreSQL (pgvector)..."
docker compose up -d pgvector 2>/dev/null || docker-compose up -d pgvector 2>/dev/null

echo "       Waiting for PostgreSQL to become ready..."
retries=0
max_retries=30
until docker exec recalldb-pgvector pg_isready -U recalldb -d recalldb > /dev/null 2>&1; do
    retries=$((retries + 1))
    if [ $retries -ge $max_retries ]; then
        echo "       ERROR: PostgreSQL did not become ready within ${max_retries} seconds."
        exit 1
    fi
    sleep 1
done
echo "       PostgreSQL is ready."

echo ""
echo "[5/6] Applying factory database schema and default records..."
docker exec -i recalldb-pgvector psql -U recalldb -d recalldb < "$SCRIPT_DIR/recalldb_factory.sql"
echo "       Database initialized."

echo ""
echo "[6/6] Starting all services..."
docker compose up -d 2>/dev/null || docker-compose up -d 2>/dev/null

echo ""
echo "========================================="
echo "  Factory reset complete!"
echo "========================================="
echo ""
echo "  Server:     http://localhost:8600"
echo "  Dashboard:  http://localhost:8601"
echo ""
echo "  Default login:"
echo "    Email:    admin@recall"
echo "    Password: password"
echo "    Bearer:   default"
echo "    Admin key: recalldbadmin"
echo ""
