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
  public static string Duo(Rx truth,Rx lens){float d=(Power(lens)-Power(truth)).x;return Mathf.Abs(d)<.13f?"兩邊一樣清楚。":d>0?"紅色那邊的字比較清楚。":"綠色那邊的字比較清楚。";}
  public static Vector3 JCC(float redAxis){return new Vector3(0,.25f*Mathf.Cos(2*redAxis*Mathf.Deg2Rad),.25f*Mathf.Sin(2*redAxis*Mathf.Deg2Rad));}
  public static string CompareJCC(Rx truth,Rx lens,bool axis){
   float angle=lens.a+(axis?45:0);Vector3 res=Power(lens)-Power(truth),j=JCC(angle);
   float first=(res+j).magnitude,second=(res-j).magnitude;
   if(Mathf.Abs(first-second)<.03f)return "兩片差不多清楚。";
   return first<second?"第一片比較清楚。":"第二片比較清楚。";
  }
  public static readonly float[] Rows={.1f,.2f,.3f,.4f,.5f,.6f,.7f,.8f,1f,1.2f};
  public static float BestRow(Rx truth,Rx lens,bool ph=false){float v=Acuity(truth,lens,ph),best=.1f;foreach(float row in Rows)if(row<=v+.025f)best=row;return best;}
 }
 [Serializable] public class EyeHistory {
  public Rx lens=new Rx(0,0,180),gross,net,mp1,jccResult,final;
  public bool grossSaved,netSaved,finalSaved,fogged,duoAsked,phRead;
  public int reads,axisComparisons,powerComparisons,sweeps;
  public float measuredVA,phVA,mp1VA,finalVA,cylComp,highestFog,lastStreak=-999;
  public string finalKey="";
  public bool vaSaved,mp1Saved,jccSaved,secondFogged;
  public string route="APA";
  public int routeIndex,routeComparisons;
 }
 [Serializable] public class ClinicSession {
  public int patient=1,stage=0,eye=0;
  public string mode="教學";
  public bool[] prepared=new bool[9];
  public EyeHistory[] eyes={new EyeHistory(),new EyeHistory()};
  public float wdCm=67,pd=60,level=3,height=.1f,vertex=18,row=.6f,streak;
  public int chart=0,jccMode=0;
  public bool ph,occluded,wdRemoved,completed;
  public float seconds;
  public List<string> events=new List<string>();
  public List<string> examErrors=new List<string>();
  public Rx Truth(int e){int k=(patient-1)%6;Rx[] od={new Rx(-2,-1,180),new Rx(-1.25f,-.75f,90),new Rx(.75f,-1.25f,175),new Rx(-3,-1.5f,10),new Rx(-.5f,-.5f,80),new Rx(1.5f,-.75f,165)};Rx[] os={new Rx(-1.5f,-.75f,90),new Rx(-1,-.5f,85),new Rx(1,-1,170),new Rx(-2.75f,-1.25f,5),new Rx(-.75f,-.75f,95),new Rx(1.25f,-1,160)};return e==0?od[k]:os[k];}
  public int TruePD {get{return new int[]{64,62,66,63,61,65}[(patient-1)%6];}}
  public string PatientName {get{return new string[]{"林同學","陳同學","許同學","張同學","李同學","王同學"}[(patient-1)%6];}}
  public EyeHistory H {get{return eyes[eye];}}
  public string EyeName {get{return eye==0?"右眼 OD":"左眼 OS";}}
  public static readonly string[] Titles={"診間準備","右眼檢影","左眼檢影","扣除工作距離","右眼 · 視力與針孔","右眼 · 霧視 / 第一次 MPMVA","右眼 · 紅綠平衡","右眼 · JCC 軸位","右眼 · JCC 度數","右眼 · 第二次 MPMVA","左眼 · 視力與針孔","左眼 · 霧視 / 第一次 MPMVA","左眼 · 紅綠平衡","左眼 · JCC 軸位","左眼 · JCC 度數","左眼 · 第二次 MPMVA","完成紀錄"};
  public static readonly string[] PrepNames={"消毒儀器","量瞳距並設定 PD","調整水平","輔助鏡歸 O","打開雙眼窺孔","調整桌高與頂點距離","設定紅綠大 E","鎖定支臂","調暗燈光"};
  public void Log(string e){events.Add(stage+" | "+e);if(events.Count>150)events.RemoveAt(0);}
  public string LensKey(int e){return eyes[e].lens.ToString();}
  public int Phase {get{return stage>=4&&stage<=15?(stage-4)%6:-1;}}
  public int[] JCCPattern {get{return H.route=="APA"?new[]{1,2,1}:new[]{2,2,1,2};}}
  public string StageTitle {get{return Phase==3?EyeName+" · JCC "+H.route+" ("+(H.routeIndex+1)+"/"+JCCPattern.Length+")":Phase==4?EyeName+" · JCC 複核與紀錄":Titles[stage];}}
  public void EnterStage(){
   if(stage==1)eye=0;if(stage==2)eye=1;if(stage>=4&&stage<10)eye=0;if(stage>=10&&stage<16)eye=1;
   ph=false;jccMode=0;chart=0;
   if(Phase==0){eyes[eye].lens=new Rx(0,0,180);H.reads=0;H.measuredVA=0;H.phRead=false;occluded=false;row=.6f;}
   if(Phase==1){H.lens=H.net;H.reads=0;H.fogged=false;occluded=true;row=1;}
   if(Phase==2)chart=1;
   if(Phase==3){chart=2;H.route=Mathf.Abs(H.lens.c)>=.75f?"APA":"PPAP";H.routeIndex=0;H.routeComparisons=0;jccMode=JCCPattern[0];}
   if(Phase==4){chart=2;jccMode=0;}
   if(Phase==5){H.reads=0;H.secondFogged=false;chart=0;jccMode=0;}
  }
  public void Adjust(int parameter,int sign){
   Rx r=H.lens;
   if(parameter==0)r.s=Mathf.Clamp(r.s+sign*.25f,-12,12);
   if(parameter==1){float old=r.c;r.c=Mathf.Clamp(r.c+sign*.25f,-4,0);if(stage>3){H.cylComp-=.5f*(r.c-old);if(Mathf.Abs(H.cylComp)>=.249f){float comp=Mathf.Round(H.cylComp*4)/4;r.s+=comp;H.cylComp-=comp;}}}
   if(parameter==2)r.a=Optics.Axis(r.a+sign*5);
   H.lens=r;
   if(Phase==1){float fog=(Optics.Power(r)-Optics.Power(Truth(eye))).x;H.highestFog=Mathf.Max(fog,H.highestFog);if(fog>=.75f)H.fogged=true;}
   Log("調鏡 "+EyeName+" "+r);
  }
  public void Read(){
   H.reads++;float best=Optics.BestRow(Truth(eye),H.lens,ph);
   if(Phase==0&&!ph)H.measuredVA=Mathf.Max(H.measuredVA,Mathf.Min(row,best));
   if(ph){H.phRead=true;H.phVA=Mathf.Max(H.phVA,Mathf.Min(row,best));}
   H.finalVA=best;Log("讀 VA "+row.ToString("0.0")+" PH="+ph);
  }
  public void Save(){
   if(stage==1||stage==2){H.gross=H.lens;H.grossSaved=true;}
   else if(stage==3){foreach(var h in eyes){h.net=h.lens;h.netSaved=true;}}
   else if(Phase==0)H.vaSaved=true;
   else if(Phase==1){H.mp1=H.lens;H.mp1VA=H.finalVA;H.mp1Saved=true;}
   else if(Phase==4){H.jccResult=H.lens;H.jccSaved=true;}
   else if(Phase==5){H.final=H.lens;H.finalSaved=true;H.finalKey=LensKey(eye);}
   Log("寫入紀錄");
  }
  public List<string> Missing(){
   var m=new List<string>();
   if(stage==0){for(int i=0;i<9;i++)if(!prepared[i])m.Add(PrepNames[i]);return m;}
   if(stage==1||stage==2){if(!Optics.Neutral(Truth(eye),H.lens,Optics.WD(wdCm)))m.Add("兩條主子午線都要中和");if(H.sweeps<2)m.Add("至少在兩個不同方向掃動光帶");if(!H.grossSaved||H.gross.ToString()!=H.lens.ToString())m.Add("記錄目前中和鏡片");return m;}
   if(stage==3){if(!wdRemoved)m.Add("從兩眼球面扣除工作距離度數");if(!eyes[0].netSaved||!eyes[1].netSaved)m.Add("記錄淨檢影值與工作距離");return m;}
   if(Phase==0){if(!occluded)m.Add("遮蓋非測試眼");if(H.reads<2)m.Add("至少讀兩列以找出裸眼最佳視力");if(H.measuredVA<.8f&&!H.phRead)m.Add("VA < 0.8，補測 PH 視力");if(ph)m.Add("將針孔轉開");if(!H.vaSaved)m.Add("記錄裸眼與 PH 視力");}
   if(Phase==1){if(!H.fogged)m.Add("先加正度數霧視至少 +0.75D");if(H.reads<2)m.Add("邊退霧邊讀視標");if(Mathf.Abs((Optics.Power(H.lens)-Optics.Power(Truth(eye))).x)>.13f)m.Add("找出維持最佳視力的最大正度數");if(!H.mp1Saved||H.mp1.ToString()!=H.lens.ToString())m.Add("記錄第一次 MPMVA");}
   if(Phase==2){if(!H.duoAsked)m.Add("切紅綠視標並詢問清晰度");if(Mathf.Abs((Optics.Power(H.lens)-Optics.Power(Truth(eye))).x)>.13f)m.Add("紅清加負、綠清加正，至等清");}
   if(Phase==3){if(jccMode!=JCCPattern[H.routeIndex])m.Add("切到本輪要求的 JCC "+(JCCPattern[H.routeIndex]==1?"軸位 A":"度數 P"));if(H.routeComparisons<2)m.Add("本輪至少做兩次翻面比較");if(!Optics.CompareJCC(Truth(eye),H.lens,JCCPattern[H.routeIndex]==1).StartsWith("兩片"))m.Add("依受測者回答微調至兩片等清");}
   if(Phase==4){if(H.axisComparisons<2||H.powerComparisons<2)m.Add("完成 APA 或 PPAP 路線");if((Optics.Power(H.lens)-Optics.Power(Truth(eye))).magnitude>.19f)m.Add("複核柱面與軸位，並保持球面等效");if(jccMode!=0)m.Add("收回 JCC");if(!H.jccSaved||H.jccResult.ToString()!=H.lens.ToString())m.Add("記錄 JCC 後處方");}
   if(Phase==5){if(!H.secondFogged)m.Add("加 +0.50D 再霧視，逐格退霧");if(H.reads<2)m.Add("JCC 收回後再核對視力至少兩次");if((Optics.Power(H.lens)-Optics.Power(Truth(eye))).magnitude>.2f)m.Add("再次確認最大正度數最佳視力");if(!H.finalSaved||H.finalKey!=LensKey(eye))m.Add("記錄目前處方與最佳 VA");}
   if(stage==16){foreach(var h in eyes)if(!h.finalSaved)m.Add("雙眼最終紀錄未齊");}
   if(stage>=4&&stage<=15&&!occluded&&!m.Contains("遮蓋非測試眼"))m.Add("保持非測試眼遮蓋");
   return m;
  }
  public void RegisterJCC(){if(jccMode==1)H.axisComparisons++;else H.powerComparisons++;if(Phase==3&&jccMode==JCCPattern[H.routeIndex])H.routeComparisons++;}
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
