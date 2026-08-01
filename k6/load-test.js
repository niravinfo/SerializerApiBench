import http from 'k6/http';
import { check } from 'k6';

// Usage:
//   BASE_URL=http://localhost:5000 ENDPOINT=messagepack COUNT=1000 \
//   CONTENT_TYPE=application/x-msgpack MODE=serialize-only k6 run load-test.js
//
// ENDPOINT : json | messagepack | messagepack-lz4 | protobuf-net | google-protobuf
// COUNT    : 10 | 100 | 1000 (must match a generated payload file)
// MODE     : roundtrip (default) | serialize-only | deserialize-only

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const ENDPOINT = __ENV.ENDPOINT || 'json';
const COUNT = __ENV.COUNT || '1000';
const CONTENT_TYPE = __ENV.CONTENT_TYPE || 'application/json';
const MODE = __ENV.MODE || 'serialize-only';

export const options = {
  vus: Number(__ENV.VUS || 25),
  duration: __ENV.DURATION || '30s',
  // Don't buffer response bodies: we only check status, and keeping every body in
  // JS memory (esp. 1000-item payloads) adds GC pressure that can make k6 itself
  // the bottleneck instead of the API under test.
  discardResponseBodies: true,
  thresholds: {
    http_req_failed: ['rate<0.01'],
  },
};

// serialize-only sends no body (server already holds the data in memory),
// so only load the payload file for the other two modes.
const payload =
  MODE === 'serialize-only' ? null : open(`../payloads/payload_${ENDPOINT}_${COUNT}.bin`, 'b');

export default function () {
  let res;

  if (MODE === 'serialize-only') {
    res = http.get(`${BASE_URL}/api/${ENDPOINT}/serialize-only?count=${COUNT}`);
  } else if (MODE === 'deserialize-only') {
    res = http.post(`${BASE_URL}/api/${ENDPOINT}/deserialize-only`, payload, {
      headers: { 'Content-Type': CONTENT_TYPE },
    });
  } else {
    res = http.post(`${BASE_URL}/api/${ENDPOINT}/roundtrip`, payload, {
      headers: { 'Content-Type': CONTENT_TYPE },
    });
  }

  check(res, { 'status is 200': (r) => r.status === 200 });
}
