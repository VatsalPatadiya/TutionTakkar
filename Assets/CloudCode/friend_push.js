// Tuition Takkar · friend request / accepted request as a phone notification.
// Same secrets and tokens as invite_push: Android via FCM ("fcmToken"), iPhone via Apple ("apnsToken", APNS_KEY).
// When the game is open, the phone hands the message to the game, which shows its own pop-up.
const axios = require("axios-1.6");
const { DataApi } = require("@unity-services/cloud-save-1.4");

let cached = { token: "", until: 0 };
const sent = {}; // spam guard per instance: sender+target+kind → last time

module.exports = async ({ params, context, logger, secretManager }) => {
  const to = String(params.to || "");
  const kind = String(params.kind || "");
  const from = String(params.from || "A classmate").substring(0, 20);
  if (!to || (kind !== "request" && kind !== "accepted")) return { sent: false, error: "bad request" };
  if (to === context.playerId) return { sent: false, error: "that's you" };

  const key = context.playerId + ">" + to + ":" + kind;
  const now = Date.now();
  if (sent[key] && now - sent[key] < 60000) return { sent: false, error: "already sent" };

  // Only for a real request / friendship between the two (best effort: skipped if the Friends API can't be reached).
  try {
    const { RelationshipsApi } = require("@unity-services/friends-1.0");
    const rel = new RelationshipsApi(context);
    const res = await rel.getRelationships(100, 0, false, false, [kind === "request" ? "FRIEND_REQUEST" : "FRIEND"]);
    const list = res.data || [];
    const ok = list.some((r) => (r.members || []).some((m) => m.id === to) || (r.member && r.member.id === to));
    if (list.length > 0 && !ok) return { sent: false, error: "no such request" };
  } catch (e) {
    logger.warning("friend_push: relationship check skipped: " + e.message);
  }

  const api = new DataApi({ accessToken: context.serviceToken });
  let fcm = "", apns = "";
  try {
    const items = await api.getItems(context.projectId, to, ["fcmToken", "apnsToken"]);
    for (const r of (items.data.results || [])) {
      if (r.key === "fcmToken" && r.value) fcm = String(r.value);
      if (r.key === "apnsToken" && r.value) apns = String(r.value);
    }
  } catch (e) {
    logger.warning("friend_push: token read failed " + (e.response ? e.response.status : e.message));
  }
  if (!fcm && !apns) return { sent: false, error: "no phone token" };

  const title = kind === "request" ? `${from} wants to be friends ✏️` : `${from} accepted your friend request 🎉`;
  const body = kind === "request" ? "Open Tuition Takkar to accept." : "You're friends now. Save them a seat in your next room!";
  const link = kind === "request" ? "tuitiontakkar://friends/requests" : "tuitiontakkar://friends";
  const data = { link, channel: "invites", kind: "friend_" + kind, fromName: from, fromId: context.playerId, title, body };

  let ok = false;
  const errors = [];
  if (apns) {
    const r = await sendApns(secretManager, logger, apns, title, body, data, 0);
    if (r.ok) ok = true; else errors.push(r.error);
  }
  if (fcm) {
    try {
      const account = JSON.parse((await secretManager.getSecret("FCM_SERVICE_ACCOUNT")).value);
      const access = await googleToken(account);
      await axios.post(`https://fcm.googleapis.com/v1/projects/${account.project_id}/messages:send`, {
        message: {
          token: fcm,
          notification: { title, body },
          data,
          android: { priority: "HIGH", notification: { channel_id: "invites" } },
        },
      }, { headers: { Authorization: "Bearer " + access, "Content-Type": "application/json" } });
      ok = true;
    } catch (e) {
      const why = describe(e);
      logger.warning("friend_push: FCM send failed " + why);
      errors.push("FCM " + why);
    }
  }
  if (!ok) return { sent: false, error: "send failed: " + errors.join("; ").substring(0, 200) };
  sent[key] = now;
  return { sent: true };
};

module.exports.params = {
  to: { type: "String", required: true },
  kind: { type: "String", required: true },
  from: { type: "String", required: false },
};

// ---------------------------------------------------------------------------
// Google OAuth access token from a service account (JWT signed with RS256).
// Uses Node's crypto when the runtime has it, otherwise a small pure-JS RS256.
// ---------------------------------------------------------------------------

async function googleToken(acc) {
  if (cached.token && Date.now() < cached.until) return cached.token;
  const iat = Math.floor(Date.now() / 1000);
  const header = b64url(JSON.stringify({ alg: "RS256", typ: "JWT" }));
  const claim = b64url(JSON.stringify({
    iss: acc.client_email, scope: "https://www.googleapis.com/auth/firebase.messaging",
    aud: "https://oauth2.googleapis.com/token", iat, exp: iat + 3600,
  }));
  const input = header + "." + claim;
  const sig = signRS256(input, acc.private_key);
  const jwt = input + "." + sig;
  const res = await axios.post("https://oauth2.googleapis.com/token",
    "grant_type=" + encodeURIComponent("urn:ietf:params:oauth:grant-type:jwt-bearer") + "&assertion=" + jwt,
    { headers: { "Content-Type": "application/x-www-form-urlencoded" } });
  cached = { token: res.data.access_token, until: Date.now() + (res.data.expires_in - 120) * 1000 };
  return cached.token;
}

// "404 UNREGISTERED: Requested entity was not found." from an axios error (or the plain message).
function describe(e) {
  if (e && e.response) {
    const d = e.response.data || {};
    const err = d.error || {};
    const msg = typeof err === "string" ? err + (d.error_description ? " " + d.error_description : "") : (err.status || "") + " " + (err.message || "");
    return (e.response.status + " " + msg).trim().substring(0, 200);
  }
  return String(e && e.message ? e.message : e).substring(0, 200);
}

function b64url(s) {
  const b = typeof s === "string" ? Buffer.from(s, "utf8") : Buffer.from(s);
  return b.toString("base64").replace(/=+$/, "").replace(/\+/g, "-").replace(/\//g, "_");
}

function signRS256(input, pem) {
  try {
    const crypto = require("crypto");
    const s = crypto.createSign("RSA-SHA256");
    s.update(input);
    return s.sign(pem, "base64").replace(/=+$/, "").replace(/\+/g, "-").replace(/\//g, "_");
  } catch (e) {
    return b64url(rsaSignPure(Buffer.from(input, "utf8"), pem));
  }
}

// ----- pure JS RS256 (PKCS#1 v1.5 + SHA-256) -----

function rsaSignPure(msg, pem) {
  const der = Buffer.from(pem.replace(/-----[^-]+-----/g, "").replace(/\s+/g, ""), "base64");
  const key = parseKey(der);
  const k = (key.n.toString(16).length + 1) >> 1;
  const prefix = Buffer.from("3031300d060960864801650304020105000420", "hex");
  const t = Buffer.concat([prefix, sha256(msg)]);
  const ps = Buffer.alloc(k - t.length - 3, 0xff);
  const em = Buffer.concat([Buffer.from([0, 1]), ps, Buffer.from([0]), t]);
  const m = BigInt("0x" + em.toString("hex"));
  let s;
  if (key.p && key.q && key.dp && key.dq && key.qi) {
    const m1 = modPow(m % key.p, key.dp, key.p), m2 = modPow(m % key.q, key.dq, key.q);
    let h = (key.qi * ((m1 - m2) % key.p + key.p)) % key.p;
    s = m2 + h * key.q;
  } else s = modPow(m, key.d, key.n);
  let hex = s.toString(16);
  while (hex.length < k * 2) hex = "0" + hex;
  return Buffer.from(hex, "hex");
}

function modPow(b, e, m) {
  let r = 1n; b %= m;
  while (e > 0n) { if (e & 1n) r = (r * b) % m; e >>= 1n; b = (b * b) % m; }
  return r;
}

function parseKey(der) {
  let pos = 0;
  function read() {
    const tag = der[pos++];
    let len = der[pos++];
    if (len & 0x80) { const n = len & 0x7f; len = 0; for (let i = 0; i < n; i++) len = (len << 8) | der[pos++]; }
    const start = pos; pos += len;
    return { tag, start, len, end: pos };
  }
  const int = (t) => BigInt("0x" + (der.slice(t.start, t.end).toString("hex") || "0"));
  const outer = read(); pos = outer.start;   // SEQUENCE
  read();                                     // version
  if (der[pos] === 0x30) {                    // PKCS#8: AlgorithmIdentifier, then OCTET STRING { RSAPrivateKey }
    read();
    const oct = read(); pos = oct.start;
    const seq = read(); pos = seq.start;
    read();                                   // RSAPrivateKey version
  }
  const n = int(read()), e = int(read()), d = int(read()), p = int(read()), q = int(read()), dp = int(read()), dq = int(read()), qi = int(read());
  return { n, e, d, p, q, dp, dq, qi };
}

function sha256(buf) {
  const K = [0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,
    0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,
    0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,0xd192e819,0xd6990624,0xf40e3585,0x106aa070,
    0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2];
  const H = [0x6a09e667,0xbb67ae85,0x3c6ef372,0xa54ff53a,0x510e527f,0x9b05688c,0x1f83d9ab,0x5be0cd19];
  const l = buf.length;
  const withPad = Buffer.alloc(((l + 9 + 63) >> 6) << 6);
  buf.copy(withPad); withPad[l] = 0x80;
  const bits = BigInt(l) * 8n;
  for (let i = 0; i < 8; i++) withPad[withPad.length - 1 - i] = Number((bits >> BigInt(8 * i)) & 0xffn);
  const w = new Array(64);
  const rotr = (x, n) => (x >>> n) | (x << (32 - n));
  for (let off = 0; off < withPad.length; off += 64) {
    for (let i = 0; i < 16; i++) w[i] = withPad.readUInt32BE(off + i * 4);
    for (let i = 16; i < 64; i++) {
      const s0 = rotr(w[i - 15], 7) ^ rotr(w[i - 15], 18) ^ (w[i - 15] >>> 3);
      const s1 = rotr(w[i - 2], 17) ^ rotr(w[i - 2], 19) ^ (w[i - 2] >>> 10);
      w[i] = (w[i - 16] + s0 + w[i - 7] + s1) | 0;
    }
    let [a, b, c, d, e, f, g, h] = H;
    for (let i = 0; i < 64; i++) {
      const S1 = rotr(e, 6) ^ rotr(e, 11) ^ rotr(e, 25);
      const ch = (e & f) ^ (~e & g);
      const t1 = (h + S1 + ch + K[i] + w[i]) | 0;
      const S0 = rotr(a, 2) ^ rotr(a, 13) ^ rotr(a, 22);
      const mj = (a & b) ^ (a & c) ^ (b & c);
      const t2 = (S0 + mj) | 0;
      h = g; g = f; f = e; e = (d + t1) | 0; d = c; c = b; b = a; a = (t1 + t2) | 0;
    }
    H[0] = (H[0] + a) | 0; H[1] = (H[1] + b) | 0; H[2] = (H[2] + c) | 0; H[3] = (H[3] + d) | 0;
    H[4] = (H[4] + e) | 0; H[5] = (H[5] + f) | 0; H[6] = (H[6] + g) | 0; H[7] = (H[7] + h) | 0;
  }
  const out = Buffer.alloc(32);
  H.forEach((v, i) => out.writeUInt32BE(v >>> 0, i * 4));
  return out;
}

// ---------------------------------------------------------------------------
// iPhone: Apple Push Notification service (the iPhone build has no Firebase SDK).
// Needs the Secret Manager secret APNS_KEY: the contents of the AuthKey_XXXXXXXXXX.p8 file.
// Optional secret APNS_KEY_ID if that key's ID isn't the default below.
// Route 1: straight to Apple over HTTP/2 (if the server runtime has Node's http2).
// Route 2 (no http2 here): Firebase turns the Apple token into a Firebase one and delivers it
//          (needs the same .p8 uploaded in Firebase > Project settings > Cloud Messaging > Apple app).
// Development builds (Xcode Run) get sandbox tokens, TestFlight / App Store get production ones:
// production is tried first, then sandbox.
// Cloud Code has no Node "crypto", so the ES256 signature is computed here in plain JavaScript.
// ---------------------------------------------------------------------------

const APNS = { keyId: "T38W267384", teamId: "87CTM2H52V", topic: "com.unitygame.tuitiontakkar" };
let apnsJwt = { token: "", at: 0 };
const fcmIos = {}; // Apple token + env -> Firebase token (cached per instance)

async function sendApns(secretManager, logger, device, title, body, data, ttlSeconds) {
  device = String(device || "").replace(/[^0-9a-fA-F]/g, "");
  if (device.length < 32) return { ok: false, gone: true, error: "bad iPhone token" };
  let http2 = null;
  try { http2 = require("http2"); } catch (e) { http2 = null; }
  if (!http2) return sendIosViaFirebase(secretManager, logger, device, title, body, data, ttlSeconds);

  let jwt;
  try { jwt = await apnsAuth(secretManager); }
  catch (e) {
    logger.error("apns: " + e.message);
    return { ok: false, setup: true, error: e.message };
  }
  const payload = Object.assign({}, data, { aps: { alert: { title, body }, sound: "default" } });
  const exp = ttlSeconds ? Math.floor(Date.now() / 1000) + ttlSeconds : 0;
  let r = await apnsPost(http2, "api.push.apple.com", device, jwt, payload, exp);
  if (r.status === 400 && r.reason === "BadDeviceToken") r = await apnsPost(http2, "api.sandbox.push.apple.com", device, jwt, payload, exp);
  if (r.status === 200) return { ok: true };
  if (r.status === 403) apnsJwt = { token: "", at: 0 }; // key problem: make a fresh token next time
  const why = ("APNs " + r.status + " " + r.reason).trim();
  logger.warning("apns: " + why);
  return { ok: false, gone: r.status === 410 || r.reason === "BadDeviceToken" || r.reason === "Unregistered", setup: r.status === 403, error: why };
}

/** No HTTP/2 here: let Firebase deliver to the iPhone (Apple token imported into Firebase first). */
async function sendIosViaFirebase(secretManager, logger, device, title, body, data, ttlSeconds) {
  let account;
  try { account = JSON.parse((await secretManager.getSecret("FCM_SERVICE_ACCOUNT")).value); }
  catch (e) { return { ok: false, setup: true, error: "FCM_SERVICE_ACCOUNT secret missing" }; }
  let access;
  try { access = await googleToken(account); }
  catch (e) { return { ok: false, setup: true, error: "Google token: " + describe(e) }; }
  let last = "";
  for (const sandbox of [false, true]) {
    let token;
    try { token = await importApnsToken(access, device, sandbox); }
    catch (e) { last = "import " + (sandbox ? "sandbox" : "production") + ": " + describe(e); continue; }
    if (!token) { last = "import " + (sandbox ? "sandbox" : "production") + ": no token"; continue; }
    try {
      await axios.post(`https://fcm.googleapis.com/v1/projects/${account.project_id}/messages:send`, {
        message: {
          token,
          notification: { title, body },
          data,
          apns: {
            headers: Object.assign({ "apns-priority": "10" }, ttlSeconds ? { "apns-expiration": String(Math.floor(Date.now() / 1000) + ttlSeconds) } : {}),
            payload: { aps: { sound: "default" } },
          },
        },
      }, { headers: { Authorization: "Bearer " + access, "Content-Type": "application/json" } });
      return { ok: true };
    } catch (e) {
      last = "FCM " + (sandbox ? "sandbox" : "production") + ": " + describe(e);
      delete fcmIos[device + (sandbox ? ":s" : ":p")];
    }
  }
  logger.warning("apns via firebase: " + last);
  const setup = /THIRD_PARTY_AUTH|APNS|auth|403|401/i.test(last);
  return { ok: false, setup, gone: !setup && /UNREGISTERED|NOT_FOUND|404/i.test(last), error: last.substring(0, 160) };
}

async function importApnsToken(access, device, sandbox) {
  const key = device + (sandbox ? ":s" : ":p");
  if (fcmIos[key]) return fcmIos[key];
  const res = await axios.post("https://iid.googleapis.com/iid/v1:batchImport",
    { application: APNS.topic, sandbox, apns_tokens: [device] },
    { headers: { Authorization: "Bearer " + access, access_token_auth: "true", "Content-Type": "application/json" } });
  const r = ((res.data || {}).results || [])[0] || {};
  if (r.status && r.status !== "OK") throw new Error(r.status);
  if (r.registration_token) fcmIos[key] = r.registration_token;
  return r.registration_token || "";
}

async function apnsAuth(secretManager) {
  if (apnsJwt.token && Date.now() - apnsJwt.at < 40 * 60 * 1000) return apnsJwt.token;
  let raw;
  try { raw = (await secretManager.getSecret("APNS_KEY")).value; }
  catch (e) { throw new Error("APNS_KEY secret missing"); }
  let keyId = APNS.keyId;
  try { const k = (await secretManager.getSecret("APNS_KEY_ID")).value; if (k && String(k).trim()) keyId = String(k).trim(); } catch (e) { /* optional */ }
  // Works however the key was pasted (with or without line breaks / header lines).
  const b64 = String(raw || "").replace(/\\n/g, "\n").replace(/-----[^-]+-----/g, "").replace(/\s+/g, "");
  if (b64.length < 100) throw new Error("APNS_KEY secret looks empty");
  const input = b64url(JSON.stringify({ alg: "ES256", kid: keyId })) + "." + b64url(JSON.stringify({ iss: APNS.teamId, iat: Math.floor(Date.now() / 1000) }));
  let sig;
  try { sig = es256Sign(Buffer.from(input, "utf8"), Buffer.from(b64, "base64")); }
  catch (e) { throw new Error("APNS_KEY can't sign: " + e.message); }
  apnsJwt = { token: input + "." + b64url(sig), at: Date.now() };
  return apnsJwt.token;
}

// Apple only accepts HTTP/2 (axios is HTTP/1.1).
function apnsPost(http2, host, device, jwt, payload, exp) {
  return new Promise((resolve) => {
    let done = false, client, timer;
    const finish = (r) => {
      if (done) return;
      done = true;
      clearTimeout(timer);
      try { client.close(); } catch (e) { /* closed */ }
      resolve(r);
    };
    timer = setTimeout(() => finish({ status: 0, reason: "timeout" }), 8000);
    try {
      client = http2.connect("https://" + host);
      client.on("error", (e) => finish({ status: 0, reason: e.message }));
      const req = client.request({
        ":method": "POST", ":path": "/3/device/" + device,
        "authorization": "bearer " + jwt, "apns-topic": APNS.topic, "apns-push-type": "alert",
        "apns-priority": "10", "apns-expiration": String(exp || 0), "content-type": "application/json",
      });
      let status = 0, text = "";
      req.setEncoding("utf8");
      req.on("response", (h) => { status = Number(h[":status"]) || 0; });
      req.on("data", (c) => { text += c; });
      req.on("end", () => {
        let reason = "";
        try { reason = text ? (JSON.parse(text).reason || "") : ""; } catch (e) { reason = text.substring(0, 80); }
        finish({ status, reason });
      });
      req.on("error", (e) => finish({ status: 0, reason: e.message }));
      req.end(JSON.stringify(payload));
    } catch (e) { finish({ status: 0, reason: e.message }); }
  });
}

// ----- ES256 (ECDSA P-256 + SHA-256) in plain JavaScript, deterministic k (RFC 6979) -----

const P256 = {
  p: BigInt("0xffffffff00000001000000000000000000000000ffffffffffffffffffffffff"),
  n: BigInt("0xffffffff00000000ffffffffffffffffbce6faada7179e84f3b9cac2fc632551"),
  gx: BigInt("0x6b17d1f2e12c4247f8bce6e563a440f277037d812deb33a0f4a13945d898c296"),
  gy: BigInt("0x4fe342e2fe1a7f9b8ee7eb4a7c0f9e162bce33576b315ececbb6406837bf51f5"),
};

function es256Sign(msg, der) {
  const d = ecPrivateScalar(der);
  const n = P256.n;
  const h = bytesToInt(sha256(msg)) % n;
  const k = rfc6979k(d, sha256(msg));
  const R = ecMul(k, [P256.gx, P256.gy]);
  const r = R[0] % n;
  if (r === 0n) throw new Error("bad signature r");
  const s = (modInv(k, n) * ((h + r * d) % n)) % n;
  if (s === 0n) throw new Error("bad signature s");
  return Buffer.concat([intToBytes(r, 32), intToBytes(s, 32)]);
}

/** The 32-byte private number inside a PKCS#8 (or SEC1) EC key: INTEGER 1, OCTET STRING(32). */
function ecPrivateScalar(der) {
  for (let i = 0; i + 37 <= der.length; i++)
    if (der[i] === 0x02 && der[i + 1] === 0x01 && der[i + 2] === 0x01 && der[i + 3] === 0x04 && der[i + 4] === 0x20)
      return bytesToInt(der.slice(i + 5, i + 37));
  throw new Error("not an EC P-256 private key (.p8)");
}

function rfc6979k(x, h1) {
  const n = P256.n;
  const xb = intToBytes(x, 32);
  const hb = intToBytes(bytesToInt(h1) % n, 32);
  let V = Buffer.alloc(32, 1), K = Buffer.alloc(32, 0);
  K = hmac(K, Buffer.concat([V, Buffer.from([0]), xb, hb])); V = hmac(K, V);
  K = hmac(K, Buffer.concat([V, Buffer.from([1]), xb, hb])); V = hmac(K, V);
  for (;;) {
    V = hmac(K, V);
    const k = bytesToInt(V);
    if (k > 0n && k < n) return k;
    K = hmac(K, Buffer.concat([V, Buffer.from([0])])); V = hmac(K, V);
  }
}

function hmac(key, msg) {
  if (key.length > 64) key = sha256(key);
  const k = Buffer.alloc(64, 0); key.copy(k);
  const ipad = Buffer.alloc(64), opad = Buffer.alloc(64);
  for (let i = 0; i < 64; i++) { ipad[i] = k[i] ^ 0x36; opad[i] = k[i] ^ 0x5c; }
  return sha256(Buffer.concat([opad, sha256(Buffer.concat([ipad, msg]))]));
}

function mod(a, m) { const r = a % m; return r < 0n ? r + m : r; }

function modInv(a, m) {
  let [r0, r1] = [mod(a, m), m], [s0, s1] = [1n, 0n];
  while (r1 !== 0n) { const q = r0 / r1; [r0, r1] = [r1, r0 - q * r1]; [s0, s1] = [s1, s0 - q * s1]; }
  return mod(s0, m);
}

// Jacobian coordinates on y^2 = x^3 - 3x + b (one inversion at the end).
function ecDouble(P) {
  const p = P256.p;
  const [X, Y, Z] = P;
  if (Y === 0n) return [0n, 1n, 0n];
  const Y2 = mod(Y * Y, p), S = mod(4n * X * Y2, p), Z2 = mod(Z * Z, p);
  const M = mod(3n * (X - Z2) * (X + Z2), p);
  const X3 = mod(M * M - 2n * S, p);
  const Y3 = mod(M * (S - X3) - 8n * Y2 * Y2, p);
  const Z3 = mod(2n * Y * Z, p);
  return [X3, Y3, Z3];
}

function ecAdd(P, Q) {
  const p = P256.p;
  if (P[2] === 0n) return Q;
  if (Q[2] === 0n) return P;
  const Z1Z1 = mod(P[2] * P[2], p), Z2Z2 = mod(Q[2] * Q[2], p);
  const U1 = mod(P[0] * Z2Z2, p), U2 = mod(Q[0] * Z1Z1, p);
  const S1 = mod(P[1] * Q[2] * Z2Z2, p), S2 = mod(Q[1] * P[2] * Z1Z1, p);
  if (U1 === U2) return S1 === S2 ? ecDouble(P) : [0n, 1n, 0n];
  const H = mod(U2 - U1, p), R = mod(S2 - S1, p);
  const H2 = mod(H * H, p), H3 = mod(H * H2, p), U1H2 = mod(U1 * H2, p);
  const X3 = mod(R * R - H3 - 2n * U1H2, p);
  const Y3 = mod(R * (U1H2 - X3) - S1 * H3, p);
  const Z3 = mod(H * P[2] * Q[2], p);
  return [X3, Y3, Z3];
}

function ecMul(k, G) {
  const p = P256.p;
  let R = [0n, 1n, 0n], A = [G[0], G[1], 1n];
  while (k > 0n) {
    if (k & 1n) R = ecAdd(R, A);
    A = ecDouble(A);
    k >>= 1n;
  }
  const zi = modInv(R[2], p), zi2 = mod(zi * zi, p);
  return [mod(R[0] * zi2, p), mod(R[1] * zi2 * zi, p)];
}

function bytesToInt(b) { return BigInt("0x" + (Buffer.from(b).toString("hex") || "0")); }

function intToBytes(x, len) {
  let hex = x.toString(16);
  while (hex.length < len * 2) hex = "0" + hex;
  return Buffer.from(hex, "hex");
}

