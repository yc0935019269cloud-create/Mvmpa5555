using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace RefractionRoom {
 public class ClinicUI : MonoBehaviour {
  public ClinicWorld world;
  public GameObject menu;
  public bool voice=true,canResume,hints=true,immersive;
  public int selectedPatient=1;
  public float toastUntil;
  Font font;
  Transform root;
  GameObject hud,station,book,pausedPanel,retPanel;
  Text task,step,patient,clock,mission,helper,context,toast,conversation,readout,retReadout,record,bookTitle,previewLabel,patientChoice;
  Button next,resume;
  List<Button> prepButtons=new List<Button>();
  List<GameObject> retControls=new List<GameObject>();
  Color pale=new Color(.93f,.94f,.88f),muted=new Color(.64f,.72f,.68f),accent=new Color(.73f,.59f,.33f),dark=new Color(.055f,.105f,.10f,.94f),lighter=new Color(.12f,.21f,.20f,.96f);
  RectTransform Rect(GameObject g,Transform parent,float x,float y,float w,float h,Vector2 anchor){g.transform.SetParent(parent,false);var rt=g.GetComponent<RectTransform>();if(!rt)rt=g.AddComponent<RectTransform>();rt.anchorMin=rt.anchorMax=anchor;rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=new Vector2(x,y);rt.sizeDelta=new Vector2(w,h);return rt;}
  GameObject Panel(string name,Transform parent,float x,float y,float w,float h,Vector2 anchor,Color color){var g=new GameObject(name,typeof(RectTransform),typeof(Image));Rect(g,parent,x,y,w,h,anchor);g.GetComponent<Image>().color=color;return g;}
  Text Txt(string name,Transform parent,string text,float x,float y,float w,float h,int size,Color color,Vector2 anchor,TextAnchor align=TextAnchor.MiddleLeft){var g=new GameObject(name,typeof(RectTransform),typeof(Text));Rect(g,parent,x,y,w,h,anchor);var t=g.GetComponent<Text>();t.font=font;t.fontSize=size;t.text=text;t.color=color;t.alignment=align;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.supportRichText=true;t.raycastTarget=false;return t;}
  Button Btn(string name,Transform parent,string text,float x,float y,float w,float h,UnityAction callback,bool main=false){var g=Panel(name,parent,x,y,w,h,new Vector2(.5f,.5f),main?accent:lighter);var b=g.AddComponent<Button>();ColorBlock cb=b.colors;cb.normalColor=Color.white;cb.highlightedColor=new Color(1.18f,1.18f,1.1f);cb.pressedColor=new Color(.7f,.8f,.73f);cb.disabledColor=new Color(.38f,.45f,.4f);b.colors=cb;b.onClick.AddListener(callback);Txt(name+" label",g.transform,text,0,0,w-12,h,18,main?dark:pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);return b;}
  public void Build(Font f,Texture scope){
   font=f;var canvas=new GameObject("Clinic UI",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;root=canvas.transform;
   if(!FindFirstObjectByType<EventSystem>()){new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));}
   menu=Panel("Welcome",root,0,0,1600,900,new Vector2(.5f,.5f),new Color(.025f,.07f,.07f,.25f));
   var card=Panel("Welcome card",menu.transform,-430,0,650,820,new Vector2(.5f,.5f),dark);
   Txt("eyebrow",card.transform,"CLINICAL PRACTICE  /  MIDTERM",0,345,550,36,16,accent,new Vector2(.5f,.5f));
   Txt("title",card.transform,"視 光 室",0,254,550,100,66,pale,new Vector2(.5f,.5f));
   Txt("subtitle",card.transform,"THE REFRACTION ROOM",0,183,550,35,24,muted,new Vector2(.5f,.5f));
   Txt("intro",card.transform,"走進診間，完成一次驗光。\n拿起工具、觀察反射、調整鏡片，\n聽見受測者眼中的清晰與模糊。",0,83,550,130,23,pale,new Vector2(.5f,.5f));
   Txt("scope",card.transform,"期中範圍 · 檢影 / 單眼自覺式驗光 / JCC\n不包含鐘面圖與期末項目",0,-12,550,64,18,muted,new Vector2(.5f,.5f));
   patientChoice=Txt("case",card.transform,"病例 01  /  林同學",0,-83,330,45,22,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   Btn("previous case",card.transform,"−",-226,-83,52,44,()=>{selectedPatient=selectedPatient==1?6:selectedPatient-1;CaseLabel();});
   Btn("next case",card.transform,"+",226,-83,52,44,()=>{selectedPatient=selectedPatient%6+1;CaseLabel();});
   Btn("tutorial",card.transform,"進入診間  /  教學",0,-152,550,58,()=>world.Begin("教學"),true);
   Btn("free",card.transform,"自由練習",-142,-218,266,50,()=>world.Begin("自由"));
   Btn("exam",card.transform,"期中考試 · 18 分鐘",142,-218,266,50,()=>{hints=false;world.Begin("考試");});
   resume=Btn("resume",card.transform,"繼續上次練習",0,-277,550,45,()=>world.Resume());
   Txt("menu help",card.transform,"WASD 移動 · 滑鼠轉頭 · E 互動 · F 坐近\nEsc 釋放滑鼠 · Tab 病歷 · 建議電腦全螢幕",0,-349,550,70,17,muted,new Vector2(.5f,.5f));
   Txt("model label",menu.transform,"UNITY × BLENDER\n第一人稱視光練習",485,-337,450,90,20,pale,new Vector2(.5f,.5f),TextAnchor.LowerRight);

   hud=new GameObject("HUD",typeof(RectTransform));var hrt=Rect(hud,root,0,0,1600,900,new Vector2(.5f,.5f));
   var top=Panel("Header",hud.transform,0,413,1540,55,new Vector2(.5f,.5f),dark);
   Txt("brand",top.transform,"視光室  /  REFRACTION",-587,0,320,50,19,pale,new Vector2(.5f,.5f));
   patient=Txt("patient",top.transform,"",-228,0,320,50,18,muted,new Vector2(.5f,.5f));
   clock=Txt("clock",top.transform,"",122,0,330,50,18,accent,new Vector2(.5f,.5f));
   Btn("notebook",top.transform,"病歷  Tab",445,0,130,38,()=>world.ToggleNotebook());
   Btn("voice",top.transform,"語音 開／關",589,0,140,38,()=>{voice=!voice;world.Toast(voice?"受測者語音已開啟。":"受測者語音已關閉。 ");});
   Btn("pause",top.transform,"Esc",703,0,60,38,()=>{world.paused=!world.paused;world.Release();});
   var objectives=Panel("Objective card",hud.transform,-598,204,340,330,new Vector2(.5f,.5f),dark);
   step=Txt("step",objectives.transform,"",0,135,300,35,16,accent,new Vector2(.5f,.5f));
   task=Txt("task",objectives.transform,"",0,83,300,70,27,pale,new Vector2(.5f,.5f));
   mission=Txt("mission",objectives.transform,"",0,-32,300,150,18,pale,new Vector2(.5f,.5f),TextAnchor.UpperLeft);
   Btn("hint",objectives.transform,"提示 開／關",0,-137,300,34,()=>{if(world.session.mode=="考試"){world.Toast("考試模式不提供提示。");return;}hints=!hints;});
   var helperCard=Panel("Reason card",hud.transform,-598,-59,340,160,new Vector2(.5f,.5f),dark);
   helper=Txt("helper",helperCard.transform,"",0,0,306,144,18,pale,new Vector2(.5f,.5f),TextAnchor.UpperLeft);
   var cross=Txt("crosshair",hud.transform,"+",0,0,30,30,24,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   context=Txt("context",hud.transform,"",0,-230,780,60,23,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   toast=Txt("toast",hud.transform,"",0,334,800,68,21,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   var bubble=Panel("Patient subtitle",hud.transform,100,-110,740,76,new Vector2(.5f,.5f),new Color(.94f,.94f,.87f,.95f));
   conversation=Txt("speech",bubble.transform,"",0,0,700,68,21,dark,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   conversation.transform.parent.gameObject.SetActive(false);
   Txt("walk help",hud.transform,"WASD 移動  ·  E 互動  ·  F 坐近  ·  C 受測者視野  ·  H 沉浸模式  ·  Tab 病歷",0,-416,1480,36,18,muted,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   station=Panel("Instrument controls",hud.transform,160,-280,1130,244,new Vector2(.5f,.5f),dark);
   readout=Txt("lens readout",station.transform,"",0,91,1085,45,21,pale,new Vector2(.5f,.5f));
   for(int i=0;i<3;i++){int p=i;Btn("parameter"+i,station.transform,new[]{"球面 S","柱面 C","軸位 A"}[i],-493+i*113,41,105,40,()=>world.SetParameter(p));}
   Btn("minus",station.transform,"−",-136,41,65,40,()=>world.Adjust(-1));Btn("plus",station.transform,"+",-64,41,65,40,()=>world.Adjust(1));
   Btn("eye",station.transform,"換 OD / OS",58,41,166,40,()=>world.SelectEye());
   Btn("occlude",station.transform,"遮／開另一眼 O",233,41,174,40,()=>world.Occlude());
   Btn("pinhole",station.transform,"PH 擺入／收回",419,41,179,40,()=>world.Pinhole());
   Btn("read",station.transform,"詢問／讀視標  空白鍵",-427,-10,244,41,()=>world.Ask(),true);
   Btn("fog",station.transform,"霧視 +1.00",-214,-10,165,41,()=>world.Fog());
   Btn("jcc",station.transform,"JCC 收／軸／度",-35,-10,183,41,()=>world.CycleJCC());
   Btn("record",station.transform,"記錄 R",130,-10,130,41,()=>world.Record());
   next=Btn("next",station.transform,"完成此步驟  Enter",358,-10,308,41,()=>world.Next(),true);
   Btn("chart E",station.transform,"E 字",-510,-63,75,39,()=>world.ChangeChart(0));
   Btn("chart RG",station.transform,"紅綠",-429,-63,75,39,()=>world.ChangeChart(1));
   Btn("chart dots",station.transform,"點陣",-348,-63,75,39,()=>world.ChangeChart(2));
   Btn("larger",station.transform,"大一列 ↑",-238,-63,132,39,()=>world.Row(-1));
   Btn("smaller",station.transform,"小一列 ↓",-97,-63,132,39,()=>world.Row(1));
   Btn("hold ret",station.transform,"取／放檢影鏡",62,-63,174,39,()=>world.HoldRet());
   Btn("WD",station.transform,"扣工作距離",244,-63,170,39,()=>world.RemoveWD());
   Btn("leave",station.transform,"回診間 F",433,-63,185,39,()=>world.Focus());
   previewLabel=Txt("station help",station.transform,"滾輪調鏡 · V 切換參數 · J 切換 JCC · 每次比較都會實際翻面",0,-105,1080,31,15,muted,new Vector2(.5f,.5f));
   var prep=Panel("Preparation",hud.transform,585,89,360,440,new Vector2(.5f,.5f),dark);
   Txt("prep title",prep.transform,"診間準備",0,193,315,43,23,pale,new Vector2(.5f,.5f));
   for(int i=0;i<9;i++){int p=i;prepButtons.Add(Btn("prep"+i,prep.transform,ClinicSession.PrepNames[i],0,149-i*39,318,34,()=>world.Prepare(p)));}
   retPanel=Panel("Retinoscope view",hud.transform,600,136,332,472,new Vector2(.5f,.5f),dark);
   Txt("ret title",retPanel.transform,"檢影鏡窺孔 · 放大觀察",0,211,306,40,18,pale,new Vector2(.5f,.5f));
   var raw=new GameObject("Pupil camera",typeof(RectTransform),typeof(RawImage));Rect(raw,retPanel.transform,0,45,290,290,new Vector2(.5f,.5f));raw.GetComponent<RawImage>().texture=scope;raw.GetComponent<RawImage>().raycastTarget=false;
   retReadout=Txt("reflex description",retPanel.transform,"",0,-129,306,55,18,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   Btn("streak minus",retPanel.transform,"光帶 −15°",-78,-175,148,35,()=>world.Streak(-1));
   Btn("streak plus",retPanel.transform,"光帶 +15°",78,-175,148,35,()=>world.Streak(1));
   Btn("sweep",retPanel.transform,"掃動 E",-78,-216,148,35,()=>world.Sweep(),true);
   Btn("work distance",retPanel.transform,"工作距離",78,-216,148,35,()=>world.WorkingDistance());

   book=Panel("Clinical notes",root,0,0,1600,900,new Vector2(.5f,.5f),new Color(.02f,.05f,.04f,.84f));
   var sheet=Panel("Paper",book.transform,0,0,1050,800,new Vector2(.5f,.5f),new Color(.94f,.92f,.84f));
   bookTitle=Txt("book title",sheet.transform,"驗 光 紀 錄",0,338,930,60,35,dark,new Vector2(.5f,.5f));
   var noteViewport=Panel("Scroll paper",sheet.transform,0,25,930,560,new Vector2(.5f,.5f),new Color(1,1,1,0));noteViewport.AddComponent<RectMask2D>();var scroll=noteViewport.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;scroll.viewport=noteViewport.GetComponent<RectTransform>();
   record=Txt("records",noteViewport.transform,"",0,0,920,1000,22,dark,new Vector2(.5f,1),TextAnchor.UpperLeft);var noteContent=record.GetComponent<RectTransform>();noteContent.pivot=new Vector2(.5f,1);noteContent.anchoredPosition=Vector2.zero;scroll.content=noteContent;
   Btn("download",sheet.transform,"下載本次紀錄",-305,-345,280,50,()=>world.Export(),true);
   Btn("close notebook",sheet.transform,"返回診間 Tab",0,-345,280,50,()=>world.ToggleNotebook());
   Btn("new session",sheet.transform,"回到選單",305,-345,280,50,()=>{world.begun=false;world.notebook=false;world.focused=false;world.Release();menu.SetActive(true);});
   pausedPanel=Panel("Pause",root,0,0,1600,900,new Vector2(.5f,.5f),new Color(.02f,.05f,.04f,.84f));
   var pcard=Panel("Pause card",pausedPanel.transform,0,0,650,390,new Vector2(.5f,.5f),dark);
   Txt("pause title",pcard.transform,"暫 停",0,120,530,60,40,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   Txt("pause help",pcard.transform,"按 Esc 或繼續練習回到診間。\nWASD 移動，F 坐近驗光儀，E 操作眼前物件。\n先走到左側工具櫃取工具，再回到受測者面前。",0,10,550,135,21,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   Btn("continue",pcard.transform,"繼續練習",0,-115,530,60,()=>{world.paused=false;world.Lock();},true);
   Refresh();
  }
  void CaseLabel(){var s=new ClinicSession();s.patient=selectedPatient;patientChoice.text="病例 "+selectedPatient.ToString("00")+"  /  "+s.PatientName;}
  public void Refresh(){if(!world||!root)return;var s=world.session;bool play=world.begun;hud.SetActive(play);book.SetActive(play&&world.notebook);pausedPanel.SetActive(play&&world.paused);resume.interactable=canResume;
   if(!play)return;
   task.text=s.StageTitle;step.text="STEP "+(s.stage+1).ToString("00")+" / 17  ·  "+s.mode;
   task.transform.parent.gameObject.SetActive(!immersive);
   patient.text=s.PatientName+" · 病例 "+s.patient.ToString("00")+" · "+s.EyeName;
   int sec=Mathf.FloorToInt(s.mode=="考試"?Mathf.Max(0,1080-s.seconds):s.seconds);clock.text=(s.mode=="考試"?"剩餘 ":"練習 ")+string.Format("{0:00}:{1:00}",sec/60,sec%60)+"  /  六公尺視標";
   var missing=s.Missing();mission.text=s.mode=="考試"?"依期中檢查流程獨立操作。\n完成時按 Enter 前往下一階段。":missing.Count==0?"✓ 這一步的條件已完成。\n按 Enter 前往下一步。":string.Join("\n",missing.GetRange(0,Mathf.Min(4,missing.Count)).ConvertAll(x=>"· "+x));
   next.interactable=!world.busy&&(missing.Count==0||s.mode=="自由"||s.mode=="考試");
   bool showHints=hints&&s.mode!="考試";helper.transform.parent.gameObject.SetActive(showHints&&!immersive);helper.text=showHints?Hint():"";
   context.text=!world.focused&&!world.patientView?(world.focusLabel!=""?"E  "+world.focusLabel:"走近工具或受測者 · F 坐近驗光儀"):world.patientView?"受測者視野（模糊示意） · C 返回":"";
   toast.text=Time.time<toastUntil?world.lastAction:"";
   conversation.transform.parent.gameObject.SetActive(Time.time<world.speechUntil);conversation.text="受測者  /  "+world.speech;
   station.SetActive(world.focused&&!world.patientView&&!world.notebook&&!world.paused&&!immersive);
   if(immersive&&world.focused&&!world.patientView)context.text=s.EyeName+" "+s.H.lens+"  ·  滾輪調鏡 / V 切換參數 / H 工具列";
   readout.text=s.EyeName+"  "+s.H.lens+"    PD "+s.pd+" mm   VA 列 "+s.row.ToString("0.0")+"    "+new[]{"◀ 球面","◀ 柱面","◀ 軸位"}[world.parameter];
   var prep=prepButtons[0].transform.parent.gameObject;prep.SetActive(s.stage==0&&!world.notebook&&!world.paused&&!immersive);
   for(int i=0;i<9;i++)prepButtons[i].GetComponentInChildren<Text>().text=(s.prepared[i]?"✓  ":"")+ClinicSession.PrepNames[i];
   retPanel.SetActive(world.focused&&world.held==1&&!world.patientView&&!world.notebook&&!world.paused);
   float r=Optics.RetResidual(s.Truth(s.eye),s.H.lens,Optics.WD(s.wdCm),s.streak);
   retReadout.text="光帶 "+s.streak.ToString("0")+"° · WD "+s.wdCm+"cm\n"+(showHints?(Mathf.Abs(r)<.125f?"中和":r<0?"順動 → 加正鏡":"逆動 → 加負鏡"):"觀察反射光的移動方向");
   if(world.notebook)Records();
  }
  string Hint(){var s=world.session;if(s.mode=="自由")return "自由練習參考（近似病例）\nOD "+s.Truth(0)+"\nOS "+s.Truth(1)+"\nPD "+s.TruePD+" mm";
   if(s.stage==0)return "先走近工具櫃取得棉布、PD 尺；也可 F 坐近後逐項操作。受測者眼睛需對準窺孔。";
   if(s.stage==1||s.stage==2)return "拿起檢影鏡 → F 坐近。\n順動加正、逆動加負。\n旋轉光帶找主子午線；中和後記錄。檢查軸位與柱面。";
   if(s.stage==3)return "Gross − 1 / 工作距離（公尺）= Net。只扣球面，柱面與軸位不變。扣完按 R 記錄。";
   if(s.Phase==0)return "此階段先回到平光測裸眼 VA。遮另一眼，換大小列找到讀得出的最小一列；VA < 0.8 再轉 PH 讀一次，最後收回。";
   if(s.Phase==1)return "以淨檢影值起始。先 +1.00 霧視，再逐格減正並讀表；視力不再改善時保留最多正度數。";
   if(s.Phase==2)return "比較紅綠的字是否一樣清楚。紅清加負，綠清加正。重看視力，不追求顏色亮度。";
   if(s.Phase==3)return "本眼路線 "+s.H.route+"："+(s.H.route=="APA"?"軸 → 度數 → 軸":"初步度數 → 複核度數 → 軸 → 度數")+"。\n本輪 "+(s.JCCPattern[s.H.routeIndex]==1?"A：負柱面軸朝較清楚面的紅點移。":"P：紅加白減；每 -0.50DC 補 +0.25DS。")+"\n等清並比較至少兩次後按 Enter。";
   if(s.Phase==4)return "APA／PPAP 路線完成。收回 JCC，按 R 記錄 JCC 後處方，再做第二次 MPMVA。";
   if(s.Phase==5)return "收回 JCC，切回 E 字，先 +0.50D 再霧視並逐格退霧，確認最大正度數最佳視力。按 R 記錄，改鏡後須重記。";
   return "雙眼流程完成後，按 Enter 完成並保存本次紀錄。";
  }
  void Records(){var s=world.session;bookTitle.text=s.completed?"本 次 練 習 回 饋":"驗 光 紀 錄  /  "+s.PatientName;string text="病例 "+s.patient.ToString("00")+"    模式 "+s.mode+"    PD "+s.pd+"mm    WD "+s.wdCm+"cm\n\n";
   for(int e=0;e<2;e++){var h=s.eyes[e];text+=(e==0?"右眼 OD":"左眼 OS")+"\n";
    text+="裸眼 VA  "+(h.vaSaved?h.measuredVA.ToString("0.0"):"未測")+"     PH  "+(h.phRead?h.phVA.ToString("0.0"):"未補測")+"\n";
    text+="Gross 檢影  "+(h.grossSaved?h.gross.ToString():"待記錄")+"\n";
    text+="Net 檢影      "+(h.netSaved?h.net.ToString():"待記錄")+"\n";
    text+="1st MPMVA   "+(h.mp1Saved?h.mp1.ToString():"待記錄")+"\n";
    text+="JCC "+h.route+"       "+(h.jccSaved?h.jccResult.ToString():"待記錄")+"\n";
    text+="最終處方      "+(h.finalSaved?h.final.ToString():"待記錄")+"    VA "+h.finalVA.ToString("0.0")+"\n";
    if(h.finalSaved&&h.finalKey!=s.LensKey(e))text+="＊鏡片已變更，需重新確認紀錄。\n";text+="\n";}
   if(s.completed){text+="流程回饋  "+s.Score()+" / 100    ·    "+s.events.Count+" 次操作\n";text+="參考病例：OD "+s.Truth(0)+"  /  OS "+s.Truth(1)+"\n";if(s.examErrors.Count>0)text+="待改進項目\n"+string.Join("\n",s.examErrors)+"\n";}
   text+="\n本次只涵蓋期中：檢影、單眼自覺式驗光、JCC。\n受測者回答及模糊是教學近似模型；此紀錄不是臨床處方。";record.text=text;var rt=record.GetComponent<RectTransform>();rt.sizeDelta=new Vector2(rt.sizeDelta.x,Mathf.Max(560,record.preferredHeight+24));
  }
 }
}
