// 操作台的各個分頁:前置 / 驗光台 / 檢影 / 視力表 / 紀錄單 / 對話 / 速查
import { h, btn, segmented, fmt2 } from './dom.js';
import {
  SETUP_ITEMS, EYES, EYE_LABEL, other,
} from '../game/state.js';
import { VA_ROWS, fmtRx, fmtSph, jccRedAxis, snellen, mod180 } from '../sim/optics.js';
import { WD_OPTIONS } from '../sim/retino.js';
import { RetinoView } from './retinoCanvas.js';
import { drawChart, drawBlurred, CHART_W, CHART_H } from '../render/chartCanvas.js';
import { effectiveBlur, eyeResidual } from '../sim/patient.js';
import { toVec } from '../sim/optics.js';

/* ============ 共用小元件 ============ */
function fillA(el, ...kids) { el.append(...kids.flat(Infinity).filter((k) => k !== null && k !== undefined && k !== false)); return el; }
function fillK(el, ...kids) { el.replaceChildren(...kids.flat(Infinity).filter((k) => k !== null && k !== undefined && k !== false)); return el; }


function axisDial(angle, jcc) {
  // 以 TABO 角度(從右側逆時針)畫軸線;JCC 紅點在紅軸兩端、白點在垂直方向
  const R = 34, cx = 40, cy = 40;
  const pt = (a, r) => [cx + Math.cos((a * Math.PI) / 180) * r, cy - Math.sin((a * Math.PI) / 180) * r];
  const [x1, y1] = pt(angle, R), [x2, y2] = pt(angle + 180, R);
  let dots = '';
  if (jcc !== null && jcc !== undefined) {
    for (const a of [jcc, jcc + 180]) { const [x, y] = pt(a, R - 2); dots += `<circle cx="${x}" cy="${y}" r="5.5" fill="#d33a2c" stroke="#fff" stroke-width="1"/>`; }
    for (const a of [jcc + 90, jcc + 270]) { const [x, y] = pt(a, R - 2); dots += `<circle cx="${x}" cy="${y}" r="5.5" fill="#fff" stroke="#51606a" stroke-width="1.5"/>`; }
  }
  const ticks = [0, 45, 90, 135].map((a) => { const [ax, ay] = pt(a, R + 3), [bx, by] = pt(a + 180, R + 3); return `<line x1="${ax}" y1="${ay}" x2="${bx}" y2="${by}" stroke="#cfd7db" stroke-width="1"/>`; }).join('');
  const el = h('div', { class: 'axdial' });
  el.innerHTML = `<svg viewBox="0 0 80 80" width="58" height="58" aria-hidden="true"><circle cx="40" cy="40" r="36" fill="#f6f8f8" stroke="#aab6bb"/>${ticks}<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="#1e5566" stroke-width="3" stroke-linecap="round"/>${dots}</svg>`;
  return el;
}

function section(title, ...kids) {
  return h('section', { class: 'sec' }, h('h4', {}, title), ...kids);
}

function stepper(label, value, steps, onStep, { fmt = (v) => v, cls = '' } = {}) {
  // steps: [[label, delta], ...] 左側負、右側正
  const left = steps.filter((s) => s[1] < 0), right = steps.filter((s) => s[1] > 0);
  return h('div', { class: `stepper ${cls}` },
    h('span', { class: 'lab' }, label),
    h('div', { class: 'row' },
      left.map(([l, d]) => btn(l, () => onStep(d), { cls: 'sm' })),
      h('output', { class: 'val' }, fmt(value)),
      right.map(([l, d]) => btn(l, () => onStep(d), { cls: 'sm' }))));
}

/* ============ 前置 ============ */

export function buildSetup(game, ctx) {
  const root = h('div', { class: 'panel' });
  function render() {
    root.replaceChildren();
    const st = game.setupState();
    const P = game.phoro;
    const row = (id, label, controls, note) => {
      const ok = st[id];
      return h('div', { class: `chk${ok ? ' ok' : ''}` },
        h('div', { class: 'chk-h' }, h('span', { class: 'mark', 'aria-hidden': 'true' }, ok ? '✓' : '○'), h('b', {}, label)),
        h('div', { class: 'chk-b' }, controls, note ? h('p', { class: 'note' }, note) : null));
    };
    const heightTxt = Math.abs(P.height - 0.5) <= 0.07 ? '高度適中' : P.height < 0.5 ? '偏低' : '偏高';
    fillA(root,
      h('p', { class: 'lead' }, '考試前置:儀器、位置、光線都設好,再開始檢查。每一項都要真的做到才會打勾。'),
      row('sanitize', '儀器消毒', btn(game.room.sanitized ? '已消毒' : '擦拭消毒', () => game.sanitize(), { disabled: game.room.sanitized })),
      row('pd', 'PD:量遠方瞳距 → 設定到綜合驗光儀',
        h('div', { class: 'row wrap' },
          btn(game.measuredPD ? `PD 尺:${game.measuredPD} mm` : '用 PD 尺量', () => game.measurePD(), { cls: game.measuredPD ? 'ghost' : '' }),
          h('div', { class: 'inline' }, h('span', {}, '驗光儀 PD'),
            btn('−', () => game.setPD(P.pd - 1), { cls: 'sm' }), h('output', { class: 'val' }, `${P.pd}`), btn('+', () => game.setPD(P.pd + 1), { cls: 'sm' }), h('span', {}, 'mm'))),
        '量到的 PD 會自動記到紀錄單;驗光儀要設成一樣。'),
      row('level', '綜合驗光儀調水平',
        h('div', { class: 'slider' },
          h('input', {
            type: 'range', min: -6, max: 6, step: 0.5, value: P.level, 'aria-label': '水平角度',
            oninput: (e) => { game.setLevel(+e.target.value, true); out.textContent = `${(+e.target.value).toFixed(1)}°`; },
            onchange: (e) => game.setLevel(+e.target.value),
          }),
          (out = h('output', {}, `${P.level.toFixed(1)}°`))),
        '水平泡要置中(0°)。'),
      row('aux', '輔助鏡、稜鏡挪開 → O',
        h('div', { class: 'row wrap' }, h('span', {}, `OD:${P.OD.aux}  OS:${P.OS.aux}`), btn('全部歸 O', () => { game.setAux('OD', 'O'); game.setAux('OS', 'O'); })),
        '原本有些輔助鏡沒有挪開。'),
      row('aperture', '雙眼窺孔打開', btn(P.aperture ? '窺孔已打開' : '打開窺孔', () => game.setAperture(true), { disabled: P.aperture })),
      row('height', '升降桌:受測者的眼睛輕靠驗光儀,與你的視線等高',
        h('div', { class: 'slider' },
          h('input', {
            type: 'range', min: 0, max: 1, step: 0.01, value: P.height, 'aria-label': '桌子高度',
            oninput: (e) => { game.setHeight(+e.target.value, true); out2.textContent = +e.target.value < 0.43 ? '偏低' : +e.target.value > 0.57 ? '偏高' : '高度適中'; },
            onchange: (e) => game.setHeight(+e.target.value),
          }),
          (out2 = h('output', {}, heightTxt)))),
      row('chart', '視標切成紅綠大 E(或 20/400)',
        h('div', { class: 'row wrap' }, btn('紅綠大 E', () => game.setChart({ mode: 'big_e' }), { cls: game.chart.mode === 'big_e' ? 'on' : '' }),
          btn('數字視標', () => game.setChart({ mode: 'digits' }), { cls: game.chart.mode === 'digits' ? 'on' : '' }))),
      row('lock', '綜合驗光儀鎖緊,受測者額頭輕靠',
        btn(P.locked ? '已鎖緊(按一下放鬆)' : '鎖緊', () => game.setLocked(!P.locked))),
      row('dim', '室內燈光微暗',
        btn(game.room.dim ? '燈光已調暗(按一下恢復)' : '調暗燈光', () => game.setDim(!game.room.dim))),
    );
  }
  let out, out2;
  render();
  return { el: root, render };
}

/* ============ 鏡片控制(驗光台與檢影共用) ============ */

export function lensControls(game, eye, { showAux = true, showJcc = true } = {}) {
  const L = game.phoro[eye];
  const jccOn = game.phoro.jcc.mode !== 'off' && game.activeEye === eye;
  const jccRed = jccOn ? jccRedAxis(game.phoro.jcc.mode, game.phoro.jcc.pos, L.a) : null;
  const wrap = h('div', { class: 'lens' });
  fillA(wrap,
    h('div', { class: 'rxline' }, h('span', { class: 'eyetag' }, eye), h('b', { class: 'rx' }, fmtRx(L)),
      L.aux !== 'O' ? h('span', { class: `auxtag ${L.aux}` }, L.aux) : null,
      axisDial(L.a, jccRed)),
    stepper('球面 SPH', L.s, [['−1', -1], ['−.25', -0.25], ['+.25', 0.25], ['+1', 1]], (d) => game.stepSph(eye, d), { fmt: (v) => fmtSph(v) }),
    stepper('散光 CYL', L.c, [['加 −.25', -0.25], ['退 +.25', 0.25]], (d) => game.stepCyl(eye, d), { fmt: (v) => (v === 0 ? '0' : fmt2(v)) }),
    h('div', { class: 'stepper' },
      h('span', { class: 'lab' }, '軸度 AXIS'),
      h('div', { class: 'row' },
        [['−15', -15], ['−5', -5], ['−1', -1]].map(([l, d]) => btn(l, () => game.stepAxis(eye, d), { cls: 'sm' })),
        h('output', { class: 'val' }, `${String(L.a).padStart(3, '0')}°`),
        [['+1', 1], ['+5', 5], ['+15', 15]].map(([l, d]) => btn(l, () => game.stepAxis(eye, d), { cls: 'sm' })))),
  );
  if (showAux) {
    fillA(wrap,h('div', { class: 'row wrap aux' },
      h('span', { class: 'lab' }, '輔助鏡'),
      segmented([['O', 'O 開放'], ['PH', 'PH 針孔'], ['RG', '紅綠濾片']], L.aux, (v) => game.setAux(eye, v))));
  }
  if (showJcc) {
    const jm = jccOn ? game.phoro.jcc.mode : 'off';
    fillA(wrap,h('div', { class: 'row wrap aux' },
      h('span', { class: 'lab' }, 'JCC'),
      segmented([['off', '關'], ['A', 'A 軸'], ['P', 'P 度數']], jm, (v) => { game.setActiveEye(eye); game.setJccMode(v); }),
      jccOn ? btn(`⟲ 翻轉(目前鏡片 ${game.phoro.jcc.pos})`, () => game.flipJcc(), { cls: 'sm' }) : null,
      jccOn ? h('span', { class: 'jcchint' }, game.describeJcc(game.phoro.jcc.mode, game.phoro.jcc.pos, L.a)) : null));
  }
  return wrap;
}

/* ============ 驗光台(主要操作) ============ */

const SLOT_BY_PHASE = { mp1: 'mp1', jcc: 'jcc', mp2: 'mp2' };

function recordBar(game) {
  const def = game.stepDef;
  const eye = game.activeEye;
  const ph = def.phase;
  const bar = h('div', { class: 'recbar' });
  const S = game.sheet;
  const vaSel = (id, val) => h('select', { id, 'aria-label': id }, VA_ROWS.map((r) => h('option', { value: r, selected: r === val }, `${r.toFixed(1)}`)));
  const deltaSel = (id, val) => h('select', { id, 'aria-label': id }, [-3, -2, -1, 0, 1, 2, 3].map((d) => h('option', { value: d, selected: d === val }, d > 0 ? `+${d}` : d === 0 ? '±0' : `−${Math.abs(d)}`)));
  const cur = (slot) => S.va[slot]?.[eye];
  if (ph === 'va') {
    const c = cur('ret');
    fillA(bar,
      h('div', { class: 'row wrap' }, h('span', { class: 'lab' }, 'VA'), vaSel('va_row', c?.row ?? 0.8), deltaSel('va_d', c?.delta ?? 0),
        h('span', { class: 'lab' }, 'PH'), h('select', { id: 'ph_row', 'aria-label': 'PH' }, [h('option', { value: '' }, '—'), ...VA_ROWS.map((r) => h('option', { value: r, selected: c?.phRow === r }, r.toFixed(1)))]), deltaSel('ph_d', c?.phDelta ?? 0)),
      btn(`記錄 ${eye} 的 VA → 紀錄單 ①`, () => {
        const q = (id) => bar.querySelector('#' + id).value;
        const phRow = q('ph_row');
        game.recordVA('ret', eye, { row: +q('va_row'), delta: +q('va_d'), phRow: phRow ? +phRow : null, phDelta: phRow ? +q('ph_d') : 0 });
        ctxToast('已記錄 VA');
      }, { cls: 'primary' }));
  } else if (SLOT_BY_PHASE[ph]) {
    const slot = SLOT_BY_PHASE[ph];
    const label = { mp1: '② 1st MPMVA', jcc: '③ JCC', mp2: '④ 2nd MPMVA' }[slot];
    const has = S.slots[slot][eye];
    fillA(bar,h('div', { class: 'row wrap' }, btn(`記錄度數 → ${label}`, () => { game.recordRx(slot, eye); ctxToast('已記錄度數'); }, { cls: 'primary' }),
      has ? h('span', { class: 'rx small' }, fmtRx(has.rx)) : null));
    if (slot !== 'jcc') {
      const c = cur(slot);
      fillA(bar,h('div', { class: 'row wrap' }, h('span', { class: 'lab' }, 'VA'), vaSel('va_row', c?.row ?? 1.0), deltaSel('va_d', c?.delta ?? 0),
        btn('記錄 VA', () => {
          const q = (id) => bar.querySelector('#' + id).value;
          game.recordVA(slot, eye, { row: +q('va_row'), delta: +q('va_d') });
          ctxToast('已記錄 VA');
        }, { cls: 'primary' }),
        c ? h('span', { class: 'rx small' }, `${c.row.toFixed(1)}${c.delta ? (c.delta > 0 ? '+' : '−') + Math.abs(c.delta) : ''}`) : null));
    }
  }
  return bar;
}

let ctxToast = () => {};

export function buildPhoro(game, ctx) {
  ctxToast = ctx.toast;
  const root = h('div', { class: 'panel' });
  function render() {
    root.replaceChildren();
    const eye = game.activeEye;
    const P = game.phoro;
    const ph = game.stepDef.phase;
    const nextTest = () => h('div', { class: 'row wrap' },
      h('span', { class: 'lab' }, '測試眼'),
      segmented(EYES.map((e) => [e, EYE_LABEL[e]]), eye, (e) => game.setActiveEye(e)),
    );
    fillA(root,
      section('① 測試眼與遮蓋',
        nextTest(),
        h('div', { class: 'row wrap' },
          btn('只測右眼(關左眼)', () => game.setTestEye('OD'), { cls: P.occ.OD === false && P.occ.OS ? 'on' : '' }),
          btn('只測左眼(關右眼)', () => game.setTestEye('OS'), { cls: P.occ.OS === false && P.occ.OD ? 'on' : '' }),
          btn('雙眼睜開', () => game.openBoth(), { cls: !P.occ.OD && !P.occ.OS ? 'on' : '' })),
        h('p', { class: 'note' }, `目前:右眼${P.occ.OD ? '遮住' : '睜開'} · 左眼${P.occ.OS ? '遮住' : '睜開'}`)),
      section('② 綜合驗光儀', lensControls(game, eye),
        h('label', { class: 'toggle' }, h('input', { type: 'checkbox', checked: game.autoComp, onchange: (e) => game.setAutoComp(e.target.checked) }),
          h('span', {}, '改散光時自動補償球面(每 −0.50DC 補 +0.25DS)'))),
      section('③ 視標(遠方 6 m)',
        h('div', { class: 'row wrap' }, segmented([['digits', '數字'], ['rg', '紅綠'], ['honey', '蜂巢'], ['clock', '鐘面圖'], ['big_e', '大 E']], game.chart.mode, (m) => game.setChart({ mode: m }))),
        h('div', { class: 'row wrap' },
          h('span', { class: 'lab' }, '視力列'),
          btn('◀ 大', () => game.stepRow(-1), { cls: 'sm' }),
          h('output', { class: 'val' }, `${game.chart.row.toFixed(1)} · ${snellen(game.chart.row)}`),
          btn('小 ▶', () => game.stepRow(1), { cls: 'sm' }),
          h('label', { class: 'toggle inline' }, h('input', { type: 'checkbox', checked: game.chart.isolate, onchange: (e) => game.setChart({ isolate: e.target.checked }) }), h('span', {}, '只顯示單列')))),
      section('④ 詢問受測者',
        h('div', { class: 'ask' },
          btn('請讀這一排', () => ctx.ask(() => game.askRead()), { cls: 'ask1' }),
          btn('兩片比較:再加 −0.25', () => ctx.ask(() => game.askCompare(-0.25))),
          btn('兩片比較:退 +0.25', () => ctx.ask(() => game.askCompare(0.25))),
          btn('紅綠哪邊清楚?', () => ctx.ask(() => game.askDuo())),
          btn('JCC:1 與 2 哪個清楚?', () => ctx.ask(() => game.askJcc())),
          btn('鐘面圖:哪條線最黑?', () => ctx.ask(() => game.askClock()))),
        ph === 'jcc' ? pathPicker(game) : null),
      (ph === 'va' || ph === 'mp1' || ph === 'jcc' || ph === 'mp2' || game.stepId === 'wd')
        ? section('⑤ 記錄(紙本紀錄單)', game.stepId === 'wd' ? wdRecord(game) : recordBar(game)) : null,
      game.god ? godBox(game) : null,
    );
  }
  return { el: root, render };
}

function pathPicker(game) {
  const eye = game.activeEye;
  const cur = game.path[eye]?.path;
  const c = game.phoro[eye].c;
  return h('div', { class: 'path' },
    h('p', { class: 'note' }, `散光路線(目前驗光儀散光 ${c === 0 ? '0' : fmt2(c)}DC):≥ −0.75DC 走 APA;0 ~ −0.50DC 走 PPAP;或用鐘面圖。`),
    segmented([['APA', 'APA'], ['PPAP', 'PPAP'], ['clock', '鐘面圖'], ['skip', '不需要']], cur, (p) => game.choosePath(p)));
}

function wdRecord(game) {
  return h('div', { class: 'recbar' },
    h('div', { class: 'row wrap' },
      btn(`雙眼給工作距離 −${game.wdD.toFixed(2)}D`, () => game.applyWD(), { cls: 'primary', disabled: game.wdApplied }),
      btn('記錄 OD 度數 → ①', () => { game.recordRx('ret', 'OD'); ctxToast('已記錄 OD'); }),
      btn('記錄 OS 度數 → ①', () => { game.recordRx('ret', 'OS'); ctxToast('已記錄 OS'); }),
      btn(`記錄工作距離 ${game.wdCm} cm`, () => { game.recordWD(game.wdCm); ctxToast('已記錄工作距離'); })));
}

function godBox(game) {
  const eye = game.activeEye;
  const i = game.godInfo(eye);
  return h('section', { class: 'sec god' }, h('h4', {}, '教學提示(上帝視角)'),
    h('p', {}, `實際處方:`, h('b', {}, i.trueText)),
    h('p', {}, `現在鏡片:${i.lensText} · 球面殘餘 ${i.dM >= 0 ? '+' : ''}${fmt2(i.dM)}D · 預測視力 ${i.va.toFixed(2)}`),
    h('p', { class: 'note' }, '正的殘餘 = 度數偏正(霧視);負的 = 過度負矯正(調節會補償,視力不一定變差)。'));
}

/* ============ 檢影 ============ */

export function buildRet(game, ctx) {
  const root = h('div', { class: 'panel' });
  const canvas = h('canvas', { class: 'retcv', 'aria-label': '檢影:瞳孔內的反射光' });
  const view = new RetinoView(canvas, game);
  ctx.retView = view;
  function render() {
    const eye = view.eye;
    fillK(root,
      h('p', { class: 'lead' }, '雙眼睜開,右眼掃右眼、左眼掃左眼。掃動時看瞳孔裡的反射光:跟著光條走 = 順動(加正),反著走 = 逆動(減正),整個瞳孔亮起 = 中和。'),
      h('div', { class: 'row wrap' }, segmented(EYES.map((e) => [e, EYE_LABEL[e]]), eye, (e) => { view.eye = e; game.setActiveEye(e); render(); })),
      canvas,
      h('div', { class: 'row wrap' },
        h('span', { class: 'lab' }, '光條角度'),
        btn('−15', () => setAngle(view.angle - 15), { cls: 'sm' }), btn('−5', () => setAngle(view.angle - 5), { cls: 'sm' }),
        h('output', { class: 'val' }, `${String(Math.round(view.angle)).padStart(3, '0')}°`),
        btn('+5', () => setAngle(view.angle + 5), { cls: 'sm' }), btn('+15', () => setAngle(view.angle + 15), { cls: 'sm' })),
      h('div', { class: 'slider' }, h('input', { type: 'range', min: 1, max: 180, step: 1, value: view.angle || 180, 'aria-label': '光條角度', oninput: (e) => { view.angle = +e.target.value; ang.textContent = `${String(view.angle).padStart(3, '0')}°`; } }), (ang = h('output', {}, `${String(Math.round(view.angle)).padStart(3, '0')}°`))),
      h('div', { class: 'row wrap' },
        btn(view.auto ? '⏸ 暫停自動掃動' : '▶ 自動掃動', () => { view.auto = !view.auto; render(); }),
        h('label', { class: 'toggle inline' }, h('input', { type: 'checkbox', checked: view.slow, onchange: (e) => { view.slow = e.target.checked; } }), h('span', {}, '慢動作')),
        h('span', { class: 'note' }, '也可以直接用手指/滑鼠在畫面上拖曳光帶')),
      section('綜合驗光儀鏡片(中和用)', lensControls(game, eye, { showAux: false, showJcc: false })),
      section('工作距離與結束檢影',
        h('div', { class: 'row wrap' }, h('span', { class: 'lab' }, '工作距離'),
          segmented(WD_OPTIONS.map((o) => [o.cm, `${o.cm} cm${o.note ? '·' + o.note : ''}`]), game.wdCm, (cm) => game.setWD(cm))),
        h('p', { class: 'note' }, `目前 ${game.wdCm} cm = ${game.wdD.toFixed(2)}D。距離越近,工作距離度數越大(25 cm = 4.00D),光帶的反射也變化更快,記得最後要扣掉。`),
        h('p', { class: 'note' }, `兩眼都中和後,雙眼同時給工作距離的度數(= −${game.wdD.toFixed(2)}D),並把 Ret 度數與工作距離記錄下來。`),
        h('div', { class: 'row wrap' },
          btn(`雙眼給工作距離 −${game.wdD.toFixed(2)}D`, () => game.applyWD(), { cls: 'primary', disabled: game.wdApplied }),
          btn('記錄 OD → ①', () => { game.recordRx('ret', 'OD'); ctx.toast('已記錄 OD'); }),
          btn('記錄 OS → ①', () => { game.recordRx('ret', 'OS'); ctx.toast('已記錄 OS'); }),
          btn(`記錄工作距離 ${game.wdCm} cm`, () => { game.recordWD(game.wdCm); ctx.toast('已記錄工作距離'); }))),
    );
  }
  let ang;
  const setAngle = (a) => { view.angle = ((a - 1 + 180) % 180) + 1; render(); };
  render();
  return { el: root, render, onShow: () => view.start(), onHide: () => view.stop() };
}

/* ============ 視力表 ============ */

export function buildChartPanel(game, ctx) {
  const root = h('div', { class: 'panel' });
  const previewCanvas = h('canvas', { class: 'chartcv', width: CHART_W, height: CHART_H, 'aria-label': '視標畫面' });
  const patientCanvas = h('canvas', { class: 'chartcv', width: 300, height: Math.round(300 * CHART_H / CHART_W), 'aria-label': '受測者看到的樣子' });
  function render() {
    fillK(root,
      h('p', { class: 'lead' }, '這是投影機投到 6 公尺外視力表的畫面。右邊是「受測者看到的樣子」(教學提示,考試模式不顯示)。'),
      h('div', { class: 'chartpair' },
        h('figure', {}, previewCanvas, h('figcaption', {}, '視標畫面')),
        game.mode !== 'exam' ? h('figure', {}, patientCanvas, h('figcaption', {}, '受測者視野(模擬模糊)')) : null),
      h('div', { class: 'row wrap' },
        segmented([['digits', '數字'], ['rg', '紅綠'], ['honey', '蜂巢'], ['clock', '鐘面圖'], ['big_e', '大 E']], game.chart.mode, (m) => game.setChart({ mode: m }))),
      h('div', { class: 'row wrap' },
        btn('◀ 大', () => game.stepRow(-1), { cls: 'sm' }),
        h('output', { class: 'val' }, `${game.chart.row.toFixed(1)} · ${snellen(game.chart.row)}`),
        btn('小 ▶', () => game.stepRow(1), { cls: 'sm' }),
        h('label', { class: 'toggle inline' }, h('input', { type: 'checkbox', checked: game.chart.isolate, onchange: (e) => game.setChart({ isolate: e.target.checked }) }), h('span', {}, '只顯示單列'))),
      h('div', { class: 'row wrap' }, btn('請受測者讀這一排', () => ctx.ask(() => game.askRead()), { cls: 'primary' })),
    );
    paint();
  }
  function paint() {
    drawChart(previewCanvas.getContext('2d'), game.chart, game.patient.seed);
    if (game.mode !== 'exam') paintPatient();
  }
  function paintPatient() {
    const eyes = game.openEyes();
    const tmp = ctx.tmpCanvas ?? (ctx.tmpCanvas = document.createElement('canvas'));
    tmp.width = patientCanvas.width; tmp.height = patientCanvas.height;
    const t2 = tmp.getContext('2d');
    // 先把視標縮小畫進暫存畫布
    const full = ctx.fullCanvas ?? (ctx.fullCanvas = document.createElement('canvas'));
    full.width = CHART_W; full.height = CHART_H;
    drawChart(full.getContext('2d'), game.chart, game.patient.seed, { patientView: clockView(game) });
    t2.clearRect(0, 0, tmp.width, tmp.height);
    t2.drawImage(full, 0, 0, tmp.width, tmp.height);
    if (!eyes.length) {
      const c = patientCanvas.getContext('2d'); c.fillStyle = '#000'; c.fillRect(0, 0, patientCanvas.width, patientCanvas.height);
      c.fillStyle = '#9aa'; c.font = '14px sans-serif'; c.fillText('兩眼都被遮住', 90, 120); return;
    }
    const e = game.activeEye && !game.phoro.occ[game.activeEye] ? game.activeEye : eyes[0];
    const v = visionOf(game, e);
    drawBlurred(patientCanvas, tmp, { ...v, kPx: 14 });
  }
  render();
  return { el: root, render, paint };
}

export function visionOf(game, eye) {
  const res = eyeResidual(game.patient, eye, game.lensVec(eye));
  let m = res.M;
  if (m < 0) m += Math.min(-m, game.patient.accAmp) * 0.97;
  const k = game.phoro[eye].aux === 'PH' ? 0.35 : 1;
  return { m: m * k, j0: res.J0 * k, j45: res.J45 * k };
}

function clockView(game) {
  const eye = game.activeEye;
  const res = eyeResidual(game.patient, eye, game.lensVec(eye));
  const J = Math.hypot(res.J0, res.J45);
  if (J < 0.2 || res.M < 0.5) return null;
  const need = Math.atan2(-res.J45, -res.J0) * 90 / Math.PI;
  const axis = ((need % 180) + 180) % 180;
  return { clockDarkness: (n) => { const a = n * 30; const d = Math.min(Math.abs(mod180(a) - axis), 180 - Math.abs(mod180(a) - axis)); return 0.35 + 0.65 * Math.max(0, 1 - d / 40); } };
}

/* ============ 紀錄單 ============ */

export function buildSheet(game) {
  const root = h('div', { class: 'panel' });
  function render() {
    const S = game.sheet;
    const rows = [['① Retinoscopy(+ 工作距離)', 'ret'], ['② 1st MPMVA', 'mp1'], ['③ JCC', 'jcc'], ['④ 2nd MPMVA', 'mp2']];
    const cell = (slot, eye) => {
      const e = S.slots[slot][eye];
      const va = S.va[slot]?.[eye];
      return h('td', {}, e ? h('div', { class: 'rx small' }, fmtRx(e.rx)) : h('span', { class: 'dim' }, '—'),
        va ? h('div', { class: 'small' }, `VA ${va.row.toFixed(1)}${va.delta ? (va.delta > 0 ? '+' : '−') + Math.abs(va.delta) : ''}${va.phRow ? ` · PH ${va.phRow.toFixed(1)}` : ''}`) : null);
    };
    fillK(root,
      h('p', { class: 'lead' }, '紙本紀錄單。實際在「驗光台」與「檢影」分頁裡按「記錄」寫入,這裡可以檢查有沒有漏記。'),
      h('div', { class: 'sheetmeta' },
        h('span', {}, `受測者:${game.patient.name}`),
        h('span', {}, `PD:${S.pd ?? '＿'} mm`),
        h('span', {}, `工作距離:${S.wdCm ?? '＿'} cm`)),
      h('div', { class: 'tw' }, h('table', { class: 'sheet' },
        h('thead', {}, h('tr', {}, h('th', {}, ''), h('th', {}, 'OD 右眼'), h('th', {}, 'OS 左眼'))),
        h('tbody', {}, rows.map(([lab, slot]) => h('tr', {}, h('th', {}, lab), cell(slot, 'OD'), cell(slot, 'OS')))))),
      h('p', { class: 'note' }, '記錄時機:兩眼 Ret 結束 + 工作距離(度數 + VA) → 1st MPMVA(度數 + VA)→ JCC 結束(度數)→ 2nd MPMVA(度數 + 最佳 VA)。視力 < 0.8 要加測 PH 並註記。'),
    );
  }
  render();
  return { el: root, render };
}

/* ============ 對話 ============ */

export function buildLog(game) {
  const root = h('div', { class: 'panel' });
  function render() {
    fillK(root,
      h('div', { class: 'log' }, game.log.slice(-40).map((m) => h('div', { class: `msg ${m.who}` },
        h('span', { class: 'who' }, m.who === 'patient' ? game.patient.name : m.who === 'you' ? '你' : '系統'), h('span', { class: 'tx' }, m.text)))));
    const l = root.querySelector('.log'); if (l) l.scrollTop = l.scrollHeight;
  }
  render();
  return { el: root, render };
}

/* ============ 速查 ============ */

export function buildHelp() {
  const el = h('div', { class: 'panel help' });
  el.innerHTML = `
  <h4>期中考流程</h4>
  <p class="route">Auto/Ret → 1st MPMVA → JCC → 2nd MPMVA(OD、OS 各做一次)</p>
  <h4>MPMVA</h4>
  <ul>
   <li>先<b>霧視</b>:加約 +1.00D,讓 0.6 視標明顯模糊(視力約 0.3–0.5)。</li>
   <li>每次 <b>−0.25D</b> 慢慢給,視力不再進步就停;最後給兩片比較。</li>
   <li>目標:<b>最大正度數 + 最佳視力</b>(過度負矯正 → 調節會補回去)。</li>
  </ul>
  <h4>紅綠檢查(口訣:紅加負、綠加正)</h4>
  <ul><li>紅色清楚 → 加 −0.25D;綠色清楚 → 加 +0.25D;做到「綠色第一片清楚」或兩邊一樣。進 JCC 前要先到這個狀態。</li></ul>
  <h4>散光分流</h4>
  <ul>
   <li>散光 ≥ −0.75DC → JCC <b>APA</b>:軸 → 度數 → 軸</li>
   <li>0 ~ −0.50DC → <b>PPAP</b>:P(找初軸 180/45/90/135)→ P(初度數)→ A → P;沒有散光先給 −0.25DC</li>
   <li>1st MPMVA 後視力不佳又沒散光 → 霧視 + <b>鐘面圖</b>(最黑的線 數字 × 30 = 軸)</li>
  </ul>
  <h4>JCC</h4>
  <ul>
   <li>軸(A):追<b>紅點</b>,哪片清楚就往紅點方向轉,直到兩片一樣。</li>
   <li>度數(P):<b>紅加白減</b>。紅點在軸上較清楚 → 多一片 −0.25DC;白點較清楚 → 少一片。</li>
   <li>等價球面:每多 −0.50DC → 球面 +0.25DS;每少 −0.50DC → 球面 −0.25DS。</li>
  </ul>
  <h4>檢影</h4>
  <ul>
   <li>順加逆減:順動加正、逆動減正(或加負)。先中和第一軸,再轉 90° 中和第二軸。</li>
   <li>最後雙眼同時給工作距離度數(67 cm = −1.50D),並記錄。</li>
  </ul>
  <h4>VA</h4>
  <ul><li>關掉另一隻眼,從 0.6–0.8 開始問;&lt; 0.8 要加測 PH 並註記,測完把 PH 轉開。</li></ul>`;
  return { el, render() {} };
}
