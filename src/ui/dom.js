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

export function btn(label, onClick, { cls = '', title = '', disabled = false, aria } = {}) {
  return h('button', { type: 'button', class: `b ${cls}`, onclick: onClick, title, disabled, 'aria-label': aria || undefined }, label);
}

export function segmented(options, value, onPick, cls = '') {
  return h('div', { class: `seg ${cls}`, role: 'group' },
    options.map(([val, label, extra]) =>
      h('button', { type: 'button', class: `s${val === value ? ' on' : ''}${extra?.cls ? ' ' + extra.cls : ''}`, 'aria-pressed': val === value ? 'true' : 'false', disabled: extra?.disabled, onclick: () => onPick(val) }, label)));
}

export const fmt2 = (x) => (Math.round(x * 100) / 100).toFixed(2);
export const sgn = (x) => (x > 0 ? '+' : x < 0 ? '−' : '');
