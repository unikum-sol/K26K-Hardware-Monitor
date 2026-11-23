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
| System stress (0 to 100) | 50 % worst of CPU load, GPU load and thermal ratio, 50 % weighted mix (CPU 30 %, GPU 25 %, thermal 25 %, memory 10 %, fan effort 10 %) |
| Thermal ratio / headroom | Per component (CPU, GPU, drives): (T minus 30 °C) / (limit minus 30 °C); limit from the sensor if available, otherwise CPU 95, GPU 90, drives 75 °C. Headroom = smallest distance to a limit |
| Energy distribution | CPU package and GPU power from sensors; rest from a PSU sensor if present, otherwise estimated (25 W board + 3 W per DIMM + 4 W per drive + 1.5 W per fan) and labeled EST |
| Session energy | Total power integrated over the session time |
| Fan effort | Average control % of board fans, otherwise RPM relative to the highest RPM seen |
| Headroom ring | 100 minus stress |

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
