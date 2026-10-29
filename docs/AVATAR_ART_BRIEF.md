# Tuition Takkar: scribble face avatar art brief

This brief describes every drawing needed for the player avatars in **Tuition Takkar**, a pen-fight game for kids and teens aged 10–17. Each face is built by stacking separate parts (face shape, hair, eyes, mouth and so on), so every part must follow the same canvas, positions and colour rules below. If a part follows these rules, the game can use it with no extra work.

> **Style reference:** hand-drawn "doodle avatar" faces with thick black ink lines, white fill, and one red accent colour (earrings, headband, stripes, lips, blush). It should look drawn with a felt-tip pen on paper: friendly, a little wobbly, never too perfect.
>
> The art must be **our own original drawings.** Don't copy or trace any existing avatar pack (Freepik etc.). Use them only for the general feel.

---

## 1. File format

| Rule | Value |
|---|---|
| Format | **SVG**, one file per part |
| Canvas | `viewBox="0 0 512 512"`, square, no `width`/`height` in px |
| Background | Transparent (except the `bg_` layer) |
| Allowed | `<path>`, `<circle>`, `<ellipse>`, `<rect>`, `<polygon>`, `<g>` |
| Not allowed | gradients, filters, blur, masks, clip-paths, `<text>`, `<image>`, embedded bitmaps, CSS `<style>` blocks, transforms on the root |
| Line style | `stroke-linecap="round"`, `stroke-linejoin="round"` |
| Outline width | `stroke-width="7"` for outlines, `5` for inner details, `3.5` for tiny marks |
| File size | under 15 KB each; keep paths simple |

Lines should be slightly irregular, like a real pen: little bumps, uneven thickness made with two overlapping strokes, small gaps. No perfect circles for heads.

---

## 2. Colours

Use **only** these colours. The three "magic" placeholder colours are swapped by the game at runtime, which is how one drawing comes in many colours.

| Use | Colour | Notes |
|---|---|---|
| Ink (all lines) | `#1B1B1B` | |
| White fill | `#FFFFFF` | eye whites, collars, paper-white areas |
| Accent red | `#E5483A` | earrings, headbands, stripes, lips, details |
| Blush | `#F4A6A0` | cheeks only, `fill-opacity="0.7"` |
| **SKIN placeholder** | `#00FF00` | every skin area (face, ears, neck, hands). The game replaces it with the chosen skin tone. |
| **HAIR placeholder** | `#FF00FF` | every hair, eyebrow and beard fill. The game replaces it with the chosen hair colour. |
| **CLOTH placeholder** | `#00FFFF` | the main colour of tops and hats. The game replaces it with the chosen clothing colour. |

Skin tones the game will offer (for reference, don't use them in the files): `#FBE3D0`, `#F2C9A5`, `#E0A97E`, `#C68863`, `#9C6644`, `#6B4430`.

Hair colours: `#1B1B1B`, `#4A2F23`, `#8A5A3B`, `#D9A441`, `#B5482F`, `#8E8E8E`, plus fun ones `#3D7BD9`, `#D94F9C`, `#3FAF6C` (paid).

---

## 3. Where things go on the canvas (anchors)

All parts share these positions so they line up when stacked. Coordinates are in the 512×512 viewBox.

| Feature | Position |
|---|---|
| Top of head (bald) | y ≈ 110 |
| Hairline | y ≈ 150 |
| Head centre | (256, 240) |
| Head width / height | about 220 wide × 260 tall (x 146–366, y 110–370) |
| Eyebrows | centre y ≈ 200, left x ≈ 212, right x ≈ 300 |
| Eyes | centre y ≈ 232, left eye x = 212, right eye x = 300, each about 36 wide |
| Ears | left x ≈ 146, right x ≈ 366, y 220–275 |
| Nose | centre (256, 270), about 30 wide × 45 tall |
| Cheeks (blush) | left (196, 285), right (316, 285) |
| Mouth | centre (256, 318), about 60 wide |
| Chin | y ≈ 365 |
| Neck | x 226–286, y 355–410 |
| Shoulders and top | y 405–512, full width at the bottom |
| Glasses | lenses centred on the eyes, bridge at (256, 232) |
| Earrings | hang from the ear lobes at (146, 275) and (366, 275) |
| Hats | sit on top of the head, bottom edge at the hairline y ≈ 150 |

**Tip:** draw the master template first (section 6), then draw every part on a separate layer over it and export each layer alone.

---

## 4. Layers (back to front)

The game stacks the layers in this order, so a later layer covers an earlier one.

| # | Layer prefix | What it is | Required? |
|---|---|---|---|
| 1 | `bg_` | Square background (solid fill, optional doodle pattern) | yes (1 per avatar) |
| 2 | `hairback_` | Hair that falls **behind** the head and shoulders (long hair, curls) | optional, pairs with `hair_` |
| 3 | `top_` | Neck and clothes (shoulders to bottom edge) | yes |
| 4 | `face_` | Head shape, including ears and skin fill | yes |
| 5 | `cheeks_` | Blush, freckles, dimples, beauty spot | optional |
| 6 | `eyes_` | Both eyes | yes |
| 7 | `brows_` | Both eyebrows | yes |
| 8 | `nose_` | Nose | yes |
| 9 | `mouth_` | Mouth, lips, teeth | yes |
| 10 | `beard_` | Beard, moustache, stubble | optional |
| 11 | `glasses_` | Glasses, sunglasses | optional |
| 12 | `earrings_` | Earrings, studs | optional |
| 13 | `hair_` | Hair on top of the head, in front of the face edge | optional (bald / short) |
| 14 | `hat_` | Hats, headbands, caps, turbans, hijabs, crowns | optional |

**Hair rule:** a hairstyle can have two files with the same number: `hairback_07.svg` (behind) and `hair_07.svg` (in front). If a style has no back part, leave the back file out.

**Hijab and full head coverings:** these go in `hat_` and must fully hide the hair line. The game hides `hair_` and `hairback_` when one is worn. Mark these with `"hidesHair": true` in the manifest.

---

## 5. File names and manifest

- File name: `<layer>_<two-digit number>.svg`, e.g. `eyes_03.svg`, `hair_12.svg`, `hairback_12.svg`. Numbers start at `01`. `00` means "none" for optional layers, and needs no file.
- Put all files in one folder: `avatar_parts/`.
- Add a `manifest.json` describing every part:

```json
{
  "version": 1,
  "parts": [
    { "id": "eyes_03", "layer": "eyes", "name": "Sleepy", "tier": "free", "price": 0 },
    { "id": "hair_12", "layer": "hair", "name": "Big curls", "tier": "rare", "price": 1000, "hasBack": true },
    { "id": "hat_05", "layer": "hat", "name": "Hijab", "tier": "free", "price": 0, "hidesHair": true },
    { "id": "hat_20", "layer": "hat", "name": "Champion crown", "tier": "earned", "price": 0, "unlock": "beat batch 5 boss" }
  ]
}
```

**Tiers and prices (marks):**

| Tier | Price | Share of parts |
|---|---|---|
| `free` | 0 | about 40% |
| `common` | 200 | about 30% |
| `rare` | 1,000 | about 18% |
| `epic` | 5,000 | about 7% |
| `earned` | not for sale; unlocked by playing | about 5% |

**Must be free:** every face shape, every skin tone, at least 6 of each required layer (eyes, brows, nose, mouth, top), and every religious or cultural item (turban, hijab, bindi, kippah and so on). Nobody should have to pay to be represented.

---

## 6. Master template (draw this first)

`template_master.svg`: one complete neutral face using all the anchors in section 3, with simple oval eyes, a curved line nose, a small smile, short hair, and a plain round-neck top. Every other part is drawn on top of it so everything lines up. Also include `template_guides.svg` showing the anchor lines and points (not used in the game).

---

## 7. Parts list

### Step 1: starter set (60 parts), draw these first

| Layer | Count | Ideas (number them 01, 02, …) | Free / paid |
|---|---|---|---|
| `face_` | 5 | round, oval, square jaw, heart, long | all free |
| `eyes_` | 8 | dot eyes, oval with pupil, sleepy half-closed, happy closed arcs, big round, wink, side-glance, starry | 5 free, 3 common |
| `brows_` | 6 | thin arc, thick straight, angry slant, raised, bushy, unibrow | 4 free, 2 common |
| `nose_` | 5 | L-shape line, small curve, round button, long line, nostrils only | all free |
| `mouth_` | 8 | small smile, big grin with teeth, red lips, open "O", tongue out, smirk, flat line, braces | 5 free, 3 common |
| `cheeks_` | 3 | blush dots, freckles, scribble blush | 2 free, 1 common |
| `hair_` + `hairback_` | 10 styles | short crop, side part, spiky, afro (back+front), bun with red band, bob with fringe, long wavy (back+front), curly short, ponytail (back+front), buzz cut | 6 free, 3 common, 1 rare |
| `beard_` | 3 | thin moustache, curly moustache, stubble | 2 free, 1 common |
| `glasses_` | 3 | round, square, sunglasses | 2 free, 1 rare |
| `earrings_` | 2 | red studs, red hoops | 1 free, 1 common |
| `top_` | 6 | striped turtleneck (red stripes), collar shirt, hoodie with strings, polka-dot round neck, school uniform with tie, black turtleneck | 4 free, 2 common |
| `hat_` | 4 | red headband, cap, turban, hijab | 3 free (turban and hijab free), 1 common |
| `bg_` | 2 | plain (CLOTH colour), doodle pattern | 1 free, 1 common |
| **Total** | **≈ 60** (counting hair back+front as one style) | | |

### Step 4: full set (about 200 parts), later

| Layer | Target | More ideas |
|---|---|---|
| `face_` | 6 | + chubby round |
| `eyes_` | 15 | + crying, sparkle, angry, nerd, heart eyes (epic), laser eyes (epic) |
| `brows_` | 10 | + worried, scar brow, thin high |
| `nose_` | 8 | + pointy, wide, nose ring (common) |
| `mouth_` | 15 | + shocked, gap tooth, bubblegum bubble (rare), vampire teeth (epic) |
| `cheeks_` | 6 | + band-aid, ink smudge, beauty spot |
| `hair_` styles | 40 | + mohawk, braids, two buns, mullet, long straight, curtain bangs, dyed tips, flat top… |
| `beard_` | 10 | + full beard, goatee, handlebar (rare) |
| `glasses_` | 12 | + cat-eye, broken taped glasses, 3D glasses (rare), monocle (epic) |
| `earrings_` | 8 | + stars, pencils (rare), feathers |
| `top_` | 30 | + kurta, jersey, blazer, scarf (red stripes), sweater vest, superhero cape (epic), exam-hall badge… |
| `hat_` | 20 | + beanie, graduation cap (earned), headphones, party hat, bandana, crown (earned), pencil behind ear (rare) |
| `bg_` | 12 | + chalkboard, notebook lines, graph paper, stars, desk wood |

**Earned-only ideas:** Champion crown (beat all batches), Topper glasses (gold badge), Golden pen earring (win 100 online), Graduation cap (finish batch 1).

---

## 8. Do and don't

**Do**
- Keep lines thick and confident; it must read clearly at 40×40 px (the smallest size in the game).
- Leave the area outside a part fully transparent.
- Keep both eyes, both brows and both earrings in one file.
- Make faces friendly and fun: kids should want to look like them.

**Don't**
- No logos, brands, team badges, real people, cartoon characters or anything copied.
- No weapons, gang signs, flags, political symbols, skulls with blood, cigarettes or alcohol.
- No body or face features that mock any group of people.
- No text in the drawings.

---

## 9. Sample faces to check everything lines up

Make a preview sheet (`preview.png`, 4×3 grid) of these 12 combinations:

1. face_01, eyes_02, brows_01, nose_01, mouth_01, hair_01, top_01, bg_01
2. face_02, eyes_03, brows_02, nose_02, mouth_03, cheeks_01, hair_05 (bun), earrings_01, top_04
3. face_03, eyes_01, brows_04, nose_04, mouth_06, beard_01, hair_03, top_06
4. face_01, eyes_05, brows_01, nose_03, mouth_02, hair_04 + hairback_04 (afro), top_03
5. face_04, eyes_04, brows_03, nose_01, mouth_04, cheeks_02, hair_06, glasses_01, top_05
6. face_02, eyes_06, brows_05, nose_05, mouth_05, hair_09 + hairback_09 (ponytail), hat_01 (headband), top_02
7. face_05, eyes_02, brows_02, nose_02, mouth_07, beard_02, hair_10, hat_03 (turban), top_02
8. face_02, eyes_07, brows_01, nose_03, mouth_03, cheeks_01, hat_04 (hijab), top_04
9. face_03, eyes_08, brows_06, nose_01, mouth_08 (braces), hair_08, glasses_02, top_05
10. face_01, eyes_03, brows_04, nose_02, mouth_01, hair_07 + hairback_07 (long wavy), earrings_02, top_01, bg_02
11. face_04, eyes_01, brows_02, nose_04, mouth_02, beard_03, hair_02, glasses_03 (sunglasses), hat_02 (cap), top_03
12. face_05, eyes_05, brows_03, nose_05, mouth_06, cheeks_03, hair_06, top_06, bg_02

Use different skin tones, hair colours and cloth colours across the 12, so it's clear the placeholders work.

---

## 10. What to deliver

A zip containing:

```
avatar_parts/
  template_master.svg
  template_guides.svg
  face_01.svg … bg_02.svg      (all parts)
  manifest.json
  preview.png                  (the 12 sample faces)
```

Send it back to the Tuition Takkar project. It will go in `Assets/TuitionTakkar/Art/Avatar/`, and the game side (editor, shop, saving, showing faces everywhere, app and web) is built from there.

---

## 11. Prompt to paste into another Claude account

> I'm making avatar art for my mobile game. Please read the attached AVATAR_ART_BRIEF.md and follow it exactly. Start with section 6 (the master template), show it to me, then draw the Step 1 starter set from section 7, one layer at a time, as SVG files that follow sections 1–5. Use only the colours in section 2 (including the three placeholder colours) and the anchor positions in section 3. Then make manifest.json and a preview page with the 12 sample faces from section 9, with the placeholder colours swapped for real skin, hair and cloth colours so I can check them. At the end, give me everything as one zip in the folder layout from section 10.
