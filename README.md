# Tuition Takkar

An offline pen-fight game for phones, built in Unity 6. Flick your pen across a tuition-class bench and knock your batchmate's pen off. Their pen falls and yours stays, and you win the round. If your own pen falls, you lose it. Best of 5.

## Open and play

1. Open **Unity Hub**, choose **Add → Add project from disk**, and pick this `TutionTakkar` folder. Open it with **Unity 6000.5.9f1**.
2. The first import takes a few minutes. A one-time setup then runs by itself: it creates `Assets/Scenes/Main.unity`, two materials, the build settings and portrait orientation. You can re-run it from the menu: **Tuition Takkar → Run Project Setup**.
3. Open `Assets/Scenes/Main.unity` and set the **Game view** to a portrait phone size (for example 1080 × 2340).
4. Press **Play**. Click and drag your pen with the mouse to flick it.

The game builds itself in code when you press Play: the bench, pens, camera, light and UI. The scene only needs a `GameManager`, and even an empty scene works.

## How to play

- **Aim:** press on your pen, pull back, and let go. A longer pull hits harder (full power is about 30% of the screen height).
- **Spin:** grab near the tip or the cap to make the pen spin. Grab the middle for a straight push.
- **Win the round:** knock their pen off the bench and keep yours on.
- **Lose the round:** your pen falls, even if it falls together with theirs.
- A pen hanging over the edge is still in. It only counts once it drops.
- First to 3 rounds wins. The loser of a round shoots first in the next.

## Modes

| Mode | What it is |
|---|---|
| Batch Ladder | 10 bot rivals of rising skill. Beat one to keep their pen and earn marks. The last rival carries the Golden Nib. |
| Practice | Play a bot on Easy, Medium or Hard. No stakes. |
| Pass & Play | Two players on one phone. |
| Pencil Pouch | Your pens. Equip any pen you own. |

## Project layout

```
Assets/
  Resources/TT/Fonts/      Lilita One, Kalam, Nunito (SIL Open Font License)
  Resources/TT/UI/         UI Toolkit theme
  Resources/TT/Materials/  created by the setup script
  TuitionTakkar/
    Scripts/Core/      GameConfig (all tuning), GameData (pens + rivals), SaveData, GameManager, Bootstrap
    Scripts/Gameplay/  BenchWorld, PenBody, PenFactory, FlickInput, AimVisual, CameraRig, MatchController
    Scripts/AI/        BotBrain (simulates shots in a hidden physics scene)
    Scripts/Feel/      GameAudio (synthesised sounds), Haptics
    Scripts/UI/        UIRoot, HudView and one file per screen
    Editor/            ProjectSetup
    Licenses/          font licences
```

## Tuning

Almost every number lives in `Scripts/Core/GameConfig.cs`:

- `ImpulseMax` sets how far a full-power flick sends a pen. Raise it for faster, punchier rounds.
- `DeskWidth` / `DeskDepth` set the bench size. A smaller bench gives shorter rounds.
- `FullPullScreenFraction` and `PowerGamma` control how long the pull is and how power ramps up.
- `TeeterSlowMo` controls the slow motion when a pen hangs over the edge.
- `CameraRig.pitch` (68°) and the camera's field of view (24°, in `BenchWorld`) set how much perspective the desk has. Raise the pitch for a flatter, more top-down look.

Pens and rivals are in `Scripts/Core/GameData.cs`. Each pen has its own size, weight, friction, bounce, spin damping and balance point, and each rival has its own aim error, power error, caution and aggression.

The world runs at 10× real scale (1 unit = 10 cm), with gravity and impulses scaled to match. This keeps tiny pens stable in Unity's physics engine while they still move like real-size pens.

## Notes

- All sounds are generated in code, so there are no audio files to license. You can swap them for real recordings of pens on a desk later.
- Haptics: Android gets short, precise pulses. iOS only buzzes on big moments (a pen hitting the floor, a win) until a native haptics plugin is added.
- Progress is saved in PlayerPrefs.
