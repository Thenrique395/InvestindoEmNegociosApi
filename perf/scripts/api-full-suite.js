import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { config, requireAuthEnv } from '../lib/config.js';
import { authHeaders, login } from '../lib/auth.js';

requireAuthEnv();

const adminEmail = config.adminEmail;
const adminPassword = config.adminPassword;
const writeMode = config.fullSuite.writeMode;

export const options = {
  scenarios: {
    full_api_suite: {
      executor: 'constant-vus',
      vus: config.fullSuite.vus,
      duration: config.fullSuite.duration,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.02'],
    http_req_duration: ['p(95)<1200', 'p(99)<2500'],
    'http_req_duration{endpoint:auth_login}': ['p(95)<900'],
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

function getJson(res) {
  try {
    return res.json();
  } catch {
    return null;
  }
}

function callGet(url, headers, endpoint, accepted = [200]) {
  const res = http.get(url, {
    headers,
    timeout: config.timeout,
    tags: { endpoint },
  });

  check(res, {
    [`${endpoint} status ok`]: (r) => accepted.includes(r.status),
  });

  return res;
}

function callPost(url, headers, endpoint, body) {
  const res = http.post(url, body, {
    headers: {
      ...headers,
      'Content-Type': 'application/json',
    },
    timeout: config.timeout,
    tags: { endpoint },
  });

  check(res, {
    [`${endpoint} status ok`]: (r) => [200, 201, 204].includes(r.status),
  });

  return res;
}

function loginAs(email, password) {
  const res = http.post(
    `${config.baseUrl}/api/v1/auth/login`,
    JSON.stringify({ email, password }),
    {
      headers: { 'Content-Type': 'application/json' },
      timeout: config.timeout,
      tags: { endpoint: 'auth_login' },
    }
  );

  check(res, {
    'auth_login status 200': (r) => r.status === 200,
    'auth_login token': (r) => !!r.json('token'),
  });

  if (res.status !== 200) {
    throw new Error(`Login failed: ${res.status}`);
  }

  return res.json('token');
}

export function setup() {
  const userToken = login();
  const data = { userToken, adminToken: null };

  if (adminEmail && adminPassword) {
    data.adminToken = loginAs(adminEmail, adminPassword);
  }

  return data;
}

export default function (data) {
  const headers = authHeaders(data.userToken);

  group('account-and-profile', () => {
    callGet(`${config.baseUrl}/api/v1/profile`, headers, 'profile_get', [200, 204]);
    callGet(`${config.baseUrl}/api/v1/preferences`, headers, 'preferences_get');
    callGet(`${config.baseUrl}/api/v1/onboarding`, headers, 'onboarding_get');
  });

  group('lookups-and-catalogs', () => {
    callGet(`${config.baseUrl}/api/v1/lookups/payment-methods`, headers, 'lookups_payment_methods');
    callGet(`${config.baseUrl}/api/v1/lookups/card-brands`, headers, 'lookups_card_brands');
    callGet(`${config.baseUrl}/api/v1/lookups/institutions`, headers, 'lookups_institutions');
    callGet(`${config.baseUrl}/api/v1/categories`, headers, 'categories_list');
    callGet(`${config.baseUrl}/api/v1/cards`, headers, 'cards_list');
  });

  group('financial-core', () => {
    callGet(`${config.baseUrl}/api/v1/goals`, headers, 'goals_list');
    callGet(`${config.baseUrl}/api/v1/goals/income`, headers, 'goals_income', [200, 204]);
    callGet(`${config.baseUrl}/api/v1/plans`, headers, 'plans_list');
    callGet(`${config.baseUrl}/api/v1/installments`, headers, 'installments_list');
    callGet(`${config.baseUrl}/api/v1/receitas/summary`, headers, 'income_summary');
  });

  group('investments', () => {
    const positionsRes = callGet(`${config.baseUrl}/api/v1/investments/positions`, headers, 'investments_positions');
    const positions = getJson(positionsRes) || [];

    callGet(`${config.baseUrl}/api/v1/investments/goal`, headers, 'investments_goal', [200, 204]);
    callGet(`${config.baseUrl}/api/v1/investments/allocation-target`, headers, 'investments_allocation_target');
    callGet(`${config.baseUrl}/api/v1/investments/benchmarks?months=6`, headers, 'investments_benchmarks');

    if (positions.length > 0) {
      const first = positions[0];
      const id = first.id;
      const symbol = first.asset || 'VALE3';

      if (id) {
        callGet(`${config.baseUrl}/api/v1/investments/positions/${id}`, headers, 'investments_position_by_id');
      }

      callGet(`${config.baseUrl}/api/v1/investments/market/quote?symbol=${encodeURIComponent(symbol)}`, headers, 'investments_market_quote', [200, 503]);
      callGet(`${config.baseUrl}/api/v1/investments/market/profile?symbol=${encodeURIComponent(symbol)}`, headers, 'investments_market_profile', [200, 503]);
      callGet(`${config.baseUrl}/api/v1/investments/market/history?symbol=${encodeURIComponent(symbol)}&period=6mo`, headers, 'investments_market_history', [200, 503]);
    } else {
      callGet(`${config.baseUrl}/api/v1/investments/market/quote?symbol=VALE3`, headers, 'investments_market_quote', [200, 503]);
    }
  });

  group('notifications-and-portability', () => {
    const notificationsRes = callGet(`${config.baseUrl}/api/v1/notifications?limit=20`, headers, 'notifications_list');
    const notifications = getJson(notificationsRes) || [];

    if (notifications.length > 0 && notifications[0]?.id) {
      callPost(`${config.baseUrl}/api/v1/notifications/${notifications[0].id}/read`, headers, 'notifications_mark_read', '{}');
    }

    callGet(`${config.baseUrl}/api/v1/dataportability/export`, headers, 'dataportability_export');
  });

  if (writeMode) {
    group('optional-write-mode', () => {
      const suffix = `${__VU}-${Date.now()}`;
      const createBody = JSON.stringify({
        name: `k6-temp-${suffix}`,
        appliesTo: 'Expense',
      });

      const created = callPost(`${config.baseUrl}/api/v1/categories`, headers, 'categories_create', createBody);
      if ([200, 201].includes(created.status)) {
        const createdId = created.json('id');
        if (createdId) {
          const del = http.del(`${config.baseUrl}/api/v1/categories/${createdId}`, null, {
            headers,
            timeout: config.timeout,
            tags: { endpoint: 'categories_delete' },
          });
          check(del, {
            'categories_delete status ok': (r) => [200, 204].includes(r.status),
          });
        }
      }
    });
  }

  if (data.adminToken) {
    group('admin-read-endpoints', () => {
      const adminHeaders = authHeaders(data.adminToken);
      callGet(`${config.baseUrl}/api/v1/admin/users`, adminHeaders, 'admin_users_list');
      callGet(`${config.baseUrl}/api/v1/admin/categories`, adminHeaders, 'admin_categories_list');
      callGet(`${config.baseUrl}/api/v1/admin/parameters/payment-methods`, adminHeaders, 'admin_payment_methods');
      callGet(`${config.baseUrl}/api/v1/admin/parameters/card-brands`, adminHeaders, 'admin_card_brands');
      callGet(`${config.baseUrl}/api/v1/admin/parameters/institutions`, adminHeaders, 'admin_institutions');
      callGet(`${config.baseUrl}/api/v1/admin/parameters/notification-settings`, adminHeaders, 'admin_notification_settings');
    });
  }

  sleep(0.5);
}
