import http from 'k6/http';
import { check } from 'k6';

// Usage:
//   BASE_URL=http://localhost:5000 ENDPOINT=messagepack COUNT=1000 \
//   CONTENT_TYPE=application/x-msgpack k6 run load-test.js
//
// ENDPOINT must match one of: json, messagepack, messagepack-lz4, protobuf-net, google-protobuf
// COUNT must match a generated payload file: 10, 100, or 1000

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const ENDPOINT = __ENV.ENDPOINT || 'json';
const COUNT = __ENV.COUNT || '1000';
const CONTENT_TYPE = __ENV.CONTENT_TYPE || 'application/json';

const payload = open(`../payloads/payload_${ENDPOINT}_${COUNT}.bin`, 'b');

export const options = {
  vus: Number(__ENV.VUS || 50),
  duration: __ENV.DURATION || '30s',
  thresholds: {
    http_req_failed: ['rate<0.01'],
  },
};

export default function () {
  const res = http.post(`${BASE_URL}/api/${ENDPOINT}/echo`, payload, {
    headers: { 'Content-Type': CONTENT_TYPE },
  });
  check(res, { 'status is 200': (r) => r.status === 200 });
}
