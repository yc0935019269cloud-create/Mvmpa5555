// 紙本紀錄單(夾在板夾上):點黃色框的格子 = 用筆寫上目前的度數 / 視力
import { h, btn } from './dom.js';
import { VA_ROWS, fmtSph } from '../sim/optics.js';
import { rxKey } from '../game/state.js';
import { readsNow, guessVA } from './panels.js';

const ROWS = [['ret', '①', 'Retinoscopy', '已扣工作距離'], ['mp1', '②', '1st MPMVA', ''], ['jcc', '③', 'JCC', ''], ['mp2', '④', '2nd MPMVA', '最終處方']];
const vaText = (va) => va ? `${va.row.toFixed(1)}${va.delta ? (va.delta > 0 ? '+' : '−') + Math.abs(va.delta) : ''}` : '';

// 這一步可以寫哪些格子
function writable(game) {
  const ph = game.stepDef.phase, eye = game.activeEye, id = game.stepId;
  const w = new Set();
  if (id === 'wd') { w.add('rx:ret:OD'); w.add('rx:ret:OS'); w.add('wd'); }
  if (ph === 'va') w.add(`va:ret:${eye}`);
  if (ph === 'mp1' || ph === 'mp2') { w.add(`rx:${ph}:${eye}`); w.add(`va:${ph}:${eye}`); }
  if (ph === 'jcc') w.add(`rx:jcc:${eye}`);
  return w;
}

export function openPaper(game, ctx = {}) {
  const S = game.sheet;
  let vaOpen = null; // 正在寫哪一格 VA
  let prev = new Map();
  const paper = h('div', { class: 'paper', role: 'document', 'aria-label': '紀錄單' });
  const ov = h('div', { class: 'overlay paperov', onclick: (e) => { if (e.target === ov) close(); } },
    h('div', { class: 'board' }, h('div', { class: 'clip', 'aria-hidden': 'true' }), paper,
      h('div', { class: 'boardbar' },
        h('span', { class: 'note' }, '黃色框 = 現在可以寫的格子,點一下就寫上目前的度數 / 視力。Esc 放下紀錄單。'),
        btn('放下紀錄單', () => close(), { cls: 'primary' }))));

  const ink = (key, text, cls = '') => {
    const changed = prev.has(key) && prev.get(key) !== text && text;
    prev.set(key, text);
    return h('span', { class: `ink${changed ? ' fresh' : ''} ${cls}` }, text);
  };

  function cellRx(slot, eye, W) {
    const r = S.slots[slot][eye]?.rx;
    const key = `rx:${slot}:${eye}`;
    const can = W.has(key);
    const stale = r && can && rxKey(r) !== game.lensKey(eye);
    const write = () => { game.recordRx(slot, eye); ctx.toast?.(`寫上 ${eye}:${fmtSph(game.lens(eye).s)}`); };
    const td = (k, text) => h('td', { class: `w${can ? ' can' : ''}${stale ? ' stale' : ''}`, 'data-coach': can ? 'rec' : null, onclick: can ? write : null, title: can ? (stale ? '度數改過了,點一下重寫' : '點一下寫上目前的度數') : '' }, ink(`${key}:${k}`, text));
    return [
      td('s', r ? fmtSph(r.s).replace('PL', 'pl') : ''),
      td('c', r ? (r.c ? `−${Math.abs(r.c).toFixed(2)}` : 'DS') : ''),
      td('a', r && r.c ? String(r.a).padStart(3, '0') : ''),
    ];
  }

  function cellVA(slot, eye, W) {
    const key = `va:${slot}:${eye}`;
    const can = W.has(key);
    const va = S.va[slot]?.[eye];
    const ph = va?.phRow ? `PH ${va.phRow.toFixed(1)}${va.phDelta ? (va.phDelta > 0 ? '+' : '−') + Math.abs(va.phDelta) : ''}` : '';
    return h('td', { class: `w va${can ? ' can' : ''}${vaOpen === key ? ' open' : ''}`, 'data-coach': can ? 'rec' : null, onclick: can ? () => { vaOpen = vaOpen === key ? null : key; draw(); } : null, title: can ? '點一下寫上視力' : '' },
      slot === 'jcc' ? h('span', { class: 'na' }, '—') : [ink(key, vaText(va)), ph ? h('small', { class: 'ink' }, ph) : null]);
  }

  // 寫 VA 的小視窗:預設帶入「目前鏡片」真的讀到的最佳列
  function vaPad(slot, eye) {
    const rs = readsNow(game, eye);
    const g0 = guessVA(rs.filter((r) => !r.ph));
    const gP = guessVA(rs.filter((r) => r.ph));
    const cur = S.va[slot]?.[eye];
    const st = { row: g0?.row ?? cur?.row ?? game.chart.row, delta: g0?.delta ?? cur?.delta ?? 0, phRow: gP?.row ?? cur?.phRow ?? null, phDelta: gP?.delta ?? cur?.phDelta ?? 0 };
    const pad = h('div', { class: 'vapad' });
    const chips = (vals, get, set, fmt) => h('div', { class: 'chips' }, vals.map((v) => h('button', { type: 'button', class: `chip2${get() === v ? ' on' : ''}`, onclick: (e) => { e.stopPropagation(); set(v); paint(); } }, fmt(v))));
    const seen = new Map(); for (const r of rs) seen.set(`${r.ph}-${r.row}`, r);
    function paint() {
      pad.replaceChildren(
        h('p', { class: 'padnote' }, rs.length ? ['目前鏡片讀過:', [...seen.values()].map((r) => h('span', { class: `rd${r.correct === r.total ? ' ok' : ''}` }, `${r.ph ? 'PH ' : ''}${r.row.toFixed(1)} ${r.correct}/${r.total}`))] : '⚠ 目前的鏡片還沒請受測者讀過視標。'),
        h('div', { class: 'padrow' }, h('b', {}, 'VA'), chips(VA_ROWS, () => st.row, (v) => { st.row = v; }, (v) => v.toFixed(1))),
        h('div', { class: 'padrow' }, h('b', {}, '±'), chips([-2, -1, 0, 1, 2, 3], () => st.delta, (v) => { st.delta = v; }, (v) => (v > 0 ? `+${v}` : v === 0 ? '±0' : `−${-v}`))),
        slot === 'ret' ? h('div', { class: 'padrow' }, h('b', {}, 'PH'), chips([null, ...VA_ROWS], () => st.phRow, (v) => { st.phRow = v; }, (v) => (v === null ? '沒測' : v.toFixed(1)))) : null,
        h('div', { class: 'padrow end' }, btn(`寫上 VA ${vaText(st)}${st.phRow && slot === 'ret' ? ` · PH ${st.phRow.toFixed(1)}` : ''}`, (e) => {
          e.stopPropagation();
          game.recordVA(slot, eye, { row: st.row, delta: st.delta, ...(slot === 'ret' ? { phRow: st.phRow, phDelta: st.phRow ? st.phDelta : 0 } : {}) });
          vaOpen = null; draw();
        }, { cls: 'primary' })));
    }
    paint();
    return pad;
  }

  function draw() {
    const W = writable(game);
    const today = new Date();
    const body = [];
    for (const [slot, no, name, sub] of ROWS) {
      for (const [i, eye] of ['OD', 'OS'].entries()) {
        const cur = W.has(`rx:${slot}:${eye}`) || W.has(`va:${slot}:${eye}`);
        body.push(h('tr', { class: `${i === 1 ? 'os' : 'od'}${cur ? ' now' : ''}` },
          i === 0 ? h('th', { rowspan: vaOpen?.startsWith(`va:${slot}:`) ? 3 : 2, class: 'item' }, h('b', {}, `${no} ${name}`), sub ? h('small', {}, sub) : null) : null,
          h('td', { class: 'eye' }, eye), ...cellRx(slot, eye, W), cellVA(slot, eye, W)));
        if (vaOpen === `va:${slot}:${eye}`) body.push(h('tr', { class: 'padtr' }, h('td', { colspan: 5 }, vaPad(slot, eye))));
      }
    }
    const wdCan = W.has('wd');
    paper.replaceChildren(
      h('div', { class: 'phead' }, h('h2', {}, '自覺式驗光 紀錄單'), h('span', { class: 'school' }, '視光學系 · 驗光實習')),
      h('div', { class: 'meta' },
        h('span', {}, '受測者 ', h('u', {}, ink('name', game.patient.name))),
        h('span', {}, '年齡 ', h('u', {}, ink('age', String(game.patient.age)))),
        h('span', {}, '日期 ', h('u', {}, ink('date', `${today.getMonth() + 1}/${today.getDate()}`))),
        h('span', {}, 'PD ', h('u', {}, ink('pd', S.pd ? String(S.pd) : '')), ' mm'),
        h('span', { class: wdCan ? 'w can' : 'w', 'data-coach': wdCan ? 'rec' : null, onclick: wdCan ? () => game.recordWD(game.wdCm) : null, title: wdCan ? `點一下寫上 ${game.wdCm} cm` : '' }, 'WD ', h('u', {}, ink('wd', S.wdCm ? String(S.wdCm) : '')), ' cm')),
      h('div', { class: 'ptwrap' }, h('table', { class: 'ptable' },
        h('thead', {}, h('tr', {}, h('th', {}, ''), h('th', {}, ''), h('th', {}, 'SPH'), h('th', {}, 'CYL'), h('th', {}, 'AXIS'), h('th', {}, 'VA'))),
        h('tbody', {}, body))),
      h('p', { class: 'foot' }, '視力 < 0.8 加測 PH 並註記。VA 寫法:0.8+2 = 0.8 全對、下一列對 2 個;0.8−1 = 0.8 錯 1 個。'),
    );
  }

  const off = game.on((k) => { if (k !== 'log') draw(); });
  const onKey = (e) => { if (e.key === 'Escape') close(); };
  function close() { off(); document.removeEventListener('keydown', onKey); ov.remove(); ctx.onClose?.(); }
  document.addEventListener('keydown', onKey);
  draw();
  document.body.append(ov);
  return close;
}
