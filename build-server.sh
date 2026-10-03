#!/usr/bin/env bash
set -e
echo "Building RecallDB Server..."
TAG="${1:-latest}"
cd "$(dirname "$0")"
echo "Building and pushing linux/amd64 Docker image..."
if [ "$TAG" = "latest" ]; then
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64 -t jchristn77/recalldb-server:latest -f src/RecallDb.Server/Dockerfile --push .
else
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64 -t jchristn77/recalldb-server:"$TAG" -t jchristn77/recalldb-server:latest -f src/RecallDb.Server/Dockerfile --push .
fi
echo "Updating local Docker image..."
docker pull --platform linux/amd64 jchristn77/recalldb-server:"$TAG"
if [ "$TAG" != "latest" ]; then docker pull --platform linux/amd64 jchristn77/recalldb-server:latest; fi
