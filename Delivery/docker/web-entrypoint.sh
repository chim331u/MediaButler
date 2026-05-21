#!/usr/bin/env sh

# ==============================================================================
# Script: web-entrypoint.sh
# Description: Dynamically configures the SPA API Base URL at container startup
#              and executes Nginx.
# ==============================================================================

set -e

APP_SETTINGS="/usr/share/nginx/html/appsettings.json"

if [ -n "${API_BASE_URL:-}" ]; then
    echo "[Entrypoint] Configuring API Base URL at runtime to: ${API_BASE_URL}"
    
    # Escape API_BASE_URL for use in sed replacement
    ESCAPED_URL=$(echo "$API_BASE_URL" | sed 's/[&/]/\\&/g')
    
    # Update all appsettings*.json files in the target directory
    for f in /usr/share/nginx/html/appsettings*.json; do
        if [ -f "$f" ]; then
            echo "[Entrypoint] Updating ${f}..."
            sed -i "s|\"BaseUrl\"[[:space:]]*:[[:space:]]*\"[^\"]*\"|\"BaseUrl\": \"${ESCAPED_URL}\"|g" "$f"
        fi
    done
    echo "[Entrypoint] Dynamic configuration update completed."
else
    echo "[Entrypoint] No API_BASE_URL provided. Using defaults."
fi

# Execute Nginx in the foreground (daemon off)
exec "$@"
