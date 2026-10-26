// Power vectors / JCC / meridian and working-distance conventions are reused from this repo.
(()=>{'use strict';
const O=globalThis.ClinicLegacy, eyes=['OD','OS'];
const clamp=(x,a,b)=>Math.max(a,Math.min(b,x)), copy=x=>JSON.parse(JSON.stringify(x));
const q=x=>Math.round(x*4)/4, axis=a=>((Math.round(a)-1)%180+180)%180+1;
const key=r=>`${r.s}/${r.c}/${r.c?r.a:0}`;
const stages=[{id:'OD.ret',eye:'OD',phase:'ret',title:'右眼檢影'}, {id:'OS.ret',eye:'OS',phase:'ret',title:'左眼檢影'}, {id:'wd',phase:'wd',title:'扣除雙眼工作距離'}, {id:'va',phase:'va',title:'雙眼淨檢影 VA'}, ...eyes.flatMap(eye=>[{id:eye+'.mp1',eye,phase:'mp1',title:(eye==='OD'?'右眼':'左眼')+' · 第一次 MPMVA'},{id:eye+'.duo',eye,phase:'duo',title:(eye==='OD'?'右眼':'左眼')+' · 紅綠檢查'},{id:eye+'.jcc',eye,phase:'jcc',title:(eye==='OD'?'右眼':'左眼')+' · JCC'},{id:eye+'.mp2',eye,phase:'mp2',title:(eye==='OD'?'右眼':'左眼')+' · 第二次 MPMVA'}]),{id:'done',phase:'done',title:'完成與回饋'}];
function rng(seed){let a=seed>>>0;return()=>{a+=0x6D2B79F5;let t=a;t=Math.imul(t^t>>>15,t|1);t^=t+Math.imul(t^t>>>7,t|61);return((t^t>>>14)>>>0)/4294967296;};}
function randomCase(seed){const u=rng(seed),base=q(-.25-u()*6),c=-q(u()*2),a=axis(q(u()*180)/5*5);return {OD:{s:base,c,a:c?a:180},OS:{s:q(clamp(base+(u()-.5)*1.5,-9,3)),c:-q(u()*2),a:axis(Math.round((a+(u()-.5)*20)/5)*5)}};}
class Practice {
 constructor({seed=Date.now()%100000,truth,variability='realistic',wdCm=67}={}){this.seed=+seed;this.truth=copy(truth||randomCase(this.seed));for(const e of eyes){const r=this.truth[e];if(!r||!Number.isFinite(r.s)||!Number.isFinite(r.c)||!Number.isFinite(r.a)||r.s< -19||r.s>16||r.c>0||r.c< -6||r.a<1||r.a>180)throw Error('病例須使用有效的負柱面數值與 1–180° 軸位。');r.s=q(r.s);r.c=q(r.c);r.a=axis(r.a);}
 this.variability=variability;this.wdCm=+wdCm;this.wdApplied=false;this.stage=0;this.activeEye='OD';this.patient={seed:this.seed};this.mode='tutorial';this.god=false;this.nearPD=false;this.farPD=63;this.autoComp=true;this.room={dim:true};this.phoro={OD:{s:0,c:0,a:180,aux:'O'},OS:{s:0,c:0,a:180,aux:'O'},occ:{OD:false,OS:false},jcc:{mode:'off',pos:1},pd:63,level:0,vertex:12,height:.5,locked:true,aperture:true};
 this.compAcc={OD:0,OS:0};this.hist={};this.records={OD:{},OS:{}};this.offsets={};this.events=[];this.listeners=new Set();this.message='先對準右眼，旋轉光條並掃過兩條主子午線。';this.started=Date.now();this.finished=0;
 const u=rng(this.seed*7919+37);for(const e of eyes){this.hist[e]={scans:[],reads:0,jccA:0,jccP:0,jccN:0,duo:0,fogged:{mp1:false,mp2:false},entry:{},fogMax:{mp1:-99,mp2:-99},route:'APA',routeIndex:0};const r=this.truth[e],vary=variability!=='ideal'&&u()<.83;
 // A fixed measurement-stage bias models dry-retinoscopy accommodation / axis & power estimation.
 // It exists from case creation. Subjective truth never changes upon entering JCC.
 const s=vary?(u()<.72?-.25:-.5):0,dc=vary?(u()<.55?-.25:.25):0,da=vary?Math.round((u()<.5?-1:1)*(variability==='challenging'?10:5)):0;
 this.offsets[e]={s,c:dc,a:da};this.hist[e].retTarget={s:q(r.s+s),c:clamp(q(r.c+dc),-6,0),a:axis(r.a+da)};}
 this.enter();}
 get def(){return stages[this.stage];}get h(){return this.hist[this.activeEye];}get wdD(){return O.wdDiopter(this.wdCm);}get done(){return this.def.phase==='done';}
 on(fn){this.listeners.add(fn);return()=>this.listeners.delete(fn);}emit(){for(const f of this.listeners)f();}
 log(text){this.events.push({time:Date.now(),stage:this.def.id,text});if(this.events.length>350)this.events.shift();}
 say(text){this.message=text;this.log(text);this.emit();return text;}
 lens(e=this.activeEye){const {s,c,a}=this.phoro[e];return {s,c,a};}
 trueRx(e){return this.hist[e].retTarget;}
 effective(e=this.activeEye,lens=this.lens(e)){const add=this.phoro[e].aux==='+.12'?.12:0,s=lens.s+add,c=lens.c,d=(this.phoro.vertex-12)/1000;const f=x=>x/(1-d*x);return O.toVec({s:f(s),c:f(s+c)-f(s),a:lens.a});}
 retLens(e=this.activeEye){return O.fromVec(this.effective(e));}
 setActiveEye(e){if(!eyes.includes(e))return;this.activeEye=e;this.phoro.jcc.mode='off';this.emit();}
 setTestEye(e){this.activeEye=e;const other=e==='OD'?'OS':'OD';this.phoro.occ[e]=false;if(this.phoro[e].aux==='OC')this.phoro[e].aux='O';this.phoro.occ[other]=true;this.phoro[other].aux='OC';this.phoro.jcc.mode='off';this.emit();}
 setOcc(e,on){this.phoro.occ[e]=on;if(on)this.phoro[e].aux='OC';else if(this.phoro[e].aux==='OC')this.phoro[e].aux='O';this.emit();}
 setAperture(on){this.phoro.aperture=on;this.emit();}
 setAux(e,v){if(!O.AUX_POS.includes(v))return;this.phoro[e].aux=v;this.phoro.occ[e]=v==='OC';this.emit();}
 setPD(v){this.phoro.pd=clamp(Math.round(v),48,75);if(!this.nearPD)this.farPD=this.phoro.pd;this.emit();}
 setNearPD(on){if(on===this.nearPD)return;if(on){this.farPD=this.phoro.pd;this.phoro.pd=clamp(this.farPD-3,48,75);}else this.phoro.pd=this.farPD;this.nearPD=on;this.emit();}
 setLevel(v){this.phoro.level=clamp(Math.round(v*2)/2,-5,5);this.emit();}
 setVertex(v){this.phoro.vertex=clamp(Math.round(v),8,24);this.emit();}
 stepSph(e,d){this.phoro[e].s=clamp(q(this.phoro[e].s+d),-20,20);this.afterLens(e);}
 stepCyl(e,d){const l=this.phoro[e],prev=l.c;l.c=clamp(q(l.c+d),-6,0);if(this.autoComp&&(this.def.phase==='jcc'||this.phoro.jcc.mode!=='off')){this.compAcc[e]-=(l.c-prev)/2;while(this.compAcc[e]>=.25-1e-8){l.s=clamp(l.s+.25,-20,20);this.compAcc[e]-=.25;}while(this.compAcc[e]<=-.25+1e-8){l.s=clamp(l.s-.25,-20,20);this.compAcc[e]+=.25;}}this.afterLens(e);}
 axisDialAngle(e=this.activeEye){const l=this.phoro[e],t=Number.isFinite(l.axisRotation)?l.axisRotation:l.a;return t+((axis(l.a)-axis(t)+90)%180+180)%180-90;}
 stepAxis(e,d){const l=this.phoro[e];l.axisRotation=this.axisDialAngle(e)+d;l.a=axis(l.axisRotation);delete this.hist[e].searchAxis;this.afterLens(e);}
 afterLens(e){const p=this.def.phase,h=this.hist[e];if(['mp1','mp2'].includes(p)&&this.def.eye===e){const d=O.toVec(this.lens(e)).M-h.entry[p];h.fogMax[p]=Math.max(h.fogMax[p],d);if(d>=(p==='mp1'?.75:.5)-.01)h.fogged[p]=true;}this.emit();}
 setJccMode(mode){if(!['off','A','P'].includes(mode))return;this.phoro.jcc={mode,pos:1,selectedMode:mode==='off'?(this.phoro.jcc.selectedMode||'A'):mode};this.emit();}
 flipJcc(){if(this.phoro.jcc.mode==='off')return;this.phoro.jcc.pos=3-this.phoro.jcc.pos;this.say('已翻到鏡片 '+this.phoro.jcc.pos+'；按「比較 1／2」取得完整文字回答。');}
 block(kind='subjective'){const e=this.activeEye,l=this.phoro[e];if(!this.assisted&&this.def.eye&&e!==this.def.eye)return '目前流程是 '+this.def.eye+'，請先切回該眼。';if(!this.phoro.aperture||this.phoro.occ[e]||l.aux==='OC')return '病患：「這隻眼睛被遮住了，看不到。」';if(kind!=='ret'&&!this.phoro.occ[e==='OD'?'OS':'OD'])return '請遮蓋非測試眼，再做單眼比較。';if(['6ΔU','10ΔI'].includes(l.aux))return '病患：「影像分開了。」請移開輔助稜鏡再比較清晰度。';if(Math.abs(this.phoro.level)>2)return '驗光儀尚未調平，請先將氣泡對準中央。';if(kind==='jcc'&&l.aux!=='O')return 'JCC 比較前請將輔助鏡轉到 O。';return '';}
 acuity(e=this.activeEye,lens=this.lens(e),ph=this.phoro[e].aux==='PH'){let r=O.subVec(this.effective(e,lens),O.toVec(this.truth[e]));
 // Excess minus can be partly accommodated, but never raises the best available VA.
 const b=Math.hypot(r.M<0?r.M*.10:r.M,r.J0,r.J45)*(ph?.35:1);const va=1.2*Math.pow(10,-Math.max(0,b-.08)*.65);const rows=[.05,...O.VA_ROWS];return rows.filter(v=>v<=va+.015).at(-1)||.05;}
 readVA(){const b=this.block();if(b)return this.say(b);this.h.reads++;const va=this.acuity();const text=`病患：目前 VA ${va.toFixed(va<.1?2:1)}${this.phoro[this.activeEye].aux==='PH'?'（針孔）':''}。`;this.say(text);return va;}
 compareSphere(){const b=this.block();if(b)return this.say(b);const e=this.activeEye,r=this.lens(e),a={...r,s:r.s+.25},bb={...r,s:r.s-.25};const va1=this.acuity(e,a),va2=this.acuity(e,bb);this.h.reads++;return this.say(`鏡片 1：${O.fmtRx(a)}；鏡片 2：${O.fmtRx(bb)}。病患：「${va1>va2?'第一片比較清楚。':va2>va1?'第二片比較清楚。':'兩片讀到一樣多；第二片較黑、較小，沒有多看清楚。'}」目前 VA ${this.acuity().toFixed(1)}。`);}
 askDuo(){const b=this.block();if(b)return this.say(b);if(this.phoro.jcc.mode!=='off')return this.say('請先收回 JCC，再做紅綠。');this.h.duo++;const d=this.effective().M-O.toVec(this.truth[this.activeEye]).M;const msg=d>.13?'紅色那邊比較清楚。':d<-.13?'綠色那邊比較清楚。':'兩邊差不多清楚。';return this.say('病患：「'+msg+'」');}
 askJCC(){const b=this.block('jcc');if(b)return this.say(b);const {mode}=this.phoro.jcc;if(mode==='off')return this.say('先把 JCC 轉入 A（軸）或 P（度數）。');const e=this.activeEye,l=this.lens(e),red=O.jccRedAxis(mode,1,l.a),res=O.subVec(this.effective(e),O.toVec(this.truth[e])),j=O.jccVec(red);let first=O.blurStrength(O.addVec(res,j)),second=O.blurStrength(O.subVec(res,j));
 this.h.jccN++;this.h[mode==='A'?'jccA':'jccP']++;const u=rng(this.seed+this.h.jccN*101+(e==='OD'?1:17)),noise=this.variability==='ideal'?0:(u()-.5)*.014;const d=first-second+noise;this.phoro.jcc.pos=2;const result=Math.abs(d)<.028?'兩片差不多清楚。':d<0?'第一片比較清楚。':'第二片比較清楚。';return this.say(`鏡片 1 紅點 ${axis(red)}°；鏡片 2 紅點 ${axis(red+90)}°。病患：「${result}」`);}
 registerSweep(angle){const a=axis(angle+90);if(!this.h.scans.some(b=>Math.min(Math.abs(a-b),180-Math.abs(a-b))<10))this.h.scans.push(a);this.log('檢影掃描主子午線 '+a+'°');this.emit();}
 record(){const p=this.def.phase,e=this.activeEye;if(p==='wd'||p==='va'||p==='done')return;const b=this.block(p==='ret'?'ret':'subjective');if(b)return this.say(b);const h=this.hist[e];if(p==='ret'&&!this.hasMeridianPair(e))return this.say('先旋轉光條，觀察相差約 90° 的兩條主子午線。');this.records[e][p]={rx:this.lens(e),va:this.acuity(e),key:key(this.lens(e)),at:Date.now()};return this.say(`${e} ${p==='ret'?'Gross 檢影':p==='jcc'?'JCC':p==='duo'?'紅綠後':'MPMVA'} 已記錄：${O.fmtRx(this.lens(e))} · VA ${this.acuity(e).toFixed(1)}`);}
 hasMeridianPair(e){const a=this.hist[e].scans;return a.some(x=>a.some(y=>{const d=Math.min(Math.abs(x-y),180-Math.abs(x-y));return d>=75;}));}
 subtractWD(){if(this.wdApplied)return this.say('本輪已扣除工作距離，不能重複扣除。');if(!eyes.every(e=>this.records[e].ret))return this.say('請先記錄雙眼 Gross 檢影。');for(const e of eyes){this.phoro[e]={...this.phoro[e],...this.records[e].ret.rx,s:q(this.records[e].ret.rx.s-this.wdD)};this.records[e].net={rx:this.lens(e),va:this.acuity(e)};}this.wdApplied=true;this.say(`已從雙眼球面扣除 ${this.wdD.toFixed(2)}D；柱面及軸位不變。`);}
 fog(){if(!['mp1','mp2'].includes(this.def.phase))return;this.stepSph(this.activeEye,this.def.phase==='mp1'?.75:.5);this.say('已加正霧視。逐格減正，VA 會直接顯示；找維持最佳 VA 的最大正度數。');}
 missing(){const p=this.def.phase,e=this.def.eye,h=e?this.hist[e]:null,r=e?this.records[e][p]:null,m=[];if(p==='ret'){if(!this.hasMeridianPair(e))m.push('旋轉光條，掃描兩條相差約 90° 的主子午線');if(!r||r.key!==key(this.lens(e)))m.push('記錄目前 Gross 度數');}
 if(p==='wd'&&!this.wdApplied)m.push('扣除雙眼工作距離');if(['mp1','mp2'].includes(p)){if(!h.fogged[p])m.push(p==='mp1'?'至少加 +0.75D 霧視':'至少加 +0.50D 再霧視');if(h.reads<2)m.push('調鏡並核對兩次 VA');if(!r||r.key!==key(this.lens(e)))m.push('記錄目前 MPMVA');}
 if(p==='duo'){if(!h.duo)m.push('詢問紅綠');if(!r||r.key!==key(this.lens(e)))m.push('記錄紅綠後度數');}
 if(p==='jcc'){if(!h.jccA||!h.jccP)m.push('分別做 A 與 P 的兩片比較');if(this.phoro.jcc.mode!=='off')m.push('收回 JCC');if(!r||r.key!==key(this.lens(e)))m.push('記錄 JCC 結果');}return m;}
 advance(){const m=this.missing();if(m.length)return this.say('尚需：'+m.join('、')+'。');if(this.done)return;this.stage++;this.enter();this.emit();}
 enter(){const d=this.def;if(d.eye){this.setTestEye(d.eye);this.phoro[d.eye].aux='O';this.phoro.occ[d.eye]=false;}this.phoro.jcc={mode:'off',pos:1};if(['mp1','mp2'].includes(d.phase)){this.h.reads=0;this.h.entry[d.phase]=O.toVec(this.lens()).M;this.compAcc[this.activeEye]=0;}
 if(d.phase==='jcc'){this.h.route=Math.abs(this.lens().c)>=.75?'APA':'PPAP';this.h.routeIndex=0;}
 if(d.phase==='va'){for(const e of eyes)if(this.records[e].net)this.records[e].net.va=this.acuity(e);}
 if(d.phase==='done'){this.finished=Date.now();this.message='雙眼流程完成。下方可檢視參考終點、各階段紀錄及差異。';}else this.message='現在：'+d.title+'。';this.log(this.message);}
 result(){return eyes.map(e=>{const target=this.truth[e],final=this.records[e].mp2?.rx;const error=final?O.blurStrength(O.subVec(O.toVec(final),O.toVec(target))):null;return {eye:e,target,objective:this.hist[e].retTarget,final,error,records:this.records[e],pass:error!==null&&error<=.26};});}
 serialize(){const {listeners,...s}=this;return {version:1,state:copy(s)};}
 static restore(data){if(data?.version!==1||!data.state||!Number.isInteger(data.state.stage)||data.state.stage<0||data.state.stage>=stages.length)throw Error('存檔格式不正確');const s=data.state;const g=new Practice({seed:s.seed,truth:s.truth,variability:s.variability,wdCm:s.wdCm});Object.assign(g,s);for(const e of eyes){if(g.phoro.occ[e])g.phoro[e].aux='OC';else if(g.phoro[e].aux==='OC'||!O.AUX_POS.includes(g.phoro[e].aux))g.phoro[e].aux='O';delete g.hist[e].searchAxis;}g.listeners=new Set();return g;}
}
globalThis.RefractionPractice={Practice,stages,randomCase,key,axis};
})();
