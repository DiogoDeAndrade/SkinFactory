# Skin Factory

![Title](Screenshots/logo.png)

Developed for the [Replay European Masters in Games](https://replay.pt/), with the theme "Beyond the Skin".

## Game

Welcome to the skin factory. Not a game studio. A skin-first experience platform.

Every morning the boss tells you what the players want this time. Something cute and scary. Something edgy and premium. Something that trends. You have one day to make it, and the bar for "enough profit" goes up every single day. It is not a question of whether you get fired. It is a question of when.

### A day at the factory

Brainstorm. Grab ideas from the machines, a noun, a style and a gimmick, and pitch the combination to the boss. He will let you know if it is rubbish. After three rubbish pitches he picks the skin himself, and you can guess how that goes.
Concept. Trace the concept art. The closer you are, the better the stars.
Modelling. Rebuild the shape from straight segments, on a budget.
Texturing. Paint the regions with the palette you are given.
Coding. Type the code. Exactly. Backspace is allowed. Mistakes are also allowed, but remembered.
Marketing. Moderate the community: delete every post that hurts the brand, keep the ones that sell. Stop whenever you are happy with the numbers, or push your luck for one more wave.
Launch. Pull the lever, watch the stars roll in, watch the profit count up. Green means you keep your job. Red means you meet the boss one last time.

### Controls

* WASD or arrows to move
* Space to grab and drop ideas and to pull the launch lever
* Mouse to do your job!
*  Escape to walk away from the keyboard.

Made in three days for the Replay III Game Jam, theme "Beyond the Skin". 

## Future

1. Water system (we need water periodically)
2. Boss demands (stretch goal): from a certain point on the boss roams the office; touching the player adds
   demands to the skin (make it blue, only half the segments, ...)
3. Sound

## Concept pipeline

Each drawable "concept" (e.g. the fox) is a `ConceptSO` asset (`Assets/Art/<Name>.asset`) holding three
pieces of art, all in the same 256x256 space and the same pose so they overlay exactly:

1. **Base SVG** (`Assets/Art/<name>.svg`) - clean outline, no fill, straight lines only (`polygon` /
   `polyline`). Ask Claude for it: *"generate an SVG of a fox, just outlines, no color, straight lines only"*.
   The straight-line constraint is what lets the "modelling" minigame read the shape back as line segments
   (via `com.unity.vectorgraphics`' `SVGParser`, or by parsing the `points` attributes directly).
2. **Sketch SVG** (`Assets/Art/<name>_sketch.svg`) - the same outline redrawn as a light-grey, wobbly,
   hand-drawn version that the player traces. It is generated from the base SVG, not drawn by hand:

   ```
   python Tools/sketchify_svg.py Assets/Art/fox.svg Assets/Art/fox_sketch.svg
   ```

   Tweak `--seed`, `--wobble`, `--overshoot`, `--step`, `--passes` and `--color` as needed. Output is still
   polyline-only, and each stroke id (`shapeN_edgeM_passP`) maps back to a source edge.
3. **Concept art PNG** (`Assets/Art/<name>_concept.png`) - the "realistic" image the player is shown before
   tracing. Ask Claude for an OpenAI image prompt describing the same subject in the same straight-on pose
   (with a negative prompt to avoid side views, bodies, text), run it through the image generator and save
   the result as a normal PNG.

Then create/fill the `ConceptSO` asset with the three references.

## Art

- [Blocky Characters](https://kenney.nl/assets/blocky-characters) by [KenneyNL](https://kenney.nl/), [CC0] license.
- [UI Pack](https://kenney.nl/assets/ui-pack) by [KenneyNL](https://kenney.nl/), [CC0] license.
- [Factory Kit](https://kenney.nl/assets/factory-kit) by [KenneyNL](https://kenney.nl/), [CC0] license.
- [Mini Arcade](https://kenney.nl/assets/mini-arcade) by [KenneyNL](https://kenney.nl/), [CC0] license.
- [Isometric office](https://sketchfab.com/3d-models/isometric-office-d31464eed8044190911b221648aca432) by [Companion_Cube](https://sketchfab.com/Companion_Cube), [CC-BY 4.0] license.
- Font [Barlow Condensed](https://fonts.google.com/specimen/Barlow+Condensed) by Jeremy Tribby, [SIL Open Font License].
- Font [JetBrains Mono](https://www.jetbrains.com/lp/mono/) by JetBrains, [SIL Open Font License].
- Concept images and logo by ChatGPT.
- Concept outline SVGs by Claude (the sketches are derived from them with `Tools/sketchify_svg.py`).
- Code particle character sheet rendered with the OCR A Extended font (Microsoft, bundled with Windows).
- Everything else done by [Diogo de Andrade], licensed through the [CC0] license.

## Sound

- Music by [Suno](www.suno.com)
- Everything else done by [Diogo de Andrade], licensed through the [CC0] license.

## Code

- Uses [Unity Common], [MIT] license.
- [NaughtyAttributes] by Denis Rizov available through the [MIT] license.
- Some (a lot) of code assisted by Claude.
- All remaining game source code by Diogo de Andrade is licensed under the [MIT] license.

## Metadata

- Autor: [Diogo de Andrade]

[Diogo de Andrade]:https://github.com/DiogoDeAndrade
[CC0]:https://creativecommons.org/publicdomain/zero/1.0/
[CC-BY 3.0]:https://creativecommons.org/licenses/by/3.0/
[CC-BY-NC 3.0]:https://creativecommons.org/licenses/by-nc/3.0/
[CC-BY-SA 4.0]:http://creativecommons.org/licenses/by-sa/4.0/
[CC-BY 4.0]:https://creativecommons.org/licenses/by/4.0/
[CC-BY-NC 4.0]:https://creativecommons.org/licenses/by-nc/4.0/
[Unity Common]:https://github.com/DiogoDeAndrade/UnityCommon
[NaughtyAttributes]:https://github.com/dbrizov/NaughtyAttributes.git#upm
[MIT]:LICENSE
[SIL Open Font License]:https://openfontlicense.org/
