const configPathInput = __ENV.PERF_CONFIG || '../config/default.json';

function candidatePaths(pathValue) {
  const values = [pathValue];
  if (pathValue.startsWith('./perf/')) {
    values.push(pathValue.replace('./perf/', '../'));
  }
  if (pathValue.startsWith('perf/')) {
    values.push(pathValue.replace('perf/', '../'));
  }
  return [...new Set(values)];
}

let fileConfig = {};
let configPath = configPathInput;
let configLoaded = false;
let lastError = null;

for (const candidate of candidatePaths(configPathInput)) {
  try {
    fileConfig = JSON.parse(open(candidate));
    configPath = candidate;
    configLoaded = true;
    break;
  } catch (error) {
    lastError = error;
  }
}

if (!configLoaded) {
  throw new Error(`Cannot read PERF_CONFIG file at "${configPathInput}": ${String(lastError)}`);
}

function envOrFile(key, fileValue, fallback = '') {
  const value = __ENV[key];
  if (value !== undefined && value !== '') {
    return value;
  }
  if (fileValue !== undefined && fileValue !== null && fileValue !== '') {
    return fileValue;
  }
  return fallback;
}

function toBool(value, fallback = false) {
  if (typeof value === 'boolean') return value;
  if (value === undefined || value === null || value === '') return fallback;
  const normalized = String(value).toLowerCase().trim();
  return normalized === 'true' || normalized === '1' || normalized === 'yes';
}

function toNumber(value, fallback) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

const apiConfig = fileConfig.api || {};
const authConfig = fileConfig.auth || {};
const fullSuiteConfig = fileConfig.fullSuite || {};
const importConfig = fileConfig.importTest || {};
const exportConfig = fileConfig.exportTest || {};

export const config = {
  configPath,
  baseUrl: envOrFile('BASE_URL', apiConfig.baseUrl, 'http://localhost:5059'),
  email: envOrFile('EMAIL', authConfig.email),
  password: envOrFile('PASSWORD', authConfig.password),
  adminEmail: envOrFile('ADMIN_EMAIL', authConfig.adminEmail),
  adminPassword: envOrFile('ADMIN_PASSWORD', authConfig.adminPassword),
  timeout: envOrFile('HTTP_TIMEOUT', apiConfig.timeout, '30s'),

  fullSuite: {
    writeMode: toBool(envOrFile('WRITE_MODE', fullSuiteConfig.writeMode, false), false),
    vus: toNumber(envOrFile('VUS', fullSuiteConfig.vus, 10), 10),
    duration: envOrFile('DURATION', fullSuiteConfig.duration, '3m'),
  },

  importTest: {
    file: envOrFile('IMPORT_FILE', importConfig.file, '../data/user-snapshot.json'),
    replaceExisting: toBool(envOrFile('REPLACE_EXISTING', importConfig.replaceExisting, true), true),
    vus: toNumber(envOrFile('IMPORT_VUS', importConfig.vus, 2), 2),
    duration: envOrFile('IMPORT_DURATION', importConfig.duration, '2m'),
  },

  exportTest: {
    vus: toNumber(envOrFile('EXPORT_VUS', exportConfig.vus, 5), 5),
    duration: envOrFile('EXPORT_DURATION', exportConfig.duration, '2m'),
  },
};

export function requireAuthEnv() {
  if (!config.email || !config.password) {
    throw new Error(
      `Missing auth credentials. Configure EMAIL/PASSWORD env vars or set auth.email/auth.password in ${configPath}.`
    );
  }
}
