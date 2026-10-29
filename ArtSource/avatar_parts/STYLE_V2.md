# Tuition Takkar avatars: style v2 (cel-shaded cartoon)

This set replaces the doodle style. Canvas, anchors, layer order, file names and manifest fields are the same as the brief. One part = one SVG file.

## What changed from the brief

| Rule | v1 doodle | v2 cartoon |
|---|---|---|
| Outline | `7` / `5` / `3.5` | `4.5` outline, `3` inner detail, `2` tiny marks |
| Shading | none | flat cel shadows (no gradients), light from the upper left |
| Eyes | ink dots / ovals | big almond eyes with a coloured iris |
| Extra attributes | `fill-opacity` on blush | `fill-opacity` also on glasses lenses, stubble and freckles; `fill-rule="evenodd"` on the hijab |

## Placeholder colours (swap at runtime)

| Placeholder | Hex | Replace with |
|---|---|---|
| SKIN | `#00FF00` | chosen skin tone |
| SKIN_SHADE | `#00B000` | skin tone × 0.84 |
| HAIR | `#FF00FF` | chosen hair colour |
| HAIR_SHADE | `#B000B0` | hair colour × 0.70 (use `#3A3A3A` for black hair so the shade still shows) |
| CLOTH | `#00FFFF` | chosen clothing colour |
| CLOTH_SHADE | `#00B0B0` | clothing colour × 0.80 |
| IRIS | `#0000FF` | chosen eye colour (new option: browns, hazel, blue, green, grey) |

"× 0.84" means multiply each RGB channel by 0.84. Swap the SHADE placeholders before the base placeholders, or match the full hex string, so `#00B000` is never touched by a partial replace.

Fixed colours stay as in the brief: ink `#1B1B1B`, white `#FFFFFF`, red `#E5483A`, blush `#F4A6A0`.

## Tiers

The brief's "must be free" rule (at least 6 free of each required layer) overrides the per-layer splits, so all brows, noses and tops are free, plus 6 eyes and 6 mouths. The afro is free; spiky hair takes the rare slot. The turban and hijab both have `hidesHair: true`. Parts are not tagged by gender: anyone can use any part.
