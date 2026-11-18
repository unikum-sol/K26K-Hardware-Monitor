# Design Council: redesign of the web interface

The council consists of three Claude Sonnet 5.5 instances with different areas of focus.
Each round they rate the same screenshots: smartphone (390 px) and desktop (1280 px), each in dark and light mode.
The process is finished once **all three members give at least 9/10**.

| Member | Focus |
| --- | --- |
| M1 – Visual Design Lead | Typography, spacing, hierarchy, color, aesthetics |
| M2 – UX & Data-Viz Lead | Information architecture, scannability, mobile use, charts |
| M3 – Accessibility & Eye-Comfort Lead | WCAG contrast, eye comfort, touch targets, color vision deficiency |

The screenshots are taken automatically with headless Edge against a mock server with sample data,
and are stored per round in `round-N/`. Headless Edge does not render windows narrower than about 500 px,
so from round 1 on the phone screenshots show the page in a 390 px wide iframe; the gray strip on the right is not part of the UI.

Constraint for every round: the web interface keeps mirroring the desktop settings
(theme base colors, plot colors, hidden sensors, columns, expanded state and so on).

---

## Round 0 – starting point

| M1 | M2 | M3 |
| :-: | :-: | :-: |
| 4 | 5 | 6 |

**Shared main criticism**

1. Content looked cut off on the right in the phone screenshots. In a real 375 px viewport there is no overflow, but grid items
   with `min-width: auto` can be widened by the chart canvas, so this was hardened anyway.
2. Generic Windows look caused by colorful bitmap icons and gray group bands.
3. Weak surface hierarchy (gray on gray), group headers barely set apart.
4. Load bars change color with the theme (red in light mode) and look like an alarm.
5. Text and touch targets too small, no `:focus-visible`, status shown by color only.
6. Plot curves can only be told apart by color (red/green with deuteranopia), and the grid is too strong.
7. Sensor rows too tall with a lot of empty space; Min/Max hard to read.
8. With 20–60 hardware items there is no overview: no key values in the card header, no quick navigation.

## Round 1 – changes

- New design tokens derived from the desktop theme: page, surface, surface-2, border, divider, two text levels, one accent
  (teal), warning and critical colors. Light mode uses a cool off-white page with white cards and a soft shadow;
  dark mode uses no shadow. Muted text reaches at least 4.5:1.
- Monochrome line icons (inline SVG) instead of the bitmap icons; hardware icons in a tinted tile.
- Hardware card header shows the most telling values (temperature · load · power) so collapsed cards stay useful.
- Sticky header with blur, status as text ("Live"/"Getrennt") plus dot, quick-jump pills (History, CPU, GPU, …).
- Group headers as small uppercase labels with an icon and a divider, no gray band.
- Sensor rows: 52 px, fixed dot column so names align, value 17 px semibold with a muted unit, "Min · Max" line below.
- Load bars: 4 px, accent color everywhere; amber from 85 %, red from 95 %. Temperatures turn amber from 80 °C and red from 90 °C.
- Charts: transparent plot area, horizontal grid lines only (8–9 % opacity), muted axis labels,
  dash patterns per series in addition to color, legend as value chips, tooltip with sensor names.
- Touch targets ≥ 44 px, `:focus-visible` rings, `prefers-reduced-motion`, ARIA states for expandable sections.
- Grid tracks use `minmax(0, 1fr)` and the canvas has `max-width: 100%`, so charts can never widen the layout.
- Settings panel with search field, segmented control for the time span and grouped actions.

## Round 1 – scores

| M1 | M2 | M3 |
| :-: | :-: | :-: |
| 7 | 8 | 8 |

**Remaining blockers named by the council:** legend chips cut off on phones; quick-jump bar without scroll hint
and active state; key values in the card header unlabeled and redundant when the card is open; bars use the accent
instead of the sensor color and change the row height; secondary text and axis labels too faint (especially light mode);
light palette colors (yellow, light green) below 3:1 on white; warnings shown by color only; gaps below short cards on desktop.

## Round 2 – changes

- Legend chips wrap to new lines instead of being cut off; long names get an ellipsis.
- Quick-jump bar: 44 px pills, scroll snapping, fade on the right edge while more pills are available,
  and scroll-spy highlighting of the section currently on screen.
- Card header: labelled key values (icon + value) only while the card is collapsed; an open card shows the sensor count.
- Group headers show the number of sensors ("TEMPERATURES · 3"); smaller, quieter chevrons.
- Sensor names line up exactly with the group label (16 px icon slot); dividers start at the name instead of the card edge.
- Bars sit in the row's bottom padding (no height jump), use the plot color for plotted sensors and a neutral tone otherwise,
  and always show at least 3 px.
- Warnings and critical values show a warning symbol next to the colored value.
- Secondary text darker in light mode (about 5:1), grid lines 10–12 %, axis labels 12 px, accent dimmed in dark mode (#4fb3a6).
- Light mode draws a faint dark halo under each curve, so light palette colors stay visible on white.
- Desktop uses masonry-style columns, so short cards leave no empty space.
- The mock server now contains a realistic number of devices (mainboard, memory, two drives, one of them collapsed).
