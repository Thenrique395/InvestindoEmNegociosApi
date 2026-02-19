import http from 'k6/http';
import { check, sleep } from 'k6';
import { config, requireAuthEnv } from '../lib/config.js';
import { authHeaders, login } from '../lib/auth.js';

requireAuthEnv();

export const options = {
  scenarios: {
    api_reads: {
      executor: 'ramping-arrival-rate',
      startRate: 5,
      timeUnit: '1s',
      preAllocatedVUs: 20,
      maxVUs: 120,
      stages: [
        { duration: '1m', target: 20 },
        { duration: '2m', target: 60 },
        { duration: '1m', target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{endpoint:profile}': ['p(95)<400'],
    'http_req_duration{endpoint:investments_positions}': ['p(95)<500'],
    'http_req_duration{endpoint:notifications}': ['p(95)<500'],
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const token = login();
  return { token };
}

export default function (data) {
  const headers = authHeaders(data.token);

  const profileRes = http.get(`${config.baseUrl}/api/v1/profile`, {
    headers,
    timeout: config.timeout,
    tags: { endpoint: 'profile' },
  });
  check(profileRes, { 'profile 200': (r) => r.status === 200 });

  const positionsRes = http.get(`${config.baseUrl}/api/v1/investments/positions`, {
    headers,
    timeout: config.timeout,
    tags: { endpoint: 'investments_positions' },
  });
  check(positionsRes, { 'positions 200': (r) => r.status === 200 });

  const notificationsRes = http.get(`${config.baseUrl}/api/v1/notifications`, {
    headers,
    timeout: config.timeout,
    tags: { endpoint: 'notifications' },
  });
  check(notificationsRes, { 'notifications 200': (r) => r.status === 200 });

  sleep(0.2);
}
