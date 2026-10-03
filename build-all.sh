#!/usr/bin/env bash
set -e
echo "Building all RecallDB images..."
TAG="${1:-latest}"
DIR="$(cd "$(dirname "$0")" && pwd)"
"$DIR/build-server.sh" "$TAG"
"$DIR/build-dashboard.sh" "$TAG"
echo "Done building all RecallDB images (tag: $TAG)."
