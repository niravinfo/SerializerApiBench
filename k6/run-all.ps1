# Runs every serializer endpoint, at every payload size, in all three modes
# (serialize-only, deserialize-only, roundtrip), saving a JSON summary per run
# to .\results. 5 endpoints x 3 counts x 3 modes = 45 runs.
#
# A cooldown pause runs between every test so GC/connection cleanup from one run
# doesn't bleed into the next run's numbers.
#
# Usage:
#   .\run-all.ps1
#   .\run-all.ps1 -BaseUrl http://localhost:5000 -Vus 20 -Duration 15s -Cooldown 10
# To keep a first pass shorter, try: .\run-all.ps1 -Duration 10s -Cooldown 2

param(
    [string]$BaseUrl = "http://localhost:5000",
    [int]$Vus = 20,
    [string]$Duration = "15s",
    [int]$Cooldown = 10
)

$Counts = @(10, 100, 1000)
# $Modes = @("serialize-only", "deserialize-only", "roundtrip")
$Modes = @("serialize-only", "deserialize-only")

$Endpoints = @{
    "json"             = "application/json"
    "newtonsoft-json"  = "application/json"
    "messagepack"      = "application/x-msgpack"
    "messagepack-lz4"  = "application/x-msgpack"
    "protobuf-net"     = "application/x-protobuf"
    "google-protobuf"  = "application/x-protobuf"
}

New-Item -ItemType Directory -Force -Path "results" | Out-Null

foreach ($mode in $Modes) {
    foreach ($count in $Counts) {
        foreach ($endpoint in $Endpoints.Keys) {
            $contentType = $Endpoints[$endpoint]
            Write-Host ""
            Write-Host "=== $endpoint  mode=$mode  count=$count ==="

            $env:BASE_URL = $BaseUrl
            $env:ENDPOINT = $endpoint
            $env:COUNT = $count
            $env:CONTENT_TYPE = $contentType
            $env:VUS = $Vus
            $env:DURATION = $Duration
            $env:MODE = $mode

            k6 run --summary-export="results/${endpoint}_${mode}_${count}.json" load-test.js

            Write-Host "Cooling down for $Cooldown s..."
            Start-Sleep -Seconds $Cooldown
        }
    }
}

Write-Host ""
Write-Host "All runs complete. Summaries in .\results\*.json"
Write-Host "Key fields per file: metrics.http_req_duration (avg/p95/p99), metrics.http_reqs (rate), metrics.data_sent/data_received (bytes)"
