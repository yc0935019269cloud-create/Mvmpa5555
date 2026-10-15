// 電腦版「驗光台」:坐在綜合驗光儀後面的第一人稱工作台
// 左:大的、可以轉旋鈕的綜合驗光儀(上方是教練 / 步驟卡);右:6 m 外投影的視標 + 遙控器 + 受測者的回答;下:常用操作列
import { h, btn, segmented } from './dom.js';
import { PhoropterFace } from './phoropterFace.js';
import { buildQuickbar } from './quickbar.js';
import { paintPatientView } from './panels.js';
import { drawChart, CHART_W, CHART_H } from '../render/chartCanvas.js';
import { snellen } from '../sim/optics.js';
import { EYE_LABEL, other } from '../game/state.js';

const CHARTS = [['tumble', 'E 字'], ['rg', '紅綠'], ['honey', '蜂巢'], ['clock', '鐘面圖'], ['big_e', '大 E']];

export function openBench(game, ctx, { coachSlot, onClose } = {}) {
  const face = new PhoropterFace(game, { zoom: true });
  const chartCv = h('canvas', { class: 'bchart', width: CHART_W, height: CHART_H, 'aria-label': '6 公尺外的視標' });
  const pvCv = h('canvas', { class: 'bpv', width: 240, height: Math.round(240 * CHART_H / CHART_W), 'aria-label': '受測者看到的樣子' });
  const say = h('div', { class: 'bsay', role: 'status', 'aria-live': 'polite' });
  const tip = h('div', { class: 'btip' });
  const remote = h('div', { class: 'remote' });
  const occBar = h('div', { class: 'boccl' });
  const quick = buildQuickbar(game, ctx);
  const showPV = () => game.mode !== 'exam' && ctx.pvOn !== false;

  const root = h('div', { class: 'bench', role: 'dialog', 'aria-label': '驗光台' },
    h('div', { class: 'bhead' },
      btn('← 回診間', () => close(true), { cls: 'ghostd', title: 'Esc' }),
      occBar,
      h('span', { class: 'sp' }),
      btn('📋 紀錄單', () => ctx.openRecord(), { cls: 'ghostd', title: 'R' }),
      btn('🔦 檢影', () => { close(false); ctx.showTab('ret'); }, { cls: 'ghostd' })),
    h('div', { class: 'bleft' }, coachSlot ?? null, h('div', { class: 'bface' }, face.el),
      h('p', { class: 'bnote' }, '拖曳旋鈕轉動 · 點旋鈕左半(−)/右半(+) · 滾輪也可以 · 外側大轉輪手指往上 = 度數增加')),
    h('div', { class: 'bright' },
      h('div', { class: 'screen' },
        h('div', { class: 'scrlabel' }, h('span', {}, '視力表 · 6 m'), h('span', { class: 'rowlab' })),
        h('div', { class: 'scrwrap' }, chartCv,
          h('figure', { class: 'pvbox' }, pvCv, h('figcaption', {}, '受測者看到的(教學)'))),
        say),
      remote, tip),
    h('div', { class: 'bfoot' }, quick.el));

  function renderRemote() {
    remote.replaceChildren(
      h('div', { class: 'rmrow' }, h('span', { class: 'rmlab' }, '視標'),
        segmented(CHARTS, game.chart.mode, (m) => game.setChart({ mode: m }))),
      h('div', { class: 'rmrow' }, h('span', { class: 'rmlab' }, '大小'),
        btn('▲ 大一列', () => game.stepRow(-1), { cls: 'rmb', title: '↑' }),
        h('output', { class: 'rmval' }, `${game.chart.row.toFixed(1)}`, h('small', {}, snellen(game.chart.row))),
        btn('小一列 ▼', () => game.stepRow(1), { cls: 'rmb', title: '↓', }),
        h('label', { class: 'toggle inline' }, h('input', { type: 'checkbox', checked: game.chart.isolate, onchange: (e) => game.setChart({ isolate: e.target.checked }) }), h('span', {}, '只亮一列'))),
    );
    remote.querySelectorAll('.seg .s').forEach((b, i) => b.setAttribute('data-coach', `chart:${CHARTS[i][0]}`));
    remote.querySelectorAll('.rmb')[1].setAttribute('data-coach', 'rowS');
  }

  function renderOcc() {
    const P = game.phoro, eye = game.activeEye;
    occBar.replaceChildren(
      h('span', { class: 'beye' }, `測試眼 ${EYE_LABEL[eye]}`),
      ...['OD', 'OS'].map((e) => h('span', { class: `oc ${P.occ[e] ? 'shut' : 'open'}` }, `${e} ${P.occ[e] ? '遮住' : '睜開'}`)),
      btn(`只開 ${eye}`, () => game.setTestEye(eye), { cls: `ghostd${!P.occ[eye] && P.occ[other(eye)] ? ' on' : ''}`, title: 'O' }),
      btn('換眼', () => game.setTestEye(other(eye)), { cls: 'ghostd' }));
  }

  function paint() {
    drawChart(chartCv.getContext('2d'), game.chart, game.patient.seed);
    root.querySelector('.pvbox').hidden = !showPV();
    if (showPV()) paintPatientView(game, ctx, pvCv);
    root.querySelector('.rowlab').textContent = ['tumble', 'rg'].includes(game.chart.mode) ? `${game.chart.row.toFixed(1)}(${snellen(game.chart.row)})` : '';
  }

  // 受測者的回答(和教學提示)顯示在視力表下方
  function showSay() {
    const last = [...game.log].reverse().find((m) => m.who === 'patient');
    const lastTip = game.log[game.log.length - 1]?.who === 'sys' ? game.log[game.log.length - 1] : null;
    say.replaceChildren(last ? h('span', {}, h('b', {}, `${game.patient.name}:`), last.text) : h('span', { class: 'dim' }, '按「讀視標」或空白鍵問受測者'));
    say.classList.remove('pop'); void say.offsetWidth; say.classList.add('pop');
    tip.replaceChildren(lastTip && game.mode !== 'exam' && lastTip.text.startsWith('(提示)') ? h('span', {}, '💡 ', lastTip.text.replace('(提示)', '')) : '');
  }

  function render() {
    face.update();
    renderRemote();
    renderOcc();
    quick.render();
    paint();
    ctx.applyCoach?.();
  }

  const off = game.on((k) => {
    if (k === 'log') { showSay(); return; }
    if (window.__pfDrag) { face.update(); return; }
    render();
  });
  const onKey = (e) => { if (e.key === 'Escape' && !document.querySelector('.overlay')) close(true); };
  function close(manual) {
    off(); face.destroy(); document.removeEventListener('keydown', onKey);
    root.remove(); document.body.classList.remove('bench-open');
    onClose?.(manual);
  }
  document.addEventListener('keydown', onKey);
  document.body.append(root);
  document.body.classList.add('bench-open');
  render(); showSay();
  return { close, face, el: root, render };
}
