# SerializerApiBench

Web API-level companion to your [SerializersBenchmark](https://github.com/niravinfo/SerializersBenchmark) BenchmarkDotNet
project. Same `TestPayload` shape, same 10/100/1000-item counts, but measured end-to-end
through an ASP.NET Core minimal API under k6 load — so you get real latency percentiles,
throughput, and bytes-over-the-wire, not just isolated CPU time.

## Layout

```
SerializerApiBench.Models/           TestPayload/Address POCOs + .proto schema + mapper
SerializerApiBench.Api/              Minimal API, one echo endpoint per serializer
SerializerApiBench.PayloadGenerator/ Generates the binary request bodies k6 sends
k6/                                  Load test scripts
payloads/                            Generated request bodies (created by PayloadGenerator)
```

Each of the 5 formats (System.Text.Json, MessagePack, MessagePack+LZ4, protobuf-net,
Google.Protobuf) exposes three endpoints, so you can isolate serialize cost from
deserialize cost instead of only seeing the combined round-trip number:

| Endpoint | Method | What it measures |
|---|---|---|
| `/api/{format}/roundtrip` | POST | Realistic service-to-service call: deserialize request -> reserialize -> respond |
| `/api/{format}/serialize-only` | GET `?count=10\|100\|1000` | Pure serialize cost — server already holds the data in memory, no request body |
| `/api/{format}/deserialize-only` | POST | Pure deserialize cost — server returns a tiny ack (item count), not a reserialized body |

e.g. `/api/messagepack/serialize-only`, `/api/protobuf-net/deserialize-only`, etc.

## Prerequisites

- .NET 10 SDK
- [k6](https://k6.io/docs/get-started/installation/)
- Docker (optional, but recommended for a fair, resource-capped comparison)

## 1. Generate the payloads

```bash
dotnet run -c Release --project SerializerApiBench.PayloadGenerator
```

This writes `payload_{format}_{count}.bin` files into `payloads/` — the exact bytes k6
will POST to each endpoint. Same random seed across formats, so every serializer is
handed equivalent data.

## 2. Run the API

**Option A — plain dotnet run** (fine for local sanity checks, but not resource-capped):

```bash
dotnet run -c Release --project SerializerApiBench.Api
```

**Option B — Docker, capped at 2 CPU / 512MB** (recommended — this is what makes the
comparison fair, since every serializer gets exactly the same ceiling):

```bash
docker compose up --build
```

Either way, the API listens on `http://localhost:5000`.

## 3. Run the load tests

```bash
cd k6
chmod +x run-all.sh
./run-all.sh
```

This hits every endpoint, every payload size, in all three modes — 5 formats x 3 counts
x 3 modes = **45 runs** (default 50 VUs / 30s each, so ~22-23 minutes total). Override
with `VUS=` / `DURATION=` env vars; for a quick first pass try `DURATION=10s`.

Windows (PowerShell): use `run-all.ps1` instead — same behavior, same flags as named
parameters (`-Vus`, `-Duration`, `-BaseUrl`).

To run a single combination instead:

```bash
ENDPOINT=messagepack COUNT=1000 CONTENT_TYPE=application/x-msgpack MODE=serialize-only k6 run load-test.js
```

## 4. What to pull out of the results

Each `k6/results/{endpoint}_{count}.json` summary contains:

| Field | What it tells you |
|---|---|
| `metrics.http_req_duration.avg / p(95) / p(99)` | Real user-facing latency, not just CPU time |
| `metrics.http_reqs.rate` | Sustained requests/sec |
| `metrics.http_req_failed.rate` | Stability under load |
| `metrics.data_sent` / `metrics.data_received` | Actual bytes over the wire — ties directly to bandwidth/egress cost |

Optional: run `dotnet-counters monitor -p <api-pid>` (or `docker stats`) alongside a run
to capture server-side CPU% and GC activity, so you can see whether the allocation
numbers from your BenchmarkDotNet run actually show up as CPU pressure under real load.

## Notes

- Routes are separate per format (no content negotiation) so every request follows an
  identical code path — deserialize, reserialize, respond — regardless of format.
- `protobuf-net` and `MessagePack` here serialize the shared POCO directly; `Google.Protobuf`
  goes through the generated `TestPayloadProto` / `TestPayloadListProto` classes from
  `TestPayload.proto`, mapped via `ProtoMapper`, since that's the idiomatic (and
  cross-language-compatible) way to use it.
- Keep VU count and duration identical across every run in a given comparison —
  changing them mid-comparison invalidates the results.
