# Pen versions (V1 / V2 / V3)

Date: 2026-10-30
Status: approved by owner in chat, written up for planning

## Intent and why

The owner wants an easier way for players to get a better pen without the
full Workshop part-by-part build (`WorkshopScreen.cs`, 9 part-category
sections) being the only path. The agreed solution is a version ladder:
pens a player doesn't personally build from parts — classic/catalog pens,
the 600 generated shop pens, and pens won from Batch Ladder rivals — get a
V1 → V2 → V3 progression. Higher versions perform better and look
different, and unlock as the player's existing Online Level rises, rather
than being purchasable outright. Manual Workshop editing stays exactly as
it is today for players who want full control, and is untouched by this
feature.

## Goals

- Every pen a player owns that isn't self-built (classic pens, shop pens,
  rival pens) starts at V1 and can reach V2 and V3.
- Reaching a higher version makes the pen perform better (stats) and
  changes its look.
- V2/V3 are not bought — they unlock as the player's **Online Level**
  rises (the same level that already gates desk access, `OnlineLevel.Need`
  / `OnlineLevel.Level` in `Desks.cs`: level `n` needs `60 + 15n` XP).
  No separate level system.
- No rematch is required to upgrade a rival's pen — once the level
  requirement is met, the upgrade is available regardless of how it was
  obtained.
- Fully custom, self-built Workshop pens (`PenParts.Spec`, the `c.…` id
  pens) are exempt — they keep working exactly as today, no versions.

## Non-goals

- Not changing the Workshop's manual part-building flow at all.
- Not adding a new progression/currency system — this rides entirely on
  the existing Online Level.
- Not retroactively changing pen balance for pens that don't have versions
  (custom pens).

## Confirmed decisions (from the brainstorming conversation)

- Versions apply to: classic catalog pens (`Catalog.Pens`), the 600
  generated shop pens, and pens won from Batch Ladder rivals
  (`Catalog.Rivals[].penId`). Example confirmed explicitly: a shop-bought
  pen like "Blue Comet" (500 marks) also gets V1/V2/V3.
  Versions do **not** apply to custom Workshop-built pens.
  A rival's pen starts at **V1** the moment it's won; a bought pen starts
  at V1 the moment it's bought.
- Upgrading improves performance stats **and** changes the pen's visual
  look (not a stats-only or cosmetic-only change).
- Upgrading does not require beating a rival again, or any other replay —
  just reaching the required Online Level.
- The level gate is the same Online Level used for desk access today —
  explicitly confirmed, not a new or separate level track.
- **Versions only affect bot matches, not real PvP (Claude's call, owner
  asked Claude to decide):** a pen's version changes its stats in Batch
  Ladder, Practice, and bot-filled Quick Match seats. In real human-vs-human
  Desk matches, every player's pen uses its base (V1-equivalent) stats
  regardless of actual unlocked version — the version's look still shows
  cosmetically in PvP, just without the stat change. Reasoning: online
  matches today resolve a pen's stats as one fixed value looked up by pen id
  alone, identical on every device (`OnlineMatch.cs`: `Catalog.Pen(...)` by
  id, no per-player modifier) — this is a deliberate existing invariant that
  keeps every phone's physics simulation bit-identical with no server. Also,
  Desks have a level *floor* but no level ceiling or level-matched pairing,
  so letting versions raise PvP stats would let a high-level player with a
  maxed pen sit at the same paid desk as a player who only just qualifies —
  turning what was pitched as a progression reward into a pay/grind-to-win
  edge in a wagered mode. Keeping the stat effect bot-only avoids both the
  new sync work and that fairness problem; the owner should know this means
  "upgrade your pen" never changes who wins a human-vs-human Desk match.

## Open / left to the builder

The owner confirmed the upgrade is level-gated and does not require a
rematch, but did not explicitly confirm what (if anything) is spent at the
moment of upgrading. Claude's working assumption, stated to the owner and
not objected to: upgrading costs marks, with the cost scaling up per tier
(like a shop price), on top of the level requirement being met. Treat this
as a default to implement, not a hard requirement — if it turns out
marks-cost feels wrong in testing, flag it rather than silently keeping it.

Also left to the builder, since these are tuning/implementation choices
that don't change the player-facing shape of the feature:
- Exact Online Level thresholds for V2 and V3 per pen (could be a flat
  level per tier for all pens, or scale with the pen's price/rarity).
- How a version's stat improvement is computed — e.g. each version maps to
  a better fixed point within the existing power-budget system
  (`PenParts.cs` documents "stats are capped by a power budget, so no pen
  is simply the best" — a V3 pen should still respect that design intent,
  not become strictly dominant).
- How a version's look is generated/stored for pens that aren't built from
  `PenParts.Spec` at all (the classic `PenDef` catalog pens in
  `GameData.cs` have fixed hand-authored colors/shape, not a parts spec) —
  needs some new per-version look data, shape TBD by the builder.
- Data model: where version state lives (per-pen-instance save data vs.
  derived purely from current Online Level + pen id), and how it interacts
  with `SaveData.cs`.
- UI: where in the Pouch/Workshop a player sees and applies an unlocked
  version, and how a locked next-version is previewed/teased.

## Relevant existing code

- `Assets/TuitionTakkar/Scripts/Core/GameData.cs` — `Catalog.Pens`
  (classic `PenDef`s), `Catalog.Rivals` (`RivalDef.penId`).
- `Assets/TuitionTakkar/Scripts/Core/PenParts.cs` — `Spec`, power-budget
  stat system, `Encode`/`TryDecode`, used by shop pens and custom pens.
- `Assets/TuitionTakkar/Scripts/Core/Desks.cs` — `OnlineLevel.Need(level)`
  = `60 + 15 * level`, `OnlineLevel.Level(xp)`.
- `Assets/TuitionTakkar/Scripts/Core/Economy.cs` — marks, prices, prior art
  for how costs are structured elsewhere in the game.
- `Assets/TuitionTakkar/Scripts/UI/WorkshopScreen.cs` — the manual
  part-by-part build screen, untouched by this feature but the natural
  neighbor in the UI.
- `Assets/TuitionTakkar/Scripts/Core/SaveData.cs` — local save state,
  where owned pens and their state currently live.
