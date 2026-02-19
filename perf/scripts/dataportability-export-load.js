import http from 'k6/http';
import { check, sleep } from 'k6';
import { config, requireAuthEnv } from '../lib/config.js';
import { authHeaders, login } from '../lib/auth.js';

requireAuthEnv();

export const options = {
  scenarios: {
    export_json: {
      executor: 'constant-vus',
      vus: config.exportTest.vus,
      duration: config.exportTest.duration,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{endpoint:dataportability_export}': ['p(95)<2000'],
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const token = login();
  return { token };
}

export default function (data) {
  const headers = authHeaders(data.token);
  const res = http.get(`${config.baseUrl}/api/v1/dataportability/export`, {
    headers,
    timeout: '60s',
    tags: { endpoint: 'dataportability_export' },
  });

  check(res, {
    'export 200': (r) => r.status === 200,
    'export has json': (r) => (r.headers['Content-Type'] || '').includes('application/json'),
  });

  sleep(0.5);
}
