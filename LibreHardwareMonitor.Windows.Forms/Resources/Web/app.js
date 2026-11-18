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

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
  }

  // ---------- icons (monochrome line icons, 24x24) ----------

  const BOLT = '<path d="M13 3 5.5 13H11l-1 8 7.5-10H12l1-8Z"/>';
  const CLOCK = '<circle cx="12" cy="12" r="8"/><path d="M12 8v4l2.5 1.5"/>';
  const CHIP = '<rect x="6.5" y="6.5" width="11" height="11" rx="2"/><path d="M9.5 3.5v3M14.5 3.5v3M9.5 17.5v3M14.5 17.5v3M3.5 9.5h3M3.5 14.5h3M17.5 9.5h3M17.5 14.5h3"/>';
  const FAN = '<circle cx="12" cy="12" r="1.6"/><path d="M12 10.4c-.6-3.2.4-6.4 3-6.4 2.8 0 3 3.6-3 6.4ZM13.6 12c3.2-.6 6.4.4 6.4 3 0 2.8-3.6 3-6.4-3ZM12 13.6c.6 3.2-.4 6.4-3 6.4-2.8 0-3-3.6 3-6.4ZM10.4 12c-3.2.6-6.4-.4-6.4-3 0-2.8 3.6-3 6.4 3Z"/>';
  const DROP = '<path d="M12 3.5s6 6.4 6 10.7a6 6 0 0 1-12 0C6 9.9 12 3.5 12 3.5Z"/>';
  const DATA = '<ellipse cx="12" cy="6" rx="7" ry="2.8"/><path d="M5 6v12c0 1.5 3.1 2.8 7 2.8s7-1.3 7-2.8V6M5 12c0 1.5 3.1 2.8 7 2.8s7-1.3 7-2.8"/>';
  const SLIDERS = '<path d="M4 7h10M18 7h2M4 17h4M12 17h8"/><circle cx="16" cy="7" r="2"/><circle cx="10" cy="17" r="2"/>';

  const ICONS = {
    Cpu: '<rect x="6.5" y="6.5" width="11" height="11" rx="2"/><rect x="9.5" y="9.5" width="5" height="5" rx="1"/><path d="M9.5 3.5v3M14.5 3.5v3M9.5 17.5v3M14.5 17.5v3M3.5 9.5h3M3.5 14.5h3M17.5 9.5h3M17.5 14.5h3"/>',
    Gpu: '<rect x="3" y="6.5" width="18" height="10" rx="2"/><circle cx="9" cy="11.5" r="2.5"/><path d="M14.5 9.5h3.5M14.5 13.5h3.5M6 16.5v3"/>',
    Memory: '<rect x="3" y="7" width="18" height="9" rx="1.5"/><path d="M7 10.5v2M11 10.5v2M15 10.5v2M6 16v3M18 16v3"/>',
    Storage: '<rect x="4" y="4" width="16" height="16" rx="3"/><circle cx="12" cy="12" r="4"/><path d="M12 12h.01"/>',
    Network: '<path d="M3 9.5a13 13 0 0 1 18 0M6 13a8.5 8.5 0 0 1 12 0M9 16.5a4 4 0 0 1 6 0"/><path d="M12 20h.01"/>',
    Motherboard: '<rect x="4" y="4" width="16" height="16" rx="2"/><rect x="7" y="7" width="5" height="5" rx="1"/><path d="M15 7.5h2M15 10.5h2M7 15.5h10"/>',
    Chip: CHIP,
    Cooler: FAN,
    Battery: '<rect x="3" y="8" width="16" height="8" rx="2"/><path d="M21 11v2M6.5 11v2M9.5 11v2"/>',
    Power: BOLT,
    Temperature: '<path d="M14 14.6V5.5a2 2 0 0 0-4 0v9.1a4 4 0 1 0 4 0Z"/><path d="M12 11v5"/>',
    Load: '<path d="M4.5 17a8 8 0 1 1 15 0"/><path d="m12 15 3.5-4.5"/>',
    Clock: CLOCK,
    Fan: FAN,
    Flow: DROP,
    Control: SLIDERS,
    Level: '<rect x="7" y="3.5" width="10" height="17" rx="2"/><path d="M7 13h10"/>',
    Data: DATA,
    Throughput: '<path d="M8 4v16m0 0-3-3m3 3 3-3M16 20V4m0 0-3 3m3-3 3 3"/>',
    Noise: '<path d="M4 10v4h3.5l4.5 4V6l-4.5 4H4ZM16 9a4 4 0 0 1 0 6M18.5 6.5a7.5 7.5 0 0 1 0 11"/>',
    Factor: '<path d="M6 18 18 6"/><circle cx="7.5" cy="7.5" r="1.5"/><circle cx="16.5" cy="16.5" r="1.5"/>',
    Chart: '<path d="M4 19h16M5 15l4-5 4 3 6-7"/>',
    Chevron: '<path d="m6 9 6 6 6-6"/>'
  };

  const HARDWARE_ICON = {
    Cpu: 'Cpu', GpuNvidia: 'Gpu', GpuAmd: 'Gpu', GpuIntel: 'Gpu', Memory: 'Memory', Storage: 'Storage',
    Network: 'Network', Motherboard: 'Motherboard', SuperIO: 'Chip', EmbeddedController: 'Chip',
    Cooler: 'Cooler', Psu: 'Power', Battery: 'Battery', PowerMonitor: 'Power'
  };

  const TYPE_ICON = {
    Voltage: 'Power', Current: 'Power', Power: 'Power', Energy: 'Power', Clock: 'Clock', Timing: 'Clock',
    TimeSpan: 'Clock', Frequency: 'Clock', Load: 'Load', Temperature: 'Temperature', Fan: 'Fan', Flow: 'Flow',
    Humidity: 'Flow', Conductivity: 'Power', Control: 'Control', Level: 'Level', Data: 'Data', SmallData: 'Data',
    Throughput: 'Throughput', Noise: 'Noise', Factor: 'Factor'
  };

  function icon(name) {
    return '<svg class="i" viewBox="0 0 24 24" aria-hidden="true">' + (ICONS[name] || CHIP) + '</svg>';
  }

  // Groups for the jump bar at the top.
  const CATEGORIES = [
    { id: 'cpu', label: 'CPU', icon: 'Cpu', types: ['Cpu'] },
    { id: 'gpu', label: 'GPU', icon: 'Gpu', types: ['GpuNvidia', 'GpuAmd', 'GpuIntel'] },
    { id: 'board', label: 'Mainboard', icon: 'Motherboard', types: ['Motherboard', 'SuperIO', 'EmbeddedController'] },
    { id: 'ram', label: 'Speicher', icon: 'Memory', types: ['Memory'] },
    { id: 'disk', label: 'Laufwerke', icon: 'Storage', types: ['Storage'] },
    { id: 'net', label: 'Netzwerk', icon: 'Network', types: ['Network'] },
    { id: 'cool', label: 'Kühlung', icon: 'Cooler', types: ['Cooler'] },
    { id: 'power', label: 'Energie', icon: 'Power', types: ['Psu', 'Battery', 'PowerMonitor'] }
  ];

  const SPANS = [
    { s: 0, label: 'Desktop' }, { s: 60, label: '1 min' }, { s: 300, label: '5 min' },
    { s: 600, label: '10 min' }, { s: 1800, label: '30 min' }, { s: 3600, label: '1 h' },
    { s: 6 * 3600, label: '6 h' }, { s: 24 * 3600, label: '24 h' }
  ];

  const TYPE_TITLES = {
    Voltage: 'Spannung', Current: 'Strom', Clock: 'Takt', Load: 'Last', Temperature: 'Temperatur',
    Fan: 'Lüfter', Flow: 'Durchfluss', Control: 'Steuerung', Level: 'Füllstand', Power: 'Leistung',
    Data: 'Daten', SmallData: 'Daten', Throughput: 'Durchsatz', Frequency: 'Frequenz', Energy: 'Energie',
    Noise: 'Lautstärke', Humidity: 'Feuchte', Factor: 'Faktor', Timing: 'Timing', TimeSpan: 'Zeit',
    Conductivity: 'Leitfähigkeit'
  };

  // Line styles so that curves can be told apart without color.
  const DASHES = [[], [7, 4], [1.5, 4], [10, 3, 2, 3]];

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
    if (seconds < 1) return 'jetzt';
    if (seconds < 120) return '−' + Math.round(seconds) + ' s';
    if (seconds < 7200) return '−' + Math.round(seconds / 60) + ' min';
    return '−' + (seconds / 3600).toFixed(seconds < 36000 ? 1 : 0).replace('.0', '') + ' h';
  }

  // "56.2 °C" -> ["56.2", "°C"]
  function splitUnit(text) {
    const i = String(text).lastIndexOf(' ');
    if (i <= 0) return [String(text), ''];
    const num = text.slice(0, i), unit = text.slice(i + 1);
    return /\d/.test(num) ? [num, unit] : [String(text), ''];
  }

  // ---------- state ----------

  const S = {
    state: null,
    signature: '',
    collapsed: load('k26k.collapsed', {}),
    open: new Set(load('k26k.openSensors', [])),
    span: load('k26k.span', 0),
    filter: '',
    sensors: new Map(),
    rows: new Map(),
    summaries: [],
    history: {},
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
      showHidden: false, columns: { value: true, min: false, max: true }, temperatureUnit: 'C',
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
  const T = {};

  function parseHex(h) {
    const n = parseInt(h.slice(1), 16);
    return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
  }

  function mix(a, b, t) {
    const pa = parseHex(a), pb = parseHex(b);
    return '#' + pa.map((v, i) => Math.round(v + (pb[i] - v) * t).toString(16).padStart(2, '0')).join('');
  }

  function rgba(hex, alpha) {
    const p = parseHex(hex);
    return 'rgba(' + p[0] + ',' + p[1] + ',' + p[2] + ',' + alpha + ')';
  }

  // Derives all surface, text and accent tokens from the desktop theme's base colors.
  function applyTheme(theme) {
    if (!theme) return;
    const key = JSON.stringify(theme);
    if (key === appliedTheme) return;
    appliedTheme = key;

    const bg = theme.background, fg = theme.foreground, dark = theme.dark;
    const black = dark && parseHex(bg).every((v) => v < 8);
    const slate = '#5b6b82';

    Object.assign(T, dark ? {
      page: black ? '#000000' : mix(bg, '#000000', 0.22),
      surface: black ? '#0c0c0d' : mix(bg, fg, 0.035),
      surface2: black ? '#161618' : mix(bg, fg, 0.075),
      border: black ? '#1f1f22' : mix(bg, fg, 0.11),
      divider: black ? '#19191b' : mix(bg, fg, 0.075),
      text: mix(fg, bg, 0.06),
      text2: mix(fg, bg, 0.36),
      accent: '#4fb3a6',
      warn: '#e5a443',
      crit: '#ef6461',
      ok: '#4cc38a',
      shadow: 'none'
    } : {
      page: mix(bg, slate, 0.075),
      surface: bg,
      surface2: mix(bg, slate, 0.05),
      border: mix(bg, slate, 0.17),
      divider: mix(bg, slate, 0.1),
      text: mix(fg, '#1c2433', 0.6),
      text2: mix(fg === '#000000' ? '#1c2433' : fg, bg, 0.3),
      accent: '#0f7a70',
      warn: '#b45f06',
      crit: '#c62828',
      ok: '#15803d',
      shadow: '0 1px 2px rgba(28,36,51,.05), 0 1px 3px rgba(28,36,51,.04)'
    });
    T.grid = rgba(T.text, dark ? 0.1 : 0.12);
    T.dark = dark;

    const r = document.documentElement.style;
    r.setProperty('--page', T.page);
    r.setProperty('--surface', T.surface);
    r.setProperty('--surface-2', T.surface2);
    r.setProperty('--border', T.border);
    r.setProperty('--divider', T.divider);
    r.setProperty('--text', T.text);
    r.setProperty('--text-2', T.text2);
    r.setProperty('--accent', T.accent);
    r.setProperty('--accent-soft', rgba(T.accent, dark ? 0.15 : 0.1));
    r.setProperty('--warn', T.warn);
    r.setProperty('--crit', T.crit);
    r.setProperty('--ok', T.ok);
    r.setProperty('--grid', T.grid);
    r.setProperty('--bar', T.text2);
    r.setProperty('--shadow', T.shadow);
    r.setProperty('--header-bg', rgba(T.page, 0.86));
    r.setProperty('color-scheme', dark ? 'dark' : 'light');
    document.querySelector('meta[name="theme-color"]').setAttribute('content', T.page);
  }

  // ---------- thresholds ----------

  function temperatureLevel(s) {
    if (s.sensorType !== 'Temperature' || s.value === null || s.value === undefined) return '';
    const c = settings().temperatureUnit === 'F' ? (s.value - 32) / 1.8 : s.value;
    return c >= 90 ? 'crit' : c >= 80 ? 'warn' : '';
  }

  function loadLevel(s) {
    if (s.sensorType !== 'Load' || s.value === null || s.value === undefined) return '';
    return s.value >= 95 ? 'crit' : s.value >= 85 ? 'warn' : '';
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

  function setCollapsed(key, container, collapsed) {
    container.classList.toggle('collapsed', collapsed);
    const head = container.querySelector(':scope > .card-head, :scope > .group-head');
    if (head) head.setAttribute('aria-expanded', String(!collapsed));
    S.collapsed[key] = collapsed;
    save('k26k.collapsed', S.collapsed);
    if (!collapsed) requestAnimationFrame(drawAllCharts);
  }

  // Everything that changes the layout. Values are not part of it.
  function structureSignature(state) {
    const st = state.settings;
    const parts = [JSON.stringify(st.columns), st.showHidden, st.plot.visible, st.plot.stacked, st.temperatureUnit];
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
    S.summaries = [];
    S.charts = [];

    for (const hw of state.nodes) {
      if (!isShown(hw)) continue;
      tree.appendChild(renderHardware(hw, '', false));
    }

    renderPlotCard();
    renderJumpbar();
    applyFilter();
    updateValues();
    updateEmpty();
    drawAllCharts();
  }

  function renderHardware(hw, parentKey, nested) {
    const key = nodeKey(hw, parentKey);
    const card = el('section', nested ? 'card sub-hardware' : 'card');
    card.dataset.search = hw.name.toLowerCase();
    card.dataset.type = hw.hardwareType || '';
    card.id = 'hw-' + key.replace(/[^a-z0-9]+/gi, '-');
    const collapsed = isCollapsed(key, hw);
    if (collapsed) card.classList.add('collapsed');

    const head = el('button', 'card-head');
    head.type = 'button';
    head.setAttribute('aria-expanded', String(!collapsed));
    head.innerHTML = '<span class="tile">' + icon(HARDWARE_ICON[hw.hardwareType]) + '</span>' +
      '<span class="head-text"><span class="card-title"></span><span class="card-summary"></span></span>' +
      '<span class="chev">' + icon('Chevron') + '</span>';
    head.querySelector('.card-title').textContent = hw.name;
    head.addEventListener('click', () => setCollapsed(key, card, !card.classList.contains('collapsed')));
    card.appendChild(head);

    S.summaries.push({ el: head.querySelector('.card-summary'), ids: summarySensors(hw), count: countSensors(hw) });

    const body = el('div', 'card-body');
    for (const child of hw.children || []) {
      if (!isShown(child)) continue;
      if (child.kind === 'group') {
        const g = renderGroup(child, key);
        if (g) body.appendChild(g);
      } else if (child.kind === 'hardware') {
        body.appendChild(renderHardware(child, key, true));
      }
    }
    card.appendChild(body);
    if (!hw.visible) card.classList.add('hidden-sensor');
    return card;
  }

  // The most telling values of a hardware item for its collapsed header.
  function summarySensors(hw) {
    const pick = {};
    (function walk(nodes) {
      for (const n of nodes) {
        if (n.kind === 'sensor') {
          if (isShown(n) && !pick[n.sensorType]) pick[n.sensorType] = n.id;
        } else if (n.children) {
          walk(n.children);
        }
      }
    })(hw.children || []);
    const order = ['Temperature', 'Load', 'Power', 'Throughput', 'Data', 'Fan', 'Level'];
    return order.filter((t) => pick[t]).slice(0, 3).map((t) => pick[t]);
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
    const sensors = (group.children || []).filter((s) => s.kind === 'sensor' && isShown(s));
    if (!sensors.length) return null;

    const wrap = el('div', 'group');
    const collapsed = isCollapsed(key, group);
    if (collapsed) wrap.classList.add('collapsed');

    const head = el('button', 'group-head');
    head.type = 'button';
    head.setAttribute('aria-expanded', String(!collapsed));
    head.innerHTML = icon(TYPE_ICON[group.sensorType]) + '<span class="label"></span><span class="chev">' + icon('Chevron') + '</span>';
    head.querySelector('.label').innerHTML = escapeHtml(group.name) + ' <span class="count">· ' + sensors.length + '</span>';
    head.addEventListener('click', () => setCollapsed(key, wrap, !wrap.classList.contains('collapsed')));
    wrap.appendChild(head);

    const body = el('div', 'group-body');
    for (const s of sensors) body.appendChild(renderSensor(s));
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
    row.setAttribute('aria-expanded', 'false');

    const dot = el('span', 'dot');
    if (s.plot && s.color) {
      dot.style.background = s.color;
      dot.title = 'Im Desktop-Verlauf';
    }
    row.appendChild(dot);
    row.appendChild(el('span', 'sensor-name', s.name));

    const values = el('span', 'sensor-values');
    const value = el('div', 'sensor-value');
    const minmax = el('div', 'sensor-minmax');
    if (cols.value) values.appendChild(value);
    if (cols.min || cols.max) values.appendChild(minmax);
    row.appendChild(values);

    let bar = null;
    if (s.unit === '%' && s.sensorType !== 'Factor') {
      bar = el('div', 'bar');
      bar.appendChild(el('i'));
      row.appendChild(bar);
    }
    wrap.appendChild(row);

    const chartBox = el('div', 'sensor-chart');
    chartBox.hidden = true;
    wrap.appendChild(chartBox);

    const entry = { wrap, row, value, minmax, bar, chartBox, chart: null, lastValue: '', lastMinMax: '' };
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
    entry.wrap.classList.toggle('open', isOpen);
    entry.row.setAttribute('aria-expanded', String(isOpen));
    entry.chartBox.hidden = !isOpen;
    if (isOpen && !entry.chart) {
      entry.chart = createChart(entry.chartBox, { ids: [id], height: 140 });
      drawChart(entry.chart);
    } else if (!isOpen && entry.chart) {
      S.charts = S.charts.filter((c) => c !== entry.chart);
      entry.chartBox.textContent = '';
      entry.chart = null;
    }
  }

  function colorFor(s) {
    return (s && s.color) || T.accent || '#5ec4b6';
  }

  function updateValues() {
    const cols = settings().columns;
    for (const [id, entry] of S.rows) {
      const s = S.sensors.get(id);
      if (!s) continue;

      const [num, unit] = splitUnit(s.text.value);
      const level = temperatureLevel(s) || loadLevel(s);
      // warnings are shown with a symbol as well, not by color alone
      const valueHtml = (level ? '<svg class="i alert" viewBox="0 0 24 24" aria-label="' + (level === 'crit' ? 'Kritisch' : 'Warnung') +
        '"><path d="M12 4 2.5 20h19L12 4Z"/><path d="M12 10v4M12 17h.01"/></svg>' : '') +
        escapeHtml(num) + (unit ? '<span class="unit">' + escapeHtml(unit) + '</span>' : '');
      if (valueHtml !== entry.lastValue) { entry.value.innerHTML = valueHtml; entry.lastValue = valueHtml; }
      entry.value.className = 'sensor-value ' + level;

      // drop the unit from Min/Max only when it is the same as the value's (e.g. KB/s vs. MB/s differ)
      const short = (t) => { const p = splitUnit(t); return p[1] === unit ? p[0] : t; };
      const parts = [];
      if (cols.min) parts.push('Min ' + short(s.text.min));
      if (cols.max) parts.push('Max ' + short(s.text.max));
      const mm = parts.join(' · ');
      if (mm !== entry.lastMinMax) { entry.minmax.textContent = mm; entry.lastMinMax = mm; }

      if (entry.bar) {
        const v = Math.max(0, Math.min(100, s.value || 0));
        entry.bar.firstChild.style.width = v + '%';
        // plotted sensors keep their plot color, all others use a neutral tone
        entry.bar.firstChild.style.background = s.plot && s.color ? s.color : '';
        entry.bar.className = 'bar ' + loadLevel(s);
      }
    }

    // collapsed cards show labelled key values, expanded cards only the sensor count
    for (const sum of S.summaries) {
      const parts = sum.ids.map((id) => S.sensors.get(id)).filter(Boolean).map((s) => {
        const [num, unit] = splitUnit(s.text.value);
        return '<span class="kv" title="' + escapeHtml(s.name) + '">' + icon(TYPE_ICON[s.sensorType]) +
          '<b>' + escapeHtml(num) + '</b>' + escapeHtml(unit) + '</span>';
      });
      const html = parts.join('') + '<span class="expanded-only">' + sum.count + (sum.count === 1 ? ' Sensor' : ' Sensoren') + '</span>';
      if (sum.html !== html) { sum.el.innerHTML = html; sum.html = html; }
    }
  }

  function mergeValues(state) {
    (function walk(nodes) {
      for (const n of nodes) {
        if (n.kind === 'sensor') {
          const s = S.sensors.get(n.id);
          if (s) { s.value = n.value; s.min = n.min; s.max = n.max; s.text = n.text; s.unit = n.unit; }
        } else if (n.children) {
          walk(n.children);
        }
      }
    })(state.nodes);
  }

  function updateEmpty() {
    const empty = $('empty');
    empty.hidden = $('tree').children.length > 0;
    empty.textContent = 'Keine Sensoren sichtbar.';
  }

  // ---------- jump bar ----------

  function renderJumpbar() {
    const bar = $('jumpbar');
    bar.textContent = '';

    if (!$('plotCard').hidden) bar.appendChild(jumpPill('Verlauf', 'Chart', 0, [$('plotCard')]));

    const cards = Array.from($('tree').children);
    for (const cat of CATEGORIES) {
      const matches = cards.filter((c) => cat.types.includes(c.dataset.type));
      if (matches.length) bar.appendChild(jumpPill(cat.label, cat.icon, matches.length, matches));
    }
    bar.hidden = bar.children.length < 2;
    updateJumpFade();
    observeSections();
  }

  // fade the right edge while more pills can be scrolled into view
  function updateJumpFade() {
    const bar = $('jumpbar');
    bar.classList.toggle('fade-end', bar.scrollLeft + bar.clientWidth < bar.scrollWidth - 4);
  }

  // highlights the pill of the section currently at the top of the screen
  let spy = null;
  function observeSections() {
    if (spy) spy.disconnect();
    if (!('IntersectionObserver' in window)) return;
    const pills = Array.from($('jumpbar').children);
    const visible = new Map();
    spy = new IntersectionObserver((entries) => {
      for (const e of entries) visible.set(e.target, e.isIntersecting);
      const first = pills.find((p) => p._targets.some((t) => visible.get(t)));
      for (const p of pills) p.classList.toggle('active', p === first);
    }, { rootMargin: '-120px 0px -55% 0px' });
    for (const p of pills) for (const t of p._targets) spy.observe(t);
  }

  function jumpPill(label, iconName, count, targets) {
    const target = targets[0];
    const b = el('button', 'pill');
    b._targets = targets;
    b.type = 'button';
    b.innerHTML = icon(iconName) + '<span></span>' + (count > 1 ? '<span class="count">' + count + '</span>' : '');
    b.children[1].textContent = label;
    b.addEventListener('click', () => {
      if (target.classList.contains('collapsed')) target.querySelector('.card-head').click();
      const top = target.getBoundingClientRect().top + window.scrollY - document.querySelector('.topbar').offsetHeight - 12;
      window.scrollTo({ top, behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
    });
    return b;
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

    $('plotMeta').textContent = sensors.length + (sensors.length === 1 ? ' Sensor' : ' Sensoren');

    if (plot.stacked) {
      // one chart per sensor type, in the same top-to-bottom order as the desktop
      const byType = new Map();
      for (const s of sensors.slice().sort((a, b) => a.typeIndex - b.typeIndex)) {
        if (!byType.has(s.sensorType)) byType.set(s.sensorType, []);
        byType.get(s.sensorType).push(s);
      }
      for (const [type, list] of byType) {
        const section = el('div', 'plot-section');
        const title = el('div', 'chart-title');
        title.appendChild(el('span', '', TYPE_TITLES[type] || type));
        title.appendChild(el('span', '', list[0].unit || ''));
        section.appendChild(title);
        box.appendChild(section);
        createChart(section, { ids: list.map((s) => s.id), height: 140, legend: true });
      }
    } else {
      const section = el('div', 'plot-section');
      box.appendChild(section);
      createChart(section, { ids: sensors.map((s) => s.id), height: 220, legend: true, multiAxis: true });
    }
  }

  // ---------- charts ----------

  function createChart(container, opts) {
    const box = el('div', 'chart');
    const canvas = el('canvas');
    canvas.style.height = opts.height + 'px';
    canvas.setAttribute('role', 'img');
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
    canvas.addEventListener('pointerup', (ev) => clearCursor(ev.pointerType === 'mouse' ? 1500 : 3000));
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

  function timeTicks(span) {
    const steps = [15, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200, 10800, 21600, 43200];
    const step = steps.find((s) => span / s <= 5) || 43200;
    const ticks = [];
    for (let t = step; t < span - step * 0.3; t += step) ticks.push(t);
    return ticks;
  }

  function nearest(pts, ago) {
    let best = null, d = Infinity;
    for (const p of pts) {
      const dd = Math.abs(p[0] - ago);
      if (dd < d) { d = dd; best = p; }
    }
    return best;
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
    ctx.clearRect(0, 0, w, h);

    const span = effectiveSpan();
    const stroke = Math.max(1.5, Math.min(3, settings().plot.stroke || 2));
    const elapsed = S.historyTime ? Math.max(0, (Date.now() - S.historyTime) / 1000) : 0;

    const series = opts.ids.map((id, index) => {
      const s = S.sensors.get(id);
      const pts = (S.history[id] || []).map((p) => [p[0] + elapsed, p[1]]);
      if (s && s.value !== null && s.value !== undefined && isFinite(s.value)) pts.push([0, s.value]);
      return {
        s, index, unit: s ? s.unit : '',
        pts: pts.filter((p) => p[0] <= span),
        color: colorFor(s),
        dash: DASHES[index % DASHES.length]
      };
    });

    // y scales: one per unit when multiAxis, otherwise shared
    const groups = new Map();
    for (const se of series) {
      const k = opts.multiAxis ? se.unit : '_';
      if (!groups.has(k)) groups.set(k, { min: Infinity, max: -Infinity, unit: se.unit });
      const g = groups.get(k);
      for (const p of se.pts) { if (p[1] < g.min) g.min = p[1]; if (p[1] > g.max) g.max = p[1]; }
    }
    const tickCount = h > 180 ? 4 : 3;
    for (const g of groups.values()) {
      if (!isFinite(g.min)) { g.min = 0; g.max = 1; }
      if (g.unit === '%' && g.min >= 0 && g.max <= 100) {
        Object.assign(g, niceTicks(0, 100, tickCount), { lo: 0, hi: 100 });
        continue;
      }
      const pad = (g.max - g.min) * 0.12 || Math.abs(g.max) * 0.05 || 1;
      let lo = g.min - pad;
      if (g.min >= 0 && lo < 0) lo = 0;
      Object.assign(g, niceTicks(lo, g.max + pad, tickCount));
    }
    const groupList = Array.from(groups.values());

    ctx.font = '500 12px ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif';
    const labelW = (g) => Math.max(...g.ticks.map((t) => ctx.measureText(axisLabel(t, g.unit)).width));
    const leftW = labelW(groupList[0]) + 10;
    const rightW = groupList.length > 1 ? labelW(groupList[1]) + 10 : 6;
    const top = 8, bottom = 22;
    const pw = Math.max(10, w - leftW - rightW), ph = Math.max(10, h - top - bottom);
    const x = (ago) => leftW + pw * (1 - ago / span);
    const y = (g, v) => top + ph * (1 - (v - g.lo) / (g.hi - g.lo || 1));

    // horizontal grid + y labels
    ctx.lineWidth = 1;
    ctx.textBaseline = 'middle';
    const g0 = groupList[0];
    for (const t of g0.ticks) {
      const yy = Math.round(y(g0, t)) + 0.5;
      ctx.strokeStyle = T.grid;
      ctx.beginPath(); ctx.moveTo(leftW, yy); ctx.lineTo(leftW + pw, yy); ctx.stroke();
      ctx.fillStyle = T.text2;
      ctx.textAlign = 'right';
      ctx.fillText(axisLabel(t, g0.unit), leftW - 8, yy);
    }
    if (groupList.length > 1) {
      const g1 = groupList[1];
      ctx.textAlign = 'left';
      for (const t of g1.ticks) ctx.fillText(axisLabel(t, g1.unit), leftW + pw + 8, y(g1, t));
    }

    // x labels (no vertical grid, it only adds noise)
    ctx.textBaseline = 'top';
    ctx.textAlign = 'center';
    ctx.fillStyle = T.text2;
    for (const ago of timeTicks(span)) {
      const xx = x(ago);
      ctx.fillText(formatAgo(ago), Math.min(Math.max(xx, leftW + 18), leftW + pw - 18), top + ph + 7);
    }

    // curves
    ctx.save();
    ctx.beginPath(); ctx.rect(leftW, top - 2, pw, ph + 4); ctx.clip();
    ctx.lineJoin = 'round';
    ctx.lineCap = 'round';
    for (const se of series) {
      if (!se.pts.length) continue;
      const g = groups.get(opts.multiAxis ? se.unit : '_');
      if (se.pts.length === 1) {
        // no history yet: show the live value as a dot at the right edge
        ctx.fillStyle = se.color;
        ctx.beginPath(); ctx.arc(x(se.pts[0][0]) - 4, y(g, se.pts[0][1]), 3.5, 0, Math.PI * 2); ctx.fill();
        continue;
      }
      if (series.length === 1) {
        // soft gradient under a single curve
        const grad = ctx.createLinearGradient(0, top, 0, top + ph);
        grad.addColorStop(0, rgba(se.color, T.dark ? 0.2 : 0.16));
        grad.addColorStop(1, rgba(se.color, 0));
        ctx.beginPath();
        se.pts.forEach((p, i) => { const xx = x(p[0]), yy = y(g, p[1]); if (i) ctx.lineTo(xx, yy); else ctx.moveTo(xx, yy); });
        ctx.lineTo(x(se.pts[se.pts.length - 1][0]), top + ph);
        ctx.lineTo(x(se.pts[0][0]), top + ph);
        ctx.closePath();
        ctx.fillStyle = grad;
        ctx.fill();
      }
      ctx.setLineDash(series.length > 1 ? se.dash : []);
      ctx.beginPath();
      se.pts.forEach((p, i) => { const xx = x(p[0]), yy = y(g, p[1]); if (i) ctx.lineTo(xx, yy); else ctx.moveTo(xx, yy); });
      if (!T.dark) {
        // a faint dark halo keeps light palette colors (yellow, light green) visible on white
        ctx.strokeStyle = 'rgba(28,36,51,.22)';
        ctx.lineWidth = stroke + 1.5;
        ctx.stroke();
      }
      ctx.strokeStyle = se.color;
      ctx.lineWidth = stroke;
      ctx.stroke();
      ctx.setLineDash([]);
    }
    ctx.restore();

    // legend
    if (chart.legend) {
      const html = series.map((se) => '<span class="chip"><i class="swatch' + (series.length > 1 && se.index % DASHES.length ? ' dash' + Math.min(2, se.index % DASHES.length) : '') +
        '" style="border-color:' + se.color + '"></i><span class="n">' + escapeHtml(se.s ? se.s.name : '') +
        '</span><b>' + escapeHtml(se.s ? se.s.text.value : '') + '</b></span>').join('');
      if (chart.legend.innerHTML !== html) chart.legend.innerHTML = html;
    }

    // a short, readable description for screen readers
    canvas.setAttribute('aria-label', series.map((se) => (se.s ? se.s.name + ' ' + se.s.text.value : '')).join(', '));

    // crosshair + tooltip
    if (chart.cursor !== null) {
      const ago = span * (1 - chart.cursor);
      const xx = leftW + pw * chart.cursor;
      ctx.strokeStyle = rgba(T.text, 0.45);
      ctx.lineWidth = 1;
      ctx.beginPath(); ctx.moveTo(Math.round(xx) + 0.5, top); ctx.lineTo(Math.round(xx) + 0.5, top + ph); ctx.stroke();

      const rows = [];
      for (const se of series) {
        const p = nearest(se.pts, ago);
        if (!p) continue;
        const g = groups.get(opts.multiAxis ? se.unit : '_');
        ctx.fillStyle = T.surface;
        ctx.beginPath(); ctx.arc(x(p[0]), y(g, p[1]), 5, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = se.color;
        ctx.beginPath(); ctx.arc(x(p[0]), y(g, p[1]), 3.5, 0, Math.PI * 2); ctx.fill();
        rows.push('<div class="r"><i class="swatch" style="border-color:' + se.color + '"></i><span>' +
          escapeHtml(se.s ? se.s.name : '') + '</span><b>' + escapeHtml(formatNumber(p[1], se.unit)) + '</b></div>');
      }
      chart.tip.innerHTML = '<div class="t">' + formatAgo(ago) + '</div>' + rows.join('');
      chart.tip.hidden = !rows.length;
      const tw = chart.tip.offsetWidth, th = chart.tip.offsetHeight;
      chart.tip.style.left = Math.max(0, Math.min(w - tw, xx < w / 2 ? xx + 14 : xx - tw - 14)) + 'px';
      chart.tip.style.top = Math.max(0, top - Math.max(0, th - ph) / 2) + 'px';
    } else {
      chart.tip.hidden = true;
    }
  }

  // ---------- filter / menu ----------

  function applyFilter() {
    const q = S.filter.trim().toLowerCase();
    const tree = $('tree');
    for (const card of tree.querySelectorAll('.card')) card.classList.remove('hidden-by-filter');
    for (const sensor of tree.querySelectorAll('.sensor')) {
      sensor.classList.toggle('hidden-by-filter', !!q && !sensor.dataset.search.includes(q) &&
        !sensor.closest('.card').dataset.search.includes(q));
    }
    if (!q) {
      for (const g of tree.querySelectorAll('.group')) g.classList.remove('hidden-by-filter');
      return;
    }
    for (const g of tree.querySelectorAll('.group')) {
      g.classList.toggle('hidden-by-filter', !g.querySelector('.sensor:not(.hidden-by-filter)'));
      if (!g.classList.contains('hidden-by-filter')) g.classList.remove('collapsed');
    }
    for (const card of Array.from(tree.querySelectorAll('.card')).reverse()) {
      card.classList.toggle('hidden-by-filter', !card.querySelector('.sensor:not(.hidden-by-filter)'));
      if (!card.classList.contains('hidden-by-filter')) card.classList.remove('collapsed');
    }
    requestAnimationFrame(drawAllCharts);
  }

  function renderSpanChips() {
    const box = $('spanChips');
    box.textContent = '';
    for (const sp of SPANS) {
      const b = el('button', S.span === sp.s ? 'active' : '', sp.label);
      b.type = 'button';
      b.setAttribute('aria-pressed', String(S.span === sp.s));
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
      ', Spalten ' + (cols.join(' / ') || 'keine') + ', ausgeblendete Sensoren ' + (st.showHidden ? 'sichtbar' : 'versteckt') +
      ', Verlauf ' + (st.plot.visible ? 'an' : 'aus') + ', Aktualisierung alle ' + (st.updateInterval / 1000).toLocaleString() + ' s.';
  }

  function setAllCollapsed(collapsed) {
    for (const c of $('tree').querySelectorAll('.card, .group')) {
      c.classList.toggle('collapsed', collapsed);
      const head = c.querySelector(':scope > .card-head, :scope > .group-head');
      if (head) head.setAttribute('aria-expanded', String(!collapsed));
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
      if (!menu.hidden) { renderMirrorInfo(); window.scrollTo({ top: 0 }); }
    });
    $('filter').addEventListener('input', (e) => { S.filter = e.target.value; applyFilter(); });
    $('expandAll').addEventListener('click', () => setAllCollapsed(false));
    $('collapseAll').addEventListener('click', () => setAllCollapsed(true));
    $('resetLocal').addEventListener('click', () => {
      S.collapsed = {}; save('k26k.collapsed', {});
      S.open.clear(); save('k26k.openSensors', []);
      S.span = 0; save('k26k.span', 0);
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
    const interval = Math.max(2000, settings().updateInterval || 1000, effectiveSpan() / 200 * 1000);
    S.historyTimer = setTimeout(fetchHistory, interval);
  }

  function setStatus(ok, message) {
    const status = $('status');
    status.classList.toggle('ok', ok);
    status.classList.toggle('bad', !ok);
    status.textContent = ok ? 'Live' : 'Getrennt';
    const sub = $('subtitle');
    if (ok) {
      sub.textContent = new Date().toLocaleTimeString() + (S.state ? ' · v' + S.state.version : '');
    } else {
      sub.textContent = (S.lastOk ? 'seit ' + new Date(S.lastOk).toLocaleTimeString() : '') + (message ? ' · ' + message : '');
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

  $('jumpbar').addEventListener('scroll', updateJumpFade, { passive: true });

  initMenu();
  fetchState();
})();
