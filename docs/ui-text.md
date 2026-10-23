# UI text: less to read, more to see

Conner's rule (2026-10-21): minimise text in the UI, menus and pop-ups. Use icons wherever possible, colour, and
line breaks and bullets so everything can be skimmed. Block text is hard to read.

## The rules

1. **Icon before word.** A figure is an icon and a number (`Ui.Chip("gold", "120")`), not "120 gold in the
   treasury". The word goes in the hover (`tip:`). If there is no icon for it, add one (below).
2. **One point per line.** Two facts are two bullets (`Ui.Bullets`, `Pane.Bullets`), never a sentence joined with
   "and". No paragraph over two lines in a window, card or pop-up.
3. **Explain on hover, not on screen.** What a player reads once (how a system works, why a number is what it
   is) goes in a tooltip: `Ui.Tip(text, tip)`, a control's `TooltipText`, a glossary keyword, or `Ui.Brief`
   (first sentence shown, the rest behind an info mark). `Pane.Note` already does `Brief`.
4. **Colour carries meaning.** Green good, red bad, gold money and choices, `Ui.FaithHue` for the gods,
   `Ui.WaterHue` for sight and scrying, `Ui.LoreHue` for stories. Signed figures use `Ui.Signed` (green +, red −).
5. **Short labels.** Buttons are a verb and a price ("Scry 40g"), headings two or three words. Cut "the town's",
   "you can", "in order to", "currently".
6. **Flavour stays, but small.** A story's voice is one or two lines in italics; the stakes are chips.

## The kit (`godot/src/UiKit.cs`)

Rich text (returns BBCode for `Ui.Rich`, `Pane.Text`, `Pane.Row`, `Pane.Card`):

| Helper | Gives |
| --- | --- |
| `Ui.Chip(icon, value, color?, tip)` | `[icon] 120` bold in its colour, meaning on hover |
| `Ui.Chips(a, b, c)` | chips spaced on one line |
| `Ui.Bullet(text, icon?)`, `Ui.Bullets(points, icon?)` | one point a line, icon or gold dot first |
| `Ui.Head(text, icon?, color?)` | a bold coloured heading inside a card |
| `Ui.Signed(n)` | `+12` green / `−5` red |
| `Ui.Tip(bbcode, tip)` | anything with a hover |
| `Ui.Brief(text)` | first sentence, the rest behind an info mark |
| `Ui.Dim(text)` | the dimmed rest of a line |
| `Ui.GodIcon(id)` | a god's icon (`god-<id>`) |

Controls: `Ui.IconRect(icon, size, tip)`, `Ui.IconChip(icon, value, color, tip)`, `Ui.IconRow(chips...)`.

Panes: `Pane.Page(name, icon)` splits a long window into pages under a strip (what comes before the first page shows on
all of them), `Pane.Heading(text, icon?)` (each heading has an icon in `Ui.HeadingIcons`; add yours there; it folds on
click and is left out when nothing follows it, so skip "none yet" notes),
`Pane.Bullets(points)`, `Pane.Chips(chips...)`, `Pane.Note(text)` (briefed).

## Icons

All from game-icons.net (CC BY 3.0, credited), listed in `scripts/icons.ts`:

- goods and enemy kinds (tinted), `face-*` portraits, `tab-*`/`order-*` (white, the theme tints them);
- `ui-*` coloured glyphs for common ideas: `ui-info`, `ui-time`, `ui-day`, `ui-yes`, `ui-no`, `ui-warn`, `ui-road`,
  `ui-heal`, `ui-wound`, `ui-up`, `ui-lock`, `ui-star`, `ui-renown`, `ui-person`, `ui-house`, `ui-trade`, `ui-shrine`,
  `ui-holy`, `ui-follower`, `ui-miracle`, `ui-eye`, `ui-skull`, `ui-shield`, `ui-search`, `ui-faction`, `ui-bag`, `ui-heart`;
- `god-<id>` for every god in `content/original/deities.json`.

To add one: a line in `scripts/icons.ts` (in its own block per feature, to keep merges clean), `node scripts/icons.ts`,
`godot --headless --path godot --import`, commit the `.svg` and `.svg.import`.
