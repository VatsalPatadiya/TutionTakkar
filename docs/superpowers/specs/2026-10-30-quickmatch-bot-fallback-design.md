# Quick Match bot fallback + 8 Ball Pool–style matchmaking screen

Date: 2026-10-30
Status: approved by owner in chat, written up for planning

## Intent and why

Today, Quick Match (`OnlineRoom.QuickMatchAsync`, `OnlineScreens.DoQuick`) shows a
plain "finding a seat at [desk]…" busy overlay while it waits for real opponents.
If nobody shows up, the room just sits there — the host can manually start with
fewer players after 45 seconds (`OnlineRoom.QuickStartAfter`), but nothing fills
the empty seats, and plenty of quiet-hours matches simply never start.

The owner wants two things, which turn out to be one piece of work:
1. When no real opponent is found, auto-start a match against a bot instead of
   leaving the player stuck waiting or forced to manually settle for fewer seats.
2. Redesign the matchmaking screen to feel like 8 Ball Pool's: real opponents'
   cards appear live as they join, and a slot-machine-style card shuffle reveals
   whatever fills the last empty seat(s).

Reference: 8 Ball Pool's opponent-search screen, timestamp 00:13–00:17 of
https://www.youtube.com/watch?v=mMzDhYT4aOo (owner-described, not fetched by
Claude: a deck of player cards cycles rapidly, slows, and clunks to a stop on
the matched opponent).

## Goals

- Quick Match always starts, even with zero other humans online.
- The matchmaking screen shows real players' seats filling in live as they
  join (same as today's room/lobby view, just restyled).
- At the point real-player search ends, any still-empty seats get bots,
  revealed with a slot-machine card shuffle that settles on each bot's card.
- A bot's card is visually and textually indistinguishable from a real
  player's card — same name style, same pen display, no "bot" label, no
  different border or badge.
- Bot skill is picked by the desk's tier/fee, reusing the game's existing bot
  skill curve rather than inventing a new one.
- Works the same way for 2-player desks and for 3–4 player party desks: every
  seat still empty after the search window gets its own bot.

## Non-goals

- No change to how real-vs-real Quick Match or private rooms work once every
  seat has a real player.
- No change to offline Practice or Batch Ladder bots — this only touches the
  online Quick Match room-forming flow.
- Not building a "rematch vs this bot" or bot-profile persistence feature;
  each bot is assigned fresh per room.

## Confirmed decisions (from the brainstorming conversation)

- **Timing:** the room stays open 45 seconds total. Real-player search runs
  for roughly 35–40 seconds of that window. Any seats still open at that point
  get bots immediately (the match then proceeds to start).
- **Bot reveal:** the slot-machine shuffle animation plays once, at the
  35–40s mark, only for the seats that are still empty — it is not a
  continuously-running "searching" animation for the whole 45 seconds. Seats
  filled earlier by real players just appear normally as they join, same as
  today.
- **Disguise:** bot cards look exactly like real player cards (name, pen,
  whatever else a normal card shows). No visual or textual tell.
- **Party desks:** if a 3–4 player room has some real players and some empty
  seats when the window closes, bots fill every remaining empty seat — the
  match always starts full, the same as a fully-empty room would.
- **Bot skill source:** reuse the bot skill curve the game already has.
  `GameData.Catalog` has 10 `BotProfile`s for the Batch Ladder rivals (ranging
  from weakest, Monty, to strongest, Vikram) and a separate 3-tier
  `PracticeBots` (Easy/Medium/Hard). Map `DeskDef.tier`
  (`DeskTier.Classic/Silver/Gold/Platinum/Diamond`, `Desks.cs`) to a skill
  level from one of these pools so cheaper desks get easy bots and
  high-tier desks get hard ones. Exact mapping (which pool, how many buckets)
  is left to the builder — it's a tuning choice, not a product one.

## Constraints

- Online matches sync over Unity Lobby (`OnlineRoom`, `OnlineLink`) with no
  game server: each device writes only its own player's Lobby player data
  (`shot`/`rest`/`quit` strings, see `docs/PROJECT_GUIDE.md` §7). A bot has no
  device of its own.
- The existing offline bot AI (`BotBrain`) simulates a spread of shots in a
  hidden physics scene and adds human error from a `BotProfile` — this already
  exists and should be reused for bot moves, not reinvented.
- `MatchController` already has an `IsParty` path with `PartyAutoShot` for
  when a human player's timer runs out online — this is a related but
  separate mechanic (an idle human's fallback shot), not the same as a bot
  player taking normal turns.

## What's left to the builder

- **How a bot's turn gets simulated and synced.** Since a bot has no device,
  some real client (almost certainly whichever device is `InCharge` /
  the room's acting host) needs to run `BotBrain` for the bot's seat and
  write its shot/rest data somewhere every other device can read, in the
  same shape the Lobby sync already expects. Whether that's a reserved
  pseudo-player slot, a host-owned Lobby custom data key, or another
  mechanism is an implementation choice — it does not change what any
  player sees or experiences, so it doesn't need the owner's input. Pick
  whatever fits the existing `OnlineRoom`/`OnlineLink`/`OnlineMatch` sync
  model most cleanly.
- **Exact bot-skill-to-desk-tier mapping** (which pool, how finely it scales
  across the 28 desks vs. the 5 tiers).
- **Shuffle animation specifics** (how many cards cycle, duration, easing) —
  build to feel like the 8 Ball Pool reference; no numeric spec was given
  beyond that.
- **What a bot's displayed "pen"** should be on its card — likely drawn from
  the same `BotProfile`/`RivalDef` pool used for its skill, so the two stay
  consistent, but the exact source is an implementation detail.
- Whether this room/shuffle screen replaces `OnlineScreens.Busy(...)` entirely
  or is a new screen inserted before `ShowRoom` — structure it however fits
  the existing `OnlineScreens.cs` screen flow best.

## Relevant existing code

- `Assets/TuitionTakkar/Scripts/Online/OnlineRoom.cs` — Lobby room lifecycle,
  `MinPlayers`, `QuickStartAfter`, `CanStart`, `QuickMatchAsync`.
- `Assets/TuitionTakkar/Scripts/Online/OnlineMatch.cs` — match start/result,
  `MaxPlayers`, `FinishParty`.
- `Assets/TuitionTakkar/Scripts/UI/OnlineScreens.cs` — `DoQuick`, `Busy(...)`,
  `ShowRoom`, the room/lobby screen.
- `Assets/TuitionTakkar/Scripts/Gameplay/MatchController.cs` — `IsParty`,
  `ResolveParty`, `PartyAutoShot`, `RemovePartyPlayer`.
- `Assets/TuitionTakkar/Scripts/AI/BotBrain.cs` — existing bot shot
  simulation, used today by offline Ladder/Practice.
- `Assets/TuitionTakkar/Scripts/Core/GameData.cs` — `BotProfile`,
  `Catalog.Rivals` (10-step skill curve), `Catalog.PracticeBots` (3-tier).
- `Assets/TuitionTakkar/Scripts/Core/Desks.cs` — `DeskTier`, `DeskDef`, the
  28 desks.
