import http from 'k6/http';
import { check, sleep } from 'k6';
import { config, requireAuthEnv } from '../lib/config.js';
import { authHeaders, login } from '../lib/auth.js';

requireAuthEnv();

function normalizeImportPath(pathValue) {
  if (pathValue.startsWith('./perf/')) {
    return pathValue.replace('./perf/', '../');
  }
  if (pathValue.startsWith('perf/')) {
    return pathValue.replace('perf/', '../');
  }
  return pathValue;
}

const importFilePath = normalizeImportPath(config.importTest.file);
const replaceExisting = config.importTest.replaceExisting;
const importFileContent = open(importFilePath, 'b');

export const options = {
  scenarios: {
    import_json: {
      executor: 'constant-vus',
      vus: config.importTest.vus,
      duration: config.importTest.duration,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.02'],
    'http_req_duration{endpoint:dataportability_import}': ['p(95)<3000', 'p(99)<6000'],
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  const token = login();
  return { token };
}

export default function (data) {
  const headers = authHeaders(data.token);
  const formData = {
    file: http.file(importFileContent, 'snapshot.json', 'application/json'),
    replaceExisting,
  };

  const res = http.post(`${config.baseUrl}/api/v1/dataportability/import`, formData, {
    headers,
    timeout: '60s',
    tags: { endpoint: 'dataportability_import' },
  });

  check(res, {
    'import 200': (r) => r.status === 200,
    'import has importedRecords': (r) => r.status === 200 && r.json('importedRecords') !== null,
  });

  sleep(1);
}
