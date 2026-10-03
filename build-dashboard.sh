#!/usr/bin/env bash
set -e
echo "Building RecallDB Dashboard..."
TAG="${1:-latest}"
cd "$(dirname "$0")/dashboard"
echo "Building and pushing multi-platform Docker image..."
if [ "$TAG" = "latest" ]; then
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/recalldb-dashboard:latest --push .
else
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/recalldb-dashboard:"$TAG" -t jchristn77/recalldb-dashboard:latest --push .
fi
echo "Updating local Docker image..."
docker pull jchristn77/recalldb-dashboard:"$TAG"
if [ "$TAG" != "latest" ]; then docker pull jchristn77/recalldb-dashboard:latest; fi
