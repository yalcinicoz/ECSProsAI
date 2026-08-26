#!/usr/bin/env node
// Hafif, bağımlılıksız HTTP yük testi aracı.
// Kullanım: node loadtest.js <url> [--concurrency N] [--duration S] [--timeout MS]
// Yalnızca GET istekleri; keep-alive bağlantı havuzu; p50/p90/p95/p99 hesaplar.
'use strict';

const http = require('http');
const https = require('https');

function parseArgs(argv) {
  const args = { concurrency: 10, duration: 20, timeout: 30000, url: null };
  const pos = [];
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--concurrency') args.concurrency = parseInt(argv[++i], 10);
    else if (a === '--duration') args.duration = parseInt(argv[++i], 10);
    else if (a === '--timeout') args.timeout = parseInt(argv[++i], 10);
    else if (a.startsWith('http')) args.url = a;
    else pos.push(a);
  }
  if (!args.url && pos.length) args.url = pos[0];
  return args;
}

function percentile(sorted, p) {
  if (!sorted.length) return 0;
  const idx = Math.ceil((p / 100) * sorted.length) - 1;
  return sorted[Math.max(0, Math.min(idx, sorted.length - 1))];
}

function fmt(n, digits = 1) {
  return Number(n).toFixed(digits);
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  if (!args.url) {
    console.error('Kullanım: node loadtest.js <url> [--concurrency N] [--duration S] [--timeout MS]');
    process.exit(2);
  }

  const u = new URL(args.url);
  const mod = u.protocol === 'https:' ? https : http;
  const agent = new mod.Agent({ keepAlive: true, maxSockets: args.concurrency, maxFreeSockets: args.concurrency });

  const deadline = Date.now() + args.duration * 1000;
  const latencies = [];
  let ok = 0, err = 0;
  const statuses = {};
  let inFlight = 0;

  function req(workerId) {
    if (Date.now() >= deadline) return;
    const start = process.hrtime.bigint();
    inFlight++;
    const r = mod.request(u, {
      agent,
      method: 'GET',
      headers: { 'User-Agent': 'ecspros-loadtest' },
      timeout: args.timeout,
    }, (res) => {
      // gövdeyi tüket (bağlantıyı havuza geri ver)
      res.resume();
      res.on('end', () => {
        inFlight--;
        const ms = Number(process.hrtime.bigint() - start) / 1e6;
        latencies.push(ms);
        ok++;
        statuses[res.statusCode] = (statuses[res.statusCode] || 0) + 1;
        req(workerId);
      });
    });
    r.on('error', () => {
      inFlight--;
      err++;
      req(workerId);
    });
    r.on('timeout', () => { r.destroy(); });
    r.end();
  }

  console.error(`# Yük testi: ${args.url}`);
  console.error(`# Concurrency=${args.concurrency}  Duration=${args.duration}s  Timeout=${args.timeout}ms`);
  const t0 = Date.now();
  for (let i = 0; i < args.concurrency; i++) req(i);

  const timer = setInterval(() => {
    if (Date.now() >= deadline) {
      clearInterval(timer);
      // son uçuştaki isteklerin bitmesini bekle (maks. timeout)
      const waitUntil = Date.now() + args.timeout;
      const drain = setInterval(() => {
        if (inFlight <= 0 || Date.now() >= waitUntil) {
          clearInterval(drain);
          finish();
        }
      }, 50);
    }
  }, 100);

  function finish() {
    const elapsed = (Date.now() - t0) / 1000;
    const total = ok + err;
    latencies.sort((a, b) => a - b);
    const out = {
      url: args.url,
      concurrency: args.concurrency,
      duration_plan: args.duration,
      elapsed_s: fmt(elapsed, 2),
      requests_total: total,
      requests_ok: ok,
      requests_err: err,
      error_rate_pct: total ? fmt((err / total) * 100, 2) : '0.0',
      rps: total ? fmt(total / elapsed, 1) : '0.0',
      latency_ms: {
        min: fmt(latencies[0] ?? 0),
        p50: fmt(percentile(latencies, 50)),
        p90: fmt(percentile(latencies, 90)),
        p95: fmt(percentile(latencies, 95)),
        p99: fmt(percentile(latencies, 99)),
        max: fmt(latencies[latencies.length - 1] ?? 0),
        mean: latencies.length ? fmt(latencies.reduce((a, b) => a + b, 0) / latencies.length) : '0',
      },
      status_codes: statuses,
    };
    console.log(JSON.stringify(out, null, 2));
    process.exit(0);
  }
}

main();
