import http from 'k6/http';
import { check, sleep } from 'k6';
import { config, requireAuthEnv } from '../lib/config.js';

requireAuthEnv();

export const options = {
  scenarios: {
    login_spike: {
      executor: 'ramping-vus',
      stages: [
        { duration: '30s', target: 10 },
        { duration: '1m', target: 50 },
        { duration: '1m', target: 150 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{endpoint:auth_login}': ['p(95)<800', 'p(99)<1500'],
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export default function () {
  const res = http.post(
    `${config.baseUrl}/api/v1/auth/login`,
    JSON.stringify({
      email: config.email,
      password: config.password,
    }),
    {
      headers: { 'Content-Type': 'application/json' },
      timeout: config.timeout,
      tags: { endpoint: 'auth_login' },
    }
  );

  check(res, {
    'login 200': (r) => r.status === 200,
    'token returned': (r) => !!r.json('token'),
  });

  sleep(0.5);
}
