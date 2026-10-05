// 操作台底部常駐的「常用操作列」:不用捲動就能 遮眼 → 調度數 → 問受測者 → 看回答 → 記錄
import { h, btn, segmented } from './dom.js';
import { fmtRx } from '../sim/optics.js';
import { other, rxKey } from '../game/state.js';
import { recordBar, pickPath } from './panels.js';

export function openRecordSheet(game) {
  const body = h('div');
  const draw = () => body.replaceChildren(recordBar(game));
  const ov = h('div', { class: 'overlay glass recsheet', onclick: (e) => { if (e.target === ov) close(); } },
    h('div', { class: 'card small' },
      h('h3', { style: { margin: '0 0 8px', color: '#1e5566' } }, `記錄 · ${game.activeEye === 'OD' ? '右眼 OD' : '左眼 OS'} · ${game.stepDef.title}`),
      body,
      h('div', { class: 'startrow', style: { marginTop: '12px' } }, btn('完成', () => close(), { cls: 'primary' }))));
  // 記錄之後就地更新(按鈕會變成 ✓)
  const off = game.on((k) => { if (k !== 'log') draw(); });
  const onKey = (e) => { if (e.key === 'Escape') close(); };
  function close() { off(); document.removeEventListener('keydown', onKey); ov.remove(); }
  document.addEventListener('keydown', onKey);
  draw();
  document.body.append(ov);
  return close;
}

// 這一步該記的東西是不是都記了(而且是目前的度數)
export function recordDone(game) {
  const ph = game.stepDef.phase, eye = game.activeEye, S = game.sheet;
  const rxOk = (slot) => S.slots[slot][eye] && rxKey(S.slots[slot][eye].rx) === game.lensKey(eye);
  if (ph === 'va') return !!S.va.ret[eye];
  if (ph === 'mp1' || ph === 'mp2') return rxOk(ph) && !!S.va[ph][eye];
  if (ph === 'jcc') return rxOk('jcc');
  return null;
}

// 主要的「問受測者」動作(空白鍵也用這個)
export function mainAsk(game) {
  const ph = game.stepDef.phase;
  if (ph === 'duo') return () => game.askDuo();
  if (ph === 'jcc') return game.chart.mode === 'clock' ? () => game.askClock() : () => game.askJcc();
  if (ph === 'va' || ph === 'mp1' || ph === 'mp2') return () => game.askRead();
  return null;
}

export function cycleJcc(game) {
  const order = ['off', 'A', 'P'];
  game.setJccMode(order[(order.indexOf(game.phoro.jcc.mode) + 1) % 3]);
}

export function buildQuickbar(game, ctx) {
  const el = h('div', { class: 'quickbar hidden' });
  const q = (label, fn, { cls = '', title = '', disabled = false, coach = null } = {}) =>
    h('button', { type: 'button', class: `qb ${cls}`, title, 'aria-label': title || undefined, disabled, onclick: fn, 'data-coach': coach }, label);
  const hints = () => game.mode !== 'exam';

  function actions() {
    const ph = game.stepDef.phase;
    if (!ph || ph === 'done') return [];
    const eye = game.activeEye;
    const P = game.phoro;
    const ask = (fn) => () => ctx.ask(fn);
    const isolated = !P.occ[eye] && P.occ[other(eye)];
    // 只開測試眼:沒遮好時(非考試)會閃黃色提醒
    const occ = q(isolated ? `👁 只開 ${eye} ✓` : `👁 只開 ${eye}`, () => game.setTestEye(eye),
      { cls: isolated ? 'on' : hints() ? 'need' : '', title: `遮住 ${other(eye)},只用 ${eye} 看(O)`, coach: 'occ' });
    const done = recordDone(game);
    const rec = q(done ? '📝 已記錄 ✓' : '📝 記錄', () => ctx.openRecord(), { cls: done ? 'rec done' : 'rec', title: '記錄到紀錄單(R)', coach: 'rec' });
    const sphM = q('球 −.25', () => game.stepSph(eye, -0.25), { title: '球面 −0.25(←)', coach: 'sphM' });
    const sphP = q('球 +.25', () => game.stepSph(eye, 0.25), { title: '球面 +0.25(→)' });
    const rowB = q('◀ 大', () => game.stepRow(-1), { title: '視標列變大(↑)' });
    const rowS = q('小 ▶', () => game.stepRow(1), { title: '視標列變小(↓)', coach: 'rowS' });
    const read = q('🗣 讀視標', ask(() => game.askRead()), { cls: 'main', title: '請受測者讀這一排(空白鍵)', coach: 'read' });
    if (ph === 'va') {
      const pin = P[eye].aux === 'PH';
      return [occ, read, rowB, rowS, q(pin ? 'PH ✓' : 'PH 針孔', () => game.setAux(eye, pin ? 'O' : 'PH'), { cls: pin ? 'on' : '', coach: 'ph' }), rec];
    }
    if (ph === 'mp1' || ph === 'mp2') {
      const fogged = game.hist[eye].fogMax[ph] >= 0.5;
      return [occ, read,
        P.jcc.mode !== 'off' ? q('收起 JCC', () => game.setJccMode('off'), { cls: hints() ? 'need' : '', coach: 'jcc' }) : null,
        q('霧視 +1.00', () => game.stepSph(eye, 1), { cls: hints() && !fogged && isolated ? 'need' : '', title: '霧視:球面 +1.00', coach: 'fog' }),
        q('比 −.25', ask(() => game.askCompare(-0.25)), { title: '兩片比較:再加 −0.25(Z)', coach: 'cmp' }),
        q('比 +.25', ask(() => game.askCompare(0.25)), { title: '兩片比較:退 +0.25(X)', coach: 'cmp' }),
        sphM, sphP, rowB, rowS, rec].filter(Boolean);
    }
    if (ph === 'duo') {
      const rg = P[eye].aux === 'RG' || game.chart.mode === 'rg';
      return [occ, q('🗣 紅綠?', ask(() => game.askDuo()), { cls: 'main', title: '紅色、綠色哪邊清楚?(空白鍵)', coach: 'duo' }),
        rg ? null : q('放紅綠濾片', () => game.setAux(eye, 'RG'), { cls: hints() ? 'need' : '', coach: 'rg' }),
        sphM, sphP, rowB, rowS].filter(Boolean);
    }
    if (ph === 'jcc') {
      const clock = game.chart.mode === 'clock';
      const jm = P.jcc.mode;
      const jccLabel = { off: 'JCC 關', A: 'JCC · A 軸', P: 'JCC · P 度數' }[jm];
      return [
        occ,
        clock ? q('🗣 哪條最黑?', ask(() => game.askClock()), { cls: 'main', title: '鐘面圖(空白鍵)', coach: 'ask' })
          : q('🗣 1 或 2?', ask(() => game.askJcc()), { cls: 'main', disabled: jm === 'off', title: 'JCC 兩片比較(空白鍵)', coach: 'ask' }),
        clock ? q('霧視 +1.00', () => game.stepSph(eye, 1), { coach: 'fog' }) : q(jccLabel, () => cycleJcc(game), { cls: jm === 'off' ? (hints() && game.path[eye] && game.path[eye].path !== 'skip' ? 'need' : '') : 'on', title: '切換 JCC:關 → A(軸)→ P(度數)', coach: 'jcc' }),
        clock ? null : q('⟲ 翻轉', () => game.flipJcc(), { disabled: jm === 'off', title: 'JCC 翻轉(F)' }),
        q('軸 −5', () => game.stepAxis(eye, -5), { title: '軸 −5([)' }), q('軸 +5', () => game.stepAxis(eye, 5), { title: '軸 +5(])' }),
        q('散 −.25', () => game.stepCyl(eye, -0.25), { title: '散光 加 −0.25(-)' }), q('散 +.25', () => game.stepCyl(eye, 0.25), { title: '散光 退 +0.25(=)' }),
        clock ? sphM : null, rec,
      ].filter(Boolean);
    }
    return [];
  }

  // JCC 步驟:散光路線常駐在操作列上方
  function subRow() {
    if (game.stepDef.phase !== 'jcc') return null;
    const eye = game.activeEye;
    const cur = game.path[eye]?.path;
    return h('div', { class: `qsub${!cur && hints() ? ' need' : ''}`, 'data-coach': 'path' },
      h('span', { class: 'lab' }, `路線(散光 ${game.phoro[eye].c ? game.phoro[eye].c.toFixed(2) : '0'})`),
      segmented([['APA', 'APA'], ['PPAP', 'PPAP'], ['clock', '鐘面圖'], ['skip', '不需要']], cur, (p) => pickPath(game, p)));
  }

  function render() {
    const acts = actions();
    el.classList.toggle('hidden', !acts.length);
    if (!acts.length) { el.replaceChildren(); return; }
    const eye = game.activeEye;
    const last = [...game.log].reverse().find((m) => m.who === 'patient');
    const chat = h('button', { type: 'button', class: 'qchat', onclick: () => ctx.showTab?.('log'), title: '對話紀錄' },
      h('span', { class: 'qrx rx' }, `${eye} ${fmtRx(game.lens(eye))}`),
      h('span', { class: 'qsay' }, last ? `${game.patient.name}:${last.text}` : '(還沒有問過受測者)'));
    el.replaceChildren(...[chat, subRow(), h('div', { class: 'qacts' }, acts)].filter(Boolean));
  }
  return { el, render };
}
