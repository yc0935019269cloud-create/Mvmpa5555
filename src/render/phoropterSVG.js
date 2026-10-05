// 擬真綜合驗光儀(驗光師視角的正面)— 純字串 SVG,同時用在操作台(可轉旋鈕)與 3D 模型貼圖
// 左邊是受測者右眼 OD(驗光師看過去在左),右邊是 OS。
import { fmtSph, jccRedAxis } from '../sim/optics.js';

export const VB_W = 1000;
export const VB_H = 680;
// 輔助鏡轉盤:O 開放、R 紅色濾片、P 偏光、PH 針孔、RG 紅綠濾片、+.12、6ΔU / 10ΔI 稜鏡
export const AUX_POS = ['O', 'R', 'P', 'PH', 'RG', '+.12', '6ΔU', '10ΔI'];
export const PRISM_AUX = ['6ΔU', '10ΔI'];

const r1 = (n) => Math.round(n * 10) / 10;
const rad = (d) => (d * Math.PI) / 180;

function defs() {
  return `<defs>
  <linearGradient id="pf-paint" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ece7da"/><stop offset=".55" stop-color="#d8d2c3"/><stop offset="1" stop-color="#bcb6a6"/></linearGradient>
  <linearGradient id="pf-paint2" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#f1ede2"/><stop offset="1" stop-color="#cdc7b8"/></linearGradient>
  <radialGradient id="pf-cell" cx=".38" cy=".28" r=".95"><stop offset="0" stop-color="#f3efe5"/><stop offset=".62" stop-color="#dcd6c8"/><stop offset="1" stop-color="#b2ac9d"/></radialGradient>
  <linearGradient id="pf-chrome" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#fbfbfb"/><stop offset=".25" stop-color="#a7aeb2"/><stop offset=".5" stop-color="#f0f2f3"/><stop offset=".75" stop-color="#868d92"/><stop offset="1" stop-color="#dfe3e5"/></linearGradient>
  <radialGradient id="pf-chromeR" cx=".34" cy=".3" r=".85"><stop offset="0" stop-color="#ffffff"/><stop offset=".45" stop-color="#c9cfd2"/><stop offset=".8" stop-color="#8d9498"/><stop offset="1" stop-color="#e6e9ea"/></radialGradient>
  <radialGradient id="pf-glass" cx=".35" cy=".3" r=".9"><stop offset="0" stop-color="#46586a"/><stop offset=".5" stop-color="#1c2732"/><stop offset="1" stop-color="#0a0f14"/></radialGradient>
  <linearGradient id="pf-black" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#2f3438"/><stop offset="1" stop-color="#121416"/></linearGradient>
  <linearGradient id="pf-win" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#f7f5ee"/><stop offset="1" stop-color="#dcd8cb"/></linearGradient>
  <clipPath id="pf-body"><rect x="14" y="36" width="972" height="634" rx="70"/></clipPath>
  <filter id="pf-sh" x="-25%" y="-25%" width="150%" height="150%"><feDropShadow dx="0" dy="5" stdDeviation="5" flood-color="#000" flood-opacity=".38"/></filter>
  <filter id="pf-sh2" x="-25%" y="-25%" width="150%" height="150%"><feDropShadow dx="0" dy="2.5" stdDeviation="2.5" flood-color="#000" flood-opacity=".4"/></filter>
</defs>`;
}

// 旋鈕:外圈滾花、可旋轉的標示、固定的指示三角
function knob({ id, e, cx, cy, r, rot = 0, inner = '', knurl = true, label = '', hint = '', side = null }) {
  const hit = `<circle cx="${cx}" cy="${cy}" r="${r + 10}" fill="transparent"/>`;
  return `<g class="pf-knob" data-k="${id}" data-e="${e}" style="cursor:grab" ${hint ? `data-hint="${hint}"` : ''}>
  <circle cx="${cx}" cy="${cy}" r="${r}" fill="url(#pf-chromeR)" stroke="#5f666a" stroke-width="2" filter="url(#pf-sh)"/>
  <g transform="rotate(${r1(rot)} ${cx} ${cy})">
    ${knurl ? `<circle cx="${cx}" cy="${cy}" r="${r - r * 0.09}" fill="none" stroke="#4d5357" stroke-width="${r1(r * 0.17)}" stroke-dasharray="2.2 3" opacity=".55"/>` : ''}
    ${inner}
    <rect x="${cx - 2.5}" y="${cy - r + 2}" width="5" height="${r1(r * 0.28)}" rx="2" fill="#262a2d"/>
  </g>
  <polygon points="${cx - 6},${cy - r - 11} ${cx + 6},${cy - r - 11} ${cx},${cy - r - 1}" fill="#c1382b" stroke="#fff" stroke-width=".8"/>
  ${label ? (side ? `<text x="${side === 'r' ? cx + r + 8 : cx - r - 8}" y="${cy + 4}" text-anchor="${side === 'r' ? 'start' : 'end'}" font-size="12" fill="#5b574c" font-family="sans-serif">${label}</text>` : `<text x="${cx}" y="${cy + r + 17}" text-anchor="middle" font-size="12" fill="#5b574c" font-family="sans-serif">${label}</text>`) : ''}
  ${hit}
</g>`;
}

function ringLabels(cx, cy, R, items, fs, fill = '#1f2326', stepDeg = null) {
  return items.map((t, i) => {
    const a = stepDeg !== null ? i * stepDeg : (i * 360) / items.length;
    return `<text transform="translate(${cx} ${cy}) rotate(${r1(a)}) translate(0 ${-R})" text-anchor="middle" dominant-baseline="middle" font-size="${fs}" font-weight="700" fill="${fill}" font-family="'IBM Plex Mono',monospace">${t}</text>`;
  }).join('');
}

function ticks(cx, cy, R1, R2, n, stroke = '#1f2326', w = 1.2, skip = 0) {
  let s = '';
  for (let i = 0; i < n; i++) {
    if (skip && i % skip === 0) continue;
    const a = rad((i * 360) / n);
    s += `<line x1="${r1(cx + Math.sin(a) * R1)}" y1="${r1(cy - Math.cos(a) * R1)}" x2="${r1(cx + Math.sin(a) * R2)}" y2="${r1(cy - Math.cos(a) * R2)}" stroke="${stroke}" stroke-width="${w}"/>`;
  }
  return s;
}

function sideSVG(game, eye) {
  const k = eye === 'OD' ? -1 : 1;
  const L = game.phoro[eye];
  const P = game.phoro;
  const pdOff = (P.pd - 62) * 1.6;
  const ax = 500 + k * (192 + pdOff), ay = 288; // 窺孔中心
  const occ = P.occ[eye];
  const active = game.activeEye === eye;
  const jcc = P.jcc;
  const jccOn = jcc.mode !== 'off' && active;
  let out = '';

  // 腔體(圓形外殼)
  out += `<g clip-path="url(#pf-body)"><circle cx="${500 + k * 292}" cy="400" r="248" fill="url(#pf-cell)" stroke="#9d978a" stroke-width="3"/><circle cx="${500 + k * 292}" cy="400" r="226" fill="none" stroke="#fff" stroke-opacity=".35" stroke-width="2"/></g>`;

  /* --- 窺孔 --- */
  const clipId = `pf-ap-${eye}`;
  out += `<g class="pf-ap" data-k="occ" data-e="${eye}" style="cursor:pointer">
    <circle cx="${ax}" cy="${ay}" r="96" fill="url(#pf-chrome)" stroke="#555c60" stroke-width="2" filter="url(#pf-sh)"/>
    <circle cx="${ax}" cy="${ay}" r="84" fill="#222a30" stroke="#0b0d0f" stroke-width="3"/>
    <circle cx="${ax}" cy="${ay}" r="74" fill="url(#pf-glass)"/>
    <clipPath id="${clipId}"><circle cx="${ax}" cy="${ay}" r="74"/></clipPath>
    <g clip-path="url(#${clipId})">`;
  if (occ) out += `<circle cx="${ax}" cy="${ay}" r="76" fill="#050607"/><text x="${ax}" y="${ay + 6}" text-anchor="middle" font-size="17" fill="#6b7479" font-family="sans-serif">遮蓋中</text>`;
  else {
    out += `<ellipse cx="${ax - 18}" cy="${ay - 24}" rx="38" ry="17" fill="#fff" opacity=".14" transform="rotate(-28 ${ax - 18} ${ay - 24})"/>`;
    if (L.aux === 'PH') out += `<circle cx="${ax}" cy="${ay}" r="76" fill="#07080a"/><circle cx="${ax}" cy="${ay}" r="4.2" fill="#c9dbe2"/>`;
    if (L.aux === 'RG') out += `<path d="M ${ax} ${ay - 76} A 76 76 0 0 0 ${ax} ${ay + 76} Z" fill="#c8372d" opacity=".72"/><path d="M ${ax} ${ay - 76} A 76 76 0 0 1 ${ax} ${ay + 76} Z" fill="#1f9a55" opacity=".72"/>`;
    if (L.aux === 'R') out += `<circle cx="${ax}" cy="${ay}" r="76" fill="#c8372d" opacity=".6"/>`;
    if (L.aux === 'P') out += `<circle cx="${ax}" cy="${ay}" r="76" fill="#6c7a85" opacity=".5"/>`;
    if (PRISM_AUX.includes(L.aux)) out += `<path d="M ${ax - 76} ${ay + 20} L ${ax + 76} ${ay - 30} L ${ax + 76} ${ay + 76} L ${ax - 76} ${ay + 76} Z" fill="#9fb6c2" opacity=".35"/>`;
  }
  out += `</g>`;
  if (!P.aperture) out += `<g><circle cx="${ax}" cy="${ay}" r="90" fill="#8d8a80" stroke="#5c594f" stroke-width="3"/><text x="${ax}" y="${ay + 5}" text-anchor="middle" font-size="16" fill="#e9e6dc" font-family="sans-serif">窺孔蓋</text></g>`;
  out += `<text x="${ax}" y="${ay - 101}" text-anchor="middle" font-size="12" font-weight="700" fill="#4d493f" font-family="sans-serif">${eye === 'OD' ? '右眼 OD' : '左眼 OS'}${active ? ' ●' : ''}</text>`;
  out += `</g>`;

  /* --- 球面顯示窗 --- */
  const wx = 500 + k * 192, wy = 150;
  out += `<g filter="url(#pf-sh2)"><rect x="${wx - 62}" y="${wy - 17}" width="124" height="34" rx="6" fill="url(#pf-win)" stroke="#6b675c" stroke-width="2"/></g>
    <text x="${wx}" y="${wy + 7}" text-anchor="middle" font-size="22" font-weight="700" fill="#1a1d1f" font-family="'IBM Plex Mono',monospace">${fmtSph(L.s).replace('PL', ' 0.00')}</text>
    <text x="${wx}" y="${wy - 24}" text-anchor="middle" font-size="12" fill="#5b574c" font-family="sans-serif">SPH 球面 D</text>`;

  /* --- 輔助鏡轉盤(外上方大旋鈕) --- */
  const auxIdx = Math.max(0, AUX_POS.indexOf(L.aux));
  const axx = 500 + k * 392, axy = 236;
  out += knob({
    id: 'aux', e: eye, cx: axx, cy: axy, r: 74, rot: -auxIdx * 45, label: '輔助鏡',
    inner: `<circle cx="${axx}" cy="${axy}" r="58" fill="url(#pf-chrome)" opacity=".85"/>${ringLabels(axx, axy, 44, AUX_POS, 17)}`,
  });

  /* --- 強球面旋鈕(±3.00D) --- */
  out += knob({
    id: 'coarse', e: eye, cx: 500 + k * 288, cy: 112, r: 40, rot: (L.s / 3) * 30, label: '強球 ±3',
    inner: `<circle cx="${500 + k * 288}" cy="112" r="24" fill="url(#pf-chrome)" opacity=".8"/>${ringLabels(500 + k * 288, 112, 24, ['0', '3', '6', '9', '12', '15'], 11)}`,
  });

  /* --- 弱球面轉盤(每格 0.25D,數字 = 1D) --- */
  const fwx = 500 + k * 352, fwy = 470;
  const nums = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '10', '11'];
  out += knob({
    id: 'fine', e: eye, cx: fwx, cy: fwy, r: 94, rot: -(L.s * 30), label: '弱球 0.25D/格',
    inner: `<circle cx="${fwx}" cy="${fwy}" r="70" fill="url(#pf-chrome)" opacity=".92" stroke="#6a7176"/>${ticks(fwx, fwy, 60, 70, 48, '#202427', 1.1)}${ringLabels(fwx, fwy, 50, nums, 15)}<circle cx="${fwx}" cy="${fwy}" r="26" fill="#3b4146"/><circle cx="${fwx}" cy="${fwy}" r="22" fill="url(#pf-chrome)"/>`,
  });

  /* --- 散光軸環(黑色刻度環,圓心是散光鏡窗口) --- */
  const rx0 = 500 + k * 170, ry0 = 478;
  const axisLabels = Array.from({ length: 12 }, (_, i) => String(i * 15));
  out += `<g class="pf-axring" data-k="axisring" data-e="${eye}" style="cursor:grab">
    <circle cx="${rx0}" cy="${ry0}" r="72" fill="#14171a" stroke="#868d91" stroke-width="3" filter="url(#pf-sh)"/>
    <g transform="rotate(${r1(-L.a * 2)} ${rx0} ${ry0})">
      ${ticks(rx0, ry0, 56, 66, 72, '#e9ecef', 1.2)}${ringLabels(rx0, ry0, 44, axisLabels, 11, '#f1f3f4')}
    </g>
    <circle cx="${rx0}" cy="${ry0}" r="26" fill="url(#pf-glass)" stroke="#cdd2d5" stroke-width="2"/>
    <text x="${rx0}" y="${ry0 + 5}" text-anchor="middle" font-size="14" font-weight="700" fill="#8ff0cf" font-family="'IBM Plex Mono',monospace">${String(L.a).padStart(3, '0')}</text>
    <polygon points="${rx0 - 7},${ry0 - 80} ${rx0 + 7},${ry0 - 80} ${rx0},${ry0 - 68}" fill="#c1382b" stroke="#fff" stroke-width=".8"/>
    <circle cx="${rx0}" cy="${ry0}" r="82" fill="transparent"/>
  </g>`;

  /* --- 散光度旋鈕與散光軸旋鈕(下方) --- */
  const cpx = 500 + k * 145, cpy = 606;
  out += knob({
    id: 'cyl', e: eye, cx: cpx, cy: cpy, r: 44, rot: (-L.c / 0.25) * 18, label: `CYL ${L.c === 0 ? '0.00' : L.c.toFixed(2)}`,
    inner: `<circle cx="${cpx}" cy="${cpy}" r="26" fill="url(#pf-chrome)" opacity=".85"/>`,
  });
  const apx = 500 + k * 268, apy = 612;
  out += knob({
    id: 'axis', e: eye, cx: apx, cy: apy, r: 36, rot: L.a * 2, label: 'AXIS 軸',
    inner: `<circle cx="${apx}" cy="${apy}" r="21" fill="url(#pf-chrome)" opacity=".85"/>`,
  });

  /* --- 交叉圓柱鏡 JCC(窗口下方內側) --- */
  const jx = 500 + k * 88, jy = 398;
  const jl = jccOn ? jccRedAxis(jcc.mode, jcc.pos, L.a) : null;
  const jrot = jcc.mode === 'off' || !active ? -35 : jcc.mode === 'A' ? 0 : 45;
  out += `<g class="pf-knob" data-k="jcc" data-e="${eye}" style="cursor:pointer">
    <circle cx="${jx}" cy="${jy}" r="30" fill="url(#pf-black)" stroke="#8a9196" stroke-width="3" filter="url(#pf-sh)"/>
    <g transform="rotate(${jrot} ${jx} ${jy})"><rect x="${jx - 4}" y="${jy - 38}" width="8" height="40" rx="3" fill="url(#pf-chrome)" stroke="#555"/><circle cx="${jx}" cy="${jy - 40}" r="9" fill="url(#pf-chromeR)" stroke="#555"/></g>
    <circle cx="${jx}" cy="${jy}" r="11" fill="#d7dbdd"/>
    <g data-k="jccflip" data-e="${eye}" style="cursor:pointer" transform="translate(${jx} ${jy + 46})"><circle r="17" fill="${jccOn ? '#fff' : '#cfcabc'}" stroke="#444" stroke-width="1.5"/>${jccOn ? [0, 180].map((d) => `<circle cx="${r1(Math.cos(rad(jl + d)) * 10)}" cy="${r1(-Math.sin(rad(jl + d)) * 10)}" r="4" fill="#d13a2c"/>`).join('') + [90, 270].map((d) => `<circle cx="${r1(Math.cos(rad(jl + d)) * 10)}" cy="${r1(-Math.sin(rad(jl + d)) * 10)}" r="4" fill="#fff" stroke="#333" stroke-width="1"/>`).join('') : ''}<circle r="26" fill="transparent"/></g>
    <text x="${jx}" y="${jy - 46}" text-anchor="middle" font-size="12" fill="#5b574c" font-family="sans-serif">JCC ${jccOn ? jcc.mode + '・鏡片' + jcc.pos : '關'}</text>
    <circle cx="${jx}" cy="${jy}" r="42" fill="transparent"/>
  </g>`;
  return out;
}

export function faceSVG(game, { interactive = true } = {}) {
  const P = game.phoro;
  const bubbleX = 500 + Math.max(-1, Math.min(1, P.level / 5)) * 44;
  const auxOK = true;
  void auxOK;
  let s = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${VB_W} ${VB_H}" width="${VB_W}" height="${VB_H}" font-family="'Noto Sans TC',sans-serif" ${interactive ? 'class="pf-svg"' : ''}>`;
  s += defs();
  // 主體底板
  s += `<rect x="14" y="36" width="972" height="634" rx="70" fill="url(#pf-paint)" stroke="#a49e90" stroke-width="3"/>`;
  // 上方橫樑 + 中央立柱
  s += `<rect x="170" y="8" width="660" height="148" rx="34" fill="url(#pf-paint2)" stroke="#9d978a" stroke-width="3" filter="url(#pf-sh)"/>`;
  s += `<rect x="404" y="8" width="192" height="330" rx="30" fill="url(#pf-paint2)" stroke="#9d978a" stroke-width="3" filter="url(#pf-sh)"/>`;
  s += sideSVG(game, 'OD') + sideSVG(game, 'OS');

  // 中央:菱形孔 / 瞳距窗 / 氣泡水平儀 / 頂點距離
  s += `<polygon points="500,52 536,90 500,128 464,90" fill="#0c0e10" stroke="#7a8084" stroke-width="4"/>`;
  s += `<polygon points="500,64 524,90 500,116 476,90" fill="#1f272d"/>`;
  // 瞳距窗
  const pdx = 500 + ((P.pd - 62) / 14) * 46;
  s += `<g filter="url(#pf-sh2)"><rect x="438" y="168" width="124" height="38" rx="5" fill="url(#pf-win)" stroke="#6b675c" stroke-width="2"/></g>`;
  for (let v = 50; v <= 75; v += 1) {
    const x = 500 + ((v - 62) / 14) * 46;
    s += `<line x1="${r1(x)}" y1="${v % 5 === 0 ? 172 : 177}" x2="${r1(x)}" y2="184" stroke="#2a2c2e" stroke-width="${v % 5 === 0 ? 1.8 : 1}"/>`;
  }
  s += `<text x="500" y="199" text-anchor="middle" font-size="12" font-weight="700" fill="#1a1d1f" font-family="'IBM Plex Mono',monospace">PD ${P.pd}</text>`;
  s += `<polygon points="${r1(Math.max(446, Math.min(554, pdx)))},170 ${r1(Math.max(446, Math.min(554, pdx))) - 5},164 ${r1(Math.max(446, Math.min(554, pdx))) + 5},164" fill="#c1382b"/>`;
  // 氣泡水平儀
  s += `<g filter="url(#pf-sh2)"><rect x="440" y="236" width="120" height="30" rx="15" fill="#c9e3d4" stroke="#657a6e" stroke-width="2.5"/></g>
    <line x1="494" y1="238" x2="494" y2="264" stroke="#41584b" stroke-width="1.4"/><line x1="506" y1="238" x2="506" y2="264" stroke="#41584b" stroke-width="1.4"/>
    <ellipse cx="${r1(bubbleX)}" cy="251" rx="12" ry="9" fill="#f3faf6" stroke="#41584b" stroke-width="1.5" opacity=".95"/>
    <text x="500" y="282" text-anchor="middle" font-size="12" fill="#5b574c" font-family="sans-serif">氣泡水平儀 ${P.level.toFixed(1)}°</text>`;
  // 鎖定燈
  s += `<circle cx="470" cy="312" r="7" fill="${P.locked ? '#2bc46c' : '#c9382c'}" stroke="#333" stroke-width="1.5"/><text x="500" y="316" font-size="11" fill="#5b574c" font-family="sans-serif">${P.locked ? '已鎖緊' : '未鎖'}</text>`;
  // 頂點距離旋鈕(裝飾)
  s += knob({ id: 'vertex', e: 'C', cx: 500, cy: 372, r: 26, rot: 20, label: '頂點距離 12', knurl: true, inner: '' });
  // 瞳距旋鈕(左上外側)與水平旋鈕(右上外側)
  s += knob({ id: 'pd', e: 'C', cx: 78, cy: 78, r: 30, rot: P.pd * 14, label: '瞳距旋鈕', side: 'r', inner: '' });
  s += knob({ id: 'level', e: 'C', cx: 922, cy: 78, r: 30, rot: P.level * 40, label: '水平旋鈕', side: 'l', inner: '' });
  // 遠近瞳距撥桿(裝飾,中央立柱)
  s += `<g data-k="nearfar" data-e="C" style="cursor:pointer"><rect x="522" y="326" width="58" height="12" rx="6" fill="url(#pf-chrome)" stroke="#555" stroke-width="1.5"/><circle cx="${game.nearPD ? 570 : 532}" cy="332" r="10" fill="url(#pf-chromeR)" stroke="#555"/><text x="551" y="354" text-anchor="middle" font-size="11" fill="#5b574c" font-family="sans-serif">${game.nearPD ? '近用 PD' : '遠用 PD'}</text><rect x="516" y="318" width="70" height="40" fill="transparent"/></g>`;
  s += `</svg>`;
  return s;
}
