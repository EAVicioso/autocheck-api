// Loopback link emulator. The virtual clients do not share bandwidth.
import http from 'node:http';
import { writeFile } from 'node:fs/promises';
import { once } from 'node:events';
import { performance } from 'node:perf_hooks';
import { setTimeout as wait } from 'node:timers/promises';

const upstream = new URL(process.argv[2]);
const evidence = process.argv[3];
if (!evidence || upstream.protocol !== 'http:' || upstream.hostname !== '127.0.0.1')
  throw new Error('This emulator requires an isolated loopback HTTP fixture.');

const upload = 750000 / 8;
const download = 1600000 / 8;
const oneWayMs = 75;
const links = new Map(Array.from({ length: 100 }, (_, n) => [n + 1, { busy: false, requests: 0 }]));
const sample = Buffer.alloc(256 * 1024);

async function send(response, status, headers, body) {
  await wait(oneWayMs);
  response.writeHead(status, { ...headers, 'content-length': body.length });
  const started = performance.now();
  for (let offset = 0; offset < body.length; offset += 4096) {
    const end = Math.min(offset + 4096, body.length);
    const remaining = started + end / download * 1000 - performance.now();
    if (remaining > 0) await wait(remaining);
    if (response.destroyed) return;
    response.write(body.subarray(offset, end));
  }
  response.end();
}

async function readBody(request) {
  const chunks = [];
  let length = 0;
  for await (const chunk of request) {
    length += chunk.length;
    if (length > 128 * 1024) throw new Error('Fixture request too large.');
    chunks.push(chunk);
  }
  return Buffer.concat(chunks, length);
}

async function forward(request, response) {
  const link = links.get(Number(request.headers['x-rs08-client']));
  if (!link || link.busy) {
    response.writeHead(409).end();
    return;
  }
  link.busy = true;
  try {
    const body = await readBody(request);
    await wait(oneWayMs + body.length / upload * 1000);
    if (request.url?.startsWith('/_calibrate/')) {
      await send(response, 200, {}, request.url.endsWith('/download') ? sample : Buffer.alloc(0));
      return;
    }
    if (!request.url?.startsWith('/api/')) {
      response.writeHead(404).end();
      return;
    }
    link.requests++;
    const headers = { 'accept-encoding': 'identity' };
    for (const name of ['authorization', 'content-type', 'idempotency-key']) {
      if (request.headers[name]) headers[name] = request.headers[name];
    }
    const result = await fetch(new URL(request.url, upstream), {
      method: request.method, headers, body: body.length ? body : undefined,
      redirect: 'manual', signal: AbortSignal.timeout(10000),
    });
    const type = result.headers.get('content-type');
    await send(response, result.status, type ? { 'content-type': type } : {}, Buffer.from(await result.arrayBuffer()));
  } finally {
    link.busy = false;
  }
}

const server = http.createServer((request, response) => {
  if (request.url === '/_rs08/stats') {
    response.writeHead(200, { 'content-type': 'application/json' });
    response.end(JSON.stringify([...links].map(([client, link]) => ({ client, requests: link.requests }))));
    return;
  }
  forward(request, response).catch(() => {
    if (response.headersSent) response.destroy();
    else response.writeHead(502).end();
  });
});
server.listen(0, '127.0.0.1');
await once(server, 'listening');
const url = 'http://127.0.0.1:' + server.address().port;

async function measure(client, name, body) {
  const start = performance.now();
  const result = await fetch(url + '/_calibrate/' + name, {
    method: body ? 'POST' : 'GET', headers: { 'x-rs08-client': String(client) }, body,
    signal: AbortSignal.timeout(10000),
  });
  if (!result.ok) throw new Error('Per-client calibration failed.');
  await result.arrayBuffer();
  return performance.now() - start;
}

async function calibrate(client) {
  const rttMs = await measure(client, 'small');
  const down = await measure(client, 'download');
  const up = await measure(client, 'upload', sample.subarray(0, 64 * 1024));
  return { client, rttMs, downloadMbps: sample.length * 8 / ((down - rttMs) * 1000),
    uploadKbps: 64 * 1024 * 8 / (up - rttMs) };
}

const measurements = [];
for (let start = 1; start <= 100; start += 10) {
  const ids = Array.from({ length: 10 }, (_, i) => start + i);
  await Promise.all(ids.map(id => measure(id, 'small')));
  measurements.push(...await Promise.all(ids.map(calibrate)));
}
if (measurements.some(m => m.rttMs < 145 || m.rttMs > 200 ||
    m.downloadMbps < 1.45 || m.downloadMbps > 1.75 || m.uploadKbps < 675 || m.uploadKbps > 825))
  throw new Error('Measured links fall outside calibration tolerance.');

await writeFile(evidence + '.pending', JSON.stringify({
  perClient: true, downloadMbps: 1.6, uploadKbps: 750, rttMs: 150, packetLossPercent: 0,
  method: 'Loopback HTTP proxy with independent paced upload/download and 75 ms each direction',
  evidenceReference: 'Per-client round-trip and payload calibration included below',
  url, measurements,
}, null, 2));
const { rename } = await import('node:fs/promises');
await rename(evidence + '.pending', evidence);
