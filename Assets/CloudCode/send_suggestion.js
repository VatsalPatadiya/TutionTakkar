// Tuition Takkar · a signed-in player sends an idea / bug / pen idea.
// Saved as private Cloud Save game data "suggestions_YYYY_MM" (one list per month): only server
// scripts and the dashboard can read it. Read them on the web at /admin/ideas. Max 3 per player per day.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const KINDS = ["idea", "bug", "pen"];
const PER_DAY = 3;

module.exports = async ({ params, context, logger }) => {
  const api = new DataApi({ accessToken: context.serviceToken });
  const { projectId, playerId } = context;

  const text = String(params.text || "").trim().slice(0, 300);
  if (text.length < 3) throw new Error("write a little more");
  const kind = KINDS.includes(params.kind) ? params.kind : "idea";

  // Daily limit, kept as protected player data (server writes, player can read).
  const day = new Date().toISOString().slice(0, 10);
  const got = await api.getProtectedItems(projectId, playerId, ["suggestDay"]);
  const rec = (got.data.results || []).find((r) => r.key === "suggestDay");
  const prev = rec && rec.value && rec.value.day === day ? rec.value.count : 0;
  if (prev >= PER_DAY) return { ok: false, left: 0 };

  const now = new Date();
  const list = "suggestions_" + now.getUTCFullYear() + "_" + String(now.getUTCMonth() + 1).padStart(2, "0");
  const key = now.getTime() + "_" + playerId.slice(0, 8);
  // Private: only server scripts and the dashboard can read it (players can't).
  await api.setPrivateCustomItem(projectId, list, {
    key,
    value: {
      kind,
      text,
      name: String(params.name || "").slice(0, 20),
      version: String(params.version || "").slice(0, 20),
      platform: String(params.platform || "").slice(0, 20),
      playerId,
      at: now.toISOString(),
    },
  });
  await api.setProtectedItem(projectId, playerId, { key: "suggestDay", value: { day, count: prev + 1 } });
  logger.info("suggestion " + kind + " from " + playerId);
  return { ok: true, left: PER_DAY - prev - 1 };
};
