/*
  K26K Hardware Monitor - mobile web interface
  Copyright (C) 2026 K26K Hardware Monitor contributors

  This program is free software: you can redistribute it and/or modify
  it under the terms of the GNU Affero General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU Affero General Public License for more details.

  You should have received a copy of the GNU Affero General Public License
  along with this program.  If not, see <https://www.gnu.org/licenses/>.

  Everything shown here mirrors the desktop app: hidden and renamed sensors,
  the Value/Min/Max columns, the plot selection and colors, stacked axes,
  the plot time span, the temperature unit, the theme and the update interval.
  Only collapsing cards, opening sensor charts, the chart time span and the
  filter are local to this browser.
*/
(function () {
  'use strict';

  // ---------- helpers ----------

  const $ = (id) => document.getElementById(id);

  function load(key, fallback) {
    try {
      const v = localStorage.getItem(key);
      return v === null ? fallback : JSON.parse(v);
    } catch (e) {
      return fallback;
    }
  }

  function save(key, value) {
    try { localStorage.setItem(key, JSON.stringify(value)); } catch (e) { /* private mode */ }
  }

  function el(tag, cls, text) {
    const e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text !== undefined) e.textContent = text;
    return e;
  }

  const HARDWARE_ICONS = {
    Cpu: 'cpu.png', GpuNvidia: 'nvidia.png', GpuAmd: 'ati.png', GpuIntel: 'intel.png',
    Storage: 'hdd.png', Motherboard: 'mainboard.png', SuperIO: 'chip.png', Memory: 'ram.png',
    Cooler: 'fan.png', Network: 'nic.png', Psu: 'power-supply.png', Battery: 'battery.png',
    PowerMonitor: 'powermonitor.png', EmbeddedController: 'chip.png'
  };

  const TYPE_ICONS = {
    Voltage: 'voltage.png', Current: 'current.png', Clock: 'clock.png', Timing: 'clock.png',
    Load: 'load.png', Temperature: 'temperature.png', Fan: 'fan.png', Flow: 'flow.png',
    Control: 'control.png', Level: 'level.png', Power: 'power.png', Noise: 'loudspeaker.png',
    Conductivity: 'voltage.png', Throughput: 'throughput.png', Humidity: 'humidity.png',
    Data: 'data.png', SmallData: 'data.png', Factor: 'factor.png', Frequency: 'clock.png',
    TimeSpan: 'time.png', Energy: 'power.png'
  };

  const PERCENT_TYPES = new Set(['Load', 'Control', 'Level', 'Humidity']);

  const SPANS = [
    { s: 0, label: 'Desktop' }, { s: 60, label: '1 min' }, { s: 300, label: '5 min' },
    { s: 600, label: '10 min' }, { s: 1800, label: '30 min' }, { s: 3600, label: '1 h' },
    { s: 6 * 3600, label: '6 h' }, { s: 24 * 3600, label: '24 h' }
  ];

  function formatNumber(v, unit) {
    if (v === null || v === undefined || !isFinite(v)) return '–';
    const a = Math.abs(v);
    if (unit === 'B/s') {
      if (a >= 1073741824) return (v / 1073741824).toFixed(1) + ' GB/s';
      if (a >= 1048576) return (v / 1048576).toFixed(1) + ' MB/s';
      if (a >= 1024) return (v / 1024).toFixed(1) + ' KB/s';
      return v.toFixed(0) + ' B/s';
    }
    let d;
    switch (unit) {
      case 'V': case 'A': case 'ns': d = 3; break;
      case 'MHz': case 'RPM': case 'mWh': case 'dBA': d = 0; break;
      case 'GB': d = 2; break;
      case '': d = 3; break;
      default: d = a >= 1000 ? 0 : 1;
    }
    return v.toFixed(d) + (unit ? ' ' + unit : '');
  }

  function formatAgo(seconds) {
    if (seconds <= 0) return 'jetzt';
    if (seconds < 120) return '-' + Math.round(seconds) + ' s';
    if (seconds < 7200) return '-' + Math.round(seconds / 60) + ' min';
    return '-' + (seconds / 3600).toFixed(seconds < 36000 ? 1 : 0).replace('.0', '') + ' h';
  }

  // ---------- state ----------

  const S = {
    state: null,
    signature: '',
    collapsed: load('k26k.collapsed', {}),      // node key -> true/false, overrides the desktop
    open: new Set(load('k26k.openSensors', [])), // sensors with an open chart
    span: load('k26k.span', 0),                 // 0 = follow the desktop plot
    filter: '',
    sensors: new Map(),                         // id -> sensor json
    rows: new Map(),                            // id -> row elements
    history: {},                                // id -> [[ago, value], ...]
    historyTime: 0,
    charts: [],
    lastOk: 0,
    failures: 0,
    stateTimer: null,
    historyTimer: null,
    historyBusy: false
  };

  function settings() {
    return (S.state && S.state.settings) || {
      showHidden: false, columns: { value: true, min: false, max: true },
      plot: { visible: false, stacked: true, timeSpan: 600, stroke: 2 }, updateInterval: 1000
    };
  }

  function effectiveSpan() {
    if (S.span) return S.span;
    const t = settings().plot.timeSpan;
    if (t && isFinite(t) && t > 0) return Math.max(30, Math.min(86400, Math.round(t)));
    return 600;
  }

  // ---------- theme ----------

  let appliedTheme = '';

  function applyTheme(theme) {
    if (!theme) return;
    const key = JSON.stringify(theme);
    if (key === appliedTheme) return;
    appliedTheme = key;

    const r = document.documentElement.style;
    const dark = theme.dark;
    r.setProperty('--bg', theme.background);
    r.setProperty('--fg', theme.foreground);
    r.setProperty('--line', theme.line);
    r.setProperty('--strong', theme.strongLine);
    r.setProperty('--sel', theme.selectedBackground);
    r.setProperty('--card', mix(theme.background, theme.foreground, dark ? 0.05 : 0.025));
    r.setProperty('--muted', mix(theme.foreground, theme.background, 0.42));
    r.setProperty('--accent', dark ? theme.hyperlink : (theme.palette[1] || theme.hyperlink));
    r.setProperty('--plot-bg', theme.plotBackground);
    r.setProperty('--plot-text', theme.plotText);
    r.setProperty('--grid', theme.plotGridMajor);
    r.setProperty('--grid-minor', theme.plotGridMinor);
    r.setProperty('color-scheme', dark ? 'dark' : 'light');
    document.querySelector('meta[name="theme-color"]').setAttribute('content', theme.background);
  }

  function mix(a, b, t) {
    const pa = parseHex(a), pb = parseHex(b);
    const c = pa.map((v, i) => Math.round(v + (pb[i] - v) * t));
    return '#' + c.map((v) => v.toString(16).padStart(2, '0')).join('');
  }

  function parseHex(h) {
    const n = parseInt(h.slice(1), 16);
    return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
  }

  function cssVar(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  }

  // ---------- tree ----------

  function nodeKey(node, parentKey) {
    return node.id || (parentKey + '/' + (node.sensorType || node.name));
  }

  function isShown(node) {
    return node.visible || settings().showHidden;
  }

  function isCollapsed(key, node) {
    if (key in S.collapsed) return S.collapsed[key];
    return node.expanded === false;
  }

  function toggleCollapsed(key, container) {
    const collapsed = !container.classList.contains('collapsed');
    container.classList.toggle('collapsed', collapsed);
    S.collapsed[key] = collapsed;
    save('k26k.collapsed', S.collapsed);
    if (!collapsed) requestAnimationFrame(drawAllCharts);
  }

  // Everything that changes the layout. Values are not part of it.
  function structureSignature(state) {
    const parts = [JSON.stringify(state.settings.columns), state.settings.showHidden, state.settings.plot.visible,
      state.settings.plot.stacked, state.settings.temperatureUnit];
    (function walk(nodes) {
      for (const n of nodes) {
        parts.push(n.kind, n.id || '', n.name, n.visible, n.expanded, n.plot || false, n.color || '');
        if (n.children) walk(n.children);
      }
    })(state.nodes);
    return parts.join('|');
  }

  function render() {
    const state = S.state;
    const tree = $('tree');
    tree.textContent = '';
    S.rows.clear();
    S.sensors.clear();
    S.charts = [];

    for (const hw of state.nodes) {
      if (!isShown(hw)) continue;
      tree.appendChild(renderHardware(hw, '', false));
    }

    renderPlotCard();
    applyFilter();
    updateValues();
    updateEmpty();
    drawAllCharts();
  }

  function renderHardware(hw, parentKey, nested) {
    const key = nodeKey(hw, parentKey);
    const card = el('section', nested ? 'card sub-hardware' : 'card');
    card.dataset.search = hw.name.toLowerCase();
    if (isCollapsed(key, hw)) card.classList.add('collapsed');

    const head = el('button', 'card-head');
    head.type = 'button';
    const icon = el('img');
    icon.src = 'images_icon/' + (HARDWARE_ICONS[hw.hardwareType] || 'cpu.png');
    icon.alt = '';
    head.appendChild(icon);
    head.appendChild(el('span', 'card-title', hw.name));
    const meta = el('span', 'card-meta');
    head.appendChild(meta);
    head.appendChild(el('span', 'chev'));
    head.addEventListener('click', () => toggleCollapsed(key, card));
    card.appendChild(head);

    const body = el('div', 'card-body');
    let count = 0;
    for (const child of hw.children || []) {
      if (!isShown(child)) continue;
      if (child.kind === 'group') {
        const g = renderGroup(child, key);
        if (g) { body.appendChild(g); count++; }
      } else if (child.kind === 'hardware') {
        body.appendChild(renderHardware(child, key, true));
        count++;
      }
    }
    meta.textContent = countSensors(hw) + ' Sensoren';
    card.appendChild(body);
    if (!hw.visible) card.classList.add('hidden-sensor');
    return card;
  }

  function countSensors(node) {
    let c = 0;
    for (const child of node.children || []) {
      if (!isShown(child)) continue;
      if (child.kind === 'sensor') c++;
      else c += countSensors(child);
    }
    return c;
  }

  function renderGroup(group, parentKey) {
    const key = nodeKey(group, parentKey);
    const wrap = el('div', 'group');
    if (isCollapsed(key, group)) wrap.classList.add('collapsed');

    const head = el('button', 'group-head');
    head.type = 'button';
    const icon = el('img');
    icon.src = 'images_icon/' + (TYPE_ICONS[group.sensorType] || 'power.png');
    icon.alt = '';
    head.appendChild(icon);
    head.appendChild(el('span', 'card-title', group.name));
    head.appendChild(el('span', 'chev'));
    head.addEventListener('click', () => toggleCollapsed(key, wrap));
    wrap.appendChild(head);

    const body = el('div', 'group-body');
    let count = 0;
    for (const s of group.children || []) {
      if (s.kind !== 'sensor' || !isShown(s)) continue;
      body.appendChild(renderSensor(s));
      count++;
    }
    if (!count) return null;
    wrap.appendChild(body);
    return wrap;
  }

  function renderSensor(s) {
    S.sensors.set(s.id, s);
    const cols = settings().columns;

    const wrap = el('div', 'sensor' + (s.visible ? '' : ' hidden-sensor'));
    wrap.dataset.search = s.name.toLowerCase();

    const row = el('button', 'sensor-row');
    row.type = 'button';
    const name = el('span', 'sensor-name');
    if (s.plot && s.color) {
      const dot = el('span', 'dot');
      dot.style.background = s.color;
      dot.title = 'Im Desktop-Plot';
      name.appendChild(dot);
    }
    name.appendChild(document.createTextNode(s.name));
    row.appendChild(name);

    const values = el('span', 'sensor-values');
    const value = el('div', 'sensor-value');
    const minmax = el('div', 'sensor-minmax');
    const min = el('span');
    const max = el('span');
    if (cols.value) values.appendChild(value);
    if (cols.min) minmax.appendChild(min);
    if (cols.max) minmax.appendChild(max);
    if (cols.min || cols.max) values.appendChild(minmax);
    row.appendChild(values);

    let bar = null;
    if (PERCENT_TYPES.has(s.sensorType)) {
      bar = el('div', 'bar');
      bar.appendChild(el('i'));
      row.appendChild(bar);
    }

    wrap.appendChild(row);

    const chartBox = el('div', 'sensor-chart');
    wrap.appendChild(chartBox);

    const entry = { wrap, value, min, max, bar, chartBox, chart: null };
    S.rows.set(s.id, entry);

    row.addEventListener('click', () => {
      if (S.open.has(s.id)) S.open.delete(s.id); else S.open.add(s.id);
      save('k26k.openSensors', Array.from(S.open));
      syncSensorChart(s.id);
      if (S.open.has(s.id)) fetchHistory();
    });

    syncSensorChart(s.id);
    return wrap;
  }

  function syncSensorChart(id) {
    const entry = S.rows.get(id);
    if (!entry) return;
    const isOpen = S.open.has(id);
    if (isOpen && !entry.chart) {
      const s = S.sensors.get(id);
      entry.chart = createChart(entry.chartBox, {
        ids: [id],
        height: 150,
        color: () => colorFor(S.sensors.get(id))
      });
      drawChart(entry.chart);
    } else if (!isOpen && entry.chart) {
      S.charts = S.charts.filter((c) => c !== entry.chart);
      entry.chartBox.textContent = '';
      entry.chart = null;
    }
  }

  function colorFor(s) {
    if (s && s.color) return s.color;
    return cssVar('--accent') || '#82aaff';
  }

  function updateValues() {
    const cols = settings().columns;
    for (const [id, entry] of S.rows) {
      const s = S.sensors.get(id);
      if (!s) continue;
      entry.value.textContent = s.text.value;
      entry.min.textContent = '↓ ' + s.text.min;
      entry.max.textContent = '↑ ' + s.text.max;
      if (!cols.value && !cols.min && !cols.max) entry.value.textContent = '';
      if (entry.bar) {
        const v = Math.max(0, Math.min(100, s.value || 0));
        entry.bar.firstChild.style.width = v + '%';
        entry.bar.firstChild.style.background = colorFor(s);
      }
    }
  }

  // Copies the new values into the sensor objects the rows were built from.
  function mergeValues(state) {
    (function walk(nodes) {
      for (const n of nodes) {
        if (n.kind === 'sensor') {
          const s = S.sensors.get(n.id);
          if (s) {
            s.value = n.value; s.min = n.min; s.max = n.max; s.text = n.text; s.unit = n.unit;
          }
        } else if (n.children) {
          walk(n.children);
        }
      }
    })(state.nodes);
  }

  function updateEmpty() {
    const empty = $('empty');
    const hasAny = $('tree').children.length > 0;
    empty.hidden = hasAny;
    empty.textContent = 'Keine Sensoren sichtbar.';
  }

  // ---------- plot card (mirrors the desktop plot) ----------

  function plottedSensors() {
    const list = [];
    (function walk(nodes) {
      for (const n of nodes) {
        if (n.kind === 'sensor') { if (n.plot) list.push(n); } else if (n.children) walk(n.children);
      }
    })(S.state.nodes);
    return list;
  }

  function renderPlotCard() {
    const card = $('plotCard');
    const box = $('plotCharts');
    box.textContent = '';
    const plot = settings().plot;
    const sensors = plottedSensors();
    for (const s of sensors) if (!S.sensors.has(s.id)) S.sensors.set(s.id, s);

    card.hidden = !(plot.visible && sensors.length);
    if (card.hidden) return;

    $('plotMeta').textContent = sensors.length + (sensors.length === 1 ? ' Sensor' : ' Sensoren') +
      (plot.stacked ? ' · gestapelte Achsen' : '');

    if (plot.stacked) {
      // one chart per sensor type, in the same top-to-bottom order as the desktop
      const byType = new Map();
      for (const s of sensors.slice().sort((a, b) => a.typeIndex - b.typeIndex)) {
        if (!byType.has(s.sensorType)) byType.set(s.sensorType, []);
        byType.get(s.sensorType).push(s);
      }
      for (const [type, list] of byType) {
        const wrap = el('div');
        wrap.appendChild(el('div', 'chart-title', typeTitle(type) + (list[0].unit ? ' (' + list[0].unit + ')' : '')));
        box.appendChild(wrap);
        createChart(wrap, { ids: list.map((s) => s.id), height: 150, legend: list.length > 1 });
      }
    } else {
      const wrap = el('div');
      box.appendChild(wrap);
      createChart(wrap, { ids: sensors.map((s) => s.id), height: 240, legend: true, multiAxis: true });
    }
  }

  function typeTitle(type) {
    const names = {
      Voltage: 'Spannung', Current: 'Strom', Clock: 'Takt', Load: 'Last', Temperature: 'Temperatur',
      Fan: 'Lüfter', Flow: 'Durchfluss', Control: 'Steuerung', Level: 'Füllstand', Power: 'Leistung',
      Data: 'Daten', SmallData: 'Daten', Throughput: 'Durchsatz', Frequency: 'Frequenz', Energy: 'Energie',
      Noise: 'Lautstärke', Humidity: 'Feuchte', Factor: 'Faktor', Timing: 'Timing', TimeSpan: 'Zeit',
      Conductivity: 'Leitfähigkeit'
    };
    return names[type] || type;
  }

  // ---------- charts ----------

  function createChart(container, opts) {
    const box = el('div', 'chart');
    const canvas = el('canvas');
    canvas.style.height = opts.height + 'px';
    const tip = el('div', 'tooltip');
    tip.hidden = true;
    box.appendChild(canvas);
    box.appendChild(tip);
    container.appendChild(box);

    const chart = { opts, canvas, tip, cursor: null, hideTimer: null, legend: null };

    if (opts.legend) {
      chart.legend = el('div', 'legend');
      container.appendChild(chart.legend);
    }

    const setCursor = (ev) => {
      const rect = canvas.getBoundingClientRect();
      chart.cursor = Math.max(0, Math.min(1, (ev.clientX - rect.left) / rect.width));
      clearTimeout(chart.hideTimer);
      drawChart(chart);
    };
    const clearCursor = (delay) => {
      clearTimeout(chart.hideTimer);
      chart.hideTimer = setTimeout(() => { chart.cursor = null; drawChart(chart); }, delay);
    };
    canvas.addEventListener('pointerdown', setCursor);
    canvas.addEventListener('pointermove', (ev) => { if (ev.pointerType === 'mouse' || ev.buttons) setCursor(ev); });
    canvas.addEventListener('pointerup', (ev) => clearCursor(ev.pointerType === 'mouse' ? 1500 : 2500));
    canvas.addEventListener('pointerleave', (ev) => { if (ev.pointerType === 'mouse') clearCursor(0); });
    canvas.addEventListener('pointercancel', () => clearCursor(0));

    S.charts.push(chart);
    return chart;
  }

  function drawAllCharts() {
    for (const c of S.charts) drawChart(c);
  }

  function niceTicks(min, max, count) {
    if (!(max > min)) { max = min + 1; min = min - 1; }
    const raw = (max - min) / count;
    const mag = Math.pow(10, Math.floor(Math.log10(raw)));
    const norm = raw / mag;
    const step = (norm < 1.5 ? 1 : norm < 3 ? 2 : norm < 7 ? 5 : 10) * mag;
    const lo = Math.floor(min / step) * step;
    const hi = Math.ceil(max / step) * step;
    const ticks = [];
    for (let v = lo; v <= hi + step / 2; v += step) ticks.push(+v.toFixed(10));
    return { lo, hi, ticks };
  }

  function axisLabel(v, unit) {
    if (unit === 'B/s') return formatNumber(v, unit).replace('/s', '');
    const a = Math.abs(v);
    if (a >= 10000) return (v / 1000).toFixed(0) + 'k';
    if (a >= 100 || Number.isInteger(v)) return v.toFixed(0);
    if (a >= 10) return v.toFixed(1);
    return v.toFixed(2);
  }

  function drawChart(chart) {
    const { canvas, opts } = chart;
    if (!canvas.isConnected || canvas.offsetParent === null) return;

    const dpr = window.devicePixelRatio || 1;
    const w = canvas.clientWidth, h = opts.height;
    if (!w) return;
    if (canvas.width !== Math.round(w * dpr) || canvas.height !== Math.round(h * dpr)) {
      canvas.width = Math.round(w * dpr);
      canvas.height = Math.round(h * dpr);
    }
    const ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

    const bg = cssVar('--plot-bg'), text = cssVar('--plot-text'), grid = cssVar('--grid'), gridMinor = cssVar('--grid-minor');
    const span = effectiveSpan();
    const stroke = Math.max(1, Math.min(4, settings().plot.stroke || 2));
    const elapsed = S.historyTime ? Math.max(0, (Date.now() - S.historyTime) / 1000) : 0;

    // series
    const series = opts.ids.map((id) => {
      const s = S.sensors.get(id);
      const pts = (S.history[id] || []).map((p) => [p[0] + elapsed, p[1]]);
      // extend with the latest live value so the curve reaches "now"
      if (s && s.value !== null && s.value !== undefined && isFinite(s.value)) pts.push([0, s.value]);
      return { id, s, pts: pts.filter((p) => p[0] <= span), color: opts.color ? opts.color() : colorFor(s), unit: s ? s.unit : '' };
    });

    // y scales: one per unit when multiAxis, otherwise shared
    const groups = new Map();
    for (const se of series) {
      const k = opts.multiAxis ? se.unit : '_';
      if (!groups.has(k)) groups.set(k, { min: Infinity, max: -Infinity, unit: se.unit });
      const g = groups.get(k);
      for (const p of se.pts) { if (p[1] < g.min) g.min = p[1]; if (p[1] > g.max) g.max = p[1]; }
    }
    for (const g of groups.values()) {
      if (!isFinite(g.min)) { g.min = 0; g.max = 1; }
      if (g.unit === '%' && g.min >= 0 && g.max <= 100) {
        // percentages always use the full 0..100 range
        Object.assign(g, niceTicks(0, 100, h > 180 ? 5 : 4), { lo: 0, hi: 100 });
        continue;
      }
      const pad = (g.max - g.min) * 0.08 || Math.abs(g.max) * 0.05 || 1;
      let lo = g.min - pad;
      if (g.min >= 0 && lo < 0) lo = 0; // no negative axis for values that cannot be negative
      Object.assign(g, niceTicks(lo, g.max + pad, h > 180 ? 5 : 4));
    }
    const groupList = Array.from(groups.values());

    ctx.font = '11px system-ui, -apple-system, "Segoe UI", Roboto, sans-serif';
    const leftW = Math.max(28, ...groupList[0].ticks.map((t) => ctx.measureText(axisLabel(t, groupList[0].unit)).width)) + 8;
    const rightW = groupList.length > 1
      ? Math.max(28, ...groupList[1].ticks.map((t) => ctx.measureText(axisLabel(t, groupList[1].unit)).width)) + 8
      : 8;
    const top = 8, bottom = 20;
    const pw = Math.max(10, w - leftW - rightW), ph = Math.max(10, h - top - bottom);
    const x = (ago) => leftW + pw * (1 - ago / span);
    const y = (g, v) => top + ph * (1 - (v - g.lo) / (g.hi - g.lo || 1));

    ctx.fillStyle = bg;
    ctx.fillRect(0, 0, w, h);

    // grid + y labels
    ctx.lineWidth = 1;
    ctx.textBaseline = 'middle';
    const g0 = groupList[0];
    for (const t of g0.ticks) {
      const yy = Math.round(y(g0, t)) + 0.5;
      ctx.strokeStyle = gridMinor;
      ctx.beginPath(); ctx.moveTo(leftW, yy); ctx.lineTo(leftW + pw, yy); ctx.stroke();
      ctx.fillStyle = text;
      ctx.textAlign = 'right';
      ctx.fillText(axisLabel(t, g0.unit), leftW - 6, yy);
    }
    if (groupList.length > 1) {
      const g1 = groupList[1];
      ctx.textAlign = 'left';
      for (const t of g1.ticks) ctx.fillText(axisLabel(t, g1.unit), leftW + pw + 6, y(g1, t));
    }

    // x grid + labels
    const xTicks = timeTicks(span);
    ctx.textAlign = 'center';
    ctx.textBaseline = 'top';
    for (const ago of xTicks) {
      const xx = Math.round(x(ago)) + 0.5;
      ctx.strokeStyle = gridMinor;
      ctx.beginPath(); ctx.moveTo(xx, top); ctx.lineTo(xx, top + ph); ctx.stroke();
      ctx.fillStyle = text;
      ctx.fillText(formatAgo(ago), Math.min(Math.max(xx, leftW + 14), leftW + pw - 14), top + ph + 5);
    }

    // frame
    ctx.strokeStyle = grid;
    ctx.strokeRect(leftW + 0.5, top + 0.5, pw - 1, ph - 1);

    // lines
    ctx.save();
    ctx.beginPath(); ctx.rect(leftW, top, pw, ph); ctx.clip();
    ctx.lineJoin = 'round';
    ctx.lineCap = 'round';
    for (const se of series) {
      if (!se.pts.length) continue;
      const g = groups.get(opts.multiAxis ? se.unit : '_');
      ctx.strokeStyle = se.color;
      ctx.lineWidth = stroke;
      ctx.beginPath();
      se.pts.forEach((p, i) => { const xx = x(p[0]), yy = y(g, p[1]); if (i) ctx.lineTo(xx, yy); else ctx.moveTo(xx, yy); });
      ctx.stroke();
      if (series.length === 1) {
        // soft area under a single curve
        const last = se.pts[se.pts.length - 1], first = se.pts[0];
        ctx.lineTo(x(last[0]), top + ph); ctx.lineTo(x(first[0]), top + ph); ctx.closePath();
        ctx.globalAlpha = 0.12; ctx.fillStyle = se.color; ctx.fill(); ctx.globalAlpha = 1;
      }
    }
    ctx.restore();

    // legend
    if (chart.legend) {
      const html = series.map((se) => '<span><i class="dot" style="background:' + se.color + '"></i>' +
        escapeHtml(legendName(se.s)) + ' <b>' + escapeHtml(se.s ? se.s.text.value : '') + '</b></span>').join('');
      if (chart.legend.innerHTML !== html) chart.legend.innerHTML = html;
    }

    // cursor + tooltip
    if (chart.cursor !== null) {
      const ago = span * (1 - chart.cursor);
      const xx = leftW + pw * chart.cursor;
      ctx.strokeStyle = text;
      ctx.globalAlpha = 0.6;
      ctx.beginPath(); ctx.moveTo(xx, top); ctx.lineTo(xx, top + ph); ctx.stroke();
      ctx.globalAlpha = 1;

      const lines = [];
      for (const se of series) {
        const p = nearest(se.pts, ago);
        if (!p) continue;
        const g = groups.get(opts.multiAxis ? se.unit : '_');
        ctx.fillStyle = se.color;
        ctx.beginPath(); ctx.arc(x(p[0]), y(g, p[1]), 3.5, 0, Math.PI * 2); ctx.fill();
        lines.push('<span style="color:' + se.color + '">●</span> ' + (series.length > 1 ? escapeHtml(legendName(se.s)) + ' ' : '') +
          '<b>' + escapeHtml(formatNumber(p[1], se.unit)) + '</b>');
      }
      chart.tip.innerHTML = '<div>' + formatAgo(ago) + '</div>' + lines.join('<br>');
      chart.tip.hidden = !lines.length;
      const tw = chart.tip.offsetWidth;
      chart.tip.style.left = Math.max(4, Math.min(w - tw - 4, xx < w / 2 ? xx + 10 : xx - tw - 10)) + 'px';
    } else {
      chart.tip.hidden = true;
    }
  }

  function legendName(s) {
    return s ? s.name : '';
  }

  function nearest(pts, ago) {
    let best = null, d = Infinity;
    for (const p of pts) {
      const dd = Math.abs(p[0] - ago);
      if (dd < d) { d = dd; best = p; }
    }
    return best;
  }

  function timeTicks(span) {
    const steps = [10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200, 10800, 21600, 43200];
    const step = steps.find((s) => span / s <= 5) || 43200;
    const ticks = [];
    for (let t = step; t < span; t += step) ticks.push(t);
    return ticks;
  }

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
  }

  // ---------- filter / menu ----------

  function applyFilter() {
    const q = S.filter.trim().toLowerCase();
    for (const card of $('tree').querySelectorAll('.card')) card.classList.remove('hidden-by-filter');
    for (const sensor of $('tree').querySelectorAll('.sensor')) {
      sensor.classList.toggle('hidden-by-filter', !!q && !sensor.dataset.search.includes(q) &&
        !sensor.closest('.card').dataset.search.includes(q));
    }
    if (!q) { for (const g of $('tree').querySelectorAll('.group')) g.classList.remove('hidden-by-filter'); return; }
    for (const g of $('tree').querySelectorAll('.group')) {
      g.classList.toggle('hidden-by-filter', !g.querySelector('.sensor:not(.hidden-by-filter)'));
    }
    for (const card of Array.from($('tree').querySelectorAll('.card')).reverse()) {
      card.classList.toggle('hidden-by-filter', !card.querySelector('.sensor:not(.hidden-by-filter)'));
      if (q && !card.classList.contains('hidden-by-filter')) card.classList.remove('collapsed');
    }
    for (const g of $('tree').querySelectorAll('.group')) if (!g.classList.contains('hidden-by-filter')) g.classList.remove('collapsed');
    requestAnimationFrame(drawAllCharts);
  }

  function renderSpanChips() {
    const box = $('spanChips');
    box.textContent = '';
    for (const sp of SPANS) {
      const b = el('button', 'chip' + (S.span === sp.s ? ' active' : ''), sp.label);
      b.type = 'button';
      if (sp.s === 0) b.title = 'Zeitraum aus dem Desktop-Plot übernehmen';
      b.addEventListener('click', () => {
        S.span = sp.s;
        save('k26k.span', S.span);
        renderSpanChips();
        fetchHistory();
      });
      box.appendChild(b);
    }
  }

  function renderMirrorInfo() {
    const st = settings();
    const cols = ['value', 'min', 'max'].filter((c) => st.columns[c]).map((c) => ({ value: 'Wert', min: 'Min', max: 'Max' })[c]);
    $('mirrorInfo').textContent = 'Vom Desktop übernommen: Design „' + (st.theme ? st.theme.name : '?') + '“, °' + st.temperatureUnit +
      ', Spalten ' + (cols.join('/') || 'keine') + ', ausgeblendete Sensoren ' + (st.showHidden ? 'sichtbar' : 'versteckt') +
      ', Plot ' + (st.plot.visible ? 'an' : 'aus') + ', Aktualisierung alle ' + (st.updateInterval / 1000) + ' s.';
  }

  function setAllCollapsed(collapsed) {
    for (const c of $('tree').querySelectorAll('.card, .group')) {
      c.classList.toggle('collapsed', collapsed);
    }
    (function walk(nodes, parentKey) {
      for (const n of nodes) {
        if (n.kind === 'sensor') continue;
        const key = nodeKey(n, parentKey);
        S.collapsed[key] = collapsed;
        if (n.children) walk(n.children, key);
      }
    })(S.state ? S.state.nodes : [], '');
    save('k26k.collapsed', S.collapsed);
    requestAnimationFrame(drawAllCharts);
  }

  function initMenu() {
    const btn = $('menuBtn'), menu = $('menu');
    btn.addEventListener('click', () => {
      menu.hidden = !menu.hidden;
      btn.setAttribute('aria-expanded', String(!menu.hidden));
      if (!menu.hidden) renderMirrorInfo();
    });
    $('filter').addEventListener('input', (e) => { S.filter = e.target.value; applyFilter(); });
    $('expandAll').addEventListener('click', () => setAllCollapsed(false));
    $('collapseAll').addEventListener('click', () => setAllCollapsed(true));
    $('resetLocal').addEventListener('click', () => {
      S.collapsed = {}; save('k26k.collapsed', {});
      S.open.clear(); save('k26k.openSensors', []);
      S.span = 0; save('k26k.span', 0);
      S.signature = '';
      renderSpanChips();
      if (S.state) { render(); S.signature = structureSignature(S.state); }
      fetchHistory();
    });
    renderSpanChips();
  }

  // ---------- polling ----------

  async function fetchState() {
    clearTimeout(S.stateTimer);
    try {
      const res = await fetch('api/state', { cache: 'no-store' });
      if (!res.ok) throw new Error('HTTP ' + res.status);
      const state = await res.json();
      const first = !S.state;
      S.state = state;
      applyTheme(state.settings.theme);
      $('machine').textContent = state.machine;
      document.title = state.machine + ' · K26K Hardware Monitor';

      const sig = structureSignature(state);
      if (sig !== S.signature) {
        S.signature = sig;
        render();
      } else {
        mergeValues(state);
        updateValues();
        drawAllCharts();
      }
      if (!$('menu').hidden) renderMirrorInfo();

      S.lastOk = Date.now();
      S.failures = 0;
      setStatus(true);
      if (first) fetchHistory();
    } catch (e) {
      S.failures++;
      setStatus(false, e.message);
    }
    scheduleState();
  }

  function scheduleState() {
    if (document.hidden) return;
    const interval = Math.max(500, settings().updateInterval || 1000);
    const backoff = S.failures ? Math.min(10000, interval * Math.pow(2, S.failures)) : interval;
    S.stateTimer = setTimeout(fetchState, backoff);
  }

  async function fetchHistory() {
    clearTimeout(S.historyTimer);
    if (!S.state || S.historyBusy) { scheduleHistory(); return; }
    const ids = new Set(Array.from(S.open).filter((id) => S.sensors.has(id)));
    if (settings().plot.visible) for (const s of plottedSensors()) ids.add(s.id);
    if (!ids.size) { scheduleHistory(); return; }

    S.historyBusy = true;
    try {
      const points = Math.max(60, Math.min(800, Math.round(window.innerWidth * Math.min(2, window.devicePixelRatio || 1) / 2)));
      const url = 'api/history?span=' + effectiveSpan() + '&points=' + points + '&ids=' +
        Array.from(ids).map(encodeURIComponent).join(',');
      const res = await fetch(url, { cache: 'no-store' });
      if (res.ok) {
        const data = await res.json();
        S.history = data.series || {};
        S.historyTime = Date.now();
        drawAllCharts();
      }
    } catch (e) { /* the state poll reports connection problems */ }
    S.historyBusy = false;
    scheduleHistory();
  }

  function scheduleHistory() {
    if (document.hidden) return;
    // longer spans need fewer refreshes
    const interval = Math.max(2000, settings().updateInterval || 1000, effectiveSpan() / 200 * 1000);
    S.historyTimer = setTimeout(fetchHistory, interval);
  }

  function setStatus(ok, message) {
    const dot = $('status');
    dot.classList.toggle('ok', ok);
    dot.classList.toggle('bad', !ok);
    const sub = $('subtitle');
    if (ok) {
      const st = S.state;
      sub.textContent = 'Live · ' + new Date().toLocaleTimeString() + (st ? ' · v' + st.version : '');
    } else {
      sub.textContent = 'Keine Verbindung' + (S.lastOk ? ' seit ' + new Date(S.lastOk).toLocaleTimeString() : '') +
        (message ? ' (' + message + ')' : '');
    }
  }

  document.addEventListener('visibilitychange', () => {
    if (document.hidden) {
      clearTimeout(S.stateTimer);
      clearTimeout(S.historyTimer);
    } else {
      fetchState();
      fetchHistory();
    }
  });

  let resizeFrame = 0;
  window.addEventListener('resize', () => {
    cancelAnimationFrame(resizeFrame);
    resizeFrame = requestAnimationFrame(drawAllCharts);
  });

  initMenu();
  fetchState();
})();
