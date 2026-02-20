#!/bin/sh
# Generate runtime config from environment variables
cat <<EOF > /usr/share/nginx/html/config.js
window.__RECALLDB_CONFIG__ = {
  RECALLDB_SERVER_URL: "${RECALLDB_SERVER_URL:-http://localhost:8601}"
};
EOF

exec "$@"
