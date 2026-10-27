// Tuition Takkar · turns a friend ID (TT-XXXXXX) into a player id. Search is by ID only.
const { DataApi } = require("@unity-services/cloud-save-1.4");

module.exports = async ({ params, context, logger }) => {
  let code = String(params.code || "").trim().toUpperCase();
  if (code.startsWith("TT-")) code = code.substring(3);
  if (!/^[0-9A-Z]{6}$/.test(code)) return { playerId: "", error: "bad id" };
  const api = new DataApi(context);
  const r = await api.getCustomItems(context.projectId, "fid_" + code.substring(0, 2), [code]);
  const hit = (r.data.results || []).find((x) => x.key === code);
  return { playerId: hit ? String(hit.value) : "" };
};

module.exports.params = { code: { type: "String", required: true } };
