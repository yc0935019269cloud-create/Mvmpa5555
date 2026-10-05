// 操作台底部常駐的「常用操作列」:不用捲動就能 調度數 → 問受測者 → 看回答 → 記錄
import { h, btn } from './dom.js';
import { fmtRx } from '../sim/optics.js';
import { recordBar } from './panels.js';

export function openRecordSheet(game) {
  const ov = h('div', { class: 'overlay glass recsheet', onclick: (e) => { if (e.target === ov) close(); } },
    h('div', { class: 'card small' },
      h('h3', { style: { margin: '0 0 8px', color: '#1e5566' } }, `記錄 · ${game.activeEye === 'OD' ? '右眼 OD' : '左眼 OS'} · ${game.stepDef.title}`),
      recordBar(game),
      h('div', { class: 'startrow', style: { marginTop: '12px' } }, btn('完成', () => close(), { cls: 'primary' }))));
  const off = game.on(() => {});
  function close() { off(); ov.remove(); }
  document.body.append(ov);
}

export function buildQuickbar(game, ctx) {
  const el = h('div', { class: 'quickbar hidden' });
  const q = (label, fn, { cls = '', title = '', disabled = false } = {}) =>
    h('button', { type: 'button', class: `qb ${cls}`, title, disabled, onclick: fn }, label);

  function actions() {
    const ph = game.stepDef.phase;
    const eye = game.activeEye;
    const ask = (fn) => () => ctx.ask(fn);
    const rec = q('📝 記錄', () => openRecordSheet(game), { cls: 'rec' });
    const sphM = q('球 −.25', () => game.stepSph(eye, -0.25));
    const sphP = q('球 +.25', () => game.stepSph(eye, 0.25));
    const read = q('🗣 讀視標', ask(() => game.askRead()), { cls: 'main' });
    if (ph === 'va') {
      const ph_ = game.phoro[eye].aux === 'PH';
      return [read, q('◀ 大', () => game.stepRow(-1)), q('小 ▶', () => game.stepRow(1)),
        q(ph_ ? 'PH ✓' : 'PH 針孔', () => game.setAux(eye, ph_ ? 'O' : 'PH'), { cls: ph_ ? 'on' : '' }), rec];
    }
    if (ph === 'mp1' || ph === 'mp2') {
      return [read, q('比 −.25', ask(() => game.askCompare(-0.25)), { title: '兩片比較:再加 −0.25' }),
        q('比 +.25', ask(() => game.askCompare(0.25)), { title: '兩片比較:退 +0.25' }), sphM, sphP, rec];
    }
    if (ph === 'duo') {
      return [q('🗣 紅綠?', ask(() => game.askDuo()), { cls: 'main' }), sphM, sphP, q('◀ 大', () => game.stepRow(-1)), q('小 ▶', () => game.stepRow(1))];
    }
    if (ph === 'jcc') {
      const clock = game.chart.mode === 'clock';
      const jm = game.phoro.jcc.mode;
      return [
        clock ? q('🗣 哪條最黑?', ask(() => game.askClock()), { cls: 'main' }) : q('🗣 1 或 2?', ask(() => game.askJcc()), { cls: 'main', disabled: jm === 'off' }),
        q('⟲ 翻轉', () => game.flipJcc(), { disabled: jm === 'off' }),
        q('軸 −5', () => game.stepAxis(eye, -5)), q('軸 +5', () => game.stepAxis(eye, 5)),
        q('散 −.25', () => game.stepCyl(eye, -0.25)), q('散 +.25', () => game.stepCyl(eye, 0.25)), rec,
      ];
    }
    return [];
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
    el.replaceChildren(chat, h('div', { class: 'qacts' }, acts));
  }
  return { el, render };
}
