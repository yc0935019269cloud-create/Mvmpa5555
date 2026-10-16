using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RefractionRoom {
 // Next-step coach for the practice modes (教學 / 自由, hints on): a gold ring, a bobbing arrow and a short
 // "how" label sit on the exact button or instrument part to use next, with a moving fingertip dot when the
 // action is a drag. It is derived from the session state each time, so it follows whatever the student did.
 // The ring points at WHAT to operate, never at the answer; exam mode has none.
 public partial class ClinicUI {
  class Coach {public string ui,wid,label;public int kind;public bool tagOnly;}   // kind 0 tap/click, 1 drag round a dial, 2 drag a sweep
  Coach coachNow;float coachNext;
  GameObject coachFrame,coachArrow,coachDot,coachTag;Text coachText;

  void BuildCoach(){
   if(coachFrame)return;
   coachFrame=new GameObject("Coach frame",typeof(RectTransform),typeof(Image));coachFrame.transform.SetParent(hud.transform,false);
   var fi=coachFrame.GetComponent<Image>();fi.sprite=RingSprite();fi.type=Image.Type.Sliced;fi.color=new Color(1,.83f,.35f,1);fi.raycastTarget=false;
   coachArrow=new GameObject("Coach arrow",typeof(RectTransform),typeof(Image));coachArrow.transform.SetParent(hud.transform,false);
   var ai=coachArrow.GetComponent<Image>();ai.sprite=ArrowSprite();ai.color=new Color(1,.83f,.35f,1);ai.raycastTarget=false;((RectTransform)coachArrow.transform).sizeDelta=new Vector2(54,66);
   var sh=coachArrow.AddComponent<Shadow>();sh.effectColor=new Color(0,0,0,.6f);sh.effectDistance=new Vector2(3,-4);
   coachDot=Panel("Coach finger",hud.transform,0,0,34,34,new Vector2(.5f,.5f),new Color(1,.93f,.6f,.92f));coachDot.GetComponent<Image>().raycastTarget=false;
   coachTag=Panel("Coach tag",hud.transform,0,0,300,44,new Vector2(.5f,.5f),new Color(.03f,.07f,.075f,.95f));coachTag.GetComponent<Image>().raycastTarget=false;
   coachText=Txt("Coach text",coachTag.transform,"",0,0,290,44,19,new Color(1,.9f,.6f,1),new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   coachFrame.SetActive(false);coachArrow.SetActive(false);coachDot.SetActive(false);coachTag.SetActive(false);
  }
  void CoachOff(){if(coachFrame){coachFrame.SetActive(false);coachArrow.SetActive(false);coachDot.SetActive(false);coachTag.SetActive(false);}}

  void CoachUpdate(){
   if(coachFrame==null)return;
   bool on=world&&world.begun&&hints&&world.session.mode!="考試"&&!guideOn&&!world.paused&&!world.notebook&&!world.cinematic&&!world.patientView&&!immersive&&!menu.activeSelf;
   if(!on){CoachOff();return;}
   if(Time.unscaledTime>coachNext){coachNext=Time.unscaledTime+.2f;coachNow=CoachCompute();}
   var c=coachNow;if(c==null){CoachOff();return;}
   // where is it on screen?
   Vector2? at=null;Vector2 size=new Vector2(90,90);var hr=(RectTransform)hud.transform;
   RectTransform rt=string.IsNullOrEmpty(c.ui)?null:FindUI(c.ui);
   if(c.tagOnly){}
   else if(rt&&rt.gameObject.activeInHierarchy){Vector3[] k=new Vector3[4];rt.GetWorldCorners(k);Vector2 a,b;RectTransformUtility.ScreenPointToLocalPointInRectangle(hr,k[0],null,out a);RectTransformUtility.ScreenPointToLocalPointInRectangle(hr,k[2],null,out b);at=(a+b)/2;size=new Vector2(Mathf.Abs(b.x-a.x),Mathf.Abs(b.y-a.y))+new Vector2(22,22);}
   else if(!string.IsNullOrEmpty(c.wid)){Vector2 sp;float rpx;
    if(world.ControlScreen(c.wid,out sp,out rpx)&&sp.x>0&&sp.x<Screen.width&&sp.y>0&&sp.y<Screen.height){Vector2 l;RectTransformUtility.ScreenPointToLocalPointInRectangle(hr,sp,null,out l);at=l;float scale=hr.rect.width/Mathf.Max(1,Screen.width);float d=Mathf.Clamp(rpx*2*scale*1.6f,84,200);size=new Vector2(d,d);}}
   if(!at.HasValue){
    // target is off-screen: keep the instruction readable at the top
    coachFrame.SetActive(false);coachArrow.SetActive(false);coachDot.SetActive(false);
    coachText.text=c.label;var tr=(RectTransform)coachTag.transform;tr.sizeDelta=new Vector2(Mathf.Min(900,coachText.preferredWidth+36),44);((RectTransform)coachText.transform).sizeDelta=new Vector2(tr.sizeDelta.x-10,44);
    tr.anchoredPosition=new Vector2(0,hr.rect.height/2-(c.tagOnly?110:300));coachTag.SetActive(true);coachTag.transform.SetAsLastSibling();return;}
   var p=at.Value;var half=hr.rect.size/2;
   coachFrame.SetActive(true);coachArrow.SetActive(true);coachTag.SetActive(true);
   var fr=(RectTransform)coachFrame.transform;fr.anchoredPosition=p;fr.sizeDelta=size+new Vector2(14,14)*(1+.1f*Mathf.Sin(Time.unscaledTime*6));
   bool below=p.y>half.y-190;float bob=Mathf.Sin(Time.unscaledTime*6)*8;
   var ar=(RectTransform)coachArrow.transform;ar.localRotation=Quaternion.Euler(0,0,below?180:0);ar.anchoredPosition=p+new Vector2(0,below?-(size.y/2+26+bob):size.y/2+26+bob);
   coachText.text=c.label;var tg=(RectTransform)coachTag.transform;float w=Mathf.Min(hr.rect.width-40,coachText.preferredWidth+36);tg.sizeDelta=new Vector2(w,44);((RectTransform)coachText.transform).sizeDelta=new Vector2(w-10,44);
   // label on the far side of the ring from the arrow
   float ty=below?p.y+size.y/2+32:p.y-size.y/2-32;
   float tx=p.x;if(p.x>half.x-w/2-60)tx=p.x-size.x/2-w/2-12;   // docked buttons at the right edge: tag goes to their left
   if(tx!=p.x)ty=p.y;
   tg.anchoredPosition=new Vector2(Mathf.Clamp(tx,-half.x+w/2+12,half.x-w/2-12),Mathf.Clamp(ty,-half.y+30,half.y-30));
   // fingertip: circles the dial for a rotate, slides back and forth for a sweep
   bool dot=c.kind!=0;coachDot.SetActive(dot);
   if(dot){var dr=(RectTransform)coachDot.transform;float t=Time.unscaledTime;
    if(c.kind==1){float r=Mathf.Max(24,size.x*.3f);dr.anchoredPosition=p+new Vector2(Mathf.Cos(t*3.2f),Mathf.Sin(t*3.2f))*r;}
    else{float ang=(world.session.streak+90)*Mathf.Deg2Rad;dr.anchoredPosition=p+new Vector2(Mathf.Cos(ang),Mathf.Sin(ang))*Mathf.Sin(t*2.6f)*Mathf.Min(size.x,size.y)*.28f;}}
   coachFrame.transform.SetAsLastSibling();coachArrow.transform.SetAsLastSibling();coachDot.transform.SetAsLastSibling();coachTag.transform.SetAsLastSibling();
  }

  Coach C(string ui,string wid,string label,int kind=0){return new Coach{ui=ui,wid=wid,label=label,kind=kind};}
  string KnobWord(string what){return world.touchMode?"手指按住"+what+"，沿圓周拖曳":"指向"+what+"，滾輪轉動";}

  // The next thing to do, from the session state. ui = HUD element to ring (if it is on screen), wid = 3D control id otherwise.
  Coach CoachCompute(){
   var s=world.session;int st=s.stage,ph=s.Phase,e=s.eye;string es=e.ToString();bool touch=world.touchMode;
   if(world.pdStep>=0){
    switch(world.pdStep){
     case 0:return C("pd say",null,"點這裡：請他看你的眼睛");
     case 1:return C("pd right",null,"點這裡：閉上你的右眼");
     case 2:return new Coach{label="按住畫面左右拖曳，移動 PD 尺讓 0 對準瞳孔邊緣，再按確認",tagOnly=true};
     case 3:return C("pd left",null,"點這裡：換閉左眼");
     default:return new Coach{label="左右拖曳選讀數（鼻側邊緣對到的刻度），再按確認",tagOnly=true};
    }
   }
   Coach focus=touch?C("t focus",null,"點這裡：坐近驗光儀"):C(null,"phoropter","走近受測者，按 F 坐近驗光儀");
   Coach next=C("next",null,"點這裡：完成此步驟，進下一步");
   Coach rec=C("record",null,"點這裡：記錄目前度數");
   Coach read=C("read",null,"點這裡：詢問受測者");
   if(st==0){
    int first=-1;for(int i=0;i<9;i++)if(!s.prepared[i]){first=i;break;}
    if(first<0)return world.focused?next:focus;
    if(first>=2&&first!=8&&!world.focused)return focus;
    return C("prep"+first,null,"點這裡："+ClinicSession.PrepNames[first]);
   }
   if(!world.focused)return focus;
   if(st>=4&&st<=15&&!s.occluded)return C("occlude","aux:"+(1-e),"點一下：遮蓋另一眼 (OC)");
   var truth=s.Truth(e);Vector3 diff=Optics.Power(s.H.lens)-Optics.Power(truth);
   Coach sph=C(null,"sph:"+es,KnobWord("球面大轉盤"),1),cyl=C(null,"cyl:"+es,KnobWord("柱面度數內鈕"),1),axis=C(null,"axis:"+es,KnobWord("軸位外圈"),1);
   if(st==1||st==2){
    if(world.held!=1)return C("hold ret",null,"點這裡：拿起檢影鏡");
    if(s.H.sweeps<2){
     if(s.H.sweeps>=1&&Optics.AxisError(s.H.lastStreak,s.streak)<=25)return C("streak 90",null,"點這裡：換另一個方向再掃");
     return C("Retinoscope view",null,"在這裡拖曳，掃動光帶",2);
    }
    if(!Optics.Neutral(truth,s.H.lens,Optics.WD(s.wdCm))){
     float dx=diff.x-Optics.WD(s.wdCm);
     if(Mathf.Abs(dx)>=.2f)return sph;
     return Mathf.Abs(s.H.lens.c-truth.c)>=.2f?cyl:axis;
    }
    if(!s.H.grossSaved||s.H.gross.ToString()!=s.H.lens.ToString())return rec;
    return next;
   }
   if(st==3){
    if(!s.wdRemoved)return C("WD",null,"點這裡：扣除工作距離");
    if(!s.eyes[0].netSaved||!s.eyes[1].netSaved)return rec;
    return next;
   }
   bool asked=world.AskedCurrent();
   Coach lensAdj=Mathf.Abs(diff.x)>=.12f?sph:(Mathf.Abs(s.H.lens.c-truth.c)>=.2f?cyl:axis);
   if(ph==0){
    if(s.ph&&s.H.phRead)return C("pinhole","aux:"+es,"點一下：針孔轉回 O");
    if(s.H.reads<2)return asked?C("Projector remote",null,"遙控器：換一列視標，再按詢問"):read;
    if(s.H.measuredVA<.8f&&!s.H.phRead)return !s.ph?C("pinhole","aux:"+es,"點一下：針孔 PH，再問一次"):read;
    if(!s.H.vaSaved)return rec;
    return next;
   }
   if(ph==1){
    if(!s.H.fogged)return C("fog",null,"點這裡：先霧視 +1.00");
    if(diff.x>.38f||diff.x<-.13f)return asked?sph:read;
    if(s.H.reads<2)return read;
    if(!s.H.mp1Saved||s.H.mp1.ToString()!=s.H.lens.ToString())return rec;
    return next;
   }
   if(ph==2){
    if(!s.H.duoAsked)return s.chart!=1?C("remote chart 1",null,"遙控器：切到紅綠視標"):C("read",null,"點這裡：詢問紅綠哪邊清楚");
    if(Mathf.Abs(diff.x)>.13f)return asked?sph:C("read",null,"點這裡：再問紅綠");
    return next;
   }
   if(ph==3){
    int want=s.Searching?2:s.JCCPattern[s.H.routeIndex];
    if(s.jccMode!=want)return C("jcc","jcc:"+es,touch?"點這裡：放入 JCC（"+(want==1?"A":"P")+"）":"點 JCC 鏡片，切到 "+(want==1?"A":"P"));
    Coach flip=C("read",null,s.H.routeComparisons>=1&&!s.Searching?"點這裡：翻面再問一次":"點這裡：翻面詢問（兩面哪個清楚）");
    if(s.Searching){
     int seen=0;for(int k=0;k<4;k++)if((s.H.searchSeen&(1<<k))!=0)seen++;
     bool cur=(s.H.searchSeen&(1<<ClinicSession.SearchBucket(s.H.searchAngle)))!=0;
     if(!cur)return flip;
     if(seen<4)return C(null,"axis:"+es,KnobWord("軸位外圈")+"，轉 45° 換紅點方向",1);
     foreach(var m in s.Missing())if(m.StartsWith("受測者在")||m.StartsWith("同軸")||m.StartsWith("垂直"))return asked?cyl:flip;
     return next;
    }
    if(s.H.routeComparisons<2||!asked)return flip;
    bool same=Optics.CompareJCC(truth,s.H.lens,want==1).StartsWith("兩片");
    if(!same)return want==1?axis:cyl;
    return next;
   }
   if(ph==4){
    if(s.jccMode!=0)return touch?C("jcc","jccarm:"+es,"點這裡：JCC 切到「收」"):C(null,"jccarm:"+es,"按住 JCC 支桿，拖曳收回");
    if(!s.H.jccSaved||s.H.jccResult.ToString()!=s.H.lens.ToString())return rec;
    return next;
   }
   if(ph==5){
    if(!s.H.secondFogged)return C("fog",null,"點這裡：先霧視 +0.50");
    if(diff.magnitude>.2f)return asked?lensAdj:read;
    if(s.H.reads<2)return read;
    if(!s.H.finalSaved||s.H.finalKey!=s.LensKey(e))return rec;
    return next;
   }
   return next;
  }
 }
}
