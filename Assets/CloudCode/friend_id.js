// Tuition Takkar · gives the calling player a permanent friend ID (TT-XXXXXX), once.
// The ID → player index lives in Cloud Save custom items "fid_XX" (server-only writes);
// the player's own ID is stored as protected player data (server writes, player reads).
const { DataApi } = require("@unity-services/cloud-save-1.4");

const ALPHA = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // no 0/O, 1/I
function gen() {
  let s = "";
  for (let i = 0; i < 6; i++) s += ALPHA[Math.floor(Math.random() * ALPHA.length)];
  return s;
}
const shard = (code) => "fid_" + code.substring(0, 2);

module.exports = async ({ params, context, logger }) => {
  const api = new DataApi({ accessToken: context.serviceToken });
  const { projectId, playerId } = context;

  const own = await api.getProtectedItems(projectId, playerId, ["friendId"]);
  const found = (own.data.results || []).find((r) => r.key === "friendId");
  if (found && found.value) return { id: found.value };

  for (let i = 0; i < 12; i++) {
    const code = gen();
    const taken = await api.getCustomItems(projectId, shard(code), [code]);
    if ((taken.data.results || []).length > 0) continue;
    await api.setCustomItem(projectId, shard(code), { key: code, value: playerId });
    await api.setProtectedItem(projectId, playerId, { key: "friendId", value: "TT-" + code });
    return { id: "TT-" + code };
  }
  logger.error("friend_id: no free code after 12 tries");
  throw new Error("could not make a player ID, try again");
};
