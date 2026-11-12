// AutoCheck SRS 1.1 / RS-08 / PA-06d, PA-09a,c, PA-10b. Run profiles separately.
import http from 'k6/http';
import { check, sleep } from 'k6';
import { SharedArray } from 'k6/data';
import exec from 'k6/execution';
import { Trend, Rate } from 'k6/metrics';

const profile = __ENV.PROFILE || 'history';
if (!['history', 'pdf', 'assistant', 'contingency'].includes(profile)) throw new Error('Unknown PROFILE');
if (!__ENV.BASE_URL || !__ENV.TEST_DATA || !__ENV.NETWORK_EVIDENCE) throw new Error('BASE_URL, TEST_DATA and NETWORK_EVIDENCE are required');
const evidence = JSON.parse(open(__ENV.NETWORK_EVIDENCE));
if (evidence.perClient !== true || evidence.downloadMbps !== 1.6 || evidence.uploadKbps !== 750 || evidence.rttMs !== 150 || evidence.packetLossPercent !== 0 || !evidence.method || !evidence.evidenceReference)
  throw new Error('RS-08 requires individual network shaping and retained evidence');
const records = new SharedArray('validation records', () => JSON.parse(open(__ENV.TEST_DATA)).records);
if ((profile === 'history' && records.length < 2) || (profile !== 'history' && records.length < 20)) throw new Error('Insufficient distinct test records');
if (profile === 'pdf' && new Set(records.slice(0, 20).map(x => x.inspeccionId)).size !== 20) throw new Error('PDF validation requires 20 distinct approved inspections');
const duration = new Trend('acceptance_duration', true);
const failures = new Rate('acceptance_failures');
export const options = {
  scenarios: profile === 'history'
    ? { history: { executor: 'ramping-vus', startVUs: 0, stages: [{ duration: '60s', target: 100 }, { duration: '300s', target: 100 }], gracefulRampDown: '0s' } }
    : { separate: { executor: 'shared-iterations', vus: 1, iterations: 20, maxDuration: '10m' } },
  thresholds: profile === 'history'
    ? { 'acceptance_duration{phase:sustained}': ['p(95)<3000'], 'acceptance_failures{phase:sustained}': ['rate<0.01'] }
    : { acceptance_duration: [profile === 'contingency' ? 'max<3000' : 'max<10000'], acceptance_failures: ['rate==0'] },
};
function bytes(text) {
  let size = 0;
  for (let i = 0; i < text.length; i++) {
    const c = text.charCodeAt(i);
    if (c < 128) size++;
    else if (c < 2048) size += 2;
    else if (c >= 0xd800 && c <= 0xdbff) { size += 4; i++; }
    else size += 3;
  }
  return size;
}
export default function () {
  const record = records[(profile === 'history' ? exec.vu.idInTest : exec.scenario.iterationInTest) % records.length];
  const phase = profile !== 'history' || Date.now() - exec.scenario.startTime >= 60000 ? 'sustained' : 'ramp';
  const params = { headers: { Authorization: `Bearer ${record.token}`, 'Content-Type': 'application/json' }, timeout: '10s', tags: { phase } };
  const started = Date.now();
  let response;
  if (profile === 'history') response = http.get(`${__ENV.BASE_URL}/api/vehiculos/${record.vehiculoId}/historial?tamano=20`, params);
  else if (profile === 'pdf') response = http.post(`${__ENV.BASE_URL}/api/inspecciones/${record.inspeccionId}/certificado`, '{}', { ...params, headers: { ...params.headers, 'Idempotency-Key': `pa06-${record.inspeccionId}` } });
  else response = http.post(`${__ENV.BASE_URL}/api/asistencia`, JSON.stringify({ consulta: record.consulta }), params);
  let valid = profile === 'contingency' ? response.status === 503 : response.status >= 200 && response.status < 300;
  if (profile === 'pdf' && valid) {
    const certificate = response.json();
    // Emission may be asynchronous; total budget includes all polling and final download.
    while (certificate.estado !== 'EMITIDO' && Date.now() - started < 9000) {
      sleep(0.1);
      response = http.get(`${__ENV.BASE_URL}/api/certificados/${certificate.id}`, params);
      if (response.status !== 200) { valid = false; break; }
      certificate.estado = response.json().estado;
    }
    if (valid) { response = http.get(`${__ENV.BASE_URL}/api/certificados/${certificate.id}/pdf`, { ...params, responseType: 'binary' }); valid = response.status === 200; }
  }
  if (profile === 'history' && valid) valid = bytes(response.body) <= 100 * 1024 && response.json().items.length <= 20;
  if (profile === 'assistant' || profile === 'contingency') {
    try {
      const body = response.json();
      valid = valid && Object.keys(body).sort().join(',') === 'categoria,codigo,respuesta' && typeof body.respuesta === 'string' && body.respuesta.length > 0;
      if (profile === 'contingency') valid = valid && body.codigo === 'SERVICIO_NO_DISPONIBLE';
      else valid = valid && body.codigo !== 'SERVICIO_NO_DISPONIBLE';
    } catch (_) { valid = false; }
  }
  duration.add(Date.now() - started, { phase }); failures.add(!valid, { phase }); check(response, { 'PA contract succeeds': () => valid });
  if (profile === 'history') sleep(1);
}
