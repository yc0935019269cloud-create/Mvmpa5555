// 遊戲狀態與所有「動作」:UI 與自動測試共用同一套邏輯
import {
  toVec, addVec, fromVec, jccVec, jccRedAxis, fmtRx, mod180, r25, VA_ROWS, logMARFromDecimal, decimalFromLogMAR,
} from '../sim/optics.js';
import {
  generatePatient, effectiveBlur, predictLogMAR, readRow, compareLenses, duochrome, clockDial,
  rowCount, bestRow, say,
} from '../sim/patient.js';
import { fullyNeutral, wdDiopter } from '../sim/retino.js';
import { makeRng } from '../sim/rng.js';

export const EYES = ['OD', 'OS'];
export const other = (e) => (e === 'OD' ? 'OS' : 'OD');
export const EYE_LABEL = { OD: '右眼 OD', OS: '左眼 OS' };

// 步驟表
export const STEPS = [
  { id: 'setup', group: 'setup', title: '前置檢查' },
  { id: 'ret', group: 'ret', title: '檢影 Ret' },
  { id: 'wd', group: 'ret', title: '給工作距離並記錄' },
  ...['OD', 'OS'].flatMap((eye) => [
    { id: `${eye}.va`, group: eye, eye, phase: 'va', title: '測 VA' },
    { id: `${eye}.mp1`, group: eye, eye, phase: 'mp1', title: '霧視 · 1st MPMVA' },
    { id: `${eye}.duo`, group: eye, eye, phase: 'duo', title: '紅綠檢查' },
    { id: `${eye}.jcc`, group: eye, eye, phase: 'jcc', title: '散光:JCC / 鐘面圖' },
    { id: `${eye}.mp2`, group: eye, eye, phase: 'mp2', title: '2nd MPMVA' },
  ]),
  { id: 'done', group: 'end', title: '完成' },
];

export const SETUP_ITEMS = [
  { id: 'sanitize', label: '儀器消毒', weight: 1 },
  { id: 'pd', label: '量 PD 並設定到綜合驗光儀', weight: 2 },
  { id: 'level', label: '綜合驗光儀調水平', weight: 1 },
  { id: 'aux', label: '輔助鏡、稜鏡挪開(O)', weight: 1 },
  { id: 'aperture', label: '雙眼窺孔打開', weight: 1 },
  { id: 'height', label: '調升降桌:受測者眼睛與驗光師等高', weight: 1 },
  { id: 'chart', label: '視標切成紅綠大 E', weight: 1 },
  { id: 'lock', label: '綜合驗光儀鎖緊', weight: 1 },
  { id: 'dim', label: '室內燈光微暗', weight: 1 },
];

const PRISM_AUX = ['6ΔU', '10ΔI'];
const clamp = (x, lo, hi) => Math.max(lo, Math.min(hi, x));

export class Game {
  constructor({ mode = 'tutorial', seed = 1, difficulty = 2 } = {}) {
    this.mode = mode;
    this.seed = seed;
    this.difficulty = difficulty;
    this.patient = generatePatient(seed, { difficulty });
    const rng = makeRng(seed * 7919 + 13);
    this.listeners = new Set();
    this.step = 0;
    this.activeEye = 'OD';
    this.god = mode === 'free';
    this.wdCm = 67;
    this.wdApplied = false;
    this.nearPD = false;
    this.autoComp = mode === 'tutorial';
    this.compAcc = { OD: 0, OS: 0 };

    // 綜合驗光儀(初始狀態故意是「前置還沒做完」)
    const auxStart = rng.pick(['PH', 'RG']);
    this.phoro = {
      OD: { s: 0, c: 0, a: 180, aux: auxStart },
      OS: { s: 0, c: 0, a: 180, aux: 'O' },
      occ: { OD: false, OS: false }, // true = 遮蓋(看不到)
      jcc: { mode: 'off', pos: 1 },
      pd: 60,
      level: Math.round(rng.range(2, 4) * 2) / 2 * (rng.chance(0.5) ? 1 : -1), // 水平偏差(度)
      aperture: false,
      locked: false,
      height: 0.25, // 0~1,0.5 附近才算與驗光師等高
    };
    this.room = { dim: false, sanitized: false };
    this.chart = { mode: 'digits', row: 0.6, isolate: true };
    this.sheet = {
      wdCm: null,
      pd: null,
      slots: { ret: {}, mp1: {}, jcc: {}, mp2: {} }, // slot[eye] = { rx }
      va: { ret: {}, mp1: {}, mp2: {} }, // va[slot][eye] = { row, delta, phRow, phDelta }
    };
    this.measuredPD = null;
    this.path = {}; // eye → { path, cyl }
    this.hist = {
      OD: { fogMax: { mp1: -9, mp2: -9 }, duo: [], reads: [], compares: 0, jccAsks: 0, clockAsks: 0 },
      OS: { fogMax: { mp1: -9, mp2: -9 }, duo: [], reads: [], compares: 0, jccAsks: 0, clockAsks: 0 },
    };
    this.retLensAtLeave = {};
    this.log = [];
    this.events = [];
    this.t0 = Date.now();
    this.tStart = null;
    this.tEnd = null;
    this.say('sys', `受測者:${this.patient.name},${this.patient.age} 歲。先完成前置檢查吧。`);
  }

  /* ---------- 事件 ---------- */
  on(fn) { this.listeners.add(fn); return () => this.listeners.delete(fn); }
  emit(kind = 'change') { for (const fn of this.listeners) fn(kind); }
  say(who, text) {
    this.log.push({ who, text, t: Date.now() });
    if (this.log.length > 80) this.log.shift();
    this.emit('log');
  }
  ev(type, data = {}) { this.events.push({ type, t: Date.now(), step: this.stepId, ...data }); }

  get stepDef() { return STEPS[this.step]; }
  get stepId() { return STEPS[this.step].id; }
  get isExam() { return this.mode === 'exam'; }
  get wdD() { return wdDiopter(this.wdCm); }
  get elapsedMs() {
    if (!this.tStart) return 0;
    return (this.tEnd ?? Date.now()) - this.tStart;
  }

  /* ---------- 鏡片 / 儀器 ---------- */
  lens(eye) { const { s, c, a } = this.phoro[eye]; return { s, c, a }; }
  trueRx(eye) { return this.patient.eyes[eye].rx; }
  trueM(eye) { return toVec(this.trueRx(eye)).M; }
  dM(eye) { return toVec(this.lens(eye)).M - this.trueM(eye); }
  jccActiveFor(eye) { return this.phoro.jcc.mode !== 'off' && eye === this.activeEye; }

  lensVec(eye) {
    let v = toVec(this.lens(eye));
    if (this.phoro[eye].aux === '+.12') v = { ...v, M: v.M + 0.12 }; // 輔助鏡 +0.12D
    if (this.jccActiveFor(eye)) {
      const { mode, pos } = this.phoro.jcc;
      v = addVec(v, jccVec(jccRedAxis(mode, pos, this.phoro[eye].a)));
    }
    return v;
  }

  setActiveEye(eye) {
    if (eye === this.activeEye) return;
    this.activeEye = eye;
    this.phoro.jcc.mode = 'off';
    this.emit();
  }
  setTestEye(eye) {
    this.phoro.occ[eye] = false;
    this.phoro.occ[other(eye)] = true;
    this.setActiveEye(eye);
    this.ev('occlude', { eye, mode: 'single' });
    this.emit();
  }
  setOcc(eye, closed) { this.phoro.occ[eye] = closed; this.ev('occlude', { eye, closed }); this.emit(); }
  openBoth() { this.phoro.occ.OD = false; this.phoro.occ.OS = false; this.ev('occlude', { mode: 'both' }); this.emit(); }

  setLens(eye, patch) {
    const l = this.phoro[eye];
    if (patch.s !== undefined) l.s = clamp(r25(patch.s), -20, 20);
    if (patch.c !== undefined) l.c = clamp(r25(patch.c), -6, 0);
    if (patch.a !== undefined) {
      let a = Math.round(patch.a);
      while (a < 1) a += 180;
      while (a > 180) a -= 180;
      l.a = a;
    }
    this.afterLens(eye);
  }
  stepSph(eye, d) { this.setLens(eye, { s: this.phoro[eye].s + d }); }
  stepCyl(eye, d) {
    // d<0 加負散光;d>0 退散光。可選「自動補償球面」:每 −0.50DC 補 +0.25DS(以 0.125 累積)
    const before = this.phoro[eye].c;
    const after = clamp(r25(before + d), -6, 0);
    this.phoro[eye].c = after;
    if (this.autoComp && after !== before) {
      this.compAcc[eye] += -(after - before) / 2; // 多 −0.25DC → +0.125DS
      while (this.compAcc[eye] >= 0.25 - 1e-9) { this.phoro[eye].s = clamp(this.phoro[eye].s + 0.25, -20, 20); this.compAcc[eye] -= 0.25; }
      while (this.compAcc[eye] <= -0.25 + 1e-9) { this.phoro[eye].s = clamp(this.phoro[eye].s - 0.25, -20, 20); this.compAcc[eye] += 0.25; }
    }
    this.afterLens(eye);
  }
  stepAxis(eye, d) { this.setLens(eye, { a: this.phoro[eye].a + d }); }
  setAux(eye, aux) { this.phoro[eye].aux = aux; this.ev('aux', { eye, aux }); this.emit(); }
  setJccMode(mode) {
    this.phoro.jcc.mode = mode;
    this.phoro.jcc.pos = 1;
    this.ev('jccMode', { mode, eye: this.activeEye });
    this.emit();
  }
  flipJcc() { if (this.phoro.jcc.mode === 'off') return; this.phoro.jcc.pos = this.phoro.jcc.pos === 1 ? 2 : 1; this.ev('jccFlip'); this.emit(); }
  describeJcc(mode, pos, a) {
    if (mode === 'P') return pos === 1 ? '紅點在 P(軸)上' : '白點在 P(軸)上';
    return `紅點在 ${mod180(jccRedAxis('A', pos, a)) || 180}° 側`;
  }
  afterLens(eye) {
    const ph = this.stepDef.phase;
    if (ph === 'mp1' || ph === 'mp2') {
      if (this.stepDef.eye === eye && this.phoro.occ[other(eye)] && !this.phoro.occ[eye]) {
        const h = this.hist[eye].fogMax;
        h[ph] = Math.max(h[ph], this.dM(eye));
      }
    }
    this.emit();
  }

  setChart(patch) {
    Object.assign(this.chart, patch);
    this.ev('chart', { ...this.chart });
    this.emit();
  }
  stepRow(dir) {
    const i = VA_ROWS.indexOf(this.chart.row);
    this.setChart({ row: VA_ROWS[clamp(i + dir, 0, VA_ROWS.length - 1)] });
  }

  /* ---------- 前置 ---------- */
  setupState() {
    const p = this.phoro;
    return {
      sanitize: this.room.sanitized,
      pd: this.sheet.pd !== null && Math.abs(p.pd - this.patient.pd) <= 1 && this.measuredPD !== null,
      level: Math.abs(p.level) <= 0.5,
      aux: p.OD.aux === 'O' && p.OS.aux === 'O',
      aperture: p.aperture,
      height: Math.abs(p.height - 0.5) <= 0.07,
      chart: this.chart.mode === 'big_e' || this.chart.mode === 'rg',
      lock: p.locked,
      dim: this.room.dim,
    };
  }
  sanitize() { this.room.sanitized = true; this.say('sys', '已用酒精棉片擦拭綜合驗光儀與額靠、下巴托。'); this.ev('sanitize'); this.emit(); }
  measurePD() {
    this.measuredPD = this.patient.pd;
    this.sheet.pd = this.patient.pd;
    this.say('sys', `用 PD 尺量得受測者遠方瞳距 ${this.patient.pd} mm。`);
    this.ev('measurePD');
    this.emit();
  }
  setPD(v, live = false) { this.phoro.pd = clamp(Math.round(v), 50, 75); this.emit(live ? 'slider' : 'change'); }
  setLevel(v, live = false) { this.phoro.level = clamp(Math.round(v * 2) / 2, -6, 6); this.emit(live ? 'slider' : 'change'); }
  setHeight(v, live = false) { this.phoro.height = clamp(v, 0, 1); this.emit(live ? 'slider' : 'change'); }
  setAperture(open) { this.phoro.aperture = open; this.emit(); }
  setAutoComp(v) { this.autoComp = v; this.compAcc = { OD: 0, OS: 0 }; this.emit(); }
  setLocked(l) { this.phoro.locked = l; this.emit(); }
  setDim(d) { this.room.dim = d; this.ev('dim', { d }); this.emit(); }
  recordPD(v) { this.sheet.pd = v; this.emit(); }

  /* ---------- 與受測者互動 ---------- */
  // 輔助鏡轉到稜鏡 → 受測者看到重影,無法比較或讀視標
  prismBlock(eyes) {
    const hit = eyes.find((e) => PRISM_AUX.includes(this.phoro[e].aux));
    if (hit) { this.say('patient', '「咦?看到兩個重疊的影像…是不是有放稜鏡?」'); return true; }
    return false;
  }
  openEyes() { return ['OD', 'OS'].filter((e) => !this.phoro.occ[e]); }

  // 最佳睜開眼的 logMAR(含針孔、JCC)
  seenLogMAR(eyes = this.openEyes()) {
    if (!eyes.length) return null;
    let best = Infinity;
    for (const e of eyes) {
      const pinhole = this.phoro[e].aux === 'PH';
      best = Math.min(best, predictLogMAR(this.patient, e, this.lensVec(e), { pinhole }));
    }
    return best;
  }

  askRead() {
    const open = this.openEyes();
    if (!open.length) { this.say('patient', '咦?我什麼都看不到,兩邊都被擋住了。'); return null; }
    if (this.chart.mode !== 'digits' && this.chart.mode !== 'rg') { this.say('patient', '這個圖案不是數字耶,我沒辦法讀。'); return null; }
    if (this.prismBlock(open)) return null;
    const lm = this.seenLogMAR(open);
    const row = this.chart.row;
    const { correct, total } = readRow(this.patient, lm, row);
    const isolated = open.length === 1 && open[0] === this.activeEye;
    const digits = this.chartDigits(row);
    const shown = digits.map((d, i) => (i < correct ? d : (d + 3) % 10)).join(' ');
    let txt;
    if (correct === total) txt = `${shown}。`;
    else if (correct === 0) txt = '這一排太小了,看不太到…';
    else txt = `${shown}…(有點不確定)`;
    this.say('patient', `「${txt}」`);
    this.hist[this.activeEye].reads.push({ row, correct, total, isolated, ph: this.phoro[this.activeEye].aux === 'PH' });
    this.ev('read', { eye: this.activeEye, row, correct, total, isolated });
    this.emit();
    return { correct, total, row };
  }

  chartDigits(row) {
    const r = makeRng(Math.round(row * 100) * 131 + this.seed);
    const n = rowCount(row);
    return Array.from({ length: n }, () => r.int(0, 9));
  }

  // 兩片比較:鏡片1=現在,鏡片2=現在+dir(球面)
  askCompare(dir) {
    const eye = this.activeEye;
    if (this.phoro.occ[eye]) { this.say('patient', '這隻眼睛被遮住了,我看不到。'); return null; }
    if (this.prismBlock([eye])) return null;
    const pinhole = this.phoro[eye].aux === 'PH';
    const l1 = toVec(this.lens(eye));
    const l2 = toVec({ ...this.lens(eye), s: this.lens(eye).s + dir });
    const ans = compareLenses(this.patient, eye, l1, l2, 'va', { pinhole });
    this.say('you', `鏡片 1(現在) ↔ 鏡片 2(${dir > 0 ? '+' : '−'}0.25D):哪個比較清楚?`);
    this.say('patient', `「${say(this.patient, ans)}」`);
    this.hist[eye].compares++;
    this.ev('compare', { eye, dir, ans, dM: this.dM(eye) });
    this.emit();
    return ans;
  }

  askDuo() {
    const eye = this.activeEye;
    const usingRG = this.phoro[eye].aux === 'RG' || this.chart.mode === 'rg';
    if (this.phoro.occ[eye]) { this.say('patient', '這隻眼睛被遮住了,我看不到。'); return null; }
    if (this.prismBlock([eye])) return null;
    if (!usingRG) { this.say('patient', '我看到的就是黑白數字,沒有紅綠耶。'); return null; }
    const ans = duochrome(this.patient, eye, this.lensVec(eye));
    this.say('you', '請比較綠色邊與紅色邊,哪一邊的字比較清晰?');
    this.say('patient', `「${say(this.patient, ans)}」`);
    this.hist[eye].duo.push({ ans, dM: this.dM(eye), t: Date.now() });
    this.ev('duo', { eye, ans, dM: this.dM(eye) });
    this.emit();
    return ans;
  }

  // JCC 翻轉比較:回傳 { ans, pos1Red, pos2Red }
  askJcc() {
    const eye = this.activeEye;
    const { mode } = this.phoro.jcc;
    if (mode === 'off') { this.say('patient', '(還沒有放上 JCC)'); return null; }
    if (this.phoro.occ[eye]) { this.say('patient', '這隻眼睛被遮住了,我看不到。'); return null; }
    if (this.prismBlock([eye])) return null;
    if (this.chart.mode !== 'digits' && this.chart.mode !== 'honey') { this.say('patient', '這個圖案不適合比較 JCC,請換成蜂巢或數字視標。'); return null; }
    const a = this.phoro[eye].a;
    const base = toVec(this.lens(eye));
    const r1 = jccRedAxis(mode, 1, a), r2 = jccRedAxis(mode, 2, a);
    const v1 = addVec(base, jccVec(r1)), v2 = addVec(base, jccVec(r2));
    const ans = compareLenses(this.patient, eye, v1, v2, 'jcc', { pinhole: this.phoro[eye].aux === 'PH' });
    this.say('you', `JCC(${mode}):鏡片 1(${this.describeJcc(mode, 1, a)}) ↔ 鏡片 2(${this.describeJcc(mode, 2, a)}),哪個比較清楚?`);
    this.say('patient', `「${say(this.patient, ans)}」`);
    if (this.mode !== 'exam' && ans !== 'same') {
      const win = ans === 'first' ? 1 : 2;
      if (mode === 'P') this.say('sys', `(提示)${win === 1 ? '紅點在軸上較清楚 → 多一片 −0.25DC' : '白點在軸上較清楚 → 少一片 −0.25DC'}`);
      else this.say('sys', `(提示)追紅點:往 ${this.describeJcc('A', win, a)} 轉軸(${win === 1 ? '軸度增加 +' : '軸度減少 −'})`);
    } else if (this.mode !== 'exam') this.say('sys', '(提示)兩片差不多 → 這一步完成');
    this.hist[eye].jccAsks++;
    this.ev('jcc', { eye, mode, ans, a, c: this.phoro[eye].c });
    this.emit();
    return { ans, r1, r2 };
  }

  askClock() {
    const eye = this.activeEye;
    if (this.phoro.occ[eye]) { this.say('patient', '這隻眼睛被遮住了,我看不到。'); return null; }
    if (this.prismBlock([eye])) return null;
    if (this.chart.mode !== 'clock') { this.say('patient', '(視標還不是散光鐘面圖)'); return null; }
    const r = clockDial(this.patient, eye, this.lensVec(eye));
    this.say('you', '請問哪一條線比較黑、比較清楚?');
    if (r.uniform) this.say('patient', '「每一條都差不多耶。」');
    else if (r.lines.length === 1) this.say('patient', `「${r.lines[0]} 點和 ${r.lines[0] + 6} 點那條最黑。」`);
    else this.say('patient', `「${r.lines[0]}、${r.lines[1]} 點中間(${r.lines[0]} 與 ${r.lines[1]} 差不多)那條最黑。」`);
    this.hist[eye].clockAsks++;
    this.ev('clock', { eye, lines: r.lines, uniform: r.uniform, fogOk: r.fogOk });
    this.emit();
    return r;
  }

  choosePath(path) {
    const eye = this.activeEye;
    this.path[eye] = { path, cyl: this.phoro[eye].c };
    this.ev('path', { eye, path, cyl: this.phoro[eye].c });
    this.emit();
  }

  /* ---------- 檢影 / 工作距離 ---------- */
  setWD(cm) { this.wdCm = cm; this.emit(); }
  retNeutral(eye, tol = 0.25) { return fullyNeutral(this.trueRx(eye), this.lens(eye), this.wdD, tol); }
  applyWD() {
    if (this.wdApplied) { this.say('sys', '工作距離已經給過了。'); return; }
    for (const e of EYES) this.setLens(e, { s: this.phoro[e].s - this.wdD });
    this.wdApplied = true;
    this.say('sys', `雙眼各給工作距離度數 −${this.wdD.toFixed(2)}D(${this.wdCm} cm)。`);
    this.ev('applyWD', { cm: this.wdCm });
    this.emit();
  }

  /* ---------- 紀錄單 ---------- */
  recordRx(slot, eye) {
    const l = this.lens(eye);
    this.sheet.slots[slot][eye] = { rx: { ...l }, t: Date.now() };
    this.ev('recordRx', { slot, eye, ...l, dM: this.dM(eye) });
    this.emit();
  }
  recordVA(slot, eye, va) {
    // 同時存下「當下真實可讀的最佳列」,計分時用來核對
    const lm = this.seenLogMAR([eye]);
    const actualRow = bestRow(lm);
    const actualLM = lm;
    this.sheet.va[slot][eye] = { ...va, actualRow, actualLM };
    this.ev('recordVA', { slot, eye, ...va });
    this.emit();
  }
  recordWD(cm) { this.sheet.wdCm = cm; this.emit(); }

  /* ---------- 步驟控制 ---------- */
  check() {
    // 教學模式用:目前步驟的完成條件 → { ok, why[] }
    const id = this.stepId;
    const why = [];
    const def = this.stepDef;
    const eye = def.eye;
    const S = this.sheet;
    if (id === 'setup') {
      const st = this.setupState();
      for (const it of SETUP_ITEMS) if (!st[it.id]) why.push(`還沒完成:${it.label}`);
    } else if (id === 'ret') {
      for (const e of EYES) if (!this.retNeutral(e, 0.8)) why.push(`${EYE_LABEL[e]} 還沒中和(順動加正、逆動減正;兩個軸都要)`);
    } else if (id === 'wd') {
      if (!this.wdApplied) why.push('還沒給工作距離度數');
      for (const e of EYES) if (!S.slots.ret[e]) why.push(`${EYE_LABEL[e]} 的 Ret 度數還沒記錄`);
      if (S.wdCm === null) why.push('紀錄單的「工作距離」還沒填');
    } else if (def.phase === 'va') {
      const iso = this.hist[eye].reads.some((r) => r.isolated);
      if (!iso) why.push('還沒有在「只測這隻眼、遮住另一隻眼」的情況下測視力');
      if (!S.va.ret[eye]) why.push('還沒把 VA 記到紀錄單 ①');
    } else if (def.phase === 'mp1' || def.phase === 'mp2') {
      const slot = def.phase;
      if (def.phase === 'mp1' && this.hist[eye].fogMax.mp1 < 0.5) why.push('還沒有霧視(讓 0.6 視標明顯模糊)');
      if (!S.slots[slot][eye]) why.push('還沒記錄度數');
      if (!S.va[slot][eye]) why.push('還沒記錄 VA');
      const d = this.dM(eye);
      if (S.slots[slot][eye] && Math.abs(S.slots[slot][eye].rx && toVec(S.slots[slot][eye].rx).M - this.trueM(eye)) > 0.8) why.push('球面度數離最佳還有一段距離,再慢慢調整(最大正度數 + 最佳視力)');
      void d;
    } else if (def.phase === 'duo') {
      const d = this.hist[eye].duo;
      if (!d.length) why.push('還沒做紅綠檢查');
      else if (d[d.length - 1].ans === 'red') why.push('最後一次答案是「紅色較清楚」,度數偏正,要再加負 −0.25D');
    } else if (def.phase === 'jcc') {
      if (!this.path[eye]) why.push('先選「這隻眼要走哪條路」(APA / PPAP / 鐘面圖 / 不需要)');
      if (!S.slots.jcc[eye]) why.push('JCC 結束後要記錄度數(③)');
    }
    return { ok: why.length === 0, why };
  }

  canAdvance() {
    if (this.mode !== 'tutorial') return { ok: true, why: [] };
    return this.check();
  }

  advance({ force = false } = {}) {
    if (this.stepId === 'done') return false;
    const c = this.canAdvance();
    if (!c.ok && !force) return false;
    const prev = this.stepDef;
    // 離開步驟時的整理
    if (prev.id === 'setup') { this.setupSnap = this.setupState(); if (!this.tStart) this.tStart = Date.now(); }
    if (prev.phase === 'jcc') { this.phoro.jcc.mode = 'off'; }
    if (prev.phase === 'duo') { this.phoro[prev.eye].aux = 'O'; }
    this.step++;
    const nd = this.stepDef;
    if (nd.eye) { this.setActiveEye(nd.eye); this.phoro.jcc.mode = 'off'; }
    if (nd.id === 'done') this.tEnd = Date.now();
    this.ev('step', { to: nd.id });
    this.say('sys', `➜ ${nd.eye ? EYE_LABEL[nd.eye] + ' · ' : ''}${nd.title}`);
    this.emit('step');
    return true;
  }

  /* ---------- 教學提示 ---------- */
  hint() {
    const def = this.stepDef;
    const eye = def.eye;
    switch (def.phase ?? def.id) {
      case 'setup': {
        const st = this.setupState();
        const left = SETUP_ITEMS.find((i) => !st[i.id]);
        return left ? `接下來:${left.label}。` : '前置都完成了,按「完成此步驟」。';
      }
      case 'ret':
        return '先把光條轉到反射光與光條平行的角度,中和第一軸(順加逆減),再轉 90° 用散光中和第二軸。右眼掃右眼、左眼掃左眼,兩眼一直睜開。';
      case 'wd':
        return `Ret 結束後,雙眼同時給工作距離(${this.wdCm} cm → −${this.wdD.toFixed(2)}D),再把度數與工作距離記到紙上。`;
      case 'va':
        return '關掉另一隻眼,從 0.6–0.8 開始問;視力不到 0.8 要加測 PH 並記錄,測完把 PH 轉開。';
      case 'mp1': {
        const m = this.hist[eye].fogMax.mp1;
        if (m < 0.5) return '先霧視:加約 +1.00D,讓 0.6 視標明顯模糊(視力約 0.3–0.5)。';
        const d = this.dM(eye);
        if (d > 0.6) return '開始每次 −0.25D 慢慢往負方向給,每次都問視力有沒有進步。';
        return '快到了:不再進步就停止加負;最後用兩片比較確認「最大正度數 + 最佳視力」。';
      }
      case 'duo':
        return '加上紅綠濾片,看比最佳視力大一行的視標。紅色清楚 → 加負;綠色清楚 → 加正;做到「綠色第一片清楚」或兩邊一樣。';
      case 'jcc': {
        const c = this.phoro[eye].c;
        return Math.abs(c) >= 0.75
          ? `目前散光 ${Math.abs(c).toFixed(2)}DC ≥ 0.75 → 走 APA:軸(追紅點) → 度數(紅加白減) → 再確認軸。`
          : '散光 0 ~ −0.50DC → 走 PPAP(沒有散光先給 −0.25DC);若 1st MPMVA 後視力仍不佳,可用鐘面圖(要先霧視)。';
      }
      case 'mp2':
        return '退掉 JCC,重新霧視,再做一次 MPMVA,記錄度數與最佳 VA。';
      default:
        return '';
    }
  }

  /* ---------- 上帝視角(教學/練習) ---------- */
  godInfo(eye = this.activeEye) {
    const t = this.trueRx(eye);
    const { B, dM } = effectiveBlur(this.patient, eye, this.lensVec(eye), { pinhole: this.phoro[eye].aux === 'PH' });
    const lm = predictLogMAR(this.patient, eye, this.lensVec(eye), { pinhole: this.phoro[eye].aux === 'PH' });
    return { trueRx: t, trueText: fmtRx(t), lensText: fmtRx(this.lens(eye)), blur: B, dM, va: decimalFromLogMAR(lm) };
  }
}

export { bestRow, logMARFromDecimal };
