#!/usr/bin/env bash
set -e

# Runs every serializer endpoint at every payload size, saving a JSON summary
# per run to ./results. Adjust VUS/DURATION to taste.
#
# Usage: BASE_URL=http://localhost:5000 ./run-all.sh

BASE_URL=${BASE_URL:-http://localhost:5000}
VUS=${VUS:-50}
DURATION=${DURATION:-30s}
COUNTS=(10 100 1000)

declare -A ENDPOINTS=(
  [json]="application/json"
  [messagepack]="application/x-msgpack"
  [messagepack-lz4]="application/x-msgpack"
  [protobuf-net]="application/x-protobuf"
  [google-protobuf]="application/x-protobuf"
)

mkdir -p results

for count in "${COUNTS[@]}"; do
  for endpoint in "${!ENDPOINTS[@]}"; do
    content_type=${ENDPOINTS[$endpoint]}
    echo ""
    echo "=== $endpoint  (count=$count) ==="
    BASE_URL="$BASE_URL" ENDPOINT="$endpoint" COUNT="$count" \
      CONTENT_TYPE="$content_type" VUS="$VUS" DURATION="$DURATION" \
      k6 run --summary-export="results/${endpoint}_${count}.json" load-test.js
  done
done

echo ""
echo "All runs complete. Summaries in ./results/*.json"
echo "Key fields per file: metrics.http_req_duration (avg/p95/p99), metrics.http_reqs (rate), metrics.data_sent/data_received (bytes)"
