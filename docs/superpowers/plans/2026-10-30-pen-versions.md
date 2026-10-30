# Pen Versions (V1/V2/V3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every classic and shop-bought pen a player owns starts at V1 and can be upgraded to V2 and V3 — unlocking as the player's existing Online Level rises, each upgrade costing marks, each version performing better and looking different — while custom Workshop-built pens are untouched.

**Architecture:** A pen's version is player save state keyed by pen id (`SaveData.penVersions`, following the exact `PenInk` list-of-records pattern already used for ink, since `JsonUtility` can't serialize dictionaries). A single `PenVersions.Apply(PenDef, version)` clones the base `PenDef` (never mutates the shared cached instance `Catalog.Pen(id)` returns) and reuses the same stat→physics lerp formulas `PenParts` already uses for generated pens, so classic and shop pens get identical treatment from one code path. The version's stat boost is applied only where the design explicitly allows it — the local player's own pen in Batch Ladder, Practice, and bot-filled Quick Match — never to any pen in real human-vs-human online play, which keeps the existing "every phone resolves a pen's stats identically from its id alone" invariant intact for PvP.

**Tech Stack:** Unity 6000.5.9f1, C#.

**Spec:** `docs/superpowers/specs/2026-10-30-pen-versions-design.md`

**Dependency:** Task 4 of this plan modifies `OnlineMatch.Begin()`'s per-seat loop, which Task 5 of `docs/superpowers/plans/2026-10-30-quickmatch-bot-fallback.md` also modifies (to recognize bot seats at all). Implement that plan's Task 5 first, or expect a merge conflict in the same loop.

## Global Constraints

- Versions apply to classic catalog pens (`Catalog.Pens`) and the 600 generated shop pens — anything `Catalog.Pen(id)` resolves that is **not** `PenParts.IsCustom(id)`.
- V2 unlocks at **Online Level 10**, V3 at **Online Level 30** (the same milestones that already open desk indices 4 and 7 — `Desks.cs`'s `Levels` array). Flat per-pen, not scaled by price or rarity.
- Upgrading costs marks: **200 for V2, 600 for V3** — paid once per pen, at the moment of upgrade, in addition to the level requirement already being met.
- No rematch or replay is required to upgrade — only the level and the marks.
- A version's stat boost applies **only** when the local player is facing a bot: Batch Ladder (`GameManager.StartLadder`), Practice (`GameManager.StartPractice`), and the local human's own seat in a bot-filled Quick Match. It never changes any pen's stats in real human-vs-human Desk matches (`OnlineMatch.Begin`'s normal per-seat lookup) or Pass & Play.
- A version always changes the pen's look (finish/colour), never silently.

## Review Focus

- **A custom Workshop-built pen (`c.…` id)** — must never show a version badge, upgrade button, or stat change; `PenVersions.Unlocked`/`CurrentVersion` must return 1 for it unconditionally.
- **Not enough marks to upgrade** — `TryUpgrade` must fail cleanly with no marks deducted, not silently charge a partial amount.
- **Upgrading past what the player's Online Level allows** (e.g., Level 8 trying to reach V3, which needs Level 30) — must be refused with a message naming the level needed, not silently capped to whatever the player can afford.
- **Real online PvP at a paid Desk** — a player's equipped pen must use base (V1-equivalent) stats regardless of their actual unlocked version; only Task 4's bot-filled-match path should ever apply the boost online.
- **Save/load round-trip** — a pen's stored version must survive `SaveData` serialization the same way `PenInk` already does (a `List<T>` keyed by pen id, not a `Dictionary`).

---

## File Structure

Create:
- `Assets/TuitionTakkar/Scripts/Core/PenVersions.cs` — the whole version system.

Modify:
- `Assets/TuitionTakkar/Scripts/Core/SaveData.cs` — add the `penVersions` save list.
- `Assets/TuitionTakkar/Scripts/Core/PenParts.cs` — name the mass/friction/angularDamping lerp-range magic numbers as constants, so `PenVersions.Apply` reuses the exact same formula instead of duplicating it.
- `Assets/TuitionTakkar/Scripts/Core/GameManager.cs` — `StartLadder`/`StartPractice` apply the local player's unlocked version to their own pen.
- `Assets/TuitionTakkar/Scripts/Online/OnlineMatch.cs` — apply the version to the local human's own seat only when the match is bot-filled.
- `Assets/TuitionTakkar/Scripts/UI/PouchScreen.cs` — show a version badge on owned pens, and an upgrade sheet.

---

### Task 1: Save data for a pen's version

**Files:**
- Modify: `Assets/TuitionTakkar/Scripts/Core/SaveData.cs`

**Interfaces:**
- Produces: `public class PenVersion { public string id; public int version; }`; `public List<PenVersion> penVersions = new List<PenVersion>();` on `SaveData`.

- [ ] **Step 1: Add the `PenVersion` record and list**

Directly above or below the existing `PenInk` class (near the top of the file), add `[Serializable] public class PenVersion { public string id; public int version; }`, and on `SaveData` add `public List<PenVersion> penVersions = new List<PenVersion>();` — same shape as the existing `public List<PenInk> inks = new List<PenInk>();`, for the same reason stated in that field's own comment (`JsonUtility can't save dictionaries`).

- [ ] **Step 2: Manual verification**

In the Editor, create a `SaveData`, add one `PenVersion` entry, call `JsonUtility.ToJson` then `JsonUtility.FromJson<SaveData>` on the result, and confirm the entry survives with the same `id`/`version` — the same round-trip the existing save/load path already relies on for `inks`.

- [ ] **Step 3: Commit**

```bash
git add Assets/TuitionTakkar/Scripts/Core/SaveData.cs
git commit -m "feat: add per-pen version state to SaveData"
```

---

### Task 2: Name the stat→physics constants in `PenParts`

**Files:**
- Modify: `Assets/TuitionTakkar/Scripts/Core/PenParts.cs`

**Interfaces:**
- Produces: `public const float MassMin = .0085f, MassMax = .0235f, FrictionMin = .18f, FrictionMax = .62f, DampingHigh = 1.25f, DampingLow = .5f;`

- [ ] **Step 1: Extract the constants**

In `Build(Spec, string, PenRarity, float)`, the lines `mass = Mathf.Lerp(.0085f, .0235f, st.weight)`, `friction = Mathf.Lerp(.18f, .62f, st.grip)`, `angularDamping = Mathf.Lerp(1.25f, .5f, st.spin)` currently hardcode these ranges. Add the six named constants near the top of `PenParts` (alongside `Budget`/`CustomBudget`/`MaxPower`) and replace the three inline literals in `Build` with them. No behavior change — this is a pure rename so Task 3 can reuse the same ranges instead of duplicating magic numbers.

- [ ] **Step 2: Manual verification**

Open the Workshop screen in Play mode, build any pen, and confirm its stats/feel are unchanged from before this edit (same mass/friction/spin for the same part choices) — this step only renames constants, so any observable difference means a typo in the extraction.

- [ ] **Step 3: Commit**

```bash
git add Assets/TuitionTakkar/Scripts/Core/PenParts.cs
git commit -m "refactor: name the stat-to-physics lerp ranges as constants"
```

---

### Task 3: `PenVersions` — unlock, upgrade, and apply

**Files:**
- Create: `Assets/TuitionTakkar/Scripts/Core/PenVersions.cs`

**Interfaces:**
- Consumes: `PenParts.IsCustom`, `PenParts.MassMin/MassMax/FrictionMin/FrictionMax/DampingHigh/DampingLow` (Task 2), `OnlineLevel.Level` (`Desks.cs`), `SaveData.penVersions` (Task 1).
- Produces: `public static class PenVersions` with `public const int MaxVersion = 3;`, `public static int Unlocked(SaveData save, string id)`, `public static int CurrentVersion(SaveData save, string id)`, `public static int LevelFor(int version)`, `public static int CostFor(int version)`, `public static bool TryUpgrade(SaveData save, string id, out string error)`, `public static PenDef Apply(PenDef baseDef, int version)`.

- [ ] **Step 1: Write `LevelFor`/`CostFor`**

`LevelFor(2) = 10`, `LevelFor(3) = 30` (else `1`, meaning no requirement). `CostFor(2) = 200`, `CostFor(3) = 600` (else `0`).

- [ ] **Step 2: Write `Unlocked(SaveData save, string id)`**

Returns `1` immediately if `PenParts.IsCustom(id)`. Otherwise: `int level = OnlineLevel.Level(save.onlineXp); if (level >= LevelFor(3)) return 3; if (level >= LevelFor(2)) return 2; return 1;`

- [ ] **Step 3: Write `CurrentVersion(SaveData save, string id)`**

Returns `1` if `PenParts.IsCustom(id)`. Otherwise look up `save.penVersions.Find(v => v.id == id)`; if found, `Mathf.Clamp(entry.version, 1, Unlocked(save, id))`, else `1`. Clamping against `Unlocked` means a pen upgraded before a level change (not applicable here, levels only rise) always stays valid, and guards against any future save tampering.

- [ ] **Step 4: Write `TryUpgrade(SaveData save, string id, out string error)`**

```
error = null;
if (PenParts.IsCustom(id)) { error = "custom pens can't be upgraded."; return false; }
int current = CurrentVersion(save, id);
if (current >= MaxVersion) { error = "already at the top version."; return false; }
int next = current + 1;
int needLevel = LevelFor(next);
if (OnlineLevel.Level(save.onlineXp) < needLevel) { error = "reach online level " + needLevel + " to unlock this."; return false; }
int cost = CostFor(next);
if (save.marks < cost) { error = "not enough marks (need " + cost + ")."; return false; }
save.marks -= cost;
var entry = save.penVersions.Find(v => v.id == id);
if (entry == null) save.penVersions.Add(new PenVersion { id = id, version = next });
else entry.version = next;
return true;
```

- [ ] **Step 5: Write `Apply(PenDef baseDef, int version)`**

If `version <= 1` or `baseDef.custom`, return `baseDef` unchanged (no clone needed — callers never mutate the result). Otherwise clone via `(PenDef)baseDef.GetType().GetMethod(...)` — no: use the same pattern `Recolor` already uses, `var d = (PenDef)baseDef.MemberwiseClone();` — then:
- `float factor = version == 3 ? 1.12f : 1.06f;`
- `d.statWeight = Mathf.Clamp01(baseDef.statWeight * factor); d.statGrip = Mathf.Clamp01(baseDef.statGrip * factor); d.statSpin = Mathf.Clamp01(baseDef.statSpin * factor);`
- `d.mass = Mathf.Lerp(PenParts.MassMin, PenParts.MassMax, d.statWeight); d.friction = Mathf.Lerp(PenParts.FrictionMin, PenParts.FrictionMax, d.statGrip); d.angularDamping = Mathf.Lerp(PenParts.DampingHigh, PenParts.DampingLow, d.statSpin);`
- Look change: `d.finish = version == 3 ? PenFinish.Holo : PenFinish.Metal;` and for V3 only, `d.color = Color.Lerp(baseDef.color, Pal.H("#D4A72C"), 0.25f);` (a gold tint, echoing the existing "Golden Nib" ladder prize).
- `d.name = baseDef.name + " V" + version;`
- Return `d`.

- [ ] **Step 6: Manual verification**

In the Editor console: take any classic `PenDef` (e.g. `Catalog.Pen("steel")`), call `PenVersions.Apply(pen, 3)`, and confirm the result has higher `statWeight/statGrip/statSpin` than the original, a different `finish`, a name ending " V3", and that the *original* `Catalog.Pen("steel")` instance is unchanged afterward (proving the clone didn't mutate the shared cached pen).

- [ ] **Step 7: Commit**

```bash
git add Assets/TuitionTakkar/Scripts/Core/PenVersions.cs
git commit -m "feat: pen version unlock, upgrade cost, and stat/look application"
```

---

### Task 4: Apply versions where the design allows — bot matches only

**Files:**
- Modify: `Assets/TuitionTakkar/Scripts/Core/GameManager.cs`
- Modify: `Assets/TuitionTakkar/Scripts/Online/OnlineMatch.cs`

**Interfaces:**
- Consumes: `PenVersions.Apply`, `PenVersions.CurrentVersion` (Task 3).

- [ ] **Step 1: Apply the version in `StartLadder`**

Change `s.pens[0] = PlayerPenForMatch();` to `var basePen = PlayerPenForMatch(); s.pens[0] = PenVersions.Apply(basePen, PenVersions.CurrentVersion(Save, basePen.id));` — `basePen.id` is correct here even for the dry-pen Starter-Click fallback inside `PlayerPenForMatch()`, since `PenVersions.CurrentVersion` on the Starter Click id simply returns 1 (no one upgrades the pen that "never leaves the pouch"), so `Apply` is a no-op in that case.

- [ ] **Step 2: Apply the version in `StartPractice`**

Same change: `var basePen = PlayerPenForMatch(); s.pens[0] = PenVersions.Apply(basePen, PenVersions.CurrentVersion(Save, basePen.id));`

- [ ] **Step 3: Leave `StartPassAndPlay` untouched**

Do not change `StartPassAndPlay`'s `PlayerPenForMatch()` call — Pass & Play is two local humans, not a bot match, so it is out of scope per the design doc (and already pays nothing, for the same anti-farming reason).

- [ ] **Step 4: Apply the version to the local human's seat in a bot-filled Quick Match**

In `OnlineMatch.Begin()`'s per-seat loop (after the bot-fallback plan's Task 5 has added the `OnlineBot.IsBotId(id)` branch), in the `else` branch that resolves a real player, add: after `s.pens[i] = Catalog.Pen(OnlineRoom.PenOf(p));`, if `i == l.LocalIndex && room.Bots.Count > 0`, replace it with `s.pens[i] = PenVersions.Apply(s.pens[i], PenVersions.CurrentVersion(gm.Save, OnlineRoom.PenOf(p)));`. This only ever touches the local player's own seat, and only when the room actually has bots — every other real player's seat, and every seat in a room with zero bots, is untouched, preserving the "online PvP always uses base stats" constraint.

- [ ] **Step 5: Manual verification**

Raise a test save's `onlineXp` past Online Level 30 (temporarily, via the Editor console), upgrade the equipped pen to V3 with `PenVersions.TryUpgrade`, then: (a) play a Practice match and confirm the pen visually shows the V3 look and the stronger stats feel in the physics; (b) start a real 2-player online match with another real device/account and confirm the equipped pen there shows its plain base look and stats, not V3 — proving the PvP path is untouched.

- [ ] **Step 6: Commit**

```bash
git add Assets/TuitionTakkar/Scripts/Core/GameManager.cs Assets/TuitionTakkar/Scripts/Online/OnlineMatch.cs
git commit -m "feat: apply pen versions in Ladder, Practice, and bot-filled Quick Match only"
```

---

### Task 5: Pouch UI — version badge and upgrade sheet

**Files:**
- Modify: `Assets/TuitionTakkar/Scripts/UI/PouchScreen.cs`

**Interfaces:**
- Consumes: `PenVersions.CurrentVersion`, `PenVersions.Unlocked`, `PenVersions.TryUpgrade`, `PenVersions.Apply`, `PenVersions.LevelFor`, `PenVersions.CostFor` (Task 3).

- [ ] **Step 1: Show the player's current version on "My pens"**

Line 92, `foreach (var id in save.owned) mine.Add(Catalog.Pen(id));`, becomes `foreach (var id in save.owned) mine.Add(PenVersions.Apply(Catalog.Pen(id), PenVersions.CurrentVersion(save, id)));` — the grid, icon, and traits for an owned pen now reflect its actual current version everywhere `OwnedCard` already renders `pen`.

- [ ] **Step 2: Add a version badge and upgrade button to `OwnedCard`**

In `OwnedCard(GameManager gm, PenDef pen, int i)`, after the existing `c.Add(RarityRow(pen));` line: if `!pen.custom && !PenParts.IsCustom(pen.id)`, compute `int current = PenVersions.CurrentVersion(save, pen.id); int unlocked = PenVersions.Unlocked(save, pen.id);` and add a small `UIKit.Strong("V" + current, ...)` chip next to `RarityRow`. If `current < unlocked`, also add a small "UPGRADE" button that opens a new `VersionSheet(gm, pen)` overlay (Step 3); if `current == unlocked && unlocked < PenVersions.MaxVersion`, instead show locked text reading `"V" + (current + 1) + " at level " + PenVersions.LevelFor(current + 1)` with no button (nothing to spend marks on yet).

- [ ] **Step 3: Implement `VersionSheet(GameManager gm, PenDef pen)`**

A new sheet following the existing `BuySheet`/`RefillSheet` pattern (`Sheet(out panel)`, `Ledger(...)`, `SheetButtons(...)`): shows the pen's current version's look/stats next to the next version's (via two `PenVersions.Apply(Catalog.Pen(pen.id), v)` calls), the marks cost (`PenVersions.CostFor(next)`) via `Ledger`, and a confirm button wired to `PenVersions.TryUpgrade(save, pen.id, out var error)` — on success, `save.Save(); gm.UI.HideOverlay(); gm.ShowPouch(0);`; on failure, show `error` via the same inline-error pattern `BuySheet` already uses for "can't afford."

- [ ] **Step 4: Manual verification**

In Play mode, with a save at Online Level 10+ and 200+ marks: open "My pens," confirm a non-custom owned pen shows "V1" and an "UPGRADE" button, tap it, confirm the sheet shows the V2 look/stats and a 200-mark cost, confirm, and confirm the card now shows "V2" with the new look. Repeat with marks below 200 and confirm the sheet shows the "not enough marks" error without deducting anything. Build a custom Workshop pen and confirm its card shows no version chip or upgrade button at all.

- [ ] **Step 5: Commit**

```bash
git add Assets/TuitionTakkar/Scripts/UI/PouchScreen.cs
git commit -m "feat: pen version badge and upgrade sheet in the Pouch"
```

---

## Self-Review Notes

- **Spec coverage:** scope (classic + shop pens, not custom) — Tasks 3–5; level-gated unlock — Task 3; marks cost — Task 3; no rematch required — Task 3 (nothing in `TryUpgrade` checks match history); stat + look change — Task 3 Step 5; bot-only stat effect — Task 4.
- **Proportion:** the core system is one new file (Task 3); the rest is small, precisely-anchored edits to five existing files, each a few lines.
- **Type consistency:** `PenVersions.Apply/CurrentVersion/Unlocked/TryUpgrade/LevelFor/CostFor` are defined once in Task 3 and used with the same signatures in Tasks 4–5.
