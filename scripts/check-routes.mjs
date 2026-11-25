// Smoke check of the routes of a RUNNING gateway against the route catalogue in appsettings.example.json.
//   node scripts/check-routes.mjs <gateway-base-url> [path-to-appsettings.example.json]
// Why: the real appsettings.json is not in git, so nothing proves that a shared environment has the routes the catalogue lists.
// Each route is called on its path prefix (catch-all part removed), then one segment below it. Reading the answer:
//   401 / 403 / 405 / 400 / 2xx   the route exists and the service behind it answered              -> OK
//   404                           no such route, or a service that answers 404 to everything it was asked -> CHECK (look at it by hand;
//                                 a black-box probe cannot tell a missing route from an upstream 404, so CHECK is never a proof of absence)
//                                 With GATEWAY_CHECK_TOKEN set (any valid user token, never printed) a 404 is asked again with the token and
//                                 compared with a path that is certainly not routed: a different answer proves the route exists.
//   502 / 503 / 504               the route exists but the service behind it is down or unreachable    -> DOWN
// Exit code 1 when any route is CHECK or DOWN, so a deployment script can stop. It sends no credential and changes no data (GET only).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const base = (process.argv[2] ?? '').replace(/\/+$/, '');
const file = process.argv[3] ?? path.join(here, '..', 'Gateway', 'appsettings.example.json');
if (!/^https?:\/\//.test(base)) {
  console.error('usage: node scripts/check-routes.mjs <gateway-base-url> [appsettings.example.json]');
  process.exit(2);
}

const token = process.env.GATEWAY_CHECK_TOKEN ?? '';
const call = async (url, withToken) => {
  try {
    const res = await fetch(url, { method: 'GET', headers: { Accept: 'application/json', ...(withToken && token ? { Authorization: `Bearer ${token}` } : {}) }, signal: AbortSignal.timeout(15000) });
    return res.status;
  } catch {
    return 0;
  }
};
const baseline = token ? await call(base + '/api/v1/__no_such_route__', true) : 404;

const routes = JSON.parse(fs.readFileSync(file, 'utf8')).ReverseProxy?.Routes ?? {};
const rows = [];
for (const [name, route] of Object.entries(routes)) {
  const template = route?.Match?.Path;
  if (!template) continue;
  // The catch-all and any {parameter} become a harmless probe value; the prefix is what proves the route exists.
  const probe = template.replace(/\/?\{\*\*[^}]*\}/g, '').replace(/\{[^}]+\}/g, 'probe') || '/';
  // The bare prefix first, then one segment below it (a catch-all route matches that for sure), without and then with the token.
  let status = await call(base + probe, false);
  const candidates = [[base + probe.replace(/\/$/, '') + '/probe', false], [base + probe, true], [base + probe.replace(/\/$/, '') + '/probe', true]];
  for (const [url, withToken] of candidates) {
    if (status !== 404) break;
    if (withToken && !token) continue;
    const answer = await call(url, withToken);
    if (answer !== 0 && (!withToken ? answer !== 404 : answer !== baseline)) status = answer;
  }
  const verdict = status === 0 || status === 502 || status === 503 || status === 504 ? 'DOWN' : status === 404 ? 'CHECK' : 'OK';
  rows.push({ route: name, probe, status: status || 'no answer', verdict });
}

console.table(rows);
const bad = rows.filter((r) => r.verdict !== 'OK');
console.log(`${rows.length} routes, ${rows.length - bad.length} OK, ${bad.filter((r) => r.verdict === 'CHECK').length} CHECK, ${bad.filter((r) => r.verdict === 'DOWN').length} DOWN`);
if (bad.length) {
  console.log('To look at: ' + bad.map((r) => `${r.route} (${r.status})`).join(', '));
}
process.exit(bad.length ? 1 : 0);
