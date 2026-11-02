// Tuition Takkar · owner-only: returns the players' suggestions for one month, for the private
// web page /admin/ideas. The password is never in the game or the page: the owner stores it in the
// Unity dashboard (Cloud Save > Game Data > Private > item "admin", key "ideasPassword"), and this
// script compares against it. Wrong passwords are slowed down and capped at 20 a day.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function readAll(fetchPage) {
  const out = [];
  let after;
  for (let i = 0; i < 100; i++) {
    const res = await fetchPage(after);
    const rows = (res.data && res.data.results) || [];
    out.push(...rows);
    if (rows.length === 0 || !(res.data.links && res.data.links.next)) break;
    after = rows[rows.length - 1].key;
  }
  return out;
}

module.exports = async ({ params, context, logger }) => {
  const api = new DataApi({ accessToken: context.serviceToken });
  const { projectId } = context;
  const day = new Date().toISOString().slice(0, 10);

  const adminRes = await api.getPrivateCustomItems(projectId, "admin", ["ideasPassword", "fails"]);
  const admin = {};
  for (const r of (adminRes.data.results || [])) admin[r.key] = r.value;
  const fails = admin.fails && admin.fails.day === day ? admin.fails.count : 0;

  if (!admin.ideasPassword) return { ok: false, error: "no password set yet: add it in the Unity dashboard first" };
  if (fails >= 20) return { ok: false, error: "too many wrong tries today, try again tomorrow" };
  if (String(params.password || "") !== String(admin.ideasPassword)) {
    await api.setPrivateCustomItem(projectId, "admin", { key: "fails", value: { day, count: fails + 1 } });
    logger.warning("list_suggestions: wrong password");
    await sleep(1500);
    return { ok: false, error: "wrong password" };
  }

  const now = new Date();
  const month = /^\d{4}_\d{2}$/.test(String(params.month || ""))
    ? params.month
    : now.getUTCFullYear() + "_" + String(now.getUTCMonth() + 1).padStart(2, "0");
  const list = "suggestions_" + month;

  // New suggestions are private; the very first ones were saved in the default area. Read both.
  const priv = await readAll((after) => api.getPrivateCustomItems(projectId, list, undefined, after));
  let pub = [];
  try { pub = await readAll((after) => api.getCustomItems(projectId, list, undefined, after)); } catch (e) { pub = []; }

  const items = [...priv, ...pub]
    .map((r) => ({ key: r.key, ...(typeof r.value === "object" ? r.value : { text: String(r.value) }) }))
    .sort((a, b) => String(b.at || "").localeCompare(String(a.at || "")));
  return { ok: true, month, items };
};
