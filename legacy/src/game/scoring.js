// 計分:全部以「實際發生的事件」與紀錄單核對,每項附上白話回饋
import { toVec, fromVec, fmtRx, VA_ROWS, mod180, blurStrength } from '../sim/optics.js';
import { subVec } from '../sim/optics.js';
import { SETUP_ITEMS, EYES, EYE_LABEL } from './state.js';
import { wdDiopter } from '../sim/retino.js';

const rowIdx = (r) => VA_ROWS.indexOf(r);
const axisDiff = (a, b) => { const d = Math.abs(mod180(a) - mod180(b)); return Math.min(d, 180 - d); };
const pct = (x) => Math.max(0, Math.min(1, x));

// 以處方向量計算「與真實處方的距離」(D)
export function rxDistance(a, b) {
  return blurStrength(subVec(toVec(a), toVec(b)));
}

export function score(game) {
  const items = []; // { cat, label, got, max, note, ok }
  const add = (cat, label, got, max, note) => items.push({ cat, label, got: Math.round(got * 10) / 10, max, note });
  const P = game.patient;

  /* 前置 */
  const st = game.setupSnap ?? game.setupState();
  for (const it of SETUP_ITEMS) {
    add('前置', it.label, st[it.id] ? it.weight : 0, it.weight, st[it.id] ? '' : '這一項沒有完成或沒有做對。');
  }

  /* 檢影 */
  for (const eye of EYES) {
    const rec = game.sheet.slots.ret[eye];
    const t = game.trueRx(eye);
    if (!rec) { add('檢影', `${EYE_LABEL[eye]} 檢影結果`, 0, 7.5, '紀錄單 ① 沒有記錄 Ret 度數。'); continue; }
    const dist = rxDistance(rec.rx, t);
    const g = dist <= 0.3 ? 7.5 : dist <= 0.55 ? 5.5 : dist <= 0.8 ? 3.5 : dist <= 1.2 ? 1.5 : 0;
    const wdOk = game.wdApplied;
    const wdPen = wdOk ? 0 : Math.min(g, 1.5);
    add('檢影', `${EYE_LABEL[eye]} 檢影結果`, g - wdPen, 7.5,
      `你記錄 ${fmtRx(rec.rx)};實際 ${fmtRx(t)}(差 ${dist.toFixed(2)}D)。` + (wdOk ? '' : ' 沒有給工作距離。'));
  }

  /* 各眼 */
  for (const eye of EYES) {
    const L = EYE_LABEL[eye];
    const h = game.hist[eye];
    const t = game.trueRx(eye);
    const tM = toVec(t).M;
    const S = game.sheet;
    const mpAcc = (slot) => {
      const r = S.slots[slot][eye];
      if (!r) return null;
      const dM = toVec(r.rx).M - tM;
      return dM;
    };

    // VA
    {
      const va = S.va.ret[eye];
      let g = 0, note = '';
      const iso = h.reads.some((r) => r.isolated);
      if (!va) note = '沒有把 VA 記到紀錄單 ①。';
      else {
        const diff = Math.abs(rowIdx(va.row) - rowIdx(va.actualRow));
        g = diff <= 1 ? 2.5 : diff <= 2 ? 1.5 : 0.5;
        note = `記錄 ${va.row},實際約 ${va.actualRow}。`;
        const needPH = va.actualRow < 0.8;
        if (needPH && va.phRow == null) { g -= 0.5; note += ' 視力 < 0.8 要加測 PH。'; }
        if (needPH && va.phRow != null) g += 0.5;
        g = Math.max(0, Math.min(2.5, g));
      }
      if (iso) g += 1.5; else note += ' 沒有遮住另一隻眼。';
      add(L, 'VA 測量與記錄', Math.min(4, g), 4, note.trim());
    }

    // 1st MPMVA(1 分霧視 + 5 分準確)
    {
      const fog = h.fogMax.mp1 >= 0.5;
      const dM = mpAcc('mp1');
      let g = fog ? 1 : 0;
      let note = fog ? '' : '沒有先霧視。';
      if (dM === null) note += ' 沒有記錄 1st MPMVA 度數。';
      else {
        const a = Math.abs(dM);
        g += dM < -0.3 ? 2 : a <= 0.3 ? 5 : a <= 0.55 ? 3 : 1;
        note += ` 球面等價${dM >= 0 ? '多正' : '多負'} ${Math.abs(dM).toFixed(2)}D。`;
        if (dM < -0.3) note += ' 過度負矯正了:視力不再進步就要停。';
      }
      add(L, '1st MPMVA', Math.min(6, g), 6, note.trim());
    }

    // 紅綠
    {
      const d = h.duo;
      let g = 0, note = '沒有做紅綠檢查。';
      if (d.length) {
        const last = d[d.length - 1];
        if (last.ans === 'red') { g = 1; note = '最後停在「紅色較清楚」(度數偏正),要繼續加負到綠色第一片清楚。'; }
        else { g = 3; note = last.ans === 'green' ? '停在綠色第一片清楚。' : '停在兩邊一樣清楚。'; }
      }
      add(L, '紅綠檢查', g, 3, note);
    }

    // 散光路線
    {
      const p = game.path[eye];
      let g = 0, note = '沒有選擇散光路線。';
      if (p) {
        const c = Math.abs(p.cyl);
        const expect = c >= 0.75 ? ['APA'] : ['PPAP', 'clock'];
        if (p.path === 'skip') { g = c === 0 && Math.abs(t.c) < 0.5 ? 3 : 0; note = g ? '無散光、不需要處理。' : '實際有散光,不能略過。'; }
        else if (expect.includes(p.path)) { g = 3; note = `散光 ${c.toFixed(2)}DC → ${p.path} 正確。`; }
        else { g = 0; note = `散光 ${c.toFixed(2)}DC 時應走 ${expect[0]},你選了 ${p.path}。`; }
      }
      add(L, '散光路線選擇', g, 3, note);
    }

    // JCC 結果(軸 4 + 度數 4)
    {
      const r = S.slots.jcc[eye];
      let g = 0, note = '沒有記錄 JCC 後的度數(③)。';
      if (r) {
        const dc = Math.abs(r.rx.c - t.c);
        let gc = dc <= 0.3 ? 4 : dc <= 0.55 ? 2 : 0;
        let ga;
        if (Math.abs(t.c) < 0.4 && Math.abs(r.rx.c) < 0.4) ga = 4;
        else {
          const da = axisDiff(r.rx.a, t.a);
          ga = da <= 10 ? 4 : da <= 20 ? 2 : 0;
        }
        g = gc + ga;
        note = `記錄 ${fmtRx(r.rx)};實際散光 ${t.c.toFixed(2)} × ${Math.round(t.a)}。`;
      }
      add(L, 'JCC / 散光度數與軸度', g, 8, note);
    }

    // 2nd MPMVA 與最終度數 + 記錄
    {
      const r = S.slots.mp2[eye];
      const va = S.va.mp2[eye];
      let g = 0, note = '沒有記錄 2nd MPMVA。';
      if (r) {
        const dist = rxDistance(r.rx, t);
        g += dist <= 0.3 ? 4 : dist <= 0.55 ? 3 : dist <= 0.8 ? 1.5 : 0;
        note = `最終 ${fmtRx(r.rx)};實際 ${fmtRx(t)}(差 ${dist.toFixed(2)}D)。`;
        if (va && Math.abs(rowIdx(va.row) - rowIdx(va.actualRow)) <= 1) g += 2; else note += ' VA 記錄與實際有落差或沒記。';
      }
      add(L, '2nd MPMVA 與最終度數', Math.min(6, g), 6, note);
    }
  }

  /* 紀錄單完整度 */
  {
    let have = 0, need = 0;
    for (const eye of EYES) {
      for (const [slot, va] of [['ret', true], ['mp1', true], ['jcc', false], ['mp2', true]]) {
        need++;
        if (game.sheet.slots[slot][eye] && (!va || game.sheet.va[slot]?.[eye])) have++;
      }
    }
    const pdOk = game.sheet.pd !== null && Math.abs(game.sheet.pd - P.pd) <= 1;
    const wdOk = game.sheet.wdCm === game.wdCm;
    let g = 4 * (have / need) + (pdOk ? 0.5 : 0) + (wdOk ? 0.5 : 0);
    add('紀錄單', '四個項目 × 兩眼 + PD + 工作距離', g, 5, `完整 ${have}/${need} 格;PD ${pdOk ? '✓' : '✗'};工作距離 ${wdOk ? '✓' : '✗'}。`);
  }

  /* 時間 */
  const min = game.elapsedMs / 60000;
  const timed = game.mode === 'exam';
  if (timed) {
    const g = min <= 18 ? 10 : min <= 22 ? 7 : min <= 25 ? 4 : 1;
    add('時間', `用時 ${min.toFixed(1)} 分鐘(期中考 18 分鐘)`, g, 10, min <= 18 ? '在時限內。' : '超過期中考時限。');
  }

  const total = items.reduce((s, i) => s + i.got, 0);
  const max = items.reduce((s, i) => s + i.max, 0);
  const cats = {};
  for (const i of items) {
    cats[i.cat] ??= { got: 0, max: 0 };
    cats[i.cat].got += i.got; cats[i.cat].max += i.max;
  }
  const score100 = Math.round((total / max) * 100);
  return { items, cats, total, max, score100, timed, minutes: min };
}

export function summarizeEyes(game) {
  return EYES.map((eye) => {
    const t = game.trueRx(eye);
    const fin = game.sheet.slots.mp2[eye]?.rx ?? game.lens(eye);
    return { eye, trueRx: t, final: fin, dist: rxDistance(fin, t), base: game.patient.eyes[eye].base };
  });
}
