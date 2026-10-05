// 教學模式的「教練」:把每一個大步驟拆成一連串小動作,自動偵測做到了沒,
// 並告訴你「現在要按哪裡、為什麼」。target 對應介面上 data-coach 的控制項(會發光)。
import { SETUP_ITEMS, EYE_LABEL, other } from './state.js';
import { toVec } from '../sim/optics.js';

const ofStep = (g, type) => g.events.filter((e) => e.step === g.stepId && e.type === type);

export function coachSteps(g) {
  const def = g.stepDef;
  const ph = def.phase ?? def.id;
  const eye = def.eye ?? g.activeEye;
  const P = g.phoro;
  const S = g.sheet;
  const isolated = !P.occ[eye] && P.occ[other(eye)];
  const reads = g.hist[eye]?.reads.filter((r) => r.step === g.stepId && r.isolated) ?? [];
  const lensReads = reads.filter((r) => r.lens === g.lensKey(eye));
  const out = [];
  const s = (done, label, why, target) => out.push({ done: !!done, label, why, target });
  const occStep = () => s(isolated, `遮住 ${other(eye)},只開 ${eye}`, '單眼驗光:另一眼一定要遮住,不然受測者會用比較好的那隻眼睛回答。', 'occ');

  switch (ph) {
    case 'setup': {
      const st = g.setupState();
      const why = {
        sanitize: '受測者會碰到額靠與下巴托,每位都要消毒。',
        pd: '驗光儀兩個窺孔的距離要等於受測者的瞳距,眼睛才會對準鏡片光學中心。',
        level: '儀器沒水平,散光軸度會整個跑掉。',
        aux: '輔助鏡如果停在針孔或稜鏡,受測者會看不清或看到重影。',
        aperture: '窺孔蓋沒打開,受測者什麼都看不到。',
        height: '受測者的眼睛要對準窺孔,而且跟你的視線同高才好檢影。',
        chart: '先用最大的視標讓受測者找到目標。',
        lock: '鎖緊後驗光儀才不會在檢查中晃動。',
        dim: '燈光調暗,瞳孔放大,檢影的反射光才看得清楚。',
      };
      for (const it of SETUP_ITEMS) s(st[it.id], it.label, why[it.id], `setup:${it.id}`);
      break;
    }
    case 'ret':
      for (const e of ['OD', 'OS']) s(g.retNeutral(e, 0.25), `${EYE_LABEL[e]}:兩個主軸都中和`, '先找到反射光與光條平行的角度,順動加正、逆動減正到中和;再轉 90° 用散光中和第二軸。', 'retfocus');
      s(false, '按「完成檢影」', '兩眼都中和之後再進工作距離。', 'advance');
      break;
    case 'wd':
      s(g.wdApplied, `雙眼給工作距離 −${g.wdD.toFixed(2)}D`, `檢影時人在 ${g.wdCm} cm,量到的度數多了 1/WD,要扣回來才是看遠的度數。`, 'wd');
      for (const e of ['OD', 'OS']) s(S.slots.ret[e], `把 ${e} 的度數寫到紀錄單 ①`, '紀錄單 ① 是 Ret 結果(已扣工作距離)。', 'rec');
      s(S.wdCm !== null, '紀錄單寫上工作距離', '老師會看你用多少工作距離。', 'rec');
      break;
    case 'va': {
      occStep();
      s(g.chart.mode === 'tumble', '視標切到 E 字視標', 'VA 用 E 字視標:請受測者說出每個 E 的缺口方向(上下左右)。', 'chart:tumble');
      s(reads.length > 0, '從 0.6–0.8 開始,請受測者讀', '從中間開始問,比從最大一路往下快。', 'read');
      const best = Math.max(0, ...reads.filter((r) => r.correct === r.total && !r.ph).map((r) => r.row));
      const foundEnd = reads.some((r) => !r.ph && r.correct < r.total) || best >= 1.2;
      s(foundEnd, '一列一列往小的換,直到讀不完整', '找到「剛好還讀得完」的那一列,就是當下最佳視力。', 'rowS');
      if (foundEnd && best < 0.8) s(reads.some((r) => r.ph), '視力 < 0.8:轉到 PH 針孔再讀一次', '針孔會消除屈光不正的模糊;PH 有進步代表是度數問題,沒進步要懷疑病理。', 'ph');
      s(S.va.ret[eye], '把 VA 寫到紀錄單 ①', '例如 0.6+2 = 0.6 那列全對、下一列對 2 個。', 'rec');
      if (reads.some((r) => r.ph)) s(P[eye].aux !== 'PH', '測完把 PH 轉開', '下一步 MPMVA 要看清楚,不能還留著針孔。', 'ph');
      break;
    }
    case 'mp1':
    case 'mp2': {
      if (ph === 'mp2') s(P.jcc.mode === 'off', '收起 JCC', '2nd MPMVA 前要把 JCC 移開。', 'jcc');
      occStep();
      const fogged = g.hist[eye].fogMax[ph] >= 0.5;
      s(fogged, '霧視:球面 +1.00(視標要明顯變模糊)', '先讓度數偏正,受測者的調節就放鬆;之後一格一格加負,才不會給太多負度數。', 'fog');
      const dM = g.dM(eye);
      s(fogged && dM <= 0.3, '每次 −0.25,每次都請受測者讀,直到視力不再進步', '加負後視力有進步才給;不再進步還繼續加負 = 過度矯正(調節會補回來,看起來一樣清楚)。', 'sphM');
      s(ofStep(g, 'compare').length > 0, '兩片比較確認(再加 −0.25 有沒有更清楚)', '「一樣清楚」或「第 1 片清楚」就不要再加負:最大正度數 + 最佳視力。', 'cmp');
      const rec = S.slots[ph][eye];
      s(rec && Math.abs(toVec(rec.rx).M - toVec(g.lens(eye)).M) < 0.01 && rec.rx.c === g.lens(eye).c, `把度數寫到紀錄單 ${ph === 'mp1' ? '②' : '④'}`, '每做完一個階段就記錄,老師會看過程。', 'rec');
      s(S.va[ph][eye], '寫上這個度數的最佳 VA', '用現在的鏡片讀到的最佳一列。', 'rec');
      break;
    }
    case 'duo': {
      occStep();
      s(P[eye].aux === 'RG' || g.chart.mode === 'rg', '放上紅綠濾片', '紅綠濾片利用色像差:紅光焦點在後、綠光在前。', 'rg');
      const d = g.hist[eye].duo.filter((x) => x.t >= (g.events.find((e) => e.type === 'step' && e.to === g.stepId)?.t ?? 0));
      s(d.length > 0, '問:「紅色、綠色哪一邊的 E 比較清楚?」', '看比最佳視力大一行的視標。', 'duo');
      const last = d[d.length - 1];
      s(last && last.ans !== 'red', '紅色清楚就加 −0.25,直到綠色第一片清楚(或一樣)', '紅清楚 = 度數偏正 → 加負;綠清楚 = 偏負 → 加正。口訣:紅加負、綠加正。', last?.ans === 'red' ? 'sphM' : 'duo');
      break;
    }
    case 'jcc': {
      occStep();
      const path = g.path[eye]?.path;
      s(path, `選散光路線(目前散光 ${P[eye].c ? P[eye].c.toFixed(2) : '0'}DC)`, '≥ −0.75DC 走 APA(軸 → 度數 → 軸);0 ~ −0.50DC 走 PPAP;沒散光但視力不好可以用鐘面圖。', 'path');
      const jcc = ofStep(g, 'jcc');
      if (path === 'APA' || path === 'PPAP') {
        const A = jcc.filter((e) => e.mode === 'A'), Pw = jcc.filter((e) => e.mode === 'P');
        const order = path === 'APA'
          ? [['A', '軸(A):JCC 切到 A,翻轉比較,往紅點那邊轉軸', '追紅點:哪片清楚就把軸往那片紅點的方向轉,一樣清楚就停。'],
            ['P', '度數(P):JCC 切到 P,紅點在軸上清楚 → 加散光,白點清楚 → 退散光', '紅加白減。每加 −0.50DC,球面補 +0.25DS。']]
          : [['P', '先用 P 找初軸(180 / 45 / 90 / 135)與初度數', '散光小的時候先確定「有沒有散光、大概在哪個軸」。沒散光先給 −0.25DC。'],
            ['A', '再用 A 精修軸度', '追紅點,直到兩片一樣。']];
        s(order[0][0] === 'A' ? A.length : Pw.length, order[0][1], order[0][2], 'jcc');
        s(order[1][0] === 'A' ? A.length : Pw.length, order[1][1], order[1][2], 'jcc');
        const lastAns = jcc[jcc.length - 1];
        s(lastAns && lastAns.ans === 'same', '直到兩片「一樣清楚」', '一樣清楚 = 軸 / 度數已經到位。', 'ask');
      } else if (path === 'clock') {
        s(g.dM(eye) >= 0.5, '先霧視(約 +0.50 ~ +1.00)', '鐘面圖要在霧視下看,最黑的線才代表散光軸。', 'fog');
        s(ofStep(g, 'clock').length, '問「哪一條線最黑?」', '最黑的線的數字 × 30 = 負散光軸。', 'ask');
      }
      if (path) s(S.slots.jcc[eye], '把度數寫到紀錄單 ③', 'JCC 結束後的度數。', 'rec');
      break;
    }
    default:
      break;
  }
  const cur = out.findIndex((x) => !x.done);
  return { steps: out, cur };
}
