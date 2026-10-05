# Sounds

Work list for the game's sound effects: what each sound is, when it plays, a Bfxr generator to start from, and
where its `SoundDef` goes in the hierarchy.

## How it is wired

- Every sound is an optional `SoundDef` reference on the component that triggers it, played with
  `sound?.Play()`. Leave a field empty and that sound simply does not play; nothing else changes.
- To make a `SoundDef`: import the clip, select it, then `Assets > Unity Common Tools > Create SoundDef From
  Selection`. The music lives in `Assets/Sound`, so that is the natural place for the clips and their SoundDefs.
- Variety comes from the SoundDef, not from code: a pitch range, a volume range, or `Mode = Multiple` with several
  clips. The notes below say where that is worth using.
- The same SoundDef can be assigned to several fields (the denied sound, for example).
- The `SoundManager` lives in the `MainMenu` scene and survives into `GameScene`. Starting `GameScene` directly
  in the editor is silent unless a `SoundManager` is added there too.
- Nothing is assigned yet: the code is in, every field is empty.

Paths below are in `GameScene` unless they say otherwise. They read `object > component > field`.

## Shared across stations (make these first)

The five station panels are `Canvas/MG_Concept` (ConceptMG), `Canvas/MG_Modeling` (ModellingMG),
`Canvas/MG_Texturing` (PaintingMG), `Canvas/MG_Coding` (CodingMG) and `Canvas/MG_Marketing` (MarketingMG). Each
has its own copy of the shared fields, so a station can have its own version of a sound.

| Sound | When | Bfxr start | Assign to |
|---|---|---|---|
| UI click | Menu options, colour swatches, brush buttons | Blip/Select | `Canvas/MG_Texturing > PaintingMG > Select Sound`<br>`MainMenu` scene: `Canvas/Menu > MainMenu > Click Sound` |
| Station open | The panel fades in when you stop at a station | Blip/Select, softer and lower | Each `Canvas/MG_* > Open Sound` |
| Prompt card | "Draw", "Model", "Paint", "Code" card appears | Powerup, very short | Each `Canvas/MG_* > Prompt Sound` |
| Submit | Work handed in at a station | Powerup | Each `Canvas/MG_* > Submit Sound` |
| Star gained | Live star meters and the results screen; pitched up per star | Pickup/Coin | Prefab `Assets/Prefabs/StarMeter.prefab > LaunchRow > Star Gained Sound` (live meters)<br>Prefab `Assets/Prefabs/LaunchRow.prefab > LaunchRow > Star Gained Sound` (results rows) |
| Star lost | A live meter drops a star | Hit/Hurt, short and quiet | Prefab `Assets/Prefabs/StarMeter.prefab > LaunchRow > Star Lost Sound` |
| Denied | Segment budget exceeded, wrong click, anything refused | Hit/Hurt, low square wave | `Canvas/MG_Modeling > ModellingMG > Denied Sound`<br>Reuse it for `Wrong Post Sound` and as the base of `Pitch Rejected Sound` |
| Clock warning | Last few seconds of a station timer or the day timer, one tick a second | Blip/Select | `Canvas/MG_Texturing > PaintingMG > Clock Warning Sound`<br>`Canvas/MG_Coding > CodingMG > Clock Warning Sound`<br>`GameManager > LevelManager > Clock Warning Sound` (the day) |
| Time up | A station timer runs out | Laser/Shoot with a downward slide | `Canvas/MG_Texturing > PaintingMG > Time Up Sound`<br>`Canvas/MG_Coding > CodingMG > Time Up Sound` |

Notes:

- The first time a station is used each day the prompt card shows, and its sound plays instead of the open
  sound. The concept station is the exception: it opens on the concept art (open sound), and the card follows
  two seconds later (prompt sound).
- When a timer runs out the time up sound plays instead of the submit sound.
- `Star Pitch Step` on `LaunchRow` is the pitch added per star along the row (0.1 by default).
- `Clock Warning Time` next to each clock warning field is how many seconds before the end the ticking starts
  (5 for the stations, 10 for the day).

## Brainstorm and the boss

| Sound | When | Bfxr start | Assign to |
|---|---|---|---|
| Idea out | A machine sends out a new idea | Jump | Prefab `Assets/Prefabs/IdeaMachine.prefab > IdeaMachine > Idea Out Sound` (covers `NounMachine`, `StyleMachine`, `GimmickMachine`; override on an instance for a different one) |
| Grab idea | Picking one up | Pickup/Coin | `Player > Player > Grab Sound` |
| Place idea | Dropping it in a pitch slot | Blip/Select, lower | `Environment/MeetingTable/NounDropArea > DropArea > Drop Sound`<br>`Environment/MeetingTable/StyleDropArea > DropArea > Drop Sound`<br>`Environment/MeetingTable/GimmickDropArea > DropArea > Drop Sound` |
| Trash idea | Dropping it in the trash | Explosion, very short | `Trash > DropArea > Drop Sound` |
| Boss blips | A burst of blips each time the boss says a line | Blip/Select, low | `GameManager > LevelManager > Boss Talk Sound`<br>`MainMenu` scene: `isometric_office/character-q > BossBabble > Talk Sound` |
| Pitch accepted | With the boss's verdict line, when he likes the pitch | Powerup | `GameManager > LevelManager > Pitch Accepted Sound` |
| Pitch rejected | With the verdict line, when it is rubbish (also when he gives up and picks the concept himself) | The denied sound, longer | `GameManager > LevelManager > Pitch Rejected Sound` |
| Intercom | The boss camera comes on (briefing, verdict, firing) | Two Blip/Select notes | `GameManager > LevelManager > Intercom Sound` |

Notes:

- The machines are silent for the idea already there when the scene starts.
- Swapping ideas at a pitch slot plays the place and grab sounds together.
- Boss blips: one blip for every three characters of the line, at most 14, 0.07 s apart. Give the SoundDef a few
  clips (`Mode = Multiple`) or a pitch range so they do not sound like a machine gun.

## Stations

| Sound | When | Bfxr start | Assign to |
|---|---|---|---|
| Vertex placed | Modelling, each click | Blip/Select | `Canvas/MG_Modeling > ModellingMG > Vertex Sound` |
| Polygon closed | Modelling, snapping back to the first vertex | Pickup/Coin | `Canvas/MG_Modeling > ModellingMG > Polygon Closed Sound` |
| Polygon removed | Modelling, cancel or delete | Hit/Hurt, soft | `Canvas/MG_Modeling > ModellingMG > Polygon Removed Sound` |
| Area claimed | Painting, an area passes the threshold | Pickup/Coin | `Canvas/MG_Texturing > PaintingMG > Area Claimed Sound` |
| Key right | Coding, correct character | Blip/Select, a few milliseconds | `Canvas/MG_Coding > CodingMG > Key Right Sound` |
| Key wrong | Coding, wrong character | Hit/Hurt, short | `Canvas/MG_Coding > CodingMG > Key Wrong Sound` |
| Backspace | Coding | Blip/Select, lower | `Canvas/MG_Coding > CodingMG > Backspace Sound` |
| Code compiled | Coding, snippet submitted with the rising characters | Powerup with vibrato | `Canvas/MG_Coding > CodingMG > Submit Sound` |
| Wave in | Marketing, a new wave of posts appears | Jump | `Canvas/MG_Marketing > MarketingMG > Wave In Sound` |
| Post deleted | Marketing, a negative post removed | Laser/Shoot, short zap | `Canvas/MG_Marketing > MarketingMG > Post Deleted Sound` |
| Wrong post | Marketing, a positive post removed | The denied sound | `Canvas/MG_Marketing > MarketingMG > Wrong Post Sound` |
| Wave out | Marketing, the wave's time is up and the survivors vanish | Laser/Shoot, soft downward slide | `Canvas/MG_Marketing > MarketingMG > Wave Out Sound` |

Notes:

- Key right: give the SoundDef a small pitch range, it plays on every keystroke. A Tab press plays it once,
  however many spaces it stood for.
- Backspace repeats while the key is held (every 0.04 s), so keep it very short.

## Skinotron, release and launch

| Sound | When | Bfxr start | Assign to |
|---|---|---|---|
| Skinotron whirr | The camera cuts to the machine | Powerup, long, with vibrato | `GameManager > LevelManager > Skinotron Sound` |
| Skin pop | The skin grows in or changes texture | Jump | `Skinotron3000 > SkinDisplay > Appear Sound` |
| Lever charge | Holding the lever; the pitch follows the pull | Laser/Shoot, long sustain, upward slide | `LaunchStation > ReleaseStation > Lever Charge Sound` |
| Lever drop | Letting go early | The same, sliding down | `LaunchStation > ReleaseStation > Lever Drop Sound` |
| Launch | The lever reaches the on position | Explosion plus Powerup | `LaunchStation > ReleaseStation > Launch Sound` (the Explosion)<br>`LaunchStation > ReleaseStation > Launch Rise Sound` (the Powerup)<br>Chained, see below |
| Profit tick | Profit counting up; the pitch rises as it climbs | Pickup/Coin, very short | `Canvas/Launch > LaunchResults > Profit Tick Sound` |
| Target passed | The target marker pops | Powerup, bright | `Canvas/Launch > LaunchResults > Target Passed Sound` |
| Day won | The count ends at or over the target | Powerup, longer jingle | `Canvas/Launch > LaunchResults > Day Won Sound` |
| Day lost | The count ends short of the target | Laser/Shoot, slow downward slide | `Canvas/Launch > LaunchResults > Day Lost Sound` |
| Day wipe | Transition to the next day | Explosion, noise only, long decay | `GameManager > LevelManager > Day Wipe Sound` |
| Fired | Game over, as the panel comes up | Explosion, low, then the day-lost sound | `GameManager > LevelManager > Fired Sound` (the Explosion)<br>`GameManager > LevelManager > Fired Tail Sound` (the day-lost sound)<br>Chained, see below |

Notes:

- Lever charge: the code multiplies its pitch from 1 up to `Lever Charge Max Pitch` (2 by default) as the lever
  moves, and cuts it when the key is let go or the lever locks. The pull takes `Hold Time` (2 s in the scene),
  so either make the clip at least that long or tick `Loop` on the SoundDef.
- Profit tick: retriggered every `Profit Tick Interval` (0.06 s) while the profit counts up (`Profit Duration`,
  3 s in the scene), its pitch going from 1 to `Profit Tick Max Pitch` (1.6). Keep the clip shorter than the
  interval.

## Chained sounds

Two effects are made of two SoundDefs. They only play when both fields are assigned; with one missing, neither
plays.

| Effect | First | Second | How they combine |
|---|---|---|---|
| Launch | `Launch Sound` | `Launch Rise Sound` | Both start together |
| Fired | `Fired Sound` | `Fired Tail Sound` | The tail starts when the first clip ends |

Everything else is independent: assigning one never requires another.

## Not wired

To do by hand later. Bfxr only makes one-shots, so these are better recorded or built as tileable loops.

- Pencil stroke (concept station): a loop while the pointer is down. `ConceptMG.Update` knows when a stroke
  starts and ends (`wasPressed`).
- Brush stroke (painting station): the same, in `PaintingMG.UpdateBrush` (`strokeColor`).
- Footsteps: two or three taps, from the player's movement.

## Checklist by object

The same fields, grouped the way they appear in the editor.

`GameScene`

- [ ] `GameManager` (LevelManager): Intercom Sound, Boss Talk Sound, Pitch Accepted Sound, Pitch Rejected
      Sound, Skinotron Sound, Day Wipe Sound, Clock Warning Sound, Fired Sound + Fired Tail Sound
- [ ] `Player` (Player): Grab Sound
- [ ] `Environment/MeetingTable/NounDropArea`, `StyleDropArea`, `GimmickDropArea` (DropArea): Drop Sound
- [ ] `Trash` (DropArea): Drop Sound
- [ ] `Skinotron3000` (SkinDisplay): Appear Sound
- [ ] `LaunchStation` (ReleaseStation): Lever Charge Sound, Lever Drop Sound, Launch Sound + Launch Rise Sound
- [ ] `Canvas/Launch` (LaunchResults): Profit Tick Sound, Target Passed Sound, Day Won Sound, Day Lost Sound
- [ ] `Canvas/MG_Concept` (ConceptMG): Open Sound, Prompt Sound, Submit Sound
- [ ] `Canvas/MG_Modeling` (ModellingMG): Open Sound, Prompt Sound, Submit Sound, Vertex Sound, Polygon Closed
      Sound, Polygon Removed Sound, Denied Sound
- [ ] `Canvas/MG_Texturing` (PaintingMG): Open Sound, Prompt Sound, Submit Sound, Select Sound, Area Claimed
      Sound, Clock Warning Sound, Time Up Sound
- [ ] `Canvas/MG_Coding` (CodingMG): Open Sound, Prompt Sound, Submit Sound (code compiled), Key Right Sound, Key
      Wrong Sound, Backspace Sound, Clock Warning Sound, Time Up Sound
- [ ] `Canvas/MG_Marketing` (MarketingMG): Open Sound, Prompt Sound, Submit Sound, Wave In Sound, Wave Out
      Sound, Post Deleted Sound, Wrong Post Sound

Prefabs

- [ ] `Assets/Prefabs/IdeaMachine.prefab` (IdeaMachine): Idea Out Sound
- [ ] `Assets/Prefabs/StarMeter.prefab` (LaunchRow): Star Gained Sound, Star Lost Sound
- [ ] `Assets/Prefabs/LaunchRow.prefab` (LaunchRow): Star Gained Sound

`MainMenu` scene

- [ ] `Canvas/Menu` (MainMenu): Click Sound
- [ ] `isometric_office/character-q` (BossBabble): Talk Sound
