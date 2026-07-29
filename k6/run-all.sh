#!/usr/bin/env bash
set -e

# Runs every serializer endpoint, at every payload size, in all three modes
# (serialize-only, deserialize-only, roundtrip), saving a JSON summary per run
# to ./results. 5 endpoints x 3 counts x 3 modes = 45 runs.
#
# A cooldown pause runs between every test so GC/connection cleanup from one run
# doesn't bleed into the next run's numbers.
#
# Usage: BASE_URL=http://localhost:5000 ./run-all.sh
# To keep a first pass shorter, try: DURATION=10s COOLDOWN=2 ./run-all.sh

BASE_URL=${BASE_URL:-http://localhost:5000}
VUS=${VUS:-20}
DURATION=${DURATION:-15s}
COOLDOWN=${COOLDOWN:-10}
COUNTS=(10 100 1000)
MODES=(serialize-only deserialize-only roundtrip)

declare -A ENDPOINTS=(
  [json]="application/json"
  [newtonsoft-json]="application/json"
  [messagepack]="application/x-msgpack"
  [messagepack-lz4]="application/x-msgpack"
  [protobuf-net]="application/x-protobuf"
  [google-protobuf]="application/x-protobuf"
)

mkdir -p results

for mode in "${MODES[@]}"; do
  for count in "${COUNTS[@]}"; do
    for endpoint in "${!ENDPOINTS[@]}"; do
      content_type=${ENDPOINTS[$endpoint]}
      echo ""
      echo "=== $endpoint  mode=$mode  count=$count ==="
      BASE_URL="$BASE_URL" ENDPOINT="$endpoint" COUNT="$count" \
        CONTENT_TYPE="$content_type" VUS="$VUS" DURATION="$DURATION" MODE="$mode" \
        k6 run --summary-export="results/${endpoint}_${mode}_${count}.json" load-test.js

      echo "Cooling down for ${COOLDOWN}s..."
      sleep "$COOLDOWN"
    done
  done
done

echo ""
echo "All runs complete. Summaries in ./results/*.json"
echo "Key fields per file: metrics.http_req_duration (avg/p95/p99), metrics.http_reqs (rate), metrics.data_sent/data_received (bytes)"
