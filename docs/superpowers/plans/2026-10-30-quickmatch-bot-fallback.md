# Quick Match Bot Fallback + Matchmaking Reveal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Quick Match always starts a playable match, even with zero real opponents, by auto-filling empty seats with bots after a 38-second search window, revealed through a slot-machine-style card shuffle that looks identical to a real player joining.

**Architecture:** Bots are not real Unity Lobby `Player`s — they have no device and no UGS session. The room's authoritative host (`OnlineRoom.InCharge`) picks bot seats and writes them once into Lobby-level custom `Data` (not player data) as a `"bots"` string, which every device decodes identically (mirroring how `"desk"`/`"seed"`/`"order"` already work). During the match, the host is also the only device that ever computes a bot's turn (reusing the existing offline `BotBrain`/`PlanAndFire` machinery unchanged) and broadcasts the result through new per-bot Lobby-Data keys; every other device just reads those keys the same way `OnlineLink.Read` already reads a real player's `shot`/`rest` data. This keeps the "every phone rebuilds identical state from synced data" invariant intact without needing a second UGS identity for the bot.

**Tech Stack:** Unity 6000.5.9f1, C#, Unity Gaming Services (Lobby, Lobby Events), UI Toolkit.

**Spec:** `docs/superpowers/specs/2026-10-30-quickmatch-bot-fallback-design.md`

## Global Constraints

- Real-player search runs for **38 seconds** (midpoint of the agreed 35–40s window) before bots fill remaining seats; the room itself still closes per the existing 45s `OnlineRoom.QuickStartAfter` (unchanged, used by private rooms).
- Bot seats only ever apply to **Quick Match** rooms (`OnlineRoom.IsQuick`), never private rooms.
- A bot's seat is visually and textually indistinguishable from a real player's: same `SeatRow` styling, no "bot" label, no different border.
- Bot-filled matches never charge the desk entry fee, never form a pot, and the winner's reward is the same flat amount a `Free` desk already pays (`Economy.OnlineWin` / `Economy.OnlineSecond` / `Economy.LossConsolation`) — not the desk's actual pot math.
- Bot-filled matches update the local winner's `onlineWins`, stats, streak, and `OnlineLevel` XP exactly as a real win does, but never call `OnlineServices.ReportWinAsync` (the public `total_wins`/`weekly_wins` leaderboard).
- A bot's displayed name comes only from a new fake-name pool (never a `Catalog.Rivals` name); its skill and pen come from `Catalog.Rivals`, sliced by the room's `DeskDef.tier`.
- Only the room's `InCharge` device ever writes the bot roster or drives a bot's turn.

## Review Focus

- **A 3–4 player room with some real players and some empty seats at the 38s mark** — every empty seat must get its own bot and the match must start full (not just the first empty one).
- **The acting host changes mid-match** (the original host backgrounds the app and `HandBackIfBorrowed` hands `IsHost` to another device) — bot turns must keep advancing from whichever device is `IsHost` now, not stall forever waiting on the old host.
- **Two bots assigned in the same room** — they must get two different fake names, never the same one twice.
- **A private room reaching 45s with empty seats** — must never get bots; only Quick Match rooms do.
- **A bot-filled match result screen** — the fee/pot line must show "nothing at stake" (or equivalent), not a pot amount the human never actually risked, and the leaderboard line must not say "updating the leaderboard…" for a bot win.

---

## File Structure

Create:
- `Assets/TuitionTakkar/Scripts/Online/OnlineBot.cs` — bot seat data, the fake-name pool, desk-tier → skill mapping, and the Lobby-Data string encoding for the bot roster.
- `Assets/TuitionTakkar/Scripts/UI/SeatShuffle.cs` — the slot-machine card-shuffle reveal widget, a small focused file alongside the project's other per-widget UI files (`PenIcon.cs`, `PowerRing.cs`).

Modify:
- `Assets/TuitionTakkar/Scripts/Online/OnlineRoom.cs` — add the bot roster, `FillWithBotsAsync`, per-bot Lobby-Data read/write helpers, and append bot ids in `StartAsync()`'s `order`.
- `Assets/TuitionTakkar/Scripts/Online/OnlineLink.cs` — `Read` and a new bot-send path so bot shot/rest data flows through Lobby Data instead of player data.
- `Assets/TuitionTakkar/Scripts/Gameplay/MatchController.cs` — `MatchSetup` gets a per-seat bot-profile array and a `HasBots` property; `BeginTurn`/`Fire`/`SyncRest` get a bot-seat branch for online matches.
- `Assets/TuitionTakkar/Scripts/Online/OnlineMatch.cs` — `Begin` builds bot seats into the match setup and skips the entry fee; `Settle` forces the free-desk payout path when bots are present; `Finish`/`FinishParty` skip the leaderboard report when bots are present.
- `Assets/TuitionTakkar/Scripts/UI/OnlineScreens.cs` — `PaintQuick` runs the 38s bot-fallback timer (host only) and renders bot seats through `SeatShuffle`.

---

### Task 1: Bot seat data, name pool, and skill mapping

**Files:**
- Create: `Assets/TuitionTakkar/Scripts/Online/OnlineBot.cs`

**Interfaces:**
- Produces: `public struct BotSeat { public string name; public string penId; public BotProfile skill; }`; `public static class OnlineBot` with `public static string BotId(int index)`, `public static bool IsBotId(string id)`, `public static int IndexFromId(string id)`, `public static List<BotSeat> PickSeats(int count, DeskTier tier)`, `public static string EncodeAll(List<BotSeat> seats)`, `public static List<BotSeat> DecodeAll(string data)`.

- [ ] **Step 1: Write the bot-id helpers**

`BotId(int index) => "bot:" + index;` `IsBotId(string id) => id != null && id.StartsWith("bot:");` `IndexFromId(string id) => int.Parse(id.Substring(4));` — these never collide with real Lobby player ids, which are UGS GUIDs.

- [ ] **Step 2: Add the fake name pool**

A `static readonly string[] Names` of at least 20 entries styled like real online display names (`"RiyaFlicks"`, `"SahilSpins"`, ...) — never reusing any `Catalog.Rivals[].name`.

- [ ] **Step 3: Implement the desk-tier → rival-skill mapping**

`static readonly (int from, int to)[] TierRivalRange` indexed by `DeskTier`: Classic → rivals `[0,1]`, Silver → `[2,3]`, Gold → `[4,5]`, Platinum → `[6,7]`, Diamond → `[8,9]` (indices into `Catalog.Rivals`, already ordered weakest → strongest). A private `static RivalDef PickRival(DeskTier tier)` picks a random rival within that tier's range.

- [ ] **Step 4: Implement `PickSeats(int count, DeskTier tier)`**

For each of `count` seats: pick a rival via `PickRival(tier)`, pick a name from `Names` not already used by an earlier seat in this call, and build a `BotSeat { name, penId = rival.penId, skill = rival.bot }`. Loop until `count` distinct-named seats are built (the name pool has 20+ entries, so this always terminates for the max 4-seat case).

- [ ] **Step 5: Implement `EncodeAll`/`DecodeAll`**

One bot per `name,penId,aimNoiseDeg,powerNoise,caution,aggression,mistakeChance` record, records joined by `;` — the same delimiter style `OnlineLink` already uses for pen state. `DecodeAll("")` returns an empty list.

- [ ] **Step 6: Manual verification**

In the Unity Editor console, call `OnlineBot.DecodeAll(OnlineBot.EncodeAll(OnlineBot.PickSeats(4, DeskTier.Diamond)))` from a temporary test button or the C# interactive window and confirm 4 seats round-trip with distinct names and plausible Diamond-tier skill values (low `aimNoiseDeg`, matching rivals 8–9).

- [ ] **Step 7: Commit**

```bash
git add Assets/TuitionTakkar/Scripts/Online/OnlineBot.cs Assets/TuitionTakkar/Scripts/Online/OnlineBot.cs.meta
git commit -m "feat: bot seat data, fake names, and desk-tier skill mapping for Quick Match"
```

---

### Task 2: Bot roster on `OnlineRoom`, visible to every device

**Files:**
- Modify: `Assets/TuitionTakkar/Scripts/Online/OnlineRoom.cs`

**Interfaces:**
- Consumes: `OnlineBot.PickSeats`, `OnlineBot.EncodeAll`, `OnlineBot.DecodeAll`, `OnlineBot.BotId` (Task 1).
- Produces: `public List<BotSeat> Bots` (property); `public Task FillWithBotsAsync()`; `public async Task SetBotDataAsync(string botId, Dictionary<string,string> values)`; `public string BotData(string botId, string key, string fallback = "")`.

- [ ] **Step 1: Add the `Bots` property**

`public List<OnlineBot.BotSeat> Bots => OnlineBot.DecodeAll(Data("bots", ""));` — decoded fresh each access from the existing `Data(key, fallback)` Lobby-Data reader, so every device sees the same roster the moment `Lobby.Data` refreshes, with no separate cache to invalidate.

- [ ] **Step 2: Implement `FillWithBotsAsync()`**

Guard: return immediately unless `InCharge && IsQuick && !Playing && Bots.Count == 0`. Compute `missing = Seats - Players.Count`; return if `missing <= 0`. Call `OnlineBot.PickSeats(missing, Desk.tier)`, then `UpdateLobbyAsync` with `Data["bots"] = new DataObject(DataObject.VisibilityOptions.Member, OnlineBot.EncodeAll(seats))`, matching the existing `SetDeskAsync` pattern. Raise `Changed?.Invoke()` after.

- [ ] **Step 3: Implement `SetBotDataAsync`/`BotData`**

`SetBotDataAsync(botId, values)`: same shape as `SetMyDataAsync`, but calls `UpdateLobbyAsync` with each key prefixed `botId + "_"` (e.g. `"bot:0_shot"`), written into `Lobby.Data` (member-visible), not player data — this is the mechanism that lets a bot "speak" without owning a real Lobby player slot. `BotData(botId, key, fallback)` reads `Data(botId + "_" + key, fallback)`.

- [ ] **Step 4: Append bot ids in `StartAsync()`**

After the existing `foreach (var p in Players) order.Add(p.Id);` loop, add `for (int i = 0; i < Bots.Count; i++) order.Add(OnlineBot.BotId(i));` — bot seats always come after every real seat, in `Bots` list order, so this ordering is reproducible from the same `"bots"` data on every device.

- [ ] **Step 5: Manual verification**

In the Editor, start a 2-player Quick Match solo (no second device), call `OnlineRoom.Current.FillWithBotsAsync()` directly from a temporary Dev-menu item, and confirm `OnlineRoom.Current.Bots.Count == 1` with a plausible name/pen, and that a second `RefreshAsync()` still shows the same single bot (not a duplicate).

- [ ] **Step 6: Commit**

```bash
git add Assets/TuitionTakkar/Scripts/Online/OnlineRoom.cs
git commit -m "feat: bot roster storage and fallback-fill on OnlineRoom"
```

---

### Task 3: Bot shot/rest sync through `OnlineLink`

**Files:**
- Modify: `Assets/TuitionTakkar/Scripts/Online/OnlineLink.cs`

**Interfaces:**
- Consumes: `OnlineBot.IsBotId`, `Room.BotData`, `Room.SetBotDataAsync` (Task 2).
- Produces: `public void SendBotShot(string botId, int turn, int shooter, BenchWorld world, Shot s)`; `public void SendBotRest(string botId, int turn, int shooter, NetPen[] pens)`.

- [ ] **Step 1: Branch `Read(string key, int index)` on bot ids**

Current body looks up `Room.Find(PlayerIdAt(index))`, which returns `null` for a synthetic bot id (no such `Player` exists). Add: if `OnlineBot.IsBotId(PlayerIdAt(index))`, read `Room.BotData(PlayerIdAt(index), key)` directly and split/validate it the same way the existing player-data path does (same `MatchId` prefix check), instead of going through `Room.Find`.

- [ ] **Step 2: Add `SendBotShot`/`SendBotRest`**

Same string-building as the existing `SendShot`/`SendRest`, but instead of calling the private `Send(key, value)` (which targets `Room.SetMyDataAsync` → the caller's own player id), call `Room.SetBotDataAsync(botId, new Dictionary<string,string> { { key, value } })` directly (fire-and-forget `async void`, same retry-free shape as a single write — callers only invoke this from the host, where a dropped write just means the bot's move is retried implicitly on the next turn broadcast attempt from `MatchController`, consistent with how a stuck remote player is already handled by `Nudge`/timeout on the *reading* side).

- [ ] **Step 3: Manual verification**

With the Task 2 Dev-menu bot-fill still in place, manually call `link.SendBotShot("bot:0", 1, 1, gm.World, someShot)` from the console mid-match and confirm `link.TryGetShot(1, 1, out _, out _)` returns `true` with the same values back — proving the bot write/read round-trips through `Read`'s new branch.

- [ ] **Step 4: Commit**

```bash
git add Assets/TuitionTakkar/Scripts/Online/OnlineLink.cs
git commit -m "feat: route bot shot/rest sync through Lobby Data instead of player data"
```

---

### Task 4: `MatchController` drives a bot's turn online

**Files:**
- Modify: `Assets/TuitionTakkar/Scripts/Gameplay/MatchController.cs`

**Interfaces:**
- Consumes: `setup.net.Room.InCharge`, `OnlineBot.BotId`/`IsBotId` (Task 1), `OnlineLink.SendBotShot`/`SendBotRest` (Task 3).
- Produces: `MatchSetup.botProfiles` (array), `MatchSetup.HasBots` (property) — used by Task 5.

- [ ] **Step 1: Add `botProfiles` and `HasBots` to `MatchSetup`**

In the constructor (`MatchSetup(int players)`), alongside the existing `isBot = new bool[players];`, add `botProfiles = new BotProfile[players];`. Add `public readonly BotProfile[] botProfiles;` and `public bool HasBots => System.Array.Exists(isBot, b => b);`. Offline Practice/Ladder never set `botProfiles[i]`, so it stays `null` there — only the single existing `public BotProfile bot;` field is used for those, unchanged.

- [ ] **Step 2: Resolve the acting bot profile in `BotTurn()`**

`BotTurn()` currently calls `PlanAndFire(FairBot(setup.bot), false)`. Change to resolve per-seat: `var profile = setup.botProfiles[shooter] ?? setup.bot; yield return PlanAndFire(FairBot(profile), false);` — offline callers (where `botProfiles[shooter]` is always `null`) keep using `setup.bot` exactly as today.

- [ ] **Step 3: Dispatch bot seats correctly in `BeginTurn()`**

Current: `if (Online && !LocalTurn) turnTimer = StartCoroutine(RemoteTurn()); else if (setup.isBot[shooter]) StartCoroutine(BotTurn()); else turnTimer = StartCoroutine(TurnTimer());` For an online bot seat, `LocalTurn` is false on every device (no device's `localIndex` equals a bot seat), so today this always falls into `RemoteTurn()` forever. Reorder the checks: if `setup.isBot[shooter]`, run `BotTurn()` when `!Online || setup.net.Room.InCharge` (offline, or this device is the room's authoritative host), otherwise `RemoteTurn()` (every other online device just waits for the host's broadcast, same as waiting on a real remote player). Only then fall through to the existing `Online && !LocalTurn` / human-turn branches for non-bot seats.

- [ ] **Step 4: Broadcast a host-driven bot's shot in `Fire()`**

Current: `if (Online && LocalTurn) setup.net.SendShot(turnNo, shooter, gm.World, s);` Extend to also send when this device is driving a bot seat: if `Online && setup.isBot[shooter] && setup.net.Room.InCharge`, call `setup.net.SendBotShot(OnlineBot.BotId(/* the bot's index among bot seats */), turnNo, shooter, gm.World, s)` instead of `SendShot`. The bot's index-among-bots is `shooter - (setup.Players - room.Bots.Count)` given bots are always appended last (Task 2, Step 4) — or simpler, store the resolved bot id per seat once in `Begin()` (Task 5) as `MatchSetup.botIds` (parallel array of `string`, null for non-bot seats) so `Fire()` just reads `setup.botIds[shooter]` without recomputing the mapping. Add `public readonly string[] botIds;` to `MatchSetup` alongside `botProfiles` for this.

- [ ] **Step 5: Mirror the same fix in `SyncRest()`**

Current: `if (LocalTurn) { ...; setup.net.SendRest(turnNo, shooter, mine); ... }` Extend the condition to `if (LocalTurn || (Online && setup.isBot[shooter] && setup.net.Room.InCharge))`, and inside, call `setup.net.SendBotRest(setup.botIds[shooter], turnNo, shooter, mine)` instead of `SendRest` when `setup.isBot[shooter]` is true.

- [ ] **Step 6: Manual verification**

Using the Task 2/3 Dev-menu hooks, start a 2-player Quick Match solo, force a bot into the second seat, and start the match. Confirm: the bot's turn fires automatically (no second device needed), the shot plays out physically on the one running Editor instance, and the match proceeds through multiple rounds to a result screen without ever hanging in `RemoteTurn()`.

- [ ] **Step 7: Commit**

```bash
git add Assets/TuitionTakkar/Scripts/Gameplay/MatchController.cs
git commit -m "feat: drive online bot seats with BotBrain from the room's acting host"
```

---

### Task 5: `OnlineMatch` builds, pays, and reports bot-filled matches correctly

**Files:**
- Modify: `Assets/TuitionTakkar/Scripts/Online/OnlineMatch.cs`

**Interfaces:**
- Consumes: `room.Bots` (Task 2), `OnlineBot.IsBotId`/`IndexFromId` (Task 1), `MatchSetup.botProfiles`/`botIds`/`HasBots` (Task 4).

- [ ] **Step 1: Build bot seats in `Begin()`**

In the `for (int i = 0; i < n; i++)` loop that currently does `var p = room.Find(l.PlayerIdAt(i)); s.pens[i] = Catalog.Pen(OnlineRoom.PenOf(p)); s.names[i] = OnlineRoom.NameOf(p);`, branch first: `string id = l.PlayerIdAt(i); if (OnlineBot.IsBotId(id)) { var seat = room.Bots[OnlineBot.IndexFromId(id)]; s.pens[i] = Catalog.Pen(seat.penId); s.names[i] = seat.name; s.isBot[i] = true; s.botProfiles[i] = seat.skill; s.botIds[i] = id; } else { /* existing lookup */ }`. Keep the existing same-pen recolor pass unchanged — it already runs after this loop over all `n` seats regardless of origin.

- [ ] **Step 2: Skip the entry fee when bots are present**

Current: `s.feePaid = Mathf.Clamp(s.desk.fee, 0, gm.Save.marks); if (s.feePaid > 0) { gm.Save.marks -= s.feePaid; gm.Save.Save(); }` Change the first line to `s.feePaid = s.HasBots ? 0 : Mathf.Clamp(s.desk.fee, 0, gm.Save.marks);` — the following `if` becomes a no-op automatically when bots are present.

- [ ] **Step 3: Force the free-desk payout path in `Settle()`**

Current: `p.staked = d != null && !d.Free;` Change to `p.staked = d != null && !d.Free && !s.HasBots;` — this alone routes bot-filled matches into the existing `else` branch that already pays `Economy.OnlineWin`/`OnlineSecond`/`LossConsolation` instead of a pot split, with no other change needed in `Settle()`.

- [ ] **Step 4: Skip the leaderboard report in `Finish()` and `FinishParty()`**

Both currently have `if (iWon) { ReportWin(gm, winsText, rankLine); Reminders.AskAfterWin(gm); }` (and `FinishParty` additionally requires `real`). Change both to also require `!s.HasBots`: `if (iWon && !s.HasBots) { ReportWin(...); Reminders.AskAfterWin(gm); }`. Leave `gm.Save.onlineWins++`, `StatsRecorder.Online(...)`, and the XP/`OnlineLevel` logic in `Settle()` untouched — those must still run for bot wins per the design doc.

- [ ] **Step 5: Show "nothing at stake" on the intro card for bot-filled matches**

`PotLine(MatchSetup s)` already branches on `d.Free` to show "friendly desk · nothing at stake" instead of the pot. Change that condition to `if (d.Free || s.HasBots)` so a bot-filled match at a paid desk shows the same friendly wording instead of a pot the human never risked.

- [ ] **Step 6: Manual verification**

Repeat the Task 4 manual bot match at a **paid** desk (not a free one). Confirm: the intro card shows "nothing at stake" (not a pot amount), the entry fee is never deducted from `gm.Save.marks`, the result screen pays the flat `Economy.OnlineWin`/`LossConsolation` amount, `gm.Save.onlineWins` and `onlineXp` still increase on a win, and the result screen never shows "updating the leaderboard…".

- [ ] **Step 7: Commit**

```bash
git add Assets/TuitionTakkar/Scripts/Online/OnlineMatch.cs
git commit -m "fix: bot-filled Quick Match plays free-desk rewards, skips fee and leaderboard"
```

---

### Task 6: Live seat-fill + slot-machine bot reveal in `OnlineScreens`

**Files:**
- Create: `Assets/TuitionTakkar/Scripts/UI/SeatShuffle.cs`
- Modify: `Assets/TuitionTakkar/Scripts/UI/OnlineScreens.cs`

**Interfaces:**
- Consumes: `room.Bots`, `room.FillWithBotsAsync()`, `room.InCharge`, `room.JoinedAt` (Task 2); existing `SeatRow`/`EmptySeat`.
- Produces: `SeatShuffle.Play(VisualElement container, string name, PenDef pen, bool dark)` — plays a brief cycling-cards animation then settles into the same visual shape `SeatRow` produces.

- [ ] **Step 1: Add a `SeatRow` overload that doesn't need a real `Player`**

Current `SeatRow(OnlineRoom room, Player p, bool dark)` reads a name and pen from a real Lobby `Player`. Extract its body into a new `static VisualElement SeatRow(string name, PenDef pen, bool dark)`, and have the existing `Player`-taking overload resolve `OnlineRoom.NameOf(p)`/`Catalog.Pen(OnlineRoom.PenOf(p))` and delegate to it. This lets bot seats render through the exact same visual path as real seats, satisfying the "indistinguishable" goal structurally, not just by copying styles.

- [ ] **Step 2: Implement `SeatShuffle.Play`**

A short (roughly 1–1.5s) coroutine-driven or `schedule.Execute`-driven animation that swaps the container's content through 4–6 random name/pen combinations in quick succession (reusing `SeatRow(name, pen, dark)` for each frame), slowing near the end, then leaves the real bot's `SeatRow(name, pen, dark)` in place. Exact easing/timing is an implementation choice — build it to read like a quick slot-machine clunk, matching the 8 Ball Pool reference described earlier in the conversation; no numeric spec beyond that.

- [ ] **Step 3: Run the bot-fallback timer in `PaintQuick`**

In `PaintQuick`, alongside the existing `float waited = Time.realtimeSinceStartup - room.JoinedAt;`, add: if `room.InCharge && waited >= 38f && room.Bots.Count == 0 && room.Players.Count < room.Seats`, fire `_ = room.FillWithBotsAsync();` (the resulting `Changed` event re-triggers `Paint()` on every device once the roster lands in `Lobby.Data`).

- [ ] **Step 4: Render bot seats through the shuffle**

In the seat-list loop (`for (int i = 0; i < room.Seats; i++) list.Add(i < found ? SeatRow(room, room.Players[i], true) : EmptySeat(...))`), add a middle case: if `i - found < room.Bots.Count` (i.e., this seat index falls in the bot range), add an empty placeholder element and call `SeatShuffle.Play(placeholder, bots[i - found].name, Catalog.Pen(bots[i - found].penId), true)` instead of `EmptySeat`. Track which bot seats have already played their shuffle (e.g., a local `HashSet<int>` closed over by `Paint()`) so a bot seat already revealed doesn't re-shuffle on every subsequent repaint.

- [ ] **Step 5: Manual verification**

Play through the full flow with no second device: open Quick Match at any paid desk, wait roughly 38 seconds, and confirm the empty seat(s) play the shuffle animation once, settle on a disguised bot card indistinguishable from a real `SeatRow`, and the match starts shortly after. Repeat at a 4-player party desk with the Editor's existing hot-seat testing to confirm multiple empty seats each shuffle independently.

- [ ] **Step 6: Commit**

```bash
git add Assets/TuitionTakkar/Scripts/UI/SeatShuffle.cs Assets/TuitionTakkar/Scripts/UI/SeatShuffle.cs.meta Assets/TuitionTakkar/Scripts/UI/OnlineScreens.cs
git commit -m "feat: slot-machine bot reveal and 38s fallback timer on the Quick Match screen"
```

---

## Self-Review Notes

- **Spec coverage:** every Goal/Confirmed-decision in the design doc maps to a task — timing (Task 6), live fill (Task 6), disguise (Task 6 Step 1/4), party desks filling every empty seat (Task 2 Step 2's `missing` loop, Task 4/5's per-seat handling), bot skill by tier (Task 1), pot/payout (Task 5), leaderboard exclusion (Task 5), bot names (Task 1).
- **Proportion:** this plan is long because the sync architecture (Tasks 2–4) genuinely has no shorter honest description — it's new wiring through three existing files that all assume "every seat is a real Lobby player." No task body duplicates code the signature/test already determines.
- **Type consistency:** `BotSeat`, `OnlineBot.BotId/IsBotId/IndexFromId`, `MatchSetup.botProfiles/botIds/HasBots` are introduced once (Tasks 1, 4) and referenced identically in every later task.
