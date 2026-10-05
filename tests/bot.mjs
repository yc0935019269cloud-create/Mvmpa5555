// 「模範學生」機器人:只用受測者的回答與遊戲 API,依課堂流程做一遍整個考試。
// 用來驗證:流程可完成、各步驟模擬合理、計分會給高分。
import { Game, EYES, other } from '../src/game/state.js';
import { toVec } from '../src/sim/optics.js';
import { VA_ROWS } from '../src/sim/optics.js';

const q = (x) => Math.round(x / 0.25) * 0.25;

function readBest(g, eye) {
  // 從 0.6 開始往上/往下找「當下最佳視力列」(讀對 >= total-1)
  let best = null;
  g.setChart({ mode: 'tumble', isolate: true });
  let startIdx = VA_ROWS.indexOf(0.6);
  const ok = (row) => { g.setChart({ row }); const r = g.askRead(); return r && r.correct >= r.total - 1; };
  let i = startIdx;
  if (ok(VA_ROWS[i])) {
    while (i + 1 < VA_ROWS.length && ok(VA_ROWS[i + 1])) i++;
    best = VA_ROWS[i];
  } else {
    while (i - 1 >= 0 && !ok(VA_ROWS[i - 1])) i--;
    best = i - 1 >= 0 ? VA_ROWS[i - 1] : VA_ROWS[0];
  }
  return best;
}

function mpmva(g, eye, { fog = true } = {}) {
  g.setChart({ mode: 'tumble', row: 0.6, isolate: true });
  if (fog) {
    for (let k = 0; k < 4; k++) g.stepSph(eye, 0.25 * 1); // placeholder, replaced below
  }
}

function runMpmva(g, eye) {
  g.setChart({ mode: 'tumble', row: 0.6, isolate: true });
  // 霧視:+1.00 起,確認 0.6 讀不好
  g.setLens(eye, { s: g.phoro[eye].s + 1.0 });
  let guard = 0;
  while (guard++ < 8) {
    const r = g.askRead();
    if (r.correct < r.total) break;
    g.stepSph(eye, 0.25);
  }
  // 每次 -0.25,直到視力不再進步
  let bestRow = readBest(g, eye);
  let steps = 0;
  while (steps++ < 40) {
    g.stepSph(eye, -0.25);
    const nb = readBest(g, eye);
    if (nb > bestRow) bestRow = nb;
    else { // 沒有進步:退回一片並停止
      g.stepSph(eye, 0.25);
      break;
    }
  }
  // 兩片比較,微調
  for (let k = 0; k < 4; k++) {
    const ans = g.askCompare(-0.25);
    if (ans === 'second') g.stepSph(eye, -0.25);
    else break;
  }
  return readBest(g, eye);
}

function runDuo(g, eye) {
  g.setAux(eye, 'RG');
  g.setChart({ mode: 'tumble', row: 0.8, isolate: true });
  let seen = new Set();
  for (let k = 0; k < 8; k++) {
    const ans = g.askDuo();
    if (ans === 'same' || ans === 'green') break;
    g.stepSph(eye, -0.25);
  }
}

function runJccAxis(g, eye) {
  g.setJccMode('A');
  let step = 15;
  let last = null;
  for (let k = 0; k < 12 && step >= 2; k++) {
    const r = g.askJcc();
    if (r.ans === 'same') break;
    const dir = r.ans === 'first' ? +1 : -1;
    if (last !== null && dir !== last) step = Math.max(2, Math.round(step / 2));
    last = dir;
    g.stepAxis(eye, dir * step);
  }
}

function runJccPower(g, eye) {
  g.setJccMode('P');
  for (let k = 0; k < 8; k++) {
    const r = g.askJcc();
    if (r.ans === 'same') break;
    const cur = g.phoro[eye];
    if (r.ans === 'first') { // 紅點在軸上較清楚 → 多一片 -0.25DC
      g.setLens(eye, { c: cur.c - 0.25, s: cur.s + 0.125 });
    } else { // 白點 → 少一片
      if (cur.c >= 0) break;
      g.setLens(eye, { c: cur.c + 0.25, s: cur.s - 0.125 });
    }
  }
}

export function playPerfect(seed, { mode = 'tutorial', difficulty = 2, verbose = false } = {}) {
  return playGame(new Game({ mode, seed, difficulty }), { verbose });
}

// 對任一個 Game(包含瀏覽器裡正在跑的那一個)執行完整流程
export function playGame(g, { verbose = false } = {}) {
  const seed = g.seed;
  const log = (...a) => verbose && console.log(...a);
  // 前置
  g.sanitize(); g.measurePD(); g.setPD(g.patient.pd); g.recordPD(g.patient.pd);
  g.setLevel(0); g.setAux('OD', 'O'); g.setAux('OS', 'O'); g.setAperture(true); g.setHeight(0.5);
  g.setChart({ mode: 'big_e' }); g.setLocked(true); g.setDim(true);
  g.advance();
  // 檢影:直接給(檢影本身由 UI 的小遊戲驗證)
  for (const e of EYES) {
    const t = g.trueRx(e);
    // 學生的檢影有誤差:球面 ±0.5、散光 ±0.5、軸 ±12°
    const j = (k) => ((Math.sin(seed * 31 + k * 17) * 43758.5453) % 1);
    const cNew = Math.min(0, q(t.c + (j(1) > 0 ? 0.25 : -0.25) * (Math.abs(j(2)) > 0.4 ? 2 : 1)));
    g.setLens(e, { s: q(t.s + g.wdD + j(3) * 0.75), c: cNew, a: t.a + Math.round(j(4) * 12) });
  }
  g.advance({ force: true });
  g.applyWD(); g.recordWD(g.wdCm);
  for (const e of EYES) g.recordRx('ret', e);
  g.advance();
  return playEyes(g, { verbose });
}

// 從 OD.va 開始的各眼流程(瀏覽器測試會從這裡接手)
export function playEyes(g, { verbose = false, onStep = null } = {}) {
  const log = (...a) => verbose && console.log(...a);
  for (const eye of EYES) {
    g.setTestEye(eye);
    onStep?.('va', eye);
    // VA
    let row = readBest(g, eye);
    let va = { row, delta: 0 };
    if (row < 0.8) { g.setAux(eye, 'PH'); const prow = readBest(g, eye); va.phRow = prow; va.phDelta = 0; g.setAux(eye, 'O'); }
    g.recordVA('ret', eye, va);
    g.advance();
    // mp1
    const b1 = runMpmva(g, eye);
    g.recordRx('mp1', eye); g.recordVA('mp1', eye, { row: b1, delta: 0 });
    onStep?.('mp1', eye);
    g.advance();
    // duo
    runDuo(g, eye);
    onStep?.('duo', eye);
    g.advance();
    // jcc
    g.setAux(eye, 'O');
    const c = Math.abs(g.phoro[eye].c);
    if (c >= 0.75) {
      g.choosePath('APA');
      g.setChart({ mode: 'honey', row: Math.max(0.3, b1 - 0.2) });
      runJccAxis(g, eye); runJccPower(g, eye); runJccAxis(g, eye);
    } else {
      g.choosePath('PPAP');
      g.setChart({ mode: 'honey', row: Math.max(0.3, b1 - 0.2) });
      if (g.phoro[eye].c === 0) g.setLens(eye, { c: -0.25, s: g.phoro[eye].s + 0.125 });
      // 四個主要軸位找初軸
      let chosen = null;
      g.setJccMode('P');
      for (const ax of [180, 45, 90, 135]) {
        g.setLens(eye, { a: ax });
        const r = g.askJcc();
        if (r.ans === 'first') { chosen = ax; break; }
      }
      if (chosen === null) {
        // 都拒絕 → 回到原本不加散光
        g.setLens(eye, { c: 0, s: g.phoro[eye].s - 0.125 });
      } else {
        runJccPower(g, eye); runJccAxis(g, eye); runJccPower(g, eye);
      }
    }
    g.setJccMode('off');
    g.recordRx('jcc', eye);
    onStep?.('jcc', eye);
    g.advance();
    // mp2
    const b2 = runMpmva(g, eye);
    g.recordRx('mp2', eye); g.recordVA('mp2', eye, { row: b2, delta: 0 });
    if (eye === 'OD') g.advance(); else g.advance();
    log(eye, 'final', g.lens(eye), 'true', g.trueRx(eye));
  }
  return g;
}
