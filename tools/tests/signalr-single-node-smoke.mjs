#!/usr/bin/env node
// VM kaybı sonrasında kalan API'nin login + authenticated SignalR smoke testi.
const [baseUrl = "http://127.0.0.1:35102"] = process.argv.slice(2);
if (!/^http:\/\/127\.0\.0\.1:\d{4,5}$/.test(baseUrl)) throw new Error("Yalnız loopback URL kabul edilir.");
let input = "";
for await (const chunk of process.stdin) input += chunk;
const [username, password] = input.replace(/\r/g, "").split("\n");
if (!username || !password) throw new Error("Test kimliği stdin'de eksik.");

const login = await fetch(`${baseUrl}/api/auth/login`, {
  method: "POST", headers: { "content-type": "application/json" },
  body: JSON.stringify({ username, password }), signal: AbortSignal.timeout(15_000)
});
if (!login.ok) throw new Error(`VM-loss login başarısız: HTTP ${login.status}`);
const token = (await login.json())?.data?.accessToken;
if (!token) throw new Error("Access token alınamadı.");
const negotiate = await fetch(`${baseUrl}/hubs/dashboard/negotiate?negotiateVersion=1`, {
  method: "POST", headers: { authorization: `Bearer ${token}` }, signal: AbortSignal.timeout(15_000)
});
if (!negotiate.ok) throw new Error(`Negotiate başarısız: HTTP ${negotiate.status}`);
const connectionToken = (await negotiate.json()).connectionToken;
const wsUrl = new URL(baseUrl.replace(/^http:/, "ws:") + "/hubs/dashboard");
wsUrl.searchParams.set("id", connectionToken);
wsUrl.searchParams.set("access_token", token);
const socket = new WebSocket(wsUrl);
const separator = "\u001e";
let buffer = "";
let metricReceived = false;
await new Promise((resolve, reject) => {
  const timer = setTimeout(() => reject(new Error("Kalan node SignalR metriği zaman aşımı.")), 15_000);
  socket.addEventListener("open", () => socket.send(JSON.stringify({ protocol: "json", version: 1 }) + separator));
  socket.addEventListener("message", event => {
    buffer += String(event.data);
    const frames = buffer.split(separator); buffer = frames.pop() ?? "";
    for (const frame of frames) {
      if (!frame) continue;
      const message = JSON.parse(frame);
      if (message.error) reject(new Error(message.error));
      if (message.type === 1 && message.target === "MetricsUpdated") {
        metricReceived = true; clearTimeout(timer); resolve();
      }
    }
  });
  socket.addEventListener("error", () => reject(new Error("WebSocket bağlantı hatası.")));
});
socket.close();
if (!metricReceived) throw new Error("MetricsUpdated alınamadı.");
console.log("signalr-single-node: login-after-peer-vm-loss-ok");
console.log("signalr-single-node: authenticated-metric-after-peer-vm-loss-ok");
console.log("signalr-single-node-smoke: OK");
