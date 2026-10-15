// 操作回饋:旋鈕「喀」一格的聲音 + 手機震動(可在選單關閉)
const KEY = 'optom3d-sound-v1';
let ac = null;
let last = 0;

const VKEY = 'optom3d-voice-v1';
const get = (k) => { try { return localStorage.getItem(k) !== 'off'; } catch { return true; } };
const set = (k, on) => { try { localStorage.setItem(k, on ? 'on' : 'off'); } catch { /* ignore */ } };
export const fx = {
  get sound() { return get(KEY); },
  set sound(on) { set(KEY, on); },
  get voice() { return get(VKEY) && 'speechSynthesis' in window; },
  set voice(on) { set(VKEY, on); if (!on) try { speechSynthesis.cancel(); } catch { /* ignore */ } },
};

// 受測者「說出」回答(瀏覽器內建的中文語音;沒有中文語音就不唸)
let zhVoice;
function pickVoice() {
  const vs = speechSynthesis.getVoices();
  zhVoice = vs.find((v) => /zh[-_]TW/i.test(v.lang)) || vs.find((v) => /zh[-_](HK|CN)|cmn/i.test(v.lang)) || null;
}
export function speak(text) {
  if (!fx.voice) return;
  try {
    if (zhVoice === undefined) { pickVoice(); speechSynthesis.onvoiceschanged = pickVoice; }
    if (!zhVoice) return;
    const t = text.replace('(有點不確定)', '').replace(/[「」()()…]/g, ' ').trim();
    if (!t) return;
    speechSynthesis.cancel();
    const u = new SpeechSynthesisUtterance(t);
    u.voice = zhVoice; u.lang = zhVoice.lang; u.rate = 1.05; u.pitch = 1;
    speechSynthesis.speak(u);
  } catch { /* ignore */ }
}

// 一格的機械聲:很短的高頻「喀」
export function tick(strong = false) {
  const now = performance.now();
  if (now - last < 28) return; // 連續轉動時不要疊成雜音
  last = now;
  if (navigator.vibrate) { try { navigator.vibrate(strong ? 14 : 6); } catch { /* ignore */ } }
  if (!fx.sound) return;
  try {
    ac ??= new (window.AudioContext || window.webkitAudioContext)();
    if (ac.state === 'suspended') ac.resume();
    const t = ac.currentTime;
    const o = ac.createOscillator(), g = ac.createGain(), f = ac.createBiquadFilter();
    o.type = 'square';
    o.frequency.setValueAtTime(strong ? 1500 : 2300, t);
    o.frequency.exponentialRampToValueAtTime(strong ? 600 : 900, t + 0.025);
    f.type = 'bandpass'; f.frequency.value = strong ? 1400 : 2600; f.Q.value = 1.2;
    g.gain.setValueAtTime(strong ? 0.07 : 0.045, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + (strong ? 0.06 : 0.035));
    o.connect(f).connect(g).connect(ac.destination);
    o.start(t); o.stop(t + 0.07);
  } catch { /* 沒有音效也沒關係 */ }
}
