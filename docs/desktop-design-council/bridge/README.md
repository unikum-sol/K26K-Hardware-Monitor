# Design council: Bridge and Bridge Fancy

The desktop app gets a third design next to Classic and Sci-Fi:

- **Bridge**: the main window as a console of flat color bars with rounded elbows, pill buttons and condensed lettering on black.
- **Bridge Fancy**: the Fancy dashboard with the same data and functions, drawn in the Bridge look.

## Copyright and originality

The look is a homage to the console style of science fiction films and series. Before any work started, the legal side was checked:
the console style of one well-known series is claimed as copyrighted by its studio, which has had fan apps that copied it removed.
Therefore Bridge is an **original design in the general style**:

- No screen, layout, logo, emblem, ship name, registry number or sound of any production is copied or used; no franchise names appear in the program or the documentation.
- The lettering is [Antonio](https://github.com/googlefonts/antonioFont) (SIL Open Font License 1.1), not the commercial font of the original.
- Colors, shapes, layout, texts and number codes were drawn for this project. Every council member also checks originality.
- All code lives in its own files (`UI/Bridge/`, `UI/Themes/BridgeTheme.cs`, `UI/MainForm.Bridge.cs`), so the design could be removed quickly.

## Council

The same four Claude models as for Sci-Fi and Fancy review the same screenshots every round. The process ends when **every member gives at least 9/10**.

| Member | Model | Focus |
| --- | --- | --- |
| M1 Art director | Claude Opus 5.5 | Overall impression, composition, typography, originality |
| M2 UX and data visualization | Claude Sonnet 5.5 | Readability of data, honest labeling, same information as Fancy, usability |
| M3 Legibility and eye comfort | Claude Sonnet 5.5 | WCAG contrast, text sizes, glare, color vision deficiency, 1366 and 150 % |
| M4 First impression | Claude Haiku 5.5 | Five-second impression, "fancy" factor, consistency, anything that looks broken |

## How the screenshots are made

`capture.ps1 -Round N` writes everything into `round-N/` without administrator rights:

- `bridge-fancy-*.png`, `bridge-header-*.png`, `bridge-status-*.png`: rendered with `K26KHardwareMonitor.exe --render-snapshot ... --view bridge-fancy|bridge-header|bridge-status` from the scenarios in `../snapshots/` (the same as for Fancy).
- `window-*.png`: the real main window (Bridge, Bridge under 100 % CPU load, Bridge Fancy), started without elevation in a temporary folder and captured with `PrintWindow`. Without elevation some sensors are n/a, and the values differ from the recorded scenarios.

---

## Round 1: first version

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | 8 | 7.5 | 6.5 | 7 |
| Bridge window | 7.5 | 8 | 7 | 7 |
| Bridge Fancy | 8 | 7 | 6 | 8 |

**Shared criticism**

1. A bug: the animated runners of the system flow were drawn on a different baseline than the tracks (one header height in the static pass, another in the motion pass).
2. Core matrix: the rising fill cut through the core labels; the hot state relied on a small corner mark.
3. Small and dim text (block codes, weights, captions) and low contrast on inactive state blocks.
4. Large solid color blocks in the device column: glare for long viewing, and under load the heat color replaced the device color.
5. Information missing compared with Fancy: the meaning of the four stress parts, the formula, the meaning of the track thickness.
6. Decorative numbers without meaning; empty colored areas in the frame; device codes that differed between views.
7. Main window: two typefaces (monospaced values), a framed legend, a crowded bottom edge, a right-aligned SENSOR header.
8. Originality (advisory, M1): the double frame is the archetype of the genre; give it a signature of its own.

**Not changed, with reason**

- Values differ between the window captures and the renders: they come from different days and the captures run without admin rights (M4).
- The fan value of 96 % is recorded data, not a mapping error (M4).
- "Core #2 at 0.0 % with a red meter" (M2) was the MIN column; under the load test every core was at 100 %.
- The plot keeps Consolas: OxyPlot draws with GDI+, which cannot use the embedded Antonio font.

**Changes for round 2**

- One header height for both passes: the runners sit on the lit part of their tracks.
- Core cells: a label row the fill never enters, "HOT" in red plus an outline at 90 % or more; the heading says "HOT >= 90 %".
- Device column: a tab in the device color plus a dark readout with the value, the temperature with its heat word in heat colors, and two meters (load, heat). The empty elbows carry "HARDWARE / NN DEVICES" and the meter legend.
- The four stress parts with meaning, weight and "NN / 100", plus the formula line; the flow heading explains fill and thickness; rows without a power sensor say so; a note when CPU or GPU power is missing.
- Text at least 14 px in the dashboard, dim text #B5AAA0, inactive text #D2C6BA, a nearly opaque boot veil, one device code everywhere, decorative numbers removed.
- A signature element: the long segment of the upper rail is a live strip of the stress of the last 60 updates.
- Main window: Antonio for values and min/max, legend on a plain dark block, frames end in the bottom elbow, SENSOR left aligned, brighter meter track.
- 1366: one line per flow row, shorter heading notes, long device values shrink to fit.

## Round 2

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | 9 | 8.5 | 8.5 | 8 |
| Bridge window | 8.5 | 8.5 | 8.5 | 7 |
| Bridge Fancy | 9 | 8.5 | 8.5 | 8 |

**Shared criticism**

1. Core matrix: the values still sat on the edge of the fill; red and orange cells differed mainly in hue.
2. The stress strip had no scale and shrank to a hairline at idle.
3. The energy legend showed "CPU 0%" when the CPU power sensor was missing; the thickness encoding was still subtle and some rows did not state their power.
4. Headroom in K reads like "thousands" at a glance; the headroom meter had two looks (solid, gapped).
5. Under full load the main window tree shows many red rows at once.
6. The plot was the only part of the main window in a monospaced face.

**Not changed, with reason**

- The partial last tree row is a normal scroll position; the tan bar at the right edge of the tree is the scroll indicator.
- The WEB address in the rendered status strip comes from the recorded scenario (a private LAN address, as in the Sci-Fi rounds); the real window captures show WEB OFFLINE.

**Changes for round 3**

- Core cells: name and value in the dark label row, the fill only shows the level; hot cells (90 % or more) "!92" in a lighter red (#FF7A6B) with a 3 px light outline; the heading says "! = LOAD >= 90 %".
- Stress strip: cells colored by the state scale, at least 35 % high, labeled "0 TO 100" and "NOW NN".
- Energy: a missing power sensor reads "n/a" and has no segment. Flow: thickness 6 to 20 px in the device color, every row states its power share or "NO POWER DATA".
- Headroom in °C (°F with Fahrenheit) with one solid meter; a missing CPU temperature is said in the warning color.
- Main window: hot values in the lighter red; the plot uses Bahnschrift SemiCondensed (part of Windows) when Bridge is active, as GDI+ cannot use the embedded Antonio.
- The boot percentage on its own plate; weights and meanings SemiBold.

## Round 3

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | 9 | 9 | 9 | 8 |
| Bridge window | 9 | 8.5 | 9 | 7 |
| Bridge Fancy | 9 | 9 | 9 | 8 |

**Shared criticism**

1. At 1366 the text of the flow rows ran into the end of the thick tracks.
2. "NO POWER DATA" repeated on most flow rows.
3. Narrow core cells at 1366: name and value almost touched ("C11!95").
4. The scroll indicator of the tree was too faint to read as one (M4).
5. A solid headroom meter next to a missing CPU temperature looked like plenty of reserve (M2); the energy legend should say that the rest contains the CPU.

**Changes for round 4**

- Flow rows with fixed columns that depend on the size only: short rows show track, "NN% PWR" and the value (temperature and watts are in the device column); one footnote names the devices without a power sensor.
- Core cells: at least 26 px between name and value; when the widest case does not fit, the whole grid drops the "C".
- Bridge scroll indicator: bright orange, 6 px wide (a theme setting; the other designs keep theirs).
- Headroom meter hatched when the CPU temperature is missing (the same language as the estimated rest); the energy legend reads "REST + CPU".
- The dotted plot series in a lighter lilac.

## Round 4

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | 9 | 9 | 9.3 | 8 |
| Bridge window | 9 | 9 | 9.3 | 7 |
| Bridge Fancy | 9 | 9 | 9.3 | 9 |

**Shared criticism**

1. The core matrix dropped the "C" prefix at 1920 as well (the fit test was too strict).
2. The PWR column had gaps on the rows without power data.
3. The last row of the main window tree was cut in half at the bottom edge (M4).
4. Red text appeared in two tones; the darker one is the weaker text color.

**Changes for round 5**

- Core cells: a measured fit with 5 px inner margins; the "C" stays from 1600 px up and is dropped only in the 1366 grid.
- A dim "-" in the PWR column for rows without power data.
- In Bridge the sensor tree snaps to whole rows: the bottom padding of its panel takes the remainder. For this the header height of the tree control (`TreeViewAdv.ColumnHeaderHeight`) became public.
- Red text in #FF7A6B everywhere (also the CPU value in the header); #FF5C4D only for fills and the close button. Explanatory notes SemiBold.

## Round 5

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | 9 | 9 | 9.4 | 9 |
| Bridge window | 9 | 9 | 9.4 | 8 |
| Bridge Fancy | 9 | 9 | 9.4 | 9 |

M1, M2 and M3 confirmed their scores without regressions. M4 asked for one more change in the main window:

1. The scroll thumb of the tree was hard to find. It is proportional (visible rows divided by all rows), and with several hundred expanded rows it was short.
2. The hyphen in the PWR column looked like a stray character.

**Changes for round 6**

- Bridge scroll thumb with a minimum length of 36 px (at 96 DPI); it keeps its position along the track (a theme setting, the other designs stay strictly proportional).
- Rows without power data show a dim short tick in the PWR column.

## Round 6

| | M4 Haiku |
| --- | :-: |
| Overall | 9 |
| Bridge window | 8 |
| Bridge Fancy | 9 |

Only M4 reviewed (M1, M2 and M3 were at 9 or more since round 3 and had no required fix; round 6 changed two details only).
M4 asked for a visible track behind the scroll thumb, so it reads as a scrollbar and not as a floating tab.

**Changes for round 7**

- The Bridge scrollbar has a dark groove (#2E2822) along the full tree height behind the orange thumb.

## Round 7: result

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | 9 | 9 | 9.4 | 9 |
| Bridge window | 9 | 9 | 9.4 | 9 |
| Bridge Fancy | 9 | 9 | 9.4 | 9 |

(M1 to M3: confirmed scores of round 5; M4: round 7.) **Every member gives at least 9/10, the process ends here.**

Polish suggested in the last rounds and left for later: a slightly lighter scroll groove, three different shapes for the meter legend pills, 13 px plot text in the main window, an optional unit next to the count pills of the tree groups, and a taller stress strip with marks at 50 and 80.

**Verified outside the council**

- Sci-Fi and Fancy render byte-identical to the state before this work (the shared hooks change nothing for them); Classic looks unchanged.
- Switching at runtime: Sci-Fi to Bridge, F11 to Bridge Fancy, Esc back, Classic, F11 from Classic to Fancy, Esc to Sci-Fi; the setting `design` is saved as `bridgefancy` after closing in Bridge Fancy.
- Smooth values (frame sequence across updates) and the runners on the flow tracks; CPU of Bridge Fancy at 1600x900 about 18 % of one core (Fancy: about 25 %).
- net472 and net10.0-windows build; the net472 build renders the Bridge header identically.
