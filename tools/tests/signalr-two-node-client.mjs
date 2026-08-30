#!/usr/bin/env node
// İki API node'u arasında authenticated SignalR Redis backplane kabul istemcisi.
// Kullanıcı adı ve parola yalnız stdin'den iki ayrı satır olarak alınır.

const [urlA = "http://127.0.0.1:25101", urlB = "http://127.0.0.1:25102"] = process.argv.slice(2);
if (!/^http:\/\/127\.0\.0\.1:\d{4,5}$/.test(urlA) ||
    !/^http:\/\/127\.0\.0\.1:\d{4,5}$/.test(urlB)) {
  throw new Error("Yalnız loopback acceptance URL'leri kabul edilir.");
}

const input = await new Promise((resolve, reject) => {
  let value = "";
  process.stdin.setEncoding("utf8");
  process.stdin.on("data", chunk => value += chunk);
  process.stdin.on("end", () => resolve(value));
  process.stdin.on("error", reject);
});
const [username, password] = input.replace(/\r/g, "").split("\n");
if (!username || !password) throw new Error("SignalR test kimliği stdin'de eksik.");

const login = await fetch(`${urlA}/api/auth/login`, {
  method: "POST",
  headers: { "content-type": "application/json" },
  body: JSON.stringify({ username, password }),
  signal: AbortSignal.timeout(15_000)
});
const loginBody = await login.json().catch(() => ({}));
if (!login.ok) {
  const reason = typeof loginBody?.error === "string" ? loginBody.error.slice(0, 160) : "ayrıntı yok";
  throw new Error(`Test kullanıcısı login başarısız: HTTP ${login.status} (${reason})`);
}
const token = loginBody?.data?.accessToken;
if (!token) throw new Error("Login cevabında accessToken bulunamadı.");

const separator = "\u001e";
async function connect(baseUrl, onInvocation) {
  const negotiate = await fetch(`${baseUrl}/hubs/dashboard/negotiate?negotiateVersion=1`, {
    method: "POST",
    headers: { authorization: `Bearer ${token}` },
    signal: AbortSignal.timeout(15_000)
  });
  if (!negotiate.ok) throw new Error(`SignalR negotiate başarısız: HTTP ${negotiate.status}`);
  const negotiation = await negotiate.json();
  if (!negotiation.connectionToken) throw new Error("SignalR connectionToken alınamadı.");

  const wsUrl = new URL(baseUrl.replace(/^http:/, "ws:") + "/hubs/dashboard");
  wsUrl.searchParams.set("id", negotiation.connectionToken);
  wsUrl.searchParams.set("access_token", token);
  const socket = new WebSocket(wsUrl);
  let buffer = "";
  await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error("WebSocket açılma zaman aşımı.")), 15_000);
    socket.addEventListener("open", () => {
      socket.send(JSON.stringify({ protocol: "json", version: 1 }) + separator);
    });
    socket.addEventListener("message", event => {
      buffer += String(event.data);
      const frames = buffer.split(separator);
      buffer = frames.pop() ?? "";
      for (const frame of frames) {
        if (!frame) continue;
        const message = JSON.parse(frame);
        if (message.error) reject(new Error(`SignalR handshake hatası: ${message.error}`));
        else if (message.type === undefined) {
          clearTimeout(timer);
          resolve();
        } else if (message.type === 1) onInvocation(message);
      }
    });
    socket.addEventListener("error", () => reject(new Error("WebSocket bağlantı hatası.")));
  });
  return socket;
}

const receivedAtB = [];
const socketA = await connect(urlA, () => {});
const socketB = await connect(urlB, message => {
  if (message.target === "MetricsUpdated") {
    const collectedAt = message.arguments?.[0]?.collectedAt;
    if (collectedAt && !receivedAtB.includes(collectedAt)) receivedAtB.push(collectedAt);
  }
});

try {
  const deadline = Date.now() + 20_000;
  while (receivedAtB.length < 2 && Date.now() < deadline) {
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  if (receivedAtB.length < 2) {
    throw new Error(`API-B yalnız ${receivedAtB.length} benzersiz MetricsUpdated aldı; A→B backplane kanıtlanamadı.`);
  }
  console.log("signalr-two-node: authenticated-a-and-b-connected");
  console.log("signalr-two-node: api-b-received-local-and-cross-node-events");
  console.log("signalr-two-node-client: OK");
} finally {
  socketA.close();
  socketB.close();
}
