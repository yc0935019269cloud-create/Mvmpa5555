using System;
using System.Collections.Generic;
using UnityEngine;

namespace RefractionRoom {
 [Serializable] public struct Rx {
  public float s,c,a;
  public Rx(float sphere,float cylinder,float axis){s=sphere;c=cylinder;a=axis;}
  public override string ToString(){return (s>=0?"+":"")+s.ToString("0.00")+" / "+c.ToString("0.00")+" × "+Mathf.RoundToInt(a).ToString("000");}
 }
 public static class Optics {
  public static float Axis(float a){a=(a%180+180)%180;return Mathf.Abs(a)<.001f?180:a;}
  public static float AxisError(float a,float b){float d=Mathf.Abs(Axis(a)-Axis(b));return Mathf.Min(d,180-d);}
  public static Vector3 Power(Rx r){float h=-r.c*.5f;return new Vector3(r.s+r.c*.5f,h*Mathf.Cos(2*r.a*Mathf.Deg2Rad),h*Mathf.Sin(2*r.a*Mathf.Deg2Rad));}
  public static float Meridian(Rx r,float angle){float sn=Mathf.Sin((angle-r.a)*Mathf.Deg2Rad);return r.s+r.c*sn*sn;}
  public static float WD(float cm){return Mathf.Round(400/cm)/4f;}
  public static float RetResidual(Rx truth,Rx lens,float wd,float streak){return Meridian(lens,streak+90)-Meridian(truth,streak+90)-wd;}
  public static bool Neutral(Rx truth,Rx lens,float wd){for(int a=0;a<180;a+=5)if(Mathf.Abs(RetResidual(truth,lens,wd,a))>=.125f)return false;return true;}
  public static float Blur(Rx truth,Rx lens,bool pinhole=false){Vector3 r=Power(lens)-Power(truth);if(r.x<0)r.x*=.08f;float b=r.magnitude;return pinhole?b*.35f:b;}
  public static float Acuity(Rx truth,Rx lens,bool ph=false){return Mathf.Min(1.2f,Mathf.Pow(10,.04f-Mathf.Max(0,.5f*(Blur(truth,lens,ph)-.1f))));}
  // Duochrome: red clearer while the eye is under-minused by a step or more; green from the end point on.
  // Practice rule: red -> add minus, green -> add plus; the end point is the first green after red.
  public static string Duo(Rx truth,Rx lens){float d=(Power(lens)-Power(truth)).x;return d>.375f?"紅色那邊比較清楚，差很多。":d>.12f?"紅色那邊比較清楚。":d<-.37f?"綠色那邊比較清楚，差很多。":"綠色那邊比較清楚。";}
  public static Vector3 JCC(float redAxis){return new Vector3(0,.25f*Mathf.Cos(2*redAxis*Mathf.Deg2Rad),.25f*Mathf.Sin(2*redAxis*Mathf.Deg2Rad));}
  public static string CompareJCC(Rx truth,Rx lens,bool axis){return CompareJCCAt(truth,lens,lens.a+(axis?45:0));}
  // first face = red dot (minus cylinder axis) at redAxis, second face = white dot there
  public static string CompareJCCAt(Rx truth,Rx lens,float angle){
   Vector3 res=Power(lens)-Power(truth),j=JCC(angle);
   float first=(res+j).magnitude,second=(res-j).magnitude;
   if(Mathf.Abs(first-second)<.03f)return "兩片差不多清楚。";
   return first<second?"第一片比較清楚。":"第二片比較清楚。";
  }
  public static readonly float[] Rows={.1f,.2f,.3f,.4f,.5f,.6f,.7f,.8f,.9f,1f,1.2f,1.5f,2f};
  public static readonly string[] Snellen={"6/60","6/30","6/20","6/15","6/12","6/10","6/8.6","6/7.5","6/6.7","6/6","6/5","6/4","6/3"};
  public static int LettersOnRow(int i){return 5;}
  // tumbling-E orientation per letter (0 right, 1 up, 2 left, 3 down), fixed like a printed chart
  public static int RowRot(int i,int k){int[][] t={new[]{0,3,1,2,1},new[]{2,0,3,1,3},new[]{1,2,0,3,0},new[]{3,1,2,0,2},new[]{0,2,1,3,1},new[]{2,3,0,1,0},new[]{3,0,1,2,3},new[]{1,3,2,0,2},new[]{0,1,3,2,0},new[]{2,1,0,3,1},new[]{3,2,1,0,3},new[]{1,0,3,2,0},new[]{0,2,3,1,2}};return t[Mathf.Clamp(i,0,t.Length-1)][k%5];}
  public static int RowIndex(float row){int i=0;for(int k=0;k<Rows.Length;k++)if(Mathf.Abs(Rows[k]-row)<.01f)i=k;return i;}
  // Five optotypes per line, 0.02 logMAR per letter: letters correct on a line for the current lens.
  public static int LettersRead(Rx truth,Rx lens,bool ph,float row){float v=Acuity(truth,lens,ph);float d=Mathf.Log10(row)-Mathf.Log10(v);return Mathf.Clamp(Mathf.RoundToInt(5-d/.02f),0,5);}
  // Clinical notation: 5/5 -> line; 3-4/5 -> line -missed; 1-2/5 -> previous line +read.
  public static string VALabel(float row,int n,out float baseVA,out float logmar){
   int i=0;for(int k=0;k<Rows.Length;k++)if(Mathf.Abs(Rows[k]-row)<.01f)i=k;float prev=i>0?Rows[i-1]:0;
   logmar=-Mathf.Log10(row)+.02f*(5-n);
   if(n>=5){baseVA=row;return row.ToString("0.0");}
   if(n>=3){baseVA=row;return row.ToString("0.0")+" -"+(5-n);}
   if(n>=1&&i>0){baseVA=prev;return prev.ToString("0.0")+" +"+n;}
   baseVA=prev;return n>=1?row.ToString("0.0")+" -"+(5-n):"";
  }
  public static float BestRow(Rx truth,Rx lens,bool ph=false){float v=Acuity(truth,lens,ph),best=.1f;foreach(float row in Rows)if(row<=v+.025f)best=row;return best;}
 }
 [Serializable] public class EyeHistory {
  public Rx lens=new Rx(0,0,180),gross,net,mp1,jccResult,final;
  public bool grossSaved,netSaved,finalSaved,fogged,duoAsked,phRead;
  public int reads,axisComparisons,powerComparisons,sweeps;
  public float measuredVA,phVA,mp1VA,finalVA,cylComp,highestFog,lastStreak=-999;
  public string finalKey="",vaText="",phVAText="",mp1VAText="",finalVAText="",readKey="",readText="";
  public float vaLog=99,phLog=99,readLog=99,readVA;public int lastLetters;
  public bool vaSaved,mp1Saved,jccSaved,secondFogged;
  public string route="APA";
  public float searchAngle=180;public int searchSeen;
  public int routeIndex,routeComparisons;
 }
 [Serializable] public class ClinicSession {
  public int patient=1,stage=0,eye=0;
  public string mode="教學";
  public bool[] prepared=new bool[9];
  public EyeHistory[] eyes={new EyeHistory(),new EyeHistory()};
  public float wdCm=57,pd=60,level=3,height=.1f,vertex=18,row=.6f,streak;
  public int chart=0,jccMode=0;
  public bool ph,occluded,wdRemoved,completed;
  public float seconds,pdMeasured,pdZeroError;
  public bool phoropterIn;
  public List<string> events=new List<string>();
  public List<string> examErrors=new List<string>();
  // patient 0 = random case generated at Begin (19-21-year-old, mostly myopic, mostly with-the-rule)
  public Rx randOD=new Rx(-2,-.5f,180),randOS=new Rx(-2,-.5f,180);public int randPD=63;
  public void Randomize(int seed){
   var rng=new System.Random(seed);System.Func<double> U=()=>rng.NextDouble();
   System.Func<double,double,double> N=(m,sd)=>m+sd*System.Math.Sqrt(-2*System.Math.Log(1-U()))*System.Math.Cos(2*System.Math.PI*U());
   double p=U(),se=p<.08?N(.25,.35):p<.11?1+U()*1.25:p<.92?-.75-System.Math.Abs(N(0,2.3)):-6.5-U()*2.5;
   se=System.Math.Max(-9,System.Math.Min(2.25,se));
   double cyl=U()<.25?0:-.25-System.Math.Abs(N(0,.6));cyl=System.Math.Max(-2.5,cyl);
   double q=U(),ax=q<.7?N(180,9):q<.85?N(90,10):(U()<.5?N(45,12):N(135,12));
   float Q(double x){return Mathf.Round((float)x*4)/4f;}float A(double a){return Optics.Axis(Mathf.Round((float)a/5)*5);}
   float c1=Q(cyl),c2=Mathf.Min(0,Q(cyl+N(0,.2)));float se2=(float)(se+Mathf.Clamp((float)N(0,.4),-1.25f,1.25f));
   randOD=new Rx(Q(se-c1/2),c1,c1==0?180:A(ax));randOS=new Rx(Q(se2-c2/2),c2,c2==0?180:A(180-ax+N(0,6)));
   randPD=Mathf.Clamp(Mathf.RoundToInt((float)N(63,2.4)),59,68);
  }
  public Rx Truth(int e){if(patient==0)return e==0?randOD:randOS;int k=(patient-1)%6;Rx[] od={new Rx(-2,-1,180),new Rx(-1.25f,-.75f,90),new Rx(.75f,-1.25f,175),new Rx(-3,-1.5f,10),new Rx(-.5f,-.5f,80),new Rx(1.5f,-.75f,165)};Rx[] os={new Rx(-1.5f,-.75f,90),new Rx(-1,-.5f,85),new Rx(1,-1,170),new Rx(-2.75f,-1.25f,5),new Rx(-.75f,-.75f,95),new Rx(1.25f,-1,160)};return e==0?od[k]:os[k];}
  public int TruePD {get{return patient==0?randPD:new int[]{64,62,66,63,61,65}[(patient-1)%6];}}
  public string PatientName {get{return patient==0?"隨機病患":new string[]{"林同學","陳同學","許同學","張同學","李同學","王同學"}[(patient-1)%6];}}
  public EyeHistory H {get{return eyes[eye];}}
  // PPAP first P: cylinder search with the JCC power dots at 180/45/90/135; the lens is untouched
  public bool Searching {get{return Phase==3&&H.route=="PPAP"&&H.routeIndex==0;}}
  public float JCCRef(int e){return e==eye&&Searching?eyes[e].searchAngle:eyes[e].lens.a;}
  public string AskJCC(){return Optics.CompareJCCAt(Truth(eye),H.lens,JCCRef(eye)+(jccMode==1?45:0));}
  public static int SearchBucket(float a){return Mathf.RoundToInt(Optics.Axis(a)/45f)%4;}
  public static readonly float[] SearchAngles={180,45,90,135};
  public List<float> RedAngles(){var r=new List<float>();foreach(float a in SearchAngles)if(Optics.CompareJCCAt(Truth(eye),H.lens,a).StartsWith("第一"))r.Add(a);return r;}
  public string EyeName {get{return eye==0?"右眼 OD":"左眼 OS";}}
  // Order follows the midterm record sheet: ① RET (both eyes) + WD + VA, ② 1st MPMVA + duochrome (both eyes),
  // ③ JCC (both eyes, recorded), ④ 2nd MPMVA (both eyes, best VA).
  public static readonly string[] Titles={"診間準備","右眼檢影","左眼檢影","扣除工作距離","右眼 · 檢影後 VA","左眼 · 檢影後 VA","右眼 · 霧視 / 第一次 MPMVA","右眼 · 紅綠","左眼 · 霧視 / 第一次 MPMVA","左眼 · 紅綠","右眼 · JCC","右眼 · JCC 紀錄","左眼 · JCC","左眼 · JCC 紀錄","右眼 · 第二次 MPMVA","左眼 · 第二次 MPMVA","完成紀錄"};
  static readonly int[] StageEye={0,0,1,0,0,1,0,0,1,1,0,0,1,1,0,1,1};
  static readonly int[] StagePhase={-1,-1,-1,-1,0,0,1,2,1,2,3,4,3,4,5,5,-1};
  public static readonly string[] PrepNames={"消毒儀器與 PD 尺","量瞳距並設定 PD","調整水平","輔助鏡歸 O","打開雙眼窺孔","調整桌高與頂點距離","設定紅綠大 E","鎖定支臂","調暗燈光"};
  public void Log(string e){events.Add(stage+" | "+e);if(events.Count>150)events.RemoveAt(0);}
  public string LensKey(int e){return eyes[e].lens.ToString();}
  public int Phase {get{return stage>=0&&stage<StagePhase.Length?StagePhase[stage]:-1;}}
  public int[] JCCPattern {get{return H.route=="APA"?new[]{1,2,1}:new[]{2,2,1,2};}}
  public string StageTitle {get{return Phase==3?EyeName+" · JCC "+H.route+" ("+(H.routeIndex+1)+"/"+JCCPattern.Length+")":Phase==4?EyeName+" · JCC 複核與紀錄":Titles[stage];}}
  public void EnterStage(){
   if(stage!=3&&stage<StageEye.Length)eye=StageEye[stage];
   ph=false;jccMode=0;chart=0;
   if(Phase==0){if(H.netSaved)H.lens=H.net;H.reads=0;H.measuredVA=0;H.vaLog=99;H.vaText="";H.phRead=false;occluded=true;row=.6f;}
   if(Phase==1){H.lens=H.net;H.reads=0;H.fogged=false;occluded=true;row=1;}
   if(Phase==2)chart=1;
   if(Phase==3){chart=2;H.route=Mathf.Abs(H.lens.c)>=.75f?"APA":"PPAP";H.routeIndex=0;H.routeComparisons=0;jccMode=JCCPattern[0];H.searchAngle=180;H.searchSeen=0;}
   if(Phase==4){chart=2;jccMode=0;}
   if(Phase==5){H.reads=0;H.secondFogged=false;chart=0;jccMode=0;}
  }
  public void Adjust(int parameter,int sign,int steps=1,int e=-1){
   if(e<0)e=eye;var h=eyes[e];Rx r=h.lens;
   for(int k=0;k<steps;k++){
    if(parameter==0)r.s=Mathf.Clamp(r.s+sign*.25f,-19,16.75f);
    if(parameter==1&&Searching&&e==eye&&r.c==0&&sign<0)r.a=h.searchAngle;
    if(parameter==2&&Searching&&e==eye){h.searchAngle=Optics.Axis(h.searchAngle+sign*45);continue;}
    if(parameter==1){float old=r.c;r.c=Mathf.Clamp(r.c+sign*.25f,-6,0);if(stage>3){h.cylComp-=.5f*(r.c-old);if(Mathf.Abs(h.cylComp)>=.249f){float comp=Mathf.Round(h.cylComp*4)/4;r.s+=comp;h.cylComp-=comp;}}}
    if(parameter==2)r.a=Optics.Axis(r.a+sign*5);
   }
   h.lens=r;
   if(Phase==1&&e==eye){float fog=(Optics.Power(r)-Optics.Power(Truth(eye))).x;H.highestFog=Mathf.Max(fog,H.highestFog);if(fog>=.75f)H.fogged=true;}
   Log("調鏡 "+(e==0?"右眼 OD":"左眼 OS")+" "+r);
  }
  public void Read(){
   H.reads++;int n=Optics.LettersRead(Truth(eye),H.lens,ph,row);float bv,lm;string lab=Optics.VALabel(row,n,out bv,out lm);H.lastLetters=n;
   string key=LensKey(eye)+(ph?"PH":"");if(H.readKey!=key){H.readKey=key;H.readLog=99;H.readText="";H.readVA=0;}
   if(n>0&&lm<H.readLog){H.readLog=lm;H.readText=lab;H.readVA=bv;}
   if(Phase==0&&!ph&&n>0&&lm<H.vaLog){H.vaLog=lm;H.vaText=lab;H.measuredVA=bv;}
   if(ph&&n>0){H.phRead=true;if(lm<H.phLog){H.phLog=lm;H.phVAText=lab;H.phVA=bv;}}
   if(ph&&n==0)H.phRead=true;
   H.finalVA=H.readVA;Log("讀 VA 列 "+row.ToString("0.0")+" 對 "+n+"/5"+(ph?" PH":"")+(lab!=""?" → "+lab:""));
  }
  public string CurrentVAText(){return H.readKey==LensKey(eye)&&H.readText!=""?H.readText:"未讀";}
  public void Save(){
   if(stage==1||stage==2){H.gross=H.lens;H.grossSaved=true;}
   else if(stage==3){foreach(var h in eyes){h.net=h.lens;h.netSaved=true;}}
   else if(Phase==0)H.vaSaved=true;
   else if(Phase==1){H.mp1=H.lens;H.mp1VA=H.readKey==LensKey(eye)?H.readVA:0;H.mp1VAText=CurrentVAText();H.mp1Saved=true;}
   else if(Phase==4){H.jccResult=H.lens;H.jccSaved=true;}
   else if(Phase==5){H.final=H.lens;H.finalVA=H.readKey==LensKey(eye)?H.readVA:0;H.finalVAText=CurrentVAText();H.finalSaved=true;H.finalKey=LensKey(eye);}
   Log("寫入紀錄");
  }
  public List<string> Missing(){
   var m=new List<string>();
   if(stage==0){for(int i=0;i<9;i++)if(!prepared[i])m.Add(PrepNames[i]);return m;}
   if(stage==1||stage==2){if(!Optics.Neutral(Truth(eye),H.lens,Optics.WD(wdCm)))m.Add("兩條主子午線都要中和");if(H.sweeps<2)m.Add("至少在兩個不同方向掃動光帶");if(!H.grossSaved||H.gross.ToString()!=H.lens.ToString())m.Add("記錄目前中和鏡片");return m;}
   if(stage==3){if(!wdRemoved)m.Add("從兩眼球面扣除工作距離度數");if(!eyes[0].netSaved||!eyes[1].netSaved)m.Add("記錄淨檢影值與工作距離");return m;}
   if(Phase==0){if(!occluded)m.Add("遮蓋非測試眼");if(H.reads<2)m.Add("以淨檢影值至少讀兩列找出最佳視力");if(H.measuredVA<.8f&&!H.phRead)m.Add("VA < 0.8，補測 PH 視力");if(ph)m.Add("將針孔轉開");if(!H.vaSaved)m.Add("記錄檢影度數與 VA");}
   if(Phase==1){float fe=(Optics.Power(H.lens)-Optics.Power(Truth(eye))).x;if(!H.fogged)m.Add("先加正度數霧視至少 +0.75D");if(H.reads<2)m.Add("邊退霧邊讀視標");if(fe>.38f||fe<-.13f)m.Add("找出維持最佳視力的最大正度數");if(!H.mp1Saved||H.mp1.ToString()!=H.lens.ToString())m.Add("記錄第一次 MPMVA 度數與 VA");}
   if(Phase==2){if(!H.duoAsked)m.Add("切紅綠視標並詢問清晰度");if(Mathf.Abs((Optics.Power(H.lens)-Optics.Power(Truth(eye))).x)>.13f)m.Add("紅清加負、綠清加正，停在第一次出現綠色");}
   if(Phase==3&&Searching){if(jccMode!=2)m.Add("切到 JCC 度數 P");int seen=0;for(int k=0;k<4;k++)if((H.searchSeen&(1<<k))!=0)seen++;if(seen<4)m.Add("JCC P 紅點依序放 180°、45°、90°、135° 各翻面比較一次（已做 "+seen+"/4，轉柱軸鈕一次轉 45°）");else foreach(float ra in RedAngles()){if(H.lens.c==0){m.Add("受測者在 "+ra.ToString("0")+"° 選紅點：柱面加 −0.25、軸放 "+ra.ToString("0")+"°，再翻面確認");break;}if(Optics.AxisError(ra,H.lens.a)<1){m.Add("同軸方向仍選紅點：柱面再加 −0.25");break;}if(Optics.AxisError(ra,H.lens.a)>89){m.Add("垂直方向選紅點：柱面減少 0.25");break;}}}
   else if(Phase==3){if(jccMode!=JCCPattern[H.routeIndex])m.Add("切到本輪要求的 JCC "+(JCCPattern[H.routeIndex]==1?"軸位 A":"度數 P"));if(H.routeComparisons<2)m.Add("本輪至少做兩次翻面比較");if(!Optics.CompareJCC(Truth(eye),H.lens,JCCPattern[H.routeIndex]==1).StartsWith("兩片"))m.Add("依受測者回答微調至兩片等清");}
   if(Phase==4){if(H.axisComparisons<2||H.powerComparisons<2)m.Add("完成 APA 或 PPAP 路線");if((Optics.Power(H.lens)-Optics.Power(Truth(eye))).magnitude>.19f)m.Add("複核柱面與軸位，並保持球面等效");if(jccMode!=0)m.Add("收回 JCC");if(!H.jccSaved||H.jccResult.ToString()!=H.lens.ToString())m.Add("記錄 JCC 後處方");}
   if(Phase==5){if(!H.secondFogged)m.Add("加 +0.50D 再霧視，逐格退霧");if(H.reads<2)m.Add("雙眼 JCC 後再核對視力至少兩次");if((Optics.Power(H.lens)-Optics.Power(Truth(eye))).magnitude>.2f)m.Add("再次確認最大正度數最佳視力");if(!H.finalSaved||H.finalKey!=LensKey(eye))m.Add("記錄第二次 MPMVA 度數與最佳 VA");}
   if(stage==16){foreach(var h in eyes)if(!h.finalSaved)m.Add("雙眼最終紀錄未齊");}
   if(stage>=4&&stage<=15&&!occluded&&!m.Contains("遮蓋非測試眼"))m.Add("保持非測試眼遮蓋");
   return m;
  }
  public void RegisterJCC(){if(Searching&&jccMode==2)H.searchSeen|=1<<SearchBucket(H.searchAngle);if(jccMode==1)H.axisComparisons++;else H.powerComparisons++;if(Phase==3&&jccMode==JCCPattern[H.routeIndex])H.routeComparisons++;}
  public bool Advance(){var missing=Missing();if(mode=="教學"&&missing.Count>0)return false;if(mode=="考試"&&missing.Count>0)examErrors.Add(StageTitle+"："+string.Join("、",missing));if(Phase==3&&H.routeIndex<JCCPattern.Length-1){H.routeIndex++;H.routeComparisons=0;jccMode=JCCPattern[H.routeIndex];Log("JCC "+H.route+" 第 "+(H.routeIndex+1)+" 輪");return true;}if(stage<16){stage++;EnterStage();}else completed=true;Log("進入 "+Titles[stage]);return true;}
  public int Score(){
   float score=0;foreach(bool b in prepared)if(b)score+=10f/9;
   for(int e=0;e<2;e++){var h=eyes[e];Rx tr=Truth(e);Rx gr=new Rx(tr.s+Optics.WD(wdCm),tr.c,tr.a);
    if(h.grossSaved)score+=5*Mathf.Clamp01(1-(Optics.Power(h.gross)-Optics.Power(gr)).magnitude);if(h.netSaved&&wdRemoved)score+=2.5f;
    if(h.vaSaved&&h.measuredVA>0&&(h.measuredVA>=.8f||h.phRead))score+=4;
    if(h.fogged&&h.mp1Saved)score+=5;if(h.duoAsked)score+=3;if(h.axisComparisons>=2&&h.powerComparisons>=2&&h.jccSaved)score+=8;
    if(h.finalSaved)score+=10*Mathf.Clamp01(1-(Optics.Power(h.final)-Optics.Power(tr)).magnitude);
    if(h.finalSaved&&h.finalKey==LensKey(e))score+=2.5f;
   }
   if(completed&&seconds<=1080)score+=10;return Mathf.Clamp(Mathf.RoundToInt(score),0,100);
  }
 }
}
