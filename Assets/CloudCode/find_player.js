// Tuition Takkar · turns a friend ID (TT-XXXXXX) into a player id. Search is by ID only.
const { DataApi } = require("@unity-services/cloud-save-1.4");

module.exports = async ({ params, context, logger }) => {
  let code = String(params.code || "").trim().toUpperCase();
  if (code.startsWith("TT-")) code = code.substring(3);
  if (!/^[0-9A-Z]{6}$/.test(code)) return { playerId: "", error: "bad id: " + JSON.stringify(params).substring(0, 200) };
  const api = new DataApi({ accessToken: context.serviceToken });
  const shard = "fid_" + code.substring(0, 2);
  const r = await api.getCustomItems(context.projectId, shard, [code]);
  const results = (r.data && r.data.results) || [];
  const hit = results.find((x) => x.key === code);
  if (!hit) {
    // Diagnostics: which codes this shard holds (codes only, no player ids).
    let keys = [];
    try {
      const k = await api.getCustomKeys(context.projectId, shard);
      keys = ((k.data && k.data.results) || []).map((x) => x.key);
    } catch (e) { keys = ["keys failed: " + (e.response ? e.response.status : e.message)]; }
    logger.info("find_player: " + code + " not in " + shard + " (holds " + keys.join(",") + ")");
    return { playerId: "", error: "not found in " + shard + " [" + keys.join(",") + "]" };
  }
  return { playerId: String(hit.value) };
};

module.exports.params = { code: { type: "String", required: true } };
