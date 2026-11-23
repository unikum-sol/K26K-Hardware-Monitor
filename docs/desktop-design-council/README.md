# Design council: Sci-Fi and Fancy designs of the desktop app

The desktop app gets two new designs next to the unchanged Classic look:

- **Sci-Fi** (new default): the main window with an HUD header, status strip, framed sensor grid and plot. Style reference: the FUI scene "Patch-7" (uispace.org).
- **Fancy**: an animated dashboard inside the main window (View, Design, Fancy Mode, the FANCY button or F11; Esc returns). Style reference: the FUI scene "Bloom-Line" (uispace.org).

Only palette and style ideas were taken from the references (read from their CSS); all drawing code is our own. The references say "free for personal use"; no assets or code from them are used.

## Council

Four Claude models review the same screenshots every round. The process ends when **every member gives at least 9/10**.

| Member | Model | Focus |
| --- | --- | --- |
| M1 Art director | Claude Opus 5.5 | Overall impression, fidelity to the references, composition, typography |
| M2 UX and data visualization | Claude Sonnet 5.5 | Readability of data, honest labeling of estimates, computed metrics, usability |
| M3 Legibility and eye comfort | Claude Sonnet 5.5 | WCAG contrast, font sizes, long-term viewing, color vision deficiency, reduced motion |
| M4 First impression | Claude Haiku 5.5 | Five-second impression, "fancy" factor, consistency |

## How the screenshots are made

`capture.ps1 -Round N` writes everything into `round-N/` without administrator rights:

- `fancy-*.png` and `header-*.png`: rendered with `K26KHardwareMonitor.exe --render-snapshot` from the scenarios in `snapshots/` (`idle.json`, `load.json`). The scenarios are based on a real recording of the development PC (`real-limited.json`); values that need driver access (CPU temperature and power, fans) were filled in with plausible numbers.
- `window-*.png`: the real main window, started without elevation in a temporary folder and captured with `PrintWindow`. Without elevation some sensors are missing and the plot only has a few seconds of data.

The blue title bar in the window captures is the Windows 10 system color; on Windows 11 the Sci-Fi theme colors it.

## Computed metrics in Fancy

| Metric | How it is computed |
| --- | --- |
| System stress (0 to 100) | Since round 2: 0.40 × load (max of CPU and GPU) + 0.35 × heat (thermal ratio) + 0.15 × memory + 0.10 × fan effort, but at least 0.8 × max(load, heat). Round 0 and 1 used a less explainable mix |
| Thermal ratio / headroom | Per component (CPU, GPU, drives): (T minus 30 °C) / (limit minus 30 °C); limit from the sensor if available, otherwise CPU 95, GPU 90, drives 75 °C. Headroom = smallest distance to a limit |
| Energy distribution | CPU package and GPU power from sensors; rest from a PSU sensor if present, otherwise estimated (25 W board + 3 W per DIMM + 4 W per drive + 1.5 W per fan) and labeled EST |
| Session energy | Total power integrated over the session time |
| Fan effort | Average control % of board fans, otherwise RPM relative to the highest RPM seen |
| Thermal headroom ring | Kelvin to the nearest limit; since round 2 the ring shows the reserve that is left (full = plenty of room) |
| Stress composition | The four parts of the index (load, heat, memory, fans), each 0 to 100 (since round 1) |

---

## Round 0: first version

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | 6 | 7 | 6 | 7 |
| Sci-Fi | 5 | 7 | 7 | 5 |
| Fancy | 7 | 7 | 5 | 8 |

**Shared criticism**

1. Sci-Fi: only the header and status strip are new; the grid in between still looks like recolored Classic (colorful icons, plain checkboxes, one value color for everything, rotated gray plot titles, stacked axes whose labels touch).
2. Fancy: labels collide with nodes (GPU), long hardware names are cut, the left column ends halfway down.
3. Text too small (7 to 9 px), dim colors below a comfortable contrast, rings and tracks too faint.
4. Estimates and computed metrics not explained: total power and the rest share look like measurements, the stress index has no breakdown, the headroom ring only repeats 100 minus stress.
5. State shown by color only; pink and amber too similar; the NOMINAL / LOAD / THERMAL boxes look like buttons.
6. Too much motion and glow for long viewing (scanlines, pulses).

## Round 1: changes

Sci-Fi main window
- Node icons tinted into single-color cyan glyphs (brightness kept, shapes stay readable); bracket checkboxes with a visible outline.
- Group rows as section heads (`LOAD // 14`), hardware rows in capitals, menu in capitals.
- Values colored by kind (temperatures yellow, loads cyan, power amber, fans green); warm values orange, hot values magenta with ▲; sensors that never reported a value are dimmed.
- Segmented 10-cell meters in front of every load, control, level and humidity value. The value column gets the extra room from the name column; the settings keep the Classic widths.
- Plot: gap between stacked axes, titles in capitals, `h:mm:ss` time labels, a hint when nothing is plotted.
- Larger header, status strip and section labels; dim color raised to `#8CA3B3`.

Fancy dashboard
- Type scale with minimum sizes (11, 12, 13, 16 px), so small windows stay legible; dim color `#8FBBA8`, alarm `#FF8FA3`, data `#E8B040` (better separated); stronger rings, tracks and lines.
- Collision-free callouts: beside a node on the left and right, above or below it near the top and bottom; short names (`RTX 2070 SUPER`, `Ryzen 5 7600`).
- Stress made transparent: inner ring and a "STRESS COMPOSITION" block with load, heat, memory and fans.
- The redundant headroom ring became **thermal headroom**: Kelvin to the nearest limit, ring = share of the limit used.
- Estimates marked honestly: `≈ 393 W EST`, hatched rest segment, a line explaining what the rest contains.
- Left column filled: larger core matrix with load numbers, a THERMALS list (each component against its own limit) and a color legend (OK, WARM ≥ 60 %, HOT ≥ 85 % of the limit).
- State not by color only: `▲ HOT`, `WARM`, `▲ HIGH LOAD`; the state row at the bottom is a read-only indicator, no longer button-like.
- BOARD tile renamed to FANS; power scope with a scale and MIN / MAX markers; orbit labels on large layouts.
- Calmer: scanlines at 5 %, less glow, slower pulses.

## Round 1: scores

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | 7 | 8 | 8 | 7 |
| Sci-Fi | 6 | 8 | 8 | 6 |
| Fancy | 8 | 8 | 8 | 8 |

**Shared criticism**

1. Sci-Fi plot: the legend is missing, the rotated axis titles with units still look like Classic, separators between stacked axes are hard to see, the grid is too faint.
2. Sci-Fi grid: hardware rows do not stand out from group rows; dead sensors show `0` or `-` like real values; the off cells of the meters are almost invisible.
3. Fancy: the stress composition shows the parts but not their weights; the headroom ring fills up as the reserve shrinks (reads backwards); missing CPU values are not explained.
4. Fancy core matrix: warm and hot cores differ by color only; the left column ends with empty space.
5. Power scope: a fixed scale flattens idle curves; min and max are not visible at a glance.
6. Reduced motion should follow the Windows setting; no renders at 150 % scaling.

## Round 2: changes

Sci-Fi main window
- Plot: legend in the top right corner, horizontal axis heads (`TEMPERATURE // °C`) instead of rotated titles, dashed separators between stacked axes, stronger major and minor grid.
- Grid: hardware rows get a gradient band and an accent bar; sensors without a reading show `n/a`, their min and max show `-`; meter off cells brighter (`#33536A`).

Fancy dashboard
- Stress composition shows the weight of every part (`× 0.40`) and the formula line.
- The headroom ring shows the reserve that is left: full means plenty of room.
- A note explains values that are not available without administrator rights.
- Core matrix: hot cores marked with ▲, warm cores with a dashed border, the load number below the label.
- Left column: legend under the thermals and a new ACTIVITY block with a load sparkline.
- Power scope scales automatically to the data (nice steps), min and max in the block header.
- Dim color raised to `#9CC4B1`, stronger tracks (`#4A7A64`).

Both
- Reduced motion is on by default when Windows client area animations are off.
- Renders at 150 % scaling added to the capture set (`*-150pct.png`).

## Round 2: scores

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | 8 | 8.5 | 8.5 | 8 |
| Sci-Fi | 7 | 8.5 | 8 | 7 |
| Fancy | 8 | 8.5 | 8.5 | 8 |

**Shared criticism**

1. Sci-Fi plot legend: a light Windows-style box on top of the curves; two entries read the same (`NVIDIA GeForce RTX 2070 SUPER GPU Core` for temperature and load); series differ by color only; no marker at the newest value; the `LOAD // %` head sits on a grid line.
2. Sci-Fi contrast: axis numbers, `n/a`, `-` and the empty meter cells too dim; tree text and meters small; the grid looks like a styled table next to the HUD header.
3. Fancy: the formula line is cut at 1366 and runs past the panel at 1500 px.
4. ACTIVITY: sine-shaped sample data looks synthetic; the `100%` label sits on the curve; no line samples for CPU and GPU; idle curves stick to the floor.
5. Consistency: stress 85 in the header but 84 in Fancy (different rounding); `▲ LOAD`, `▲ HIGH LOAD` and `HIGH LOAD` for the same state; FANS 97 % in alarm pink while the system is nominal; an unlabeled 44 °C on the fans tile.
6. Small text (orbit labels, legend row, core labels) below 12 px; warm core border too thin; glow and pulses noticeable for long viewing; the power scale has too many labels in small windows.

## Round 3: changes

Sci-Fi main window
- Plot legend below the plot (never over a curve), dark panel with an accent border, short names with the kind of value and the current reading (`RTX 2070 SUPER GPU CORE // TEMP  28.0 °C`), grouped by axis.
- Every series on the same axis gets its own line style (solid, dash, dot, ...), lines at least 1.75 px, a dot on the newest value.
- Axis heads sit in a gap above each stacked axis, on the dashed separator; axis text 12.5 px in `#A8BCC8`. With overlaid axes the classic rotated titles return.
- Grid: font 9.75 pt, `n/a` and `-` in the dim color, `#4A6A82` for empty meter cells, wider and taller meters, instrument rulers and scale marks on the frames; `AXES // STACKED` instead of `STACKED // ON`.

Fancy dashboard
- Smallest text now 12 px; the stress formula on two short lines; `REST ≈ BOARD + RAM + DRIVES + FANS`.
- ACTIVITY: line samples for CPU (solid) and GPU (dashed) with average and maximum, a scale that follows the data (`SCALE 0 TO 40 %`), and a `COLLECTING` note while a session is younger than a minute. The scenario files now contain realistic, noisy histories instead of sine waves.
- One wording for the state everywhere: `NOMINAL`, `▲ HIGH LOAD`, `▲ THERMAL LIMIT`; the stress value rounds the same way in header and Fancy.
- Fans are never drawn in alarm colors (fan effort is no alarm); the fans tile says `BOARD 44°C`.
- Warm cores get a 1.75 px dashed border; the power scope labels keep at least 28 px apart.
- Less glow (smaller radius and alpha) and a new menu option **View, Design, Glow Effects** to switch all glow off (`fancy-load-no-glow-1920.png`). Motion: node pulses at most 0.75 Hz, the core breathes at 0.25 Hz, no flashing areas.

## Round 3: scores

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | 8 | **9** | **9** | 8 |
| Sci-Fi | 8 | 8.5 | 8.5 | 7 |
| Fancy | **9** | **9** | **9** | 8 |

Fancy passed for three members. **Shared criticism**

1. Sci-Fi plot (named by three members as the one blocker): autoscaled axes zoom into noise, a 0.4 K wobble fills the whole height and looks like an alarm.
2. Sci-Fi legend: columns of unequal length; the VALUE header stands above the meters, not above the numbers; the grid still reads like a table next to the HUD header.
3. Missing values shown three ways (`--`, `n/a`, empty).
4. Fancy core: `▲ HIGH LOAD` and `SYSTEM STRESS` touch the ring at 1366 px and 150 %; the stress number is pink while the state word is amber.
5. Fancy: a start-up spike in ACTIVITY and POWER; ACTIVITY and POWER squeezed at 1366 px; activity scales jump between arbitrary values; the headroom ring does not say that the CPU is missing.
6. Header: `● ▲ HIGH LOAD` marks the state twice. Requested checks: 125 % scaling, Windows animations off, high contrast.

## Round 4: changes

Sci-Fi main window
- Minimum spans for autoscaled axes: 10 K (18 °F) for temperatures, 10 % for loads and controls, 10 W, 200 MHz, 300 RPM, 0.2 V and so on. Noise stays calm; zooming by hand is still possible.
- Legend in one column in axis order (a second column starts after four entries).
- VALUE header aligned with the numbers; a thin accent rail runs down the left edge of every device block with a tick at each group, so the devices read as framed modules.
- Empty meter cells `#5A7B94`.

Fancy dashboard
- Texts in the core are fitted to the chord of the inner ring; when the core is too small, the full state goes on a framed plate below the core (never shortened).
- Stress number, ring and state word share one color scale: the state decides (nominal mint, high load amber, thermal limit pink). The header stress cell follows the same rule.
- The first two readings after the start are left out of all histories (no start-up spike).
- ACTIVITY scale in three fixed steps (25, 50, 100 %); in short columns the legend moves into the heading line and the scale onto a plate, so the chart keeps its height. Smaller headroom ring in short columns, more room for POWER.
- Headroom says `CPU TEMP n/a, NOT COUNTED` when the CPU temperature is missing.
- Orbit labels 12 px in the full dim color; more distance between nodes and their callouts.

Both
- Missing values are `n/a` everywhere (header, tiles, tree).
- The state pill shows the dot only (`● HIGH LOAD`).
- With Windows high contrast on, the first start uses Classic (system colors); Reduced Motion already follows the Windows animation setting.
- Renders at 125 % added (`fancy-load-125pct.png`, `header-load-125pct.png`).

## Round 4: scores

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | **9** | **9** | **9** | 8 |
| Sci-Fi | **9** | **9** | **9** | 7 |
| Fancy | **9** | **9** | **9** | 8 |

Three members passed both designs. **Remaining criticism** (polish from M1 to M3, blockers from M4)

1. M4: the Sci-Fi grid still reads like a styled table; it needs HUD elements that continue the header (corner marks, edge accents). Values, min and max look small and gray in the window capture; the plot is half empty because the capture only had 45 s of data.
2. ACTIVITY at 1366 px: the scale plate covers the start of the curves.
3. The core state jumps between two places when the state changes (inside the core or on the plate below); the plate sits on an energy line; orbit labels cross energy lines.
4. `HIGH LOAD` twice in the header (stress cell and pill); decimal comma in the grid but decimal point in Fancy.
5. Inactive states in the state strip only just above 4.5:1.

## Round 5: changes

Sci-Fi main window
- Frames: larger double corner brackets (outer 2 px, inner fine) and a port in the middle of each edge.
- Column header row with a fine scale along the bottom (like the HUD ruler) and an accent cap at each column edge.
- Live readings in bold; missing readings stay regular and dim, so working sensors stand out at a glance.
- Captures now wait 75 s, so the plot shows a full minute.

Fancy dashboard
- The state position is fixed per layout using the longest state (`▲ THERMAL LIMIT`), so it never jumps; the plate is larger (13 px), filled, framed and has a dark margin that keeps the energy lines away from the text.
- Orbit labels sit on dark plates above the energy lines.
- ACTIVITY in short columns: the scale stands left of the chart like in POWER (`100%`, `0`), never on a curve.
- Inactive states in the state strip in the full dim color.

Both
- One number format: the Windows locale everywhere (grid, plot legend, header, Fancy; scenario files are converted when loaded).
- The header stress cell shows `20 / 100`; the state is only in the pill.

## Round 5: scores

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | **9** | **9** | **9** | 8 |
| Sci-Fi | **9** | **9** | **9** | 8 |
| Fancy | **9** | **9** | **9** | **9** |

M1 to M3 confirmed their 9 without regressions. **Remaining criticism**

1. M4: the HUD language stops at the frame; inside, the grid still reads like a dense table. A segment line at the group heads or a fine scale between groups would continue it.
2. M1: the column caps next to VALUE, MIN and MAX look like stray pipe characters.
3. Optional: ACTIVITY still low at 1366 px; POWER shows only 0, 200, 400 there; the orbit label plates compete a little with the rings; the full headroom ring looks "all fine" although the CPU is missing.

## Round 6: changes

Sci-Fi main window
- Every group row (`LOAD // 14`) ends in a segmented scale line across the grid, with a brighter mark every sixth segment, so the HUD language runs through the grid.
- Column caps are short ticks below the text line.

Fancy dashboard
- Flatter core cells in small layouts: more chart height at 1366 px. (The power scale labels may now be 22 px apart; at 1366 px the scope is still too short for a fourth label, see round 6 scores.)
- Orbit label plates at 72 % opacity.
- The headroom ring is dashed when a part (the CPU temperature) is missing.

## Round 6: scores

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | **9** | **9** | **9** | 8 |
| Sci-Fi | **9** | **9** | **9** | 8 |
| Fancy | **9** | **9** | **9** | **9** |

M1 to M3 confirmed without regressions; M3 checked that the group lines do not make the grid restless. M1 warned that brighter group lines would compete with the meters. **Remaining criticism (M4)**

1. The group scale lines are very fine; the grid still reads like a table at first glance. Stronger marks every sixth segment, more weight for the column heads.
2. The state plate under the core is small next to the stress number.
3. Not changed on purpose: the power scale at 1366 px keeps three labels (0, 200, 400), there is no room for a fourth without crowding; M1 rated it as not necessary. The load meters in the grid already turn orange from 70 % and pink from 90 %; the window captures only show an idle system.

## Round 7: changes

Sci-Fi main window
- The group scale lines get a short vertical tick every sixth segment: more structure without extra brightness (the balance between M1 and M4).
- Column heads 9.25 pt bold in `#B4C8D4` instead of 8.5 pt in the dim color.

Fancy dashboard
- State plate text 15 px (13.5 px × scale).

## Round 7: scores

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | **9** | **9** | **9** | 8 |
| Sci-Fi | **9** | **9** | **9** | 8 |
| Fancy | **9** | **9** | **9** | **9** |

**Remaining criticism (M4)**: the group rows (`LOAD`, `TEMPERATURES`, `VOLTAGES`) carry too little weight of their own; a stronger group head with the count in a frame or on a band would carry the HUD language into the grid. MIN and MAX are still small. M1: at 1366 px the 15 px state plate is a little heavy next to the core.

## Round 8: changes

Sci-Fi main window
- Group heads as section labels: bold, `#B4C8D4`, the count as a chip (`LOAD  [14]`) and a band of their own that fades out to the right, below it the segmented scale line.
- MIN and MAX one step brighter (`#A8BCC8`) as long as the sensor reports values.

Fancy dashboard
- State plate text 14 px in small layouts, 15 px and more from 1600 px.

## Round 8: scores

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | **9** | **9** | **9** | 8 |
| Sci-Fi | **9** | **9** | **9** | 8 |
| Fancy | **9** | **9** | **9** | **9** |

**Remaining criticism (M4)**: the group heads are a clear step forward, but device rows (Ryzen, Gigabyte board) look like group rows; a clearly separated device head with edges of its own would show the hierarchy within five seconds. M2 (optional): the meter thresholds are not explained anywhere on screen.

## Round 9: changes

Sci-Fi main window
- Device rows as heads of their own: 10.5 pt bold accent text with the sensor count (`AMD RYZEN 5 7600  // 48 CH`), an accent line on top that fades out to the right, a fine line below and a notch at the right end. The hierarchy now reads device, group, sensor.
- The status strip explains the meter colors: `METER  < 70 %  ≥ 70 %  ≥ 90 %` (only when there is room).

## Round 9: scores

| | M1 Opus | M2 Sonnet | M3 Sonnet | M4 Haiku |
| --- | :-: | :-: | :-: | :-: |
| Overall | **9** | **9** | **9** | 8 |
| Sci-Fi | **9** | **9** | **9** | 8 |
| Fancy | **9** | **9** | **9** | **9** |

**Remaining criticism (M4)**: the device heads are clear but thin; a head with a frame corner or a segment bar would close the gap to the HUD header. The meter colors cannot be checked in idle captures. M1: the board head has no channel count. M3: the meter legend could be 12 px.

## Round 10: changes

Sci-Fi main window
- Device heads end in a segment bar and corner brackets at the right edge, next to the notch.
- Meter legend in the status strip at 12 px.
- New capture `window-scifi-load.png`: the real window while every CPU core runs a busy loop (started by `capture.ps1`, each loop ends after 100 s). It shows the load colors that idle captures cannot show: full pink meters with `▲ 100,0 %`, the `HIGH LOAD` pill and the stress value in the header turning orange.
- Not changed on purpose: a device without readable sensors gets no `// 0 CH`; with it, the long board name lost its end.
