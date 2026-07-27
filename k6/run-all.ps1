# Runs every serializer endpoint at every payload size, saving a JSON summary
# per run to .\results. Adjust $Vus / $Duration to taste.
#
# Usage:
#   .\run-all.ps1
#   .\run-all.ps1 -BaseUrl http://localhost:5000 -Vus 50 -Duration 30s

param(
    [string]$BaseUrl = "http://localhost:5000",
    [int]$Vus = 50,
    [string]$Duration = "30s"
)

$Counts = @(10, 100, 1000)

$Endpoints = @{
    "json"             = "application/json"
    "messagepack"      = "application/x-msgpack"
    "messagepack-lz4"  = "application/x-msgpack"
    "protobuf-net"     = "application/x-protobuf"
    "google-protobuf"  = "application/x-protobuf"
}

New-Item -ItemType Directory -Force -Path "results" | Out-Null

foreach ($count in $Counts) {
    foreach ($endpoint in $Endpoints.Keys) {
        $contentType = $Endpoints[$endpoint]
        Write-Host ""
        Write-Host "=== $endpoint  (count=$count) ==="

        $env:BASE_URL = $BaseUrl
        $env:ENDPOINT = $endpoint
        $env:COUNT = $count
        $env:CONTENT_TYPE = $contentType
        $env:VUS = $Vus
        $env:DURATION = $Duration

        k6 run --summary-export="results/${endpoint}_${count}.json" load-test.js
    }
}

Write-Host ""
Write-Host "All runs complete. Summaries in .\results\*.json"
Write-Host "Key fields per file: metrics.http_req_duration (avg/p95/p99), metrics.http_reqs (rate), metrics.data_sent/data_received (bytes)"
