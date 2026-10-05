// 應用程式:主選單 → 遊戲(3D 舞台 + 操作台)→ 結果
import { h, btn, segmented } from './dom.js';
import { Game, STEPS, EYES, EYE_LABEL } from '../game/state.js';
import { score, summarizeEyes } from '../game/scoring.js';
import { drawChart, CHART_W, CHART_H } from '../render/chartCanvas.js';
import { ClinicScene } from '../render/scene.js';
import { fmtRx } from '../sim/optics.js';
import { faces } from './phoropterFace.js';
import { buildQuickbar, openRecordSheet, recordDone, mainAsk, cycleJcc } from './quickbar.js';
import { fx, tick } from './feedback.js';
import { openRetFocus } from './retFocus.js';
import {
  buildSetup, buildPhoro, buildRet, buildChartPanel, buildSheet, buildLog, buildHelp,
} from './panels.js';

const fill = (el, ...kids) => { el.replaceChildren(...kids.flat(Infinity).filter((k) => k !== null && k !== undefined && k !== false)); return el; };
const BEST_KEY = 'optom3d-best-v1';
const GUIDE_KEY = 'optom3d-guide-v1';
const loadBest = () => { try { return JSON.parse(localStorage.getItem(BEST_KEY) || '{}'); } catch { return {}; } };
const saveBest = (mode, s) => { try { const b = loadBest(); if (!b[mode] || s > b[mode]) { b[mode] = s; localStorage.setItem(BEST_KEY, JSON.stringify(b)); return true; } } catch { /* ignore */ } return false; };

const MODES = {
  tutorial: { name: '教學模式', tag: '建議先玩', desc: '一步一步帶你做:每一步有目標、提示與過關條件,可以看「受測者視野」與上帝視角提示。', timed: false },
  exam: { name: '考試模擬', tag: '18 分鐘', desc: '照期中考:前置完成後開始計時,OD + OS 各做一次。沒有提示,結束後給分數與逐項回饋。', timed: true },
  free: { name: '自由練習', tag: '無限制', desc: '不受步驟限制,隨時顯示上帝視角(實際處方、預測視力),用來試錯、理解每個動作的後果。', timed: false },
};

// 沒有 WebGL 時的替代品:介面其餘部分不受影響
class FlatScene {
  constructor(el) {
    this.station = 'overview';
    this.chartTex = { needsUpdate: false };
    el.append(h('div', { style: { position: 'absolute', inset: 0, display: 'grid', placeItems: 'center', color: '#cfe0e6', textAlign: 'center', padding: '24px' } },
      h('div', {}, h('div', { style: { fontSize: '40px' } }, '🏥'), h('p', {}, '這個裝置不支援 3D 畫面(WebGL),已切換成 2D 操作模式。'), h('p', { style: { fontSize: '13px', opacity: 0.8 } }, '所有練習功能照常使用。'))));
  }
  goTo(name) { this.station = name; }
  refresh() {}
  pulseSpeak() {}
  dispose() {}
}

const STATION_FOR_TAB = { setup: 'overview', phoro: 'phoro', ret: 'ret', chart: 'chart', sheet: 'desk', log: 'patient', help: 'overview' };
const TAB_LABEL = { setup: '前置', phoro: '驗光台', ret: '檢影', chart: '視力表', sheet: '紀錄單', log: '對話', help: '速查' };
const STEP_GOAL = {
  setup: '儀器、位置、光線都設好。可以點 3D 診間裡的黃色熱點,或在操作台逐項處理。',
  ret: '雙眼睜開,用檢影鏡中和兩眼的兩個軸。',
  wd: '雙眼同時給工作距離度數,並把 Ret 度數與工作距離記到紀錄單。',
  va: '遮住另一隻眼,從 0.6–0.8 開始問當下最佳視力;< 0.8 加測 PH。',
  mp1: '霧視 → 每次 −0.25D → 最大正度數 + 最佳視力。',
  duo: '紅綠檢查:做到「綠色第一片清楚」。',
  jcc: '選對路線(APA / PPAP / 鐘面圖),確認軸與度數。',
  mp2: '退掉 JCC,重新 MPMVA,記錄度數與最佳 VA。',
  done: '完成!',
};

export class App {
  constructor(root) {
    this.root = root;
    this.opts = { mode: 'tutorial', difficulty: 2, seed: Math.floor(Math.random() * 90000) + 1 };
    this.showMenu();
  }

  /* ---------------- 主選單 ---------------- */
  showMenu() {
    this.teardown();
    const o = this.opts;
    const best = loadBest();
    const el = h('div', { class: 'overlay' });
    const draw = () => {
      fill(el, h('div', { class: 'card' },
        h('h1', {}, '視光診間 3D 練習'),
        h('p', { class: 'sub' }, '自覺式驗光完整練習:前置 → 檢影 → 1st MPMVA → 紅綠 → JCC / 鐘面圖 → 2nd MPMVA。每位受測者的屈光度都不一樣,答案會有猶豫、也會有偏好。'),
        h('div', { class: 'modes' }, Object.entries(MODES).map(([id, m]) =>
          h('button', { type: 'button', class: `mode${o.mode === id ? ' on' : ''}`, onclick: () => { o.mode = id; draw(); } },
            h('em', {}, m.tag), h('b', {}, m.name), h('span', {}, m.desc),
            best[id] ? h('span', { class: 'best' }, `最佳分數 ${best[id]}`) : null))),
        h('div', { class: 'opts' },
          h('div', {}, h('label', {}, '難度'), segmented([[1, '入門'], [2, '一般'], [3, '挑戰']], o.difficulty, (v) => { o.difficulty = v; draw(); })),
          h('div', {}, h('label', {}, '受測者編號'),
            h('input', { type: 'number', min: 1, max: 99999, value: o.seed, 'aria-label': '受測者編號(同一編號會生出同一位受測者)', style: { width: '96px', padding: '8px', border: '1.5px solid #b6c4ca', borderRadius: '8px', font: 'inherit' }, onchange: (e) => { o.seed = Math.max(1, Math.floor(+e.target.value || 1)); } }),
            btn('🎲 隨機', () => { o.seed = Math.floor(Math.random() * 90000) + 1; draw(); }, { cls: 'sm' }))),
        h('div', { class: 'startrow' }, btn('開始練習 ▶', () => this.start(o), { cls: 'primary' }),
          h('span', { class: 'best' }, `診間格局依你的草圖:受測者 → 綜合驗光儀 → 驗光師 → 6 m 視力表,投影機在左上角。`)),
        h('div', { class: 'tips' }, 'iPad / 手機請橫放。拖曳 3D 畫面可以左右看,點黃色熱點可以直接操作儀器。用「期中考範圍」:單眼流程,期末的雙眼平衡之後再擴充。')));
    };
    draw();
    this.root.replaceChildren(el);
  }

  teardown() {
    this.rf?.close?.(false);
    this.rf = null;
    this.recClose?.();
    this.recClose = null;
    if (this.onKey) { document.removeEventListener('keydown', this.onKey); document.removeEventListener('pointerdown', this.onPtr, true); this.onKey = null; }
    this.scene?.dispose?.();
    this.scene = null;
    clearInterval(this.timerId);
    this.root.replaceChildren();
  }

  /* ---------------- 遊戲 ---------------- */
  start(opts) {
    this.teardown();
    const game = (this.game = new Game({ mode: opts.mode, seed: opts.seed, difficulty: opts.difficulty }));
    this.tab = 'setup';
    this.cardMin = window.matchMedia('(max-height: 560px), (max-width: 600px)').matches;
    this.menuOpen = false;
    window.__game = game;

    // 視標畫布(同時貼到 3D 螢幕)
    this.chartCanvas = document.createElement('canvas');
    this.chartCanvas.width = CHART_W; this.chartCanvas.height = CHART_H;

    const stage = (this.stage = h('div', { id: 'stage' }));
    const glwrap = h('div', { class: 'glwrap' });
    stage.append(glwrap);
    this.topbar = h('div', { class: 'topbar' });
    this.stepcard = h('aside', { class: 'stepcard', 'aria-live': 'polite' });
    this.bubble = h('div', { class: 'bubble', role: 'status', 'aria-live': 'polite' });
    this.stations = h('nav', { class: 'stations', 'aria-label': '視角' });
    stage.append(this.topbar, this.stepcard, this.bubble, this.stations);

    this.deck = h('section', { id: 'deck' });
    this.tabsEl = h('div', { class: 'tabs', role: 'tablist' });
    this.deckBody = h('div', { class: 'deckbody' });
    this.quick = null;
    this.deck.append(this.tabsEl, this.deckBody);
    const app = (this.appEl = h('div', { id: 'app' }, stage, this.deck));
    this.root.replaceChildren(app);

    this.toastEl = h('div', { class: 'toast', role: 'status' });
    document.body.append(this.toastEl);

    try {
      this.scene = new ClinicScene(glwrap, game, this.chartCanvas, { onHotspot: (id) => this.hotspot(id) });
    } catch (err) {
      // 沒有 WebGL 時退回純 2D 操作(仍可完整練習)
      console.warn('WebGL 不可用,改用 2D 模式', err);
      this.scene = new FlatScene(glwrap);
    }

    const ctx = { toast: (t, warn) => this.toast(t, warn), ask: (fn) => this.ask(fn), showTab: (t) => this.showTab(t), openRetFocus: () => this.openFocus(), openRecord: () => this.openRecord() };
    this.ctx = ctx;
    this.panels = {
      setup: buildSetup(game, ctx),
      phoro: buildPhoro(game, ctx),
      ret: buildRet(game, ctx),
      chart: buildChartPanel(game, ctx),
      sheet: buildSheet(game),
      log: buildLog(game),
      help: buildHelp(),
    };
    this.quick = buildQuickbar(game, ctx);
    this.deck.append(this.quick.el);
    this.appEl.dataset.deck = 'm';
    this.deckPref = null;
    this.rfDismissed = false;
    this.unsub = game.on((kind) => this.onGame(kind));
    this.renderTabs();
    this.renderStations();
    this.showTab('setup');
    this.renderTop();
    this.renderCard();
    this.repaintChart();
    this.timerId = setInterval(() => this.renderTimer(), 500);
    this.renderTimer();
    this.phoroSig = this.sig();
    this.onKey = (e) => this.key(e);
    this.onPtr = () => { this.tabNav = false; };
    document.addEventListener('keydown', this.onKey);
    document.addEventListener('pointerdown', this.onPtr, true);
    // 開場提示
    this.showBubble('sys', '系統', `受測者 ${game.patient.name}(${game.patient.age} 歲)已就座。先完成前置檢查。`);
    if (opts.mode === 'tutorial') this.maybeGuide();
  }

  // 第一次玩教學模式:30 秒看懂怎麼操作
  maybeGuide() {
    try { if (localStorage.getItem(GUIDE_KEY)) return; } catch { /* ignore */ }
    const touch = window.matchMedia('(pointer: coarse)').matches;
    const ov = h('div', { class: 'overlay glass guide' }, h('div', { class: 'card small' },
      h('h2', {}, '30 秒上手'),
      h('ol', { class: 'guide-list' },
        h('li', {}, h('b', {}, '左上角步驟卡'), ':告訴你這一步要做什麼,清單全部打勾 ✓ 就按「完成此步驟」。'),
        h('li', {}, h('b', {}, '黃色熱點 / 下方視角列'), ':點 3D 診間裡的儀器就能操作;拖曳畫面可以左右看。'),
        h('li', {}, h('b', {}, '綜合驗光儀'), touch ? ':用手指轉旋鈕,或點旋鈕左半(−)/右半(+)。' : ':拖曳旋鈕轉動、點左半(−)/右半(+),滑鼠滾輪也可以。'),
        h('li', {}, h('b', {}, '底部常用操作列'), ':遮眼、問受測者、調度數、記錄都在這裡,受測者的回答也會顯示在上面。'),
        touch ? null : h('li', {}, h('b', {}, '鍵盤'), ':空白鍵 = 問受測者,←→ = 球面 ±0.25,↑↓ = 視標大小,Enter = 完成此步驟(其餘見「速查」)。')),
      h('div', { class: 'startrow' }, btn('開始 ▶', () => { try { localStorage.setItem(GUIDE_KEY, '1'); } catch { /* ignore */ } ov.remove(); }, { cls: 'primary' }))));
    document.body.append(ov);
  }

  openRecord() {
    this.recClose?.();
    const close = openRecordSheet(this.game);
    this.recClose = () => { close(); this.recClose = null; };
  }

  // 驗光儀的鏡片 / 輔助鏡 / JCC 狀態:變了就「喀」一聲
  sig() {
    const P = this.game.phoro;
    return ['OD', 'OS'].map((e) => `${P[e].s},${P[e].c},${P[e].a},${P[e].aux}`).join('|') + `|${P.jcc.mode}${P.jcc.pos}|${P.occ.OD}${P.occ.OS}|${P.aperture}`;
  }

  /* ---------------- 鍵盤快捷鍵(電腦版) ---------------- */
  key(e) {
    const g = this.game;
    if (!g || e.ctrlKey || e.metaKey || e.altKey) return;
    const t = e.target;
    if (t && (t.isContentEditable || /^(INPUT|SELECT|TEXTAREA)$/.test(t.tagName))) return;
    if (document.querySelector('.overlay') || this.rf) return; // 選單、結果、記錄視窗、檢影專注模式開著時不攔截
    // 用 Tab 移到按鈕上時,空白鍵 / Enter 照常按那顆按鈕;滑鼠點過的按鈕則不搶走快捷鍵
    if (e.key === 'Tab') { this.tabNav = true; return; }
    if ((e.key === ' ' || e.key === 'Enter') && this.tabNav && t?.matches?.('button, a, summary')) return;
    const eye = g.activeEye;
    const k = e.key;
    const run = (fn) => { e.preventDefault(); t?.blur?.(); fn(); };
    switch (k) {
      case ' ': { const f = mainAsk(g); if (f) run(() => this.ask(f)); else e.preventDefault(); break; }
      case 'ArrowLeft': run(() => g.stepSph(eye, e.shiftKey ? -1 : -0.25)); break;
      case 'ArrowRight': run(() => g.stepSph(eye, e.shiftKey ? 1 : 0.25)); break;
      case 'ArrowUp': run(() => g.stepRow(-1)); break;
      case 'ArrowDown': run(() => g.stepRow(1)); break;
      case 'z': case 'Z': run(() => this.ask(() => g.askCompare(-0.25))); break;
      case 'x': case 'X': run(() => this.ask(() => g.askCompare(0.25))); break;
      case 'f': case 'F': run(() => g.flipJcc()); break;
      case 'j': case 'J': run(() => cycleJcc(g)); break;
      case '[': run(() => g.stepAxis(eye, -5)); break;
      case ']': run(() => g.stepAxis(eye, 5)); break;
      case '-': run(() => g.stepCyl(eye, -0.25)); break;
      case '=': case '+': run(() => g.stepCyl(eye, 0.25)); break;
      case 'o': case 'O': run(() => g.setTestEye(eye)); break;
      case 'r': case 'R': if (recordDone(g) !== null) run(() => this.openRecord()); break; // 紅綠步驟沒有要記錄的
      case 'Enter': run(() => this.tryAdvance()); break;
      default: break;
    }
  }

  onGame(kind) {
    const g = this.game;
    const sg = this.sig();
    if (sg !== this.phoroSig) { this.phoroSig = sg; tick(); }
    if (kind === 'step') { this.hintText = null; this.showWhy = false; }
    this.scene?.refresh();
    if (kind === 'slider') return;
    if (window.__pfDrag) { for (const f of faces) f.update(); return; } // 旋鈕拖曳中:只更新驗光儀,放開後再整體刷新
    this.repaintChart();
    this.quick?.render();
    if (kind === 'log') {
      const m = g.log[g.log.length - 1];
      if (m.who === 'patient') { this.showBubble('patient', g.patient.name, m.text); this.scene?.pulseSpeak(); }
      else if (m.who === 'sys') this.showBubble('sys', '系統', m.text);
      this.panels.log.render();
      return;
    }
    this.renderTop();
    this.renderCard();
    this.renderStations();
    this.panels[this.tab]?.render();
    this.panels.sheet.render?.();
    if (kind === 'step') {
      this.onStepChange();
    }
  }

  onStepChange() {
    const g = this.game;
    const id = g.stepDef.phase ?? g.stepId;
    const want = { setup: 'setup', ret: 'ret', wd: 'phoro', va: 'phoro', mp1: 'phoro', duo: 'phoro', jcc: 'phoro', mp2: 'phoro', done: 'phoro' }[id];
    if (id !== 'ret' && id !== 'wd' && this.rf) this.rf.close(false); // 離開檢影 / 工作距離就收起專注模式
    if (g.stepId === 'done') { setTimeout(() => this.showResult(), 500); return; }
    if (id === 'ret') this.rfDismissed = false;
    if (want && (want !== this.tab || (want === 'ret' && !this.rf))) this.showTab(want);
    if (id === 'duo') { g.setAux(g.activeEye, 'RG'); g.setChart({ mode: 'digits', isolate: true }); }
    if (id === 'jcc') g.setChart({ mode: 'honey' });
    if (id === 'mp1' || id === 'mp2' || id === 'va') g.setChart({ mode: 'digits', isolate: true, row: id === 'va' ? 0.6 : g.chart.row });
  }

  repaintChart() {
    const g = this.game;
    drawChart(this.chartCanvas.getContext('2d'), g.chart, g.patient.seed);
    this.scene && (this.scene.chartTex.needsUpdate = true);
    this.panels?.chart?.paint?.();
  }

  /* ---------------- 頂端列 ---------------- */
  renderTop() {
    const g = this.game;
    const m = MODES[g.mode];
    fill(this.topbar,
      h('span', { class: 'chip' }, h('b', {}, g.patient.name), ` · ${g.patient.age} 歲`),
      h('span', { class: 'chip' }, m.name),
      h('span', { class: 'chip timer', id: 'timer' }, '--:--'),
      h('span', { class: 'sp' }),
      h('button', { class: 'iconb', type: 'button', 'aria-label': '速查', onclick: () => this.showTab('help') }, '❓'),
      h('button', { class: 'iconb', type: 'button', 'aria-label': '選單', onclick: () => { this.menuOpen = !this.menuOpen; this.renderTop(); } }, '☰'),
      this.menuOpen ? this.menuPop() : null,
    );
    this.renderTimer();
  }

  menuPop() {
    const g = this.game;
    const close = (fn) => () => { this.menuOpen = false; fn(); this.renderTop(); };
    return h('div', { class: 'menu-pop' },
      g.mode !== 'exam' ? h('button', { type: 'button', onclick: close(() => { g.god = !g.god; g.emit(); }) }, g.god ? '關閉上帝視角提示' : '開啟上帝視角提示') : null,
      h('button', { type: 'button', onclick: close(() => this.showTab('log')) }, '對話紀錄'),
      h('button', { type: 'button', onclick: close(() => { fx.sound = !fx.sound; if (fx.sound) tick(true); }) }, fx.sound ? '🔊 旋鈕音效:開' : '🔈 旋鈕音效:關'),
      h('hr'),
      h('button', { type: 'button', onclick: close(() => this.start({ ...this.opts, seed: g.seed })) }, '重來這位受測者'),
      h('button', { type: 'button', onclick: close(() => { this.opts.seed = Math.floor(Math.random() * 90000) + 1; this.start(this.opts); }) }, '換一位受測者'),
      h('button', { type: 'button', onclick: close(() => this.showMenu()) }, '回主選單'),
      g.stepId !== 'done' ? h('button', { type: 'button', onclick: close(() => { g.advance({ force: true }); g.step = STEPS.length - 1; g.tEnd = Date.now(); g.emit('step'); }) }, '直接結算') : null,
    );
  }

  renderTimer() {
    const t = this.topbar?.querySelector('#timer');
    if (!t || !this.game) return;
    const g = this.game;
    const el = g.elapsedMs;
    const fmt = (ms) => { const s = Math.floor(Math.abs(ms) / 1000); return `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`; };
    if (g.isExam) {
      if (!g.tStart) { t.textContent = '18:00'; t.title = '前置完成後開始計時'; t.classList.remove('late'); return; }
      const left = 18 * 60000 - el;
      t.textContent = left >= 0 ? fmt(left) : `+${fmt(left)}`;
      t.classList.toggle('late', left < 0);
    } else {
      t.textContent = g.tStart ? fmt(el) : '00:00';
    }
  }

  /* ---------------- 步驟卡 ---------------- */
  tryAdvance() {
    const g = this.game;
    if (g.stepId === 'done') return;
    if (g.mode === 'tutorial' && !g.canAdvance().ok) { this.showWhy = true; this.cardMin = false; this.renderCard(); this.toast('還沒達成這一步的條件', true); return; }
    this.hintText = null; this.showWhy = false;
    g.advance();
  }

  // 條件少就全列;多(前置 9 項)就只列進度 + 接下來的兩項,避免步驟卡蓋住 3D 熱點
  condList(conds, failed) {
    const nOk = conds.filter((c) => c.ok).length;
    const li = (c) => h('li', { class: c.ok ? 'ok' : '' }, h('i', { 'aria-hidden': 'true' }, c.ok ? '✓' : ''), c.label);
    if (conds.length <= 4) return h('ul', { class: `conds${failed ? ' failed' : ''}` }, conds.map(li));
    const left = conds.filter((c) => !c.ok);
    return h('div', { class: 'condsum' },
      h('div', { class: 'progline' }, h('b', {}, `${nOk} / ${conds.length}`), h('div', { class: 'tr' }, h('div', { class: 'fi', style: { width: `${(nOk / conds.length) * 100}%` } }))),
      left.length ? h('ul', { class: `conds${failed ? ' failed' : ''}` }, (failed ? left : left.slice(0, 2)).map(li), !failed && left.length > 2 ? h('li', { class: 'more' }, `…還有 ${left.length - 2} 項`) : null) : null);
  }

  renderCard() {
    const g = this.game;
    const def = g.stepDef;
    const idx = g.step;
    const total = STEPS.length - 1;
    const key = def.phase ?? def.id;
    const showHint = g.mode !== 'exam';
    const last = g.stepId === 'done';
    // 即時過關清單(教學 / 自由練習);考試模式不顯示
    const conds = showHint && !last ? g.conditions() : [];
    const nOk = conds.filter((c) => c.ok).length;
    const ready = conds.length > 0 && nOk === conds.length;
    const dots = STEPS.slice(0, -1).map((s, i) => h('i', { class: `${i < idx ? 'done' : i === idx ? 'now' : ''}${i === 3 || i === 8 ? ' gap' : ''}`, title: `${i + 1}. ${s.eye ? s.eye + ' · ' : ''}${s.title}` }));
    this.stepcard.classList.toggle('min', this.cardMin);
    this.stepcard.classList.toggle('ready', ready);
    const acts = [
      showHint ? btn(this.cardMin ? '💡' : '💡 提示', () => { this.hintText = g.hint(); this.cardMin = false; this.renderCard(); }, { cls: 'sm', aria: '提示' }) : null,
      last ? null : btn(`${idx === total - 1 ? '完成並結算' : '完成此步驟'}${this.cardMin && conds.length ? `(${nOk}/${conds.length})` : ''} ▶`, () => this.tryAdvance(), { cls: `primary${ready ? ' go' : ''}`, title: 'Enter' }),
    ];
    const toggle = h('button', { class: 'b sm ghost', type: 'button', 'aria-label': this.cardMin ? '展開' : '收合', onclick: () => { this.cardMin = !this.cardMin; this.renderCard(); } }, this.cardMin ? '▾' : '▴');
    const failed = g.mode === 'tutorial' && this.showWhy;
    const nextC = conds.find((c) => !c.ok);
    fill(this.stepcard,
      h('div', { class: 'sc-top' },
        h('span', { class: 'no' }, last ? '完成' : `${idx + 1}/${total}`),
        h('h3', {}, `${def.eye ? EYE_LABEL[def.eye] + ' · ' : ''}${def.title}`),
        toggle),
      // 收合時:第二行 = 下一個還沒做到的條件 + 按鈕
      this.cardMin ? h('div', { class: 'minrow' },
        h('span', { class: `nextc${ready ? ' ok' : ''}` }, ready ? '✓ 條件都達成了' : nextC ? `下一步:${nextC.label}` : ''),
        h('div', { class: 'acts' }, acts)) : null,
      h('div', { class: 'dots' }, dots),
      h('p', { class: 'goal' }, g.mode === 'exam' ? '考試模式:沒有提示,照你的流程做。' : STEP_GOAL[key] ?? ''),
      conds.length ? this.condList(conds, failed) : null,
      this.hintText ? h('p', { class: 'goal hint' }, this.hintText) : null,
      failed && !g.canAdvance().ok ? h('div', { class: 'why' }, h('ul', {}, g.check().why.map((w) => h('li', {}, w)))) : null,
      this.cardMin ? null : h('div', { class: 'acts' }, acts),
    );
  }

  /* ---------------- 視角與分頁 ---------------- */
  renderStations() {
    const defs = [['overview', '診間全景'], ['phoro', '驗光台'], ['ret', '檢影'], ['chart', '視力表'], ['desk', '紀錄單']];
    fill(this.stations, ...defs.map(([id, label]) =>
      h('button', { type: 'button', class: this.scene?.station === id ? 'on' : '', onclick: () => this.goStation(id) }, label)));
  }

  goStation(id) {
    const tab = { overview: this.tab === 'chart' || this.tab === 'ret' || this.tab === 'sheet' ? 'setup' : this.tab, phoro: 'phoro', ret: 'ret', chart: 'chart', desk: 'sheet' }[id] ?? this.tab;
    this.scene.goTo(id);
    this.showTab(tab, false);
    this.renderStations();
  }

  renderTabs() {
    const g = this.game;
    const tabs = ['setup', 'phoro', 'ret', 'chart', 'sheet', 'log', 'help'];
    this.tabsEl.replaceChildren(
      ...tabs.map((t) => h('button', { type: 'button', role: 'tab', class: `t-${t}${this.tab === t ? ' on' : ''}`, 'aria-selected': this.tab === t ? 'true' : 'false', onclick: () => { this.showTab(t); } }, TAB_LABEL[t])),
      h('span', { class: 'grow' }),
      h('button', { type: 'button', class: 'fold', 'aria-label': '收合/展開操作台', onclick: () => this.cycleDeck() }, '⇕'),
    );
  }

  showTab(tab, moveCamera = true) {
    this.panels[this.tab]?.onHide?.();
    this.tab = tab;
    this.renderTabs();
    const p = this.panels[tab];
    p.render();
    this.deckBody.replaceChildren(p.el);
    this.deckBody.scrollTop = 0;
    p.onShow?.();
    this.applyDeck(tab);
    this.quick?.render();
    if (tab === 'ret' && !this.rf && !this.rfDismissed) this.openFocus();
    if (moveCamera) { this.scene.goTo(STATION_FOR_TAB[tab] ?? 'overview'); this.renderStations(); }
  }

  isSmall() { return window.matchMedia('(max-width: 700px), (max-height: 520px)').matches; }

  applyDeck(tab = this.tab) {
    this.appEl.dataset.deck = tab === 'ret' ? 'l' : this.deckPref ?? 'm';
  }

  cycleDeck() {
    const order = ['m', 'l', 's'];
    const cur = this.appEl.dataset.deck;
    const next = order[(order.indexOf(cur) + 1) % order.length];
    this.deckPref = next;
    this.appEl.dataset.deck = next;
    this.appEl.classList.toggle('collapsed', next === 's');
  }

  openFocus() {
    if (this.rf) return;
    this.rf = openRetFocus(this.game, this.ctx, { onClose: (manual) => { this.rf = null; if (manual) this.rfDismissed = true; } });
  }

  hotspot(id) {
    const g = this.game;
    switch (id) {
      case 'phoro': this.showTab('phoro'); break;
      case 'chart': this.showTab('chart'); break;
      case 'sheet': this.showTab('sheet'); break;
      case 'ret': this.showTab('ret'); break;
      case 'sanitize': g.sanitize(); this.toast('已消毒'); this.focusSetup('sanitize'); break;
      case 'switch': g.setDim(!g.room.dim); this.toast(g.room.dim ? '燈光已調暗' : '燈光已打開'); this.focusSetup('dim'); break;
      case 'table': this.focusSetup('height'); break;
      case 'pd': g.measurePD(); this.toast(`PD ${g.measuredPD} mm,接著把驗光儀的 PD 調成一樣`); this.focusSetup('pd'); break;
      default: break;
    }
  }

  focusSetup(id) {
    if (this.tab !== 'setup') this.showTab('setup', false);
    if (this.appEl.dataset.deck === 's') { this.deckPref = 'm'; this.appEl.dataset.deck = 'm'; this.appEl.classList.remove('collapsed'); }
    requestAnimationFrame(() => this.panels.setup.focus?.(id));
  }

  /* ---------------- 與受測者對話 ---------------- */
  ask(fn) {
    const r = fn();
    if (this.tab === 'phoro' || this.tab === 'chart') this.panels[this.tab].render();
    return r;
  }

  showBubble(kind, who, text) {
    this.bubble.className = `bubble on ${kind === 'sys' ? 'sys' : ''}`;
    this.bubble.replaceChildren(h('small', {}, who), text);
    clearTimeout(this.bubbleT);
    this.bubbleT = setTimeout(() => this.bubble.classList.remove('on'), kind === 'sys' ? 4500 : 6500);
  }

  toast(text, warn = false) {
    this.toastEl.textContent = text;
    this.toastEl.className = `toast on${warn ? ' warn' : ''}`;
    clearTimeout(this.toastT);
    this.toastT = setTimeout(() => this.toastEl.classList.remove('on'), 1500);
  }

  /* ---------------- 結果 ---------------- */
  showResult() {
    const g = this.game;
    const res = score(g);
    const isBest = saveBest(g.mode, res.score100);
    const eyes = summarizeEyes(g);
    const C = 2 * Math.PI * 52;
    const ring = h('div', { class: 'ring' });
    ring.innerHTML = `<svg width="120" height="120" viewBox="0 0 120 120"><circle cx="60" cy="60" r="52" fill="none" stroke="#e3eaed" stroke-width="12"/><circle cx="60" cy="60" r="52" fill="none" stroke="${res.score100 >= 80 ? '#1b7b45' : res.score100 >= 60 ? '#2b7f98' : '#d98b3a'}" stroke-width="12" stroke-linecap="round" stroke-dasharray="${(C * res.score100) / 100} ${C}"/></svg><div class="num"><div>${res.score100}<small>/ 100</small></div></div>`;
    const grade = res.score100 >= 90 ? '非常穩!可以去考試了' : res.score100 >= 75 ? '不錯,再精進幾個細節' : res.score100 >= 60 ? '流程大致對,有些步驟要再練' : '再多練幾次,先看下面的逐項回饋';
    const ov = h('div', { class: 'overlay glass' }, h('div', { class: 'card' },
      h('div', { class: 'scorehead' }, ring,
        h('div', {}, h('h1', {}, grade),
          h('p', { class: 'sub' }, `${MODES[g.mode].name} · 受測者 ${g.patient.name}(編號 ${g.seed})· 用時 ${res.minutes.toFixed(1)} 分鐘${isBest ? ' · 🎉 新的最佳分數' : ''}`))),
      h('div', { class: 'bars' }, Object.entries(res.cats).map(([k, v]) => {
        const p = v.max ? v.got / v.max : 0;
        return h('div', { class: 'bar' }, h('span', {}, k), h('div', { class: 'tr' }, h('div', { class: `fi${p < 0.6 ? ' low' : ''}`, style: { width: `${Math.round(p * 100)}%` } })), h('span', { class: 'rx small' }, `${v.got.toFixed(1)}/${v.max}`));
      })),
      h('table', { class: 'cmp' },
        h('thead', {}, h('tr', {}, h('th', {}, ''), h('th', {}, '實際處方'), h('th', {}, '你的最終度數'), h('th', {}, '差距'))),
        h('tbody', {}, eyes.map((e) => h('tr', {}, h('td', {}, e.eye), h('td', { class: 'rx' }, fmtRx(e.trueRx)), h('td', { class: 'rx' }, fmtRx(e.final)), h('td', { class: 'rx' }, `${e.dist.toFixed(2)} D`))))),
      h('details', { class: 'items', open: true }, h('summary', {}, '逐項回饋'),
        res.items.filter((i) => i.got < i.max || i.note).map((i) => h('div', { class: 'it' },
          h('span', {}, `${i.cat} · ${i.label}`), h('span', { class: `pt ${i.got >= i.max ? 'full' : i.got <= 0 ? 'zero' : ''}` }, `${i.got}/${i.max}`),
          i.note ? h('span', { class: 'nt' }, i.note) : null))),
      h('div', { class: 'startrow' },
        btn('再練一位 ▶', () => { this.opts.seed = Math.floor(Math.random() * 90000) + 1; this.opts.mode = g.mode; this.start(this.opts); ov.remove(); }, { cls: 'primary' }),
        btn('同一位再練一次', () => { ov.remove(); this.start({ ...this.opts, mode: g.mode, seed: g.seed }); }),
        btn('看看診間', () => ov.remove()),
        btn('回主選單', () => { ov.remove(); this.showMenu(); })),
    ));
    document.body.append(ov);
  }
}
