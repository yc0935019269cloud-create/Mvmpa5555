/* ACP-8 control state machine. Source: supplied manual, printed pp.15–17. */
(()=>{'use strict';
const CHARTS=globalThis.ACP8_CHARTS;
const clone=x=>JSON.parse(JSON.stringify(x));
const get=id=>CHARTS.find(c=>c.id===id);
class ACP8 {
 constructor(saved){this.power=true;this.lamp=true;this.chart=1;this.mask='open';this.row=0;this.col=0;this.rg=false;this.program=0;this.step=-1;this.programs=[[],[]];this.editing=false;this.armed=false;this.lastActivity=Date.now();this.message='投影機開啟，起始圖卡 K。';if(saved?.version===1&&this.validPrograms(saved.programs))this.programs=clone(saved.programs);}
 validPrograms(p){return Array.isArray(p)&&p.length===2&&p.every(a=>Array.isArray(a)&&a.length<=30&&a.every(v=>v&&get(v.chart)&&['open','horizontal','vertical','single'].includes(v.mask)&&Number.isInteger(v.row)&&v.row>=0&&v.row<=2&&Number.isInteger(v.col)&&v.col>=0&&v.col<5&&typeof v.rg==='boolean'));}
 snapshot(){return {chart:this.chart,mask:this.mask,row:this.row,col:this.col,rg:this.rg};}
 restore(s){Object.assign(this,clone(s));this.normalize();}
 normalize(){let c=get(this.chart);if(!c.family){this.mask='open';this.rg=false;}this.row=Math.max(0,Math.min(Math.max(0,c.rows.length-1),this.row));this.col=Math.max(0,Math.min((c.counts[this.row]||1)-1,this.col));}
 select(id){if(!get(id))return;this.chart=id;this.mask='open';this.row=0;this.col=0;this.rg=false;this.message=get(id).name;}
 chooseMask(mode){const c=get(this.chart);if(!c.family){this.message='此專用圖卡不使用視力表遮罩。';return;}this.mask=mode;this.row=0;this.col=0;this.normalize();this.message={open:'開放全圖',horizontal:'水平單列遮罩',vertical:'垂直單行遮罩',single:'單一視標遮罩'}[mode];}
 adjacent(direction){const c=get(this.chart),family=CHARTS.filter(v=>v.family===c.family);const n=family.indexOf(c)+direction;if(n<0||n>=family.length){this.message='已到此類型圖卡的邊界。';return false;}this.chart=family[n].id;return true;}
 move(dx,dy){if(this.mask==='open'){this.message='先選擇水平、垂直或單字遮罩，再移動遮罩。';return;}let c=get(this.chart);if(dy&&this.mask!=='vertical'){let n=this.row+dy;if(n<0||n>=c.rows.length){if(!this.adjacent(dy))return;c=get(this.chart);this.row=dy>0?0:c.rows.length-1;}else this.row=n;}
 if(dx&&this.mask!=='horizontal'){let n=this.col+dx;if(n<0||n>=(c.counts[this.row]||1)){if(!this.adjacent(dx))return;c=get(this.chart);this.col=dx>0?0:(c.counts[this.row]||1)-1;}else this.col=n;}this.normalize();this.message='遮罩移動至 '+get(this.chart).name+' · '+(this.row+1)+' 列／'+(this.col+1)+' 字';}
 programMove(d){if(this.armed){if(d>0){this.armed=false;this.editing=true;this.programs[this.program]=[];this.message='P'+(this.program+1)+' 編程：選圖／遮罩後按 PROGRAM ▲ 寫入。';}return;}
 if(this.editing){if(d<0){this.message='編程請按 ▲ 寫入，RESET 結束。';return;}const p=this.programs[this.program];if(p.length<30)p.push(this.snapshot());this.message='已寫入 P'+(this.program+1)+' 第 '+p.length+' 步。';if(p.length===30){this.editing=false;this.step=-1;this.message+=' 30 步編程完成。';}return;}
 const p=this.programs[this.program];if(!p.length){this.message='P'+(this.program+1)+' 尚未設定，請先編寫程序。';return;}this.step=this.step<0?(d>0?0:p.length-1):(this.step+d+p.length)%p.length;this.restore(p[this.step]);this.message='P'+(this.program+1)+' · 第 '+(this.step+1)+'／'+p.length+' 步';}
 beginProgramming(){this.power=true;this.lamp=true;this.armed=true;this.editing=false;this.chart=1;this.mask='open';this.rg=false;this.step=-1;this.message='編程待命：以 P1/P2 選程序，再按 PROGRAM ▲ 開始（會取代該程序）。';}
 mainPower(resetHeld=false){if(this.power){this.power=false;this.lamp=false;this.armed=false;this.editing=false;this.message='投影機主電源已關閉。';}else if(resetHeld)this.beginProgramming();else{this.power=true;this.lamp=true;this.chart=1;this.mask='open';this.rg=false;this.step=-1;this.message='主電源開啟，起始圖卡 K。';}this.lastActivity=Date.now();}
 press(key){if(!this.power){this.message='先開啟投影機主電源。';return;}this.lastActivity=Date.now();if(key==='light'){this.lamp=!this.lamp;this.message=this.lamp?'投影燈開啟。':'投影燈關閉；操作鍵可喚醒。';return;}this.lamp=true;
 if(key.startsWith('chart:'))this.select(+key.split(':')[1]);
 else if(['open','horizontal','vertical','single'].includes(key))this.chooseMask(key);
 else if(key==='left')this.move(-1,0);else if(key==='right')this.move(1,0);else if(key==='up')this.move(0,-1);else if(key==='down')this.move(0,1);
 else if(key==='rg'){if(get(this.chart).family){this.rg=!this.rg;this.message=this.rg?'紅綠濾片加入。':'紅綠濾片移開。';}else this.message='這張專用圖卡不支援額外的 R/G 濾片。';}
 else if(key==='program-next')this.programMove(1);else if(key==='program-prev')this.programMove(-1);
 else if(key==='program'){this.program=1-this.program;this.step=-1;this.message='已選 P'+(this.program+1)+(this.armed?'；按 ▲ 開始編程。':'；按 PROGRAM ▲ 從第一步執行。');}
 else if(key==='reset'){if(this.editing||this.armed){this.editing=false;this.armed=false;this.step=-1;this.message='程序編寫結束，已儲存在此瀏覽器。';}else if(this.programs[this.program].length){this.step=0;this.restore(this.programs[this.program][0]);this.message='RESET：P'+(this.program+1)+' 第 1 步。';}else this.message='所選程序尚未設定，RESET 沒有程序第一步可呼叫。';}this.normalize();}
 idle(now=Date.now()){if(this.power&&this.lamp&&now-this.lastActivity>=600000){this.lamp=false;this.message='閒置 10 分鐘，投影燈自動關閉。';return true;}return false;}
}
globalThis.ACP8=ACP8;
// Every chart uses the publisher's original vector art; viewing a polar chart alone shows its combined projection.
function plateSVG(model){const c=get(model.chart),[x,y,w,h]=c.box;let mask='';const rows=c.rows.length;
 // Aperture boundaries follow optotype rows, avoiding a CSS blur or replacement chart.
 if(model.mask!=='open'&&rows){const count=c.counts[model.row],top=22,avail=220,rh=avail/rows;let rx=0,ry=0,rw=330,rhh=270;
 if(model.mask==='horizontal'||model.mask==='single'){ry=top+model.row*rh; rhh=rh;}
 if(model.mask==='vertical'||model.mask==='single'){const left=17,total=278;rx=left+model.col*total/count;rw=total/count;}
 mask=`<defs><mask id="mask-aperture"><rect width="330" height="270" fill="black"/><rect x="${rx}" y="${ry}" width="${rw}" height="${rhh}" fill="white"/></mask></defs>`;}
 let rg=model.rg?'<g style="mix-blend-mode:multiply"><rect width="165" height="270" fill="#ed172c"/><rect x="165" width="165" height="270" fill="#00a657"/></g>':'';
 const circle=c.round?'<defs><clipPath id="round-plate"><circle cx="165" cy="135" r="134"/></clipPath></defs>':'';
 return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 330 270" role="img" aria-label="${model.lamp&&model.power?c.name:'投影燈已熄滅'}"><rect width="330" height="270" fill="${model.lamp&&model.power?((c.round||model.mask!=='open')?'#000':'#fff'):'#121619'}"/>${model.lamp&&model.power?mask+circle+`<g ${mask?'mask="url(#mask-aperture)"':''} ${c.round?'clip-path="url(#round-plate)"':''}><svg width="330" height="270" viewBox="${x} ${y} ${w} ${h}" preserveAspectRatio="xMidYMid meet"><image href="assets/acp8-chart-source.svg" width="595.276" height="841.89"/></svg>${rg}</g>`:''}</svg>`;
}
globalThis.ACP8Plate=plateSVG;
if(typeof document==='undefined')return;
const $=id=>document.getElementById(id);let stored;try{stored=JSON.parse(localStorage.getItem('clinic.acp8.programs.v1'));}catch{}const model=new ACP8(stored);globalThis.remoteModel=model;
const chartKeys=[1,2,3,5,6,7,4,8,27,28,29,30,23,24,25,26,19,20,21,22,9,14,11,10,12,13,15,16,'program-prev','program-next',17,18,'open','reset','up','light','horizontal','left',null,'right','single','rg','down','program'];
const controlNames={'program-prev':'PROGRAM ▼ · 程序後退','program-next':'PROGRAM ▲ · 程序前進／寫入',open:'開放遮罩',reset:'RESET · 程序第一步',up:'遮罩向上',light:'LIGHT · 投影燈',horizontal:'水平遮罩',left:'遮罩向左',right:'遮罩向右',single:'單字遮罩',rg:'R/G · 紅綠濾片',down:'遮罩向下',program:'切換 P1／P2'};
// Physical remote has vertical-mask selector at row 9 col 1, horizontal at row 10 col 1, single at row 11 col 1.
chartKeys[32]='vertical';controlNames.vertical='垂直遮罩（I）';
// Open mask is obtained by reselecting a chart. The pictured I key is not an all-open key.
let svg=`<svg class="remote-photo" xmlns="http://www.w3.org/2000/svg" viewBox="88 78 335 1078"><defs><clipPath id="remote-silhouette"><path d="M120 92 Q100 94 98 116 L94 1080 Q92 1147 150 1153 L353 1153 Q412 1148 414 1090 L403 115 Q402 84 374 84 Z"/></clipPath></defs><image href="assets/acp8-photo.png" width="562" height="1156" clip-path="url(#remote-silhouette)"/>`;
const ys=[278,334,392,449,508,567,626,685,746,809,871];
chartKeys.forEach((key,i)=>{if(!key)return;const row=Math.floor(i/4),col=i%4,cx=[162,224,286,347][col],cy=ys[row];const label=typeof key==='number'?get(key).name:controlNames[key];const action=typeof key==='number'?'chart:'+key:key;svg+=`<rect tabindex="0" role="button" aria-label="${label}" data-key="${action}" class="remote-key" x="${cx-23}" y="${cy-17}" width="46" height="30" rx="3"><title>${label}</title></rect>`;});
svg+='</svg>';$('remote-host').innerHTML=svg;
let resetHeld=false;
function save(){try{localStorage.setItem('clinic.acp8.programs.v1',JSON.stringify({version:1,programs:model.programs}));}catch{model.message+='（此瀏覽器無法儲存，請匯出程序）';}}
function render(){ $('projector-screen').innerHTML=plateSVG(model);$('chart-name').textContent=get(model.chart).name;$('remote-message').textContent=model.message;$('remote-state').textContent=`電源 ${model.power?'ON':'OFF'} · 燈 ${model.lamp?'ON':'OFF'} · P${model.program+1} ${model.step<0?'待開始':`${model.step+1}/${model.programs[model.program].length}`} · ${model.editing?'編程中':model.armed?'編程待命':'操作中'}\n遮罩 ${model.mask} · R/G ${model.rg?'ON':'OFF'}`;$('main-power').textContent='投影機主電源 '+(model.power?'ON':'OFF');$('main-power').setAttribute('aria-pressed',model.power);save();}
let audio;function beep(n=1){try{audio??=new AudioContext();audio.resume();for(let i=0;i<n;i++){const osc=audio.createOscillator(),gain=audio.createGain(),t=audio.currentTime+i*.15;osc.frequency.value=1800;gain.gain.setValueAtTime(.025,t);gain.gain.exponentialRampToValueAtTime(.001,t+.07);osc.connect(gain);gain.connect(audio.destination);osc.start(t);osc.stop(t+.08);}}catch{}}
function press(key){const armed=model.armed,editing=model.editing,on=model.power;model.press(key);if(on)beep(key==='program'?model.program+1:armed&&key==='program-next'?3:editing&&key==='program-next'?4:1);render();}
$('remote-host').addEventListener('click',e=>{const k=e.target.closest('[data-key]');if(k)press(k.dataset.key);});
$('remote-host').addEventListener('keydown',e=>{const k=e.target.closest('[data-key]');if(k&&['Enter',' '].includes(e.key)){e.preventDefault();press(k.dataset.key);}});
$('remote-host').addEventListener('pointerover',e=>{const k=e.target.closest('[data-key]');if(k)$('key-caption').textContent=k.getAttribute('aria-label');});
$('remote-host').addEventListener('pointerdown',e=>{if(e.target.closest('[data-key="reset"]'))resetHeld=true;});window.addEventListener('pointerup',()=>resetHeld=false);
$('main-power').onclick=()=>{model.mainPower(resetHeld);render();};$('edit-program').onclick=()=>{model.beginProgramming();render();};
$('key-hints').onchange=e=>document.querySelectorAll('.remote-key').forEach(k=>k.style.stroke=e.target.checked?'#e7cb7388':'');
$('show-program').onclick=()=>{$('program-steps').replaceChildren();model.programs.forEach((p,i)=>{const t=document.createElement('h3');t.textContent='P'+(i+1)+' · '+p.length+' 步';$('program-steps').append(t);p.forEach((s,j)=>{const el=document.createElement('div');el.className='program-step';el.textContent=`${j+1}. ${get(s.chart).name} · ${s.mask} · ${s.row+1}/${s.col+1} · R/G ${s.rg?'ON':'OFF'}`;$('program-steps').append(el);});});};
$('export-program').onclick=()=>{const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify({version:1,programs:model.programs},null,2)],{type:'application/json'}));a.download='ACP8-P1-P2.json';a.click();setTimeout(()=>URL.revokeObjectURL(a.href),1000);};
$('import-program').onchange=async e=>{try{const data=JSON.parse(await e.target.files[0].text());if(data.version!==1||!model.validPrograms(data.programs))throw Error('檔案格式或程序內容不正確');model.programs=clone(data.programs);model.step=-1;model.message='P1、P2 已匯入。';}catch(err){model.message='匯入失敗：'+err.message;}render();};
let full=false;function fullscreen(){full=!full;$('projection').classList.toggle('projection-full',full);$('exit-full').hidden=!full;} $('projection-enlarge').onclick=fullscreen;$('exit-full').onclick=fullscreen;document.addEventListener('keydown',e=>{if(e.key==='Escape'&&full)fullscreen();});
$('chart-list').innerHTML=CHARTS.map(c=>`<button data-chart="${c.id}">${c.id}. ${c.name}</button>`).join('');$('chart-list').onclick=e=>{const b=e.target.closest('[data-chart]');if(b)press('chart:'+b.dataset.chart);};
setInterval(()=>{if(model.idle())render();},1000);render();
})();
