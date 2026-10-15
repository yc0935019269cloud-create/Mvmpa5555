// 極簡 DOM 建構工具
export function h(tag, props = {}, ...kids) {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(props || {})) {
    if (v === undefined || v === null || v === false) continue;
    if (k === 'class') el.className = v;
    else if (k === 'style' && typeof v === 'object') Object.assign(el.style, v);
    else if (k.startsWith('on') && typeof v === 'function') el.addEventListener(k.slice(2).toLowerCase(), v);
    else if (k === 'html') el.innerHTML = v;
    else if (k === 'disabled') { if (v) el.disabled = true; }
    else el.setAttribute(k, v === true ? '' : v);
  }
  for (const kid of kids.flat(Infinity)) {
    if (kid === null || kid === undefined || kid === false) continue;
    el.append(kid.nodeType ? kid : document.createTextNode(String(kid)));
  }
  return el;
}

export const clear = (el) => { while (el.firstChild) el.removeChild(el.firstChild); return el; };

export function btn(label, onClick, { cls = '', title = '', disabled = false, aria, repeat = false } = {}) {
  const el = h('button', { type: 'button', class: `b ${cls}`, onclick: onClick, title, disabled, 'aria-label': aria || undefined }, label);
  if (repeat) holdRepeat(el, onClick);
  return el;
}

// 長按連續觸發(只能用在「不會被重繪替換」的按鈕上)
export function holdRepeat(el, fn, { delay = 380, every = 110 } = {}) {
  let t = null, iv = null, held = false;
  const stop = () => { clearTimeout(t); clearInterval(iv); t = iv = null; };
  el.addEventListener('pointerdown', () => {
    held = false;
    stop();
    t = setTimeout(() => { held = true; fn(); iv = setInterval(fn, every); }, delay);
  });
  for (const ev of ['pointerup', 'pointerleave', 'pointercancel', 'blur']) el.addEventListener(ev, stop);
  // 長按觸發過就吃掉隨後的 click,避免多加一次
  el.addEventListener('click', (e) => { if (held) { held = false; e.stopImmediatePropagation(); e.preventDefault(); } }, true);
  el.addEventListener('contextmenu', (e) => e.preventDefault());
  return stop;
}

export function segmented(options, value, onPick, cls = '') {
  return h('div', { class: `seg ${cls}`, role: 'group' },
    options.map(([val, label, extra]) =>
      h('button', { type: 'button', class: `s${val === value ? ' on' : ''}${extra?.cls ? ' ' + extra.cls : ''}`, 'aria-pressed': val === value ? 'true' : 'false', disabled: extra?.disabled, onclick: () => onPick(val) }, label)));
}

export const fmt2 = (x) => (Math.round(x * 100) / 100).toFixed(2);
export const sgn = (x) => (x > 0 ? '+' : x < 0 ? '−' : '');
