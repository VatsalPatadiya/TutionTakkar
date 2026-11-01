# Tuition Takkar: pending tasks and handover

_Written 30 Oct 2026 for continuing this project in another Claude account._

Read this first, then `docs/PROJECT_GUIDE.md` for the full picture of the game, code map, menus, servers and decisions. The avatar art rules are in `docs/AVATAR_ART_BRIEF.md`.

---

## 0. Where things stand right now

| Area | State |
|---|---|
| Phone app (Android) | Release APK with the **kid-friendly UI pass** is installed on the owner's phone. Code = git `main`. |
| UI improvements | Done and committed (b1a091d). Home, ladder/rival, pouch, play online, leaderboard podium, profile tabs, badges, sign-in choice and match result were all improved. Before/after images are on the design canvas "Tuition Takkar · All screens + Web" (owner's first Claude account). |
| Web version | Live on Firebase Hosting: `https://penfighter-6be8b.web.app/play/?code=ABC123` (Unity Web build, **Unity's standard page**, guests only) and `/join/ABC123` (new invite page with PLAY IN BROWSER). A browser player and the phone app played a real match together. |
| Web Google sign-in | **Built, then rolled back at the owner's request.** The owner wants Unity's standard page with no layout changes. The files are kept in `Builds/_removed-web-signin/`, and commit 8cc0765 has the code. |
| Web cache crash | "Maximum call stack size exceeded" came from browsers mixing old and new cached files. Fixed: hashed file names every build, `no-cache` on HTML in `LinkSite/firebase.json` (a86ea89). |
| Avatar art | Starter set of **63 SVG parts** in `ArtSource/avatar_parts/` (style v2: cel-shaded cartoon, see `STYLE_V2.md`), with `manifest.json`, templates and `preview.png`. **Not used in the game yet.** |
| Git | All work is committed on `main` on the owner's Mac. **Run `git push` before working from another machine or session.** |

### Working setup (owner's Mac)

- Project: `~/Downloads/TutionTakkar`, Unity **6000.5.9f1**, Android target.
- UI is **UI Toolkit built in code**:
  - `UIKit` helpers: Btn, Disp, Strong, HandText, Plain, Avatar, Pad, Radius, Border, Rot, `.St()`
  - `Pal` colours, and `LineIcon` (Painter2D icons)
- Useful menus under **Tuition Takkar**:
  - **Build Release APK and Install:** needs the keystore passwords typed by the owner in Project Settings > Player > Android > Publishing Settings after every Unity restart. Never type or store them.
  - **Build Web Version:** builds into `LinkSite/public/play` (git-ignored).
  - **Dev: Save improved-UI screenshots / Dev: Result screen shots (v2) / Dev: Save doc screenshots:** capture every screen.
  - Dev menus for popups, desks, back gesture and so on (see PROJECT_GUIDE section 12).
- **Deploy the web:** the owner runs `firebase deploy --only hosting` from `LinkSite/` in Terminal. They're already logged in to Firebase.
- **Cloud Code params** must be declared via **Tuition Takkar > Set Cloud Code Parameters**, because there's no Node on the Mac. FCM data key `from` is reserved, so use `fromName`.
- **Rules from the owner:**
  - commit straight to `main`
  - never put secrets, keys or passwords in files or chat
  - ask before sending notifications to real users
  - show UI changes as mockups or before/after first, and don't change the UI style without approval
  - keep explanations simple

### If the new account can't control the Mac

A cloud session (claude.ai/code on the GitHub repo) can **write code** but **can't run Unity**: no compile, no screenshots, no APK or web build. The owner must then `git pull` on the Mac and compile, build and test there, or use a session linked to the Mac through the Claude desktop app.

---

## 1. The owner's 9-point list (29 Oct)

Suggested order: **7 → 8 → 9 → 2 → 1 → 6 → avatars (3) → 4 → 5.**

### 7. Change the settings icon (small)
- **Now:** the settings button on Home looks like a sun.
- **Do:** replace it with a clear **gear** icon in the `LineIcon` line style (add `LineIcon.Kind.Gear` drawing if needed; `Gear` already exists in the enum, so check how it looks). Same size and placement.
- **Done when:** a kid instantly reads it as "settings". Check it on Home (guest and signed in) and anywhere else the settings button appears.

### 8. Haptics while stretching the pen (small)
- **Do:** while the player pulls back to aim, give small vibration ticks:
  - light and sparse at low power, closer together and stronger as power rises
  - a firmer bump when hitting max power
  - nothing while holding still
- **Where:** `Scripts/Feel/Haptics.cs` (Android vibration, already guarded with `UNITY_ANDROID`) and the aim/drag code in the gameplay input. Space ticks by power steps (e.g. every 10%), not every frame, and limit them to roughly 25 ms apart at most.
- **Web:** `navigator.vibrate` works on Android browsers only (iPhone Safari has no vibration). If done for web, use a tiny `.jslib` behind `#if UNITY_WEBGL`.
- **Respect the haptics on/off setting** if there is one.

### 9. Base URL shows the old welcome screen (small)
- **Now:** `https://penfighter-6be8b.web.app/` shows the old placeholder (`LinkSite/public/index.html`).
- **Do:** make it the **web home** from the mockup "Web · home": logo, **PLAY IN BROWSER** (to `/play/`), **Join with code** (an input that opens `/play/?code=XXXXXX`), and **Get the app** (Play Store link `https://play.google.com/store/apps/details?id=com.unitygame.tuitiontakkar`).
  - Match `join.html`'s look: Lilita One, Nunito and Kalam fonts, sage background, dark header, yellow buttons.
  - The simpler fallback is redirecting `/` to `/play/`. Ask the owner which one.
- **Deploy:** the owner runs `firebase deploy --only hosting`.

### 2. Quick match: no player found → play a bot (medium)
- **Now:** quick match waits for real players. The host may start with fewer after `OnlineRoom.QuickStartAfter` (45 s), but with only one player nothing starts.
- **Do:** after about **20–25 s** with no opponent, start against a **bot** whose difficulty matches the **desk level**: easy on the first desks, harder on expensive ones. Reuse the existing ladder/practice bot (`MatchController` `BotTurn`, `setup.isBot`).
- **Decided:** the bot is hidden (no tag, looks like a classmate) and the entry fee is not refunded (normal fee and prize).
- **Done when:** a solo player on any desk always gets a match within about 25 s, the bot's difficulty follows the desk, and fees and pots behave as decided. It must work on web too.

### 1. Web quick match flow (medium)
- **Now:** a browser player goes through the app's screens: sign-in choice, Play Online, desk chooser, then searching.
- **Do (web only, behind `#if UNITY_WEBGL`):** a short flow from the mockups "Mobile web · pick a name / lobby / match / result":
  1. Pick a name (and later a face). Guest sign-in happens silently.
  2. Big **QUICK MATCH** with 2/3/4 players, starting on the free/default desk.
  3. Match, then a result screen with **"Get the app to keep your marks"** and REMATCH.
  4. Invite links (`?code=`) skip straight to joining that room.
  5. Invalid or closed code: the "room has closed" screen with **Quick match instead** and a code box.
- Hide app-only parts on web: pouch, shop, workshop, ladder, badges, friends, push.
- **Keep Unity's standard web page.** The owner doesn't want the page layout changed.

### 6. Easier custom pen maker (medium)
- **Now:** the pen workshop (Pouch > MAKE tab) is too complicated for kids.
- **Do:**
  - **3 simple steps** (Body colour → Cap → Grip), each a row of big tappable swatches, with a **big live preview** of the pen at the top.
  - A **🎲 Random** button, plus **ready-made starter pens** to tweak.
  - Remove stat sliders and long text; show stats as small icons only if needed.
  - One big **MAKE IT** button with the marks price.
- **Done when:** a new player can make a pen in about 3 taps without reading. Show a before/after mockup to the owner first.

### 3. Avatars + "doodle art" (large)
- **Plan:** see PROJECT_GUIDE section "Scribble face avatars":
  - faces stacked from layers
  - a random free face seeded by player ID for every player
  - an editor (preview, Randomize, layer tabs, part grid with lock and price, SAVE)
  - paid parts bought with marks
  - the face code saved in the public cloud profile (`ProfileCloud`)
  - shown everywhere `UIKit.Avatar` is used today: home bar, profile, leaderboard and podium, friends, match chalkboard, pop-ups, web
- **Art:** 63 parts in `ArtSource/avatar_parts/`, style v2 cartoon, with placeholder colours for SKIN/HAIR/CLOTH/IRIS and their SHADE versions (see `STYLE_V2.md`).
- **Tech suggestion:** add the **Unity Vector Graphics** package (`com.unity.vectorgraphics`), load each SVG as text, replace placeholder colours (SHADE first), tessellate at runtime, and cache the result as a texture per avatar code. Alternatively, pre-bake PNGs per part and tint in a shader. Test on web too.
- **Open question for the owner:** the point **"Doodle art"** means either:
  - (a) the avatars should be in the **black-ink doodle style** (v1 from the brief), so the cartoon set must be redrawn, or
  - (b) a doodle look for **the whole game**.
  Ask before building.

### 4. Quick match screens like 8 Ball Pool (large)
- **Do, in our own art style (don't copy 8 Ball Pool's art):**
  1. **Desk carousel:** swipeable desk cards showing the entry fee, prize and lock state (extends the current desk chooser).
  2. **Searching screen:** your face, plus slot-machine style spinning faces in the empty seats, with a timer and Cancel.
  3. **VS screen:** both (or all) players' faces, names, levels and countries/badges for about 2 s.
  4. **Pot animation:** coins fly from each player into the pot, then the match starts.
- **Depends on** the avatars (3) for the faces. Show mockups first.

### 5. Table depth for 2/3/4 players (needs clarifying)
- **Ask the owner which one they mean:**
  - (a) **visual depth:** the desk looks flat or thin, so make it thicker, with stronger perspective and better shadows, or
  - (b) **space:** with 3–4 players the desk feels cramped, so make it bigger or longer, or adjust the camera per player count.
- The desk and camera live in the world/desk code (`BenchWorld`, the desk looks in `Desks`). Physics must stay identical on every phone and the web, because online play depends on it.

---

## 2. Other pending work (from PROJECT_GUIDE)

**Testing still owed on phones**
- Friend request and accepted push notifications (app closed and open; the popup should appear again after a decline).
- Room invites both ways.
- Quick match auto-start with 2 phones.
- Opponent profile card (never "guest" for signed-in players).
- Back gesture in an online match.
- A paid desk match end to end (fees, pot, refund).

**Web**
- Optional: bring back **web Google sign-in** only if the owner asks. Add only the sign-in sheet to Unity's standard page. The files are in `Builds/_removed-web-signin/`, and the Cloud Console origins are already added.
- Make the web build smaller (strip unused desks and assets, lighter graphics).
- Custom domain (e.g. `tuitiontakkar.com`). The owner buys it; then update `DeepLinks.Host`, App Links (`assetlinks.json`, `DeepLinkSetup`) and the Firebase Hosting domain.

**Release**
- Play Store: AAB build menu, version code bumps (still 1.0 / code 1), the Play App Signing SHA-1 in Firebase, store listing, privacy policy.
- Apple sign-in and the iPhone build: waiting for the Apple developer account (APNs for push, `TTShare.mm` is already there).
- **Server wallet** (marks, fees and pots are on the phone today, so they can be cheated). Do this before selling anything for real money, including paid avatar parts.
- Remove or hide the Dev menus and test code before release.
- Confirm `total_wins` is set to **Total score** in the Unity Leaderboards dashboard.

**Housekeeping**
- git leaves `tmp_obj_*` and `*.lock.stale-*` files under `.git` (the Mac folder didn't allow deletes). It's harmless; the owner can clean them, or run `git gc`.
- `Builds/ui-backup/` holds the pre-UI-pass originals. Delete it once the new UI is approved for good.

---

## 3. Owner's decisions (1 Nov 2026), no need to ask again

1. **Bot in quick match:** the bot is **hidden**: it looks like a normal classmate, with no "bot" tag. The **entry fee is not refunded**; normal fee and prize. (Revisit if marks are ever sold for real money; disclosure rules may apply.)
2. **"Doodle art":** use the **existing avatar set** in `ArtSource/avatar_parts/` (63 parts, style v2). No redraw.
3. **Table depth:** for **3-4 player matches the desk is longer front-to-back** (more room between pens), and the camera adjusts so the whole desk fits. 2-player stays as it is. Physics must stay identical on every device.
4. **Base web address:** a **full web home page** at `/`, and **every web screen has its own address**, with the browser back button working between them:
   - `/` home
   - `/play/quick` quick match
   - `/play/room/ABC123` room
   - `/play/result` result
   - `/join/ABC123` invite stays as it is

   Use the History API (pushState/popstate) through a small `.jslib`, plus Firebase rewrites so these addresses load the game page. Keep Unity's standard web page as is.

## 4. Prompt to start the new account

> I'm continuing my Unity game Tuition Takkar. Please read `docs/PENDING_TASKS.md` first, then `docs/PROJECT_GUIDE.md`. Follow the owner's decisions in section 3, then work through the 9-point list in the suggested order (7 → 8 → 9 → 2 → 1 → 6 → 3 → 4 → 5). Show me mockups or before/after screenshots before changing any UI, keep the current UI style, commit to `main` after each finished task, and keep explanations simple. Never ask me to paste passwords or keys into chat.
