import http from 'k6/http';
import { check } from 'k6';
import { config } from './config.js';

export function login() {
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
    'login status is 200': (r) => r.status === 200,
    'login has token': (r) => !!r.json('token'),
  });

  if (res.status !== 200) {
    throw new Error(`Login failed with status=${res.status}. body=${res.body}`);
  }

  const token = res.json('token');
  if (!token) {
    throw new Error(`Login succeeded without token. body=${res.body}`);
  }

  return token;
}

export function authHeaders(token) {
  return {
    Authorization: `Bearer ${token}`,
  };
}
