# Skin Factory

Developed for the [Replay European Masters in Games](https://replay.pt/), with the theme "Beyond the Skin".

## Game

TBD

## Todo

* Coding minigame
* Marketing minigame
* Boss with demands
* Water system (we need water periodically)
* Main game loop: time limit for skin, success assessment
* More skins (beyond the fox)
* Launch button
* Brainstorm minigame
* Signposting next task
* Add boss emotes
* Improve explanation of regions

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
- [Isometric office](https://sketchfab.com/3d-models/isometric-office-d31464eed8044190911b221648aca432) by [Companion_Cube](https://sketchfab.com/Companion_Cube), [CC-BY 4.0] license.
- Everything else done by [Diogo de Andrade], licensed through the [CC0] license.

## Sound

- Everything else done by [Diogo de Andrade], licensed through the [CC0] license.

## Code

- Uses [Unity Common], [MIT] license.
- [NaughtyAttributes] by Denis Rizov available through the [MIT] license.
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
