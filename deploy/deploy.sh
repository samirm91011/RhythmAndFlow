#!/usr/bin/env bash
# Runs on the server (called by the GitHub pipeline, or by hand):   API_IMAGE=ghcr.io/owner/rhythmflow-api:TAG ./deploy.sh
# Pulls the new image, restarts what changed, and waits until the API reports healthy.
set -euo pipefail
cd "$(dirname "$0")"

if [ ! -f .env ]; then echo "Missing .env - copy .env.example to .env and fill it in first."; exit 1; fi
if [ -n "${API_IMAGE:-}" ]; then
  # remember the image in .env so a plain 'docker compose up' later uses the same version
  if grep -q '^API_IMAGE=' .env; then sed -i "s#^API_IMAGE=.*#API_IMAGE=${API_IMAGE}#" .env; else echo "API_IMAGE=${API_IMAGE}" >> .env; fi
fi

docker compose pull
docker compose up -d --remove-orphans

echo "Waiting for the API to become healthy..."
for i in $(seq 1 40); do
  status=$(docker inspect -f '{{.State.Health.Status}}' rhythmflow-api-1 2>/dev/null || echo "starting")
  if [ "$status" = "healthy" ]; then echo "API is healthy."; docker image prune -f >/dev/null; exit 0; fi
  sleep 5
done
echo "API did not become healthy. Recent logs:"
docker compose logs --tail=60 api
exit 1
