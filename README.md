# Where the Signal Ends

A visual novel built in Unity 6 (6000.4.1f1, 2D / URP). Kanamori Bay closes on Thursday;
you are the records clerk sent to file what's left of it. Three days, two women who each
want the same book for opposite reasons, and five endings.

The character art is the sprite set generated in `vn_character` — `v1-schoolgirl` plays
Rin Amagi, `v2-armor` plays Captain Mira Vale.

## Running it

1. Open the project in Unity. On first load an editor script imports the TextMeshPro
   essential resources and creates `Assets/VN/Scenes/VisualNovel.unity`.
2. **Visual Novel → Play** in the menu bar. (Or open that scene and press Play.)

Everything else is built at runtime — the canvas, the dialogue box, the menus, the
backgrounds, even the sound. There is nothing to wire up in the inspector, and no prefabs
to keep in sync.

Menu items:

| Item | What it does |
|---|---|
| **Visual Novel → Play** | Opens the play scene and enters play mode |
| **Visual Novel → Validate Scenario** | Parses the story and reports bad labels, undefined characters and missing sprites, without entering play mode |
| **Visual Novel → Create Play Scene** | Recreates the scene if it is deleted |
| **Visual Novel → Open Save Folder** / **Delete All Saves** | Save-slot housekeeping |

## Controls

| Input | Action |
|---|---|
| Click / Space / Enter | Advance; first press completes the line being typed |
| Ctrl (held) | Skip |
| Mouse wheel up | Backlog |
| Esc | Close the open panel, or open Settings |
| Top-right bar | Auto, Skip, History, Save, Load, Config, Title |

Saves live in `%APPDATA%/../LocalLow/DefaultCompany/Visual Novel/saves` — eight slots, each
with a timestamp, the last line, and a screenshot thumbnail. Endings you reach are recorded
across playthroughs and counted on the title screen.

## Writing the story

The scenario is plain text in `Assets/VN/Resources/VN/Story/`, loaded in filename order and
concatenated, so `00-cast.txt` must sort first. Labels are global across the files.

```
:: label_name                     a jump target

Plain text with no prefix           narration
Rin: Some dialogue.                 a character speaks
Rin @05-angry: Some dialogue.       ...and swaps portrait on the same line
You: Something you say.             the protagonist, named by the player

> Choice text -> label              consecutive '>' lines form one menu
> [trust_rin >= 2] Gated -> label   the option is hidden when the condition fails
```

Directives:

| Directive | Meaning |
|---|---|
| `@char rin name="Rin Amagi" sprites=v1-schoolgirl color=#F0937A` | define a character |
| `@you name=Kai color=#C9D6E8` | protagonist defaults |
| `@bg seawall fade=1.4` | cross-fade the background |
| `@show rin at=left pose=03-arms-crossed fade=0.5` | bring a character on, or change pose |
| `@show rin expr=05-angry` | use the expression sheet (the portrait follows automatically) |
| `@move rin at=right time=0.4`, `@hide rin`, `@hideall` | staging |
| `@face rin 07-embarrassed` | change the dialogue portrait only |
| `@wait 0.5`, `@shake 0.4`, `@flash #FFFFFF 0.6` | pacing and punctuation |
| `@bgm sea` / `@bgm stop`, `@sfx wave` | audio |
| `@set trust_rin += 1` | variables (`=`, `+=`, `-=`, `*=`) |
| `@if trust_rin >= 3 -> label`, `@jump label` | flow |
| `@chapter "Chapter One" "The roll call"` | title card |
| `@askname` | ask the player for their name |
| `@end ending_gate "The Gate Closes" "subtitle"` | finish a route |

`{name}` expands to the player's name in any line; `{variable}` expands to its value.

Run **Visual Novel → Validate Scenario** after editing. It catches every bad label, unknown
character and missing sprite file, and reports the ending count against `VNEndings.Total`.

## Art and audio

- **Characters** — `Assets/VN/Resources/VN/Characters/<set>/{poses,expressions,portraits}/`.
  An `AssetPostprocessor` applies the right import settings on first import (Sprite, Full
  Rect, alpha-is-transparency, bottom pivot for full bodies). Adding a new sprite set is
  a folder copy plus one `@char` line.
- **Backgrounds** are procedural: named palettes rendered to a texture at runtime. Dropping
  `Assets/VN/Resources/VN/Backgrounds/<name>.png` overrides the palette of the same name.
  Existing names: `classroom hallway rooftop seawall beach_dusk town_night shrine bunker
  infirmary fog sea_gate title white black`.
- **Audio** is synthesised: `@bgm <name>` builds a seamless pad from a hash of the name and
  `@sfx <name>` picks from a small set (`click chime impact whoosh wave signal`). Real clips
  in `Assets/VN/Resources/VN/Audio/BGM/` or `/SFX/` take precedence over both.

## Code layout

| File | Role |
|---|---|
| `VNCore.cs` | input, theme, procedural sprites and backgrounds, resource cache |
| `VNScript.cs` | scenario tokenizer, parser and compiled command list |
| `VNState.cs` | variables and conditions, settings, JSON save slots |
| `VNAudio.cs` | music and effects, including the synthesiser |
| `VNUIKit.cs` | fonts and uGUI builders |
| `VNStage.cs` | background, standing sprites, dialogue box, choice menu |
| `VNScreens.cs` | title, settings, save/load, backlog, name entry, cards |
| `VNDirector.cs` | executes the command list |
| `VNGame.cs` | assembles everything and owns the title/story flow |
