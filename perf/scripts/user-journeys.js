// ============================================================================
// Teste de carga — JORNADAS DE USUÁRIO LOGADO (leitura + escrita real)
// ----------------------------------------------------------------------------
// Simula usuários autenticados usando o app como gente de verdade: navegam no
// dashboard (leituras) e executam fluxos completos de escrita (criar/editar/
// excluir) — contas, transferências, categorias, cartões, planos parcelados +
// pagamento, metas + contribuição, investimentos + movimento. Cada jornada de
// escrita FAZ LIMPEZA (cria e apaga no mesmo ciclo), então não acumula lixo.
//
// AUTENTICAÇÃO (importante): a API usa cookie httpOnly (access_token) + defesa
// antiforgery (X-XSRF-TOKEN obrigatório em POST/PUT/DELETE). O rate-limit de
// login é POR IP; como toda a carga sai de um IP só, os VUs NÃO podem logar
// individualmente. Por isso o setup() loga as 3 contas UMA vez e injeta os
// cookies no jar de cada VU. O header X-XSRF-TOKEN é sempre lido do jar (casa
// com o cookie atual, sobrevive a rotação).
//
// USO:
//   PASSWORD=... k6 run -e BASE_URL=http://35.174.50.187:5055 -e STAGE=load \
//     InvestindoEmNegociosApi/perf/scripts/user-journeys.js
//
//   STAGE = smoke | load | stress   (default: load)
//   Contas: sobrescreva com -e ADV_EMAIL / -e INT_EMAIL / -e BASIC_EMAIL.
// ============================================================================

import http from 'k6/http';
import { check, group, sleep, fail } from 'k6';
import { Counter } from 'k6/metrics';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';

// Pool de usuários dedicados (registrados por perf/scripts/register_pool ...), 1 por
// arquivo JSON [{email, access, xsrf}]. Em modo pool cada VU usa um usuário próprio
// (Basic) => escrita sem contenção, permitindo achar o teto REAL do servidor.
const POOL_FILE = __ENV.POOL_FILE || '';
const POOL = POOL_FILE ? JSON.parse(open(POOL_FILE)) : null;

// Endpoints instrumentados — usados para gerar sub-métricas por endpoint (ranking
// de latência). Cada um vira um threshold always-pass p/ o k6 computar/exibir.
const ENDPOINTS = [
  'auth_login', 'profile_get', 'preferences_get', 'lookups_payment_methods', 'lookups_card_brands',
  'categories_list', 'accounts_list', 'cards_list', 'plans_list', 'installments_list', 'goals_list',
  'notifications_list', 'income_summary', 'investments_positions', 'investments_allocation_target',
  'account_create', 'account_transfer', 'account_balance', 'account_update', 'account_delete',
  'category_create', 'category_update', 'category_status', 'category_delete',
  'card_create', 'card_update', 'card_delete', 'plan_create', 'installments_by_plan', 'installment_pay',
  'accounts_for_pay', 'plan_delete', 'goal_create', 'goal_contribute', 'goal_progress', 'goal_delete',
  'investment_create', 'investment_movement', 'investment_get', 'investment_delete',
];

const BASE = __ENV.BASE_URL || 'http://35.174.50.187:5055';
const PASSWORD = __ENV.PASSWORD;
const ACCOUNTS = {
  advanced: __ENV.ADV_EMAIL || 'auditoria.advanced.1783096587@teste.com',
  intermediate: __ENV.INT_EMAIL || 'auditoria.intermediate.1783096587@teste.com',
  basic: __ENV.BASIC_EMAIL || 'auditoria.basic.1783096587@teste.com',
};
const STAGE = (__ENV.STAGE || 'load').toLowerCase();
// Timeout curto: uma requisição lenta falha rápido em vez de segurar o VU por
// minutos (foi o que fez um teste anterior "vazar" muito além do agendado).
const TIMEOUT = __ENV.HTTP_TIMEOUT || '10s';

// ---- métricas de segurança/qualidade (sinais que importam) ----
const serverErrors = new Counter('server_errors'); // 5xx: bug/queda
const authErrors = new Counter('auth_errors');     // 401/403: auth quebrada

// ---- perfis de carga ----
const STAGES = {
  smoke: [{ duration: '20s', target: 1 }],
  quick: [{ duration: '3s', target: 4 }, { duration: '30s', target: 4 }],
  // 80 usuários: rampa até 80, sustenta 3min, desce.
  u80: [
    { duration: '1m', target: 80 },
    { duration: '3m', target: 80 },
    { duration: '30s', target: 0 },
  ],
  // Carga combinada (rodar junto com navegadores reais/Faro): 50 VUs sustentados.
  combo: [
    { duration: '30s', target: 50 },
    { duration: '3m30s', target: 50 },
    { duration: '20s', target: 0 },
  ],
  // Até o limite: escada 400 -> 800 -> 1200 VUs (acha onde começa a quebrar).
  limit: [
    { duration: '45s', target: 400 },
    { duration: '1m', target: 400 },
    { duration: '45s', target: 800 },
    { duration: '1m', target: 800 },
    { duration: '45s', target: 1200 },
    { duration: '1m', target: 1200 },
    { duration: '30s', target: 0 },
  ],
  // Perfis curtos de propósito: os tokens capturados no setup vivem ~15 min;
  // manter cada run bem abaixo disso evita falso "token expirado".
  load: [
    { duration: '45s', target: 15 },
    { duration: '2m', target: 15 },
    { duration: '30s', target: 0 },
  ],
  stress: [
    { duration: '45s', target: 15 },
    { duration: '1m', target: 15 },
    { duration: '30s', target: 25 },
    { duration: '1m', target: 25 },
    { duration: '30s', target: 35 },
    { duration: '1m', target: 35 },
    { duration: '30s', target: 0 },
  ],
  // Busca do teto da VPS: escada 50 -> 80 -> 110 VUs. Curto o bastante p/ o token.
  peak: [
    { duration: '45s', target: 50 },
    { duration: '1m', target: 50 },
    { duration: '45s', target: 80 },
    { duration: '1m', target: 80 },
    { duration: '45s', target: 110 },
    { duration: '1m', target: 110 },
    { duration: '30s', target: 0 },
  ],
  // Ponto de ruptura: 400 -> 600 -> 800 VUs (procura onde requests estouram o timeout).
  breakpoint: [
    { duration: '30s', target: 400 },
    { duration: '45s', target: 400 },
    { duration: '30s', target: 600 },
    { duration: '45s', target: 600 },
    { duration: '30s', target: 800 },
    { duration: '1m', target: 800 },
    { duration: '20s', target: 0 },
  ],
  // Teto alto (usar com POOL_FILE p/ evitar contenção): 150 -> 250 -> 400.
  ceiling: [
    { duration: '45s', target: 150 },
    { duration: '1m', target: 150 },
    { duration: '45s', target: 250 },
    { duration: '1m', target: 250 },
    { duration: '45s', target: 400 },
    { duration: '1m30s', target: 400 },
    { duration: '30s', target: 0 },
  ],
};

// Sub-métrica por endpoint (threshold always-pass só p/ o k6 computar/exibir a latência).
const endpointThresholds = {};
for (const e of ENDPOINTS) endpointThresholds[`http_req_duration{endpoint:${e}}`] = ['p(95)>=0'];

export const options = {
  scenarios: {
    journeys: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: STAGES[STAGE] || STAGES.load,
      gracefulRampDown: '15s',
      gracefulStop: '20s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.05'],
    http_req_duration: ['p(95)<1500', 'p(99)<3000'],
    'http_req_duration{kind:read}': ['p(95)<1000'],
    'http_req_duration{kind:write}': ['p(95)<2000'],
    server_errors: ['count<1'],   // qualquer 5xx reprova
    auth_errors: ['count<20'],    // tolera corrida rara; muitos = auth quebrada
    checks: ['rate>0.98'],
    ...endpointThresholds,
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

// Ranking dos endpoints mais lentos (p95) ao final.
export function handleSummary(data) {
  const rows = [];
  for (const [key, metric] of Object.entries(data.metrics)) {
    const m = key.match(/^http_req_duration\{endpoint:([^}]+)\}$/);
    if (!m || !metric.values) continue;
    rows.push({
      endpoint: m[1],
      p95: metric.values['p(95)'] ?? 0,
      p99: metric.values['p(99)'] ?? 0,
      avg: metric.values.avg ?? 0,
      max: metric.values.max ?? 0,
      count: metric.values.count ?? 0,
    });
  }
  rows.sort((a, b) => b.p95 - a.p95);
  const fmt = (n) => `${n.toFixed(1)}ms`.padStart(9);
  let table = '\n===== ENDPOINTS MAIS LENTOS (ordenado por p95) =====\n';
  table += 'endpoint'.padEnd(32) + 'p95'.padStart(9) + 'p99'.padStart(9) + 'avg'.padStart(9) + 'max'.padStart(10) + '   amostras\n';
  for (const r of rows) {
    table += r.endpoint.padEnd(32) + fmt(r.p95) + fmt(r.p99) + fmt(r.avg) + fmt(r.max).padStart(10) + '   ' + r.count + '\n';
  }
  return { stdout: textSummary(data, { indent: ' ', enableColors: false }) + '\n' + table };
}

// ============================ helpers ============================

function loginCaptureCookies(email) {
  // Login precisa de jar LIMPO: logar carregando cookie de sessão de outro
  // usuário dispara um 500 no backend (bug conhecido — ver relatório). Cada
  // conta é capturada isoladamente.
  http.cookieJar().clear(BASE);
  const res = http.post(
    `${BASE}/api/v1/auth/login`,
    JSON.stringify({ email, password: PASSWORD }),
    { headers: { 'Content-Type': 'application/json' }, tags: { endpoint: 'auth_login', kind: 'read' }, timeout: TIMEOUT }
  );
  if (res.status !== 200) {
    fail(`Login falhou para ${email}: HTTP ${res.status} ${String(res.body).slice(0, 200)}`);
  }
  // Lê os cookies (incl. httpOnly) que o k6 guardou no jar do setup.
  const jarCookies = http.cookieJar().cookiesForURL(BASE);
  const pick = (name) => (jarCookies[name] && jarCookies[name][0]) || '';
  const access = pick('access_token');
  const xsrf = pick('XSRF-TOKEN');
  if (!access || !xsrf) {
    fail(`Não consegui capturar cookies de ${email} (access=${access.length} xsrf=${xsrf.length}).`);
  }
  return { access, xsrf };
}

// Seeda o jar do VU com os cookies da sessão escolhida.
function seedSession(session) {
  const jar = http.cookieJar();
  jar.set(BASE, 'access_token', session.access, { path: '/' });
  jar.set(BASE, 'XSRF-TOKEN', session.xsrf, { path: '/' });
}

// Header de escrita: lê o XSRF atual do jar (casa sempre com o cookie).
function writeHeaders() {
  const c = http.cookieJar().cookiesForURL(BASE);
  const xsrf = (c['XSRF-TOKEN'] && c['XSRF-TOKEN'][0]) || '';
  return { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': xsrf };
}

function tally(res) {
  if (res.status >= 500) serverErrors.add(1);
  if (res.status === 401 || res.status === 403) authErrors.add(1);
  return res;
}

function get(path, endpoint, accepted = [200, 204]) {
  const res = http.get(`${BASE}${path}`, { headers: { Accept: 'application/json' }, tags: { endpoint, kind: 'read' }, timeout: TIMEOUT });
  tally(res);
  check(res, { [`${endpoint} ok`]: (r) => accepted.includes(r.status) });
  return res;
}

function post(path, body, endpoint, accepted = [200, 201, 204]) {
  const res = http.post(`${BASE}${path}`, JSON.stringify(body), { headers: writeHeaders(), tags: { endpoint, kind: 'write' }, timeout: TIMEOUT });
  tally(res);
  check(res, { [`${endpoint} ok`]: (r) => accepted.includes(r.status) });
  return res;
}

function put(path, body, endpoint, accepted = [200, 204]) {
  const res = http.put(`${BASE}${path}`, JSON.stringify(body), { headers: writeHeaders(), tags: { endpoint, kind: 'write' }, timeout: TIMEOUT });
  tally(res);
  check(res, { [`${endpoint} ok`]: (r) => accepted.includes(r.status) });
  return res;
}

function del(path, endpoint, accepted = [200, 204]) {
  const res = http.del(`${BASE}${path}`, null, { headers: writeHeaders(), tags: { endpoint, kind: 'write' }, timeout: TIMEOUT });
  tally(res);
  check(res, { [`${endpoint} ok`]: (r) => accepted.includes(r.status) });
  return res;
}

function jsonOf(res) {
  try { return res.json(); } catch { return null; }
}

// ============================ setup ============================

export function setup() {
  // Modo pool: usuários dedicados (Basic) já registrados; cada VU usa um próprio.
  if (POOL) {
    if (!POOL.length) fail('POOL_FILE vazio/inválido.');
    console.log(`setup POOL: ${POOL.length} usuários dedicados (escrita sem contenção)`);
    return { mode: 'pool' };
  }
  if (!PASSWORD) fail('Defina a senha via env PASSWORD (não fica no repositório).');
  // Intervalo entre logins: evita rajada instantânea (que já provocou 500 transitório)
  // e fica MUITO abaixo do rate-limit de login por IP (20/min).
  const advanced = loginCaptureCookies(ACCOUNTS.advanced);
  sleep(1);
  const intermediate = loginCaptureCookies(ACCOUNTS.intermediate);
  sleep(1);
  const basic = loginCaptureCookies(ACCOUNTS.basic);
  const sessions = { advanced, intermediate, basic };
  console.log(`setup ok — sessões: advanced/intermediate/basic capturadas (access token len=${sessions.advanced.access.length})`);
  return { mode: 'profiles', sessions };
}

// ============================ jornadas ============================

// Leitura pesada: o que o dashboard/telas carregam ao navegar. Role-aware — cada
// perfil só chama o que a UI dele carrega (Basic não vê receitas/investimentos),
// espelhando o gating real e evitando 403 esperados no meio das métricas.
function journeyBrowse(role) {
  group('browse', () => {
    get('/api/v1/profile', 'profile_get', [200, 204]);
    get('/api/v1/preferences', 'preferences_get');
    get('/api/v1/lookups/payment-methods', 'lookups_payment_methods');
    get('/api/v1/lookups/card-brands', 'lookups_card_brands');
    get('/api/v1/categories', 'categories_list');
    get('/api/v1/accounts', 'accounts_list');
    get('/api/v1/cards', 'cards_list');
    get('/api/v1/plans', 'plans_list');
    get('/api/v1/installments', 'installments_list');
    get('/api/v1/goals', 'goals_list');
    get('/api/v1/notifications?limit=20', 'notifications_list');
    if (role === 'intermediate' || role === 'advanced') {
      get('/api/v1/incomes/summary', 'income_summary');
    }
    if (role === 'advanced') {
      get('/api/v1/investments/positions', 'investments_positions');
      get('/api/v1/investments/allocation-target', 'investments_allocation_target');
    }
  });
}

// Conta + transferência + saldo, com limpeza.
function journeyAccounts() {
  group('accounts_transfer', () => {
    const a = jsonOf(post('/api/v1/accounts', { name: `k6-A-${uuidv4()}`, type: 'Checking', initialBalance: 500 }, 'account_create'));
    const b = jsonOf(post('/api/v1/accounts', { name: `k6-B-${uuidv4()}`, type: 'Savings', initialBalance: 0 }, 'account_create'));
    if (a && a.id && b && b.id) {
      post('/api/v1/accounts/transfers', { fromAccountId: a.id, toAccountId: b.id, amount: 100, description: 'k6' }, 'account_transfer', [200, 201]);
      get(`/api/v1/accounts/${a.id}/balance`, 'account_balance');
      put(`/api/v1/accounts/${a.id}`, { name: `k6-A2-${uuidv4()}`, type: 'Checking', initialBalance: 500 }, 'account_update');
    }
    if (a && a.id) del(`/api/v1/accounts/${a.id}`, 'account_delete', [200, 204, 400, 409]);
    if (b && b.id) del(`/api/v1/accounts/${b.id}`, 'account_delete', [200, 204, 400, 409]);
  });
}

// Categoria (Intermediate+). CRUD + status + limpeza.
function journeyCategory() {
  group('category_crud', () => {
    const c = jsonOf(post('/api/v1/categories', { name: `k6-cat-${uuidv4()}`, appliesTo: 'Expense' }, 'category_create'));
    if (c && c.id) {
      put(`/api/v1/categories/${c.id}`, { name: `k6-cat2-${uuidv4()}`, appliesTo: 'Expense' }, 'category_update');
      put(`/api/v1/categories/${c.id}/status`, { isActive: false }, 'category_status');
      del(`/api/v1/categories/${c.id}`, 'category_delete', [200, 204]);
    }
  });
}

// Cartão. CRUD + limpeza. brandId/last4 aleatórios: há índice único
// (UserId, BrandId, Last4), então valores fixos colidiriam entre iterações.
function journeyCard() {
  const rnd4 = () => String(Math.floor(1000 + Math.random() * 9000));
  const brand = () => 1 + Math.floor(Math.random() * 5);
  // Apelido único: há checagem de apelido duplicado por usuário (409). Valor fixo
  // colidiria entre iterações da mesma conta.
  const nick = () => `k6-${uuidv4()}`;
  group('card_crud', () => {
    const card = jsonOf(post('/api/v1/cards', {
      brandId: brand(), holderName: 'K6 Tester', last4: rnd4(), nickname: nick(),
      bank: 'k6bank', creditLimit: 5000, statementCloseDay: 10, dueDay: 20,
    }, 'card_create'));
    if (card && card.id) {
      put(`/api/v1/cards/${card.id}`, {
        brandId: brand(), holderName: 'K6 Tester 2', last4: rnd4(), nickname: nick(),
        bank: 'k6bank', creditLimit: 8000, statementCloseDay: 12, dueDay: 22,
      }, 'card_update');
      del(`/api/v1/cards/${card.id}`, 'card_delete', [200, 204]);
    }
  });
}

// Plano parcelado -> gera parcelas -> paga a 1ª -> apaga plano (cascata).
function journeyPlanInstallment() {
  group('plan_installment_pay', () => {
    const plan = jsonOf(post('/api/v1/plans', {
      type: 'Expense', title: `k6-plan-${uuidv4()}`, amount: 300,
      schedule: 'Installments', startDate: '2026-07-01', installmentsCount: 3,
    }, 'plan_create'));
    if (plan && plan.id) {
      const list = jsonOf(get(`/api/v1/installments?type=Expense`, 'installments_by_plan')) || [];
      const mine = list.filter((i) => i.planId === plan.id).sort((x, y) => x.installmentNo - y.installmentNo);
      if (mine.length > 0) {
        const first = mine[0];
        // Passa accountId explícito (como a UI faz): sem ele, contas com mais de
        // uma conta retornam 400 "Conta obrigatória".
        const accounts = jsonOf(get('/api/v1/accounts', 'accounts_for_pay')) || [];
        const accountId = accounts.length > 0 ? accounts[0].id : undefined;
        post(`/api/v1/installments/${first.id}/payments`, {
          paidAt: '2026-07-05T12:00:00Z', paidAmount: first.amount, methodId: 1, accountId,
        }, 'installment_pay', [200, 201]);
      }
      del(`/api/v1/plans/${plan.id}`, 'plan_delete', [200, 204]);
    }
  });
}

// Meta + contribuição + progresso + limpeza.
function journeyGoal() {
  group('goal_contribution', () => {
    const goal = jsonOf(post('/api/v1/goals', {
      title: `k6-goal-${uuidv4()}`, targetAmount: 1000, year: 2026, description: 'k6',
      status: 'InProgress', currentAmount: 0, expectedMonthly: 100, targetDate: null, kind: 'General',
    }, 'goal_create'));
    if (goal && goal.id) {
      post(`/api/v1/goals/${goal.id}/contributions`, { amount: 50, date: '2026-07-01', note: 'k6' }, 'goal_contribute', [200, 201]);
      get(`/api/v1/goals/${goal.id}/progress`, 'goal_progress');
      del(`/api/v1/goals/${goal.id}`, 'goal_delete', [200, 204]);
    }
  });
}

// Investimento (Advanced) + movimento + limpeza.
function journeyInvestment() {
  group('investment_movement', () => {
    const pos = jsonOf(post('/api/v1/investments/positions', {
      type: 'ACOES', asset: `K6${Math.floor(Math.random() * 1e6)}`, quantity: 10, avgPrice: 5,
      openedAt: '2026-06-01', account: 'k6', category: 'Acoes', note: null, currency: 'BRL',
    }, 'investment_create'));
    if (pos && pos.id) {
      post(`/api/v1/investments/positions/${pos.id}/movements`, { type: 'APORTE', quantity: 5, price: 6, date: '2026-07-01', note: 'k6' }, 'investment_movement', [200, 201]);
      get(`/api/v1/investments/positions/${pos.id}`, 'investment_get');
      del(`/api/v1/investments/positions/${pos.id}`, 'investment_delete', [200, 204]);
    }
  });
}

// ============================ default ============================

export default function (data) {
  // Modo pool: cada VU usa um usuário dedicado (Basic) => grava na PRÓPRIA conta,
  // sem contenção. Jornada Basic: browse + card/plan/goal.
  if (data.mode === 'pool') {
    seedSession(POOL[__VU % POOL.length]);
    if (Math.random() < 0.5) {
      journeyBrowse('basic');
      sleep(1 + Math.random());
      return;
    }
    const w = Math.random();
    if (w < 0.4) journeyCard();
    else if (w < 0.7) journeyPlanInstallment();
    else journeyGoal();
    sleep(2 + Math.random() * 2);
    return;
  }

  // Cada VU tem uma conta "casa" fixa (distribui a carga de ESCRITA entre as 3
  // contas em vez de concentrar tudo numa só — o que gerava contenção de trava).
  const roles = ['advanced', 'intermediate', 'basic'];
  const role = roles[__VU % roles.length];
  seedSession(data.sessions[role]);

  // ~50% navega (leitura), ~50% executa um fluxo de escrita.
  if (Math.random() < 0.5) {
    journeyBrowse(role);
    sleep(1 + Math.random());
    return;
  }

  // Escrita respeitando o gating REAL de cada perfil (sem 403 falso):
  //  - card / plan / goal: todos
  //  - account (+transfer) / category: intermediate e advanced
  //  - investment: só advanced
  const canAccount = role === 'advanced' || role === 'intermediate';
  const canInvestment = role === 'advanced';
  const w = Math.random();

  if (canInvestment && w < 0.2) journeyInvestment();
  else if (canAccount && w < 0.4) journeyAccounts();
  else if (canAccount && w < 0.55) journeyCategory();
  else if (w < 0.7) journeyCard();
  else if (w < 0.85) journeyPlanInstallment();
  else journeyGoal();

  sleep(2 + Math.random() * 2);
}
