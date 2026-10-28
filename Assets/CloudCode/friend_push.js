// Tuition Takkar · friend request / accepted request as a phone notification (Firebase Cloud Messaging).
// Same secret and token as invite_push: FCM_SERVICE_ACCOUNT, player data key "fcmToken".
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
  let t;
  try {
    const items = await api.getItems(context.projectId, to, ["fcmToken"]);
    t = (items.data.results || []).find((r) => r.key === "fcmToken");
  } catch (e) {
    logger.warning("friend_push: token read failed " + (e.response ? e.response.status : e.message));
  }
  if (!t || !t.value) return { sent: false, error: "no phone token" };

  let account;
  try {
    const secret = await secretManager.getSecret("FCM_SERVICE_ACCOUNT");
    account = JSON.parse(secret.value);
  } catch (e) {
    logger.error("friend_push: FCM_SERVICE_ACCOUNT secret missing or not JSON");
    return { sent: false, error: "notifications aren't set up" };
  }

  const title = kind === "request" ? `${from} wants to be friends ✏️` : `${from} accepted your friend request 🎉`;
  const body = kind === "request" ? "Open Tuition Takkar to accept." : "You're friends now. Save them a seat in your next room!";
  const link = kind === "request" ? "tuitiontakkar://friends/requests" : "tuitiontakkar://friends";
  try {
    const access = await googleToken(account);
    await axios.post(`https://fcm.googleapis.com/v1/projects/${account.project_id}/messages:send`, {
      message: {
        token: String(t.value),
        notification: { title, body },
        data: { link, channel: "invites", kind: "friend_" + kind, from, fromId: context.playerId, title, body },
        android: { priority: "HIGH", notification: { channel_id: "invites" } },
      },
    }, { headers: { Authorization: "Bearer " + access, "Content-Type": "application/json" } });
  } catch (e) {
    logger.warning("friend_push: FCM send failed " + (e.response ? e.response.status : e.message));
    return { sent: false, error: "send failed" };
  }
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

