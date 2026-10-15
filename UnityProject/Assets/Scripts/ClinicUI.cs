using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace RefractionRoom {
 public partial class ClinicUI : MonoBehaviour {
  public ClinicWorld world;
  public GameObject menu;
  public bool voice=true,canResume,hints=true,immersive;
  public int selectedPatient=1;
  public float toastUntil;
  Font font;
  Transform root;
  RawImage chartRaw;Text chartTitle;
  GameObject remotePanel; Text remoteStatus;
  GameObject chartPanel,hud,station,book,pausedPanel,retPanel,pdPanel,tipBox,crossObj,touchPad,stickObj,walkButtons,knobBar,pdButtons,shadeL,shadeR;
  public ClinicStick stick;CanvasScaler scaler;Text knobLabel,wdText,wdPrepText,wdHint,sweepText;Button wdChip;GameObject wdPicker;List<Button> wdOpts=new List<Button>();RectTransform sweepAxis,sweepDot;
  Text task,step,patient,clock,mission,helper,context,toast,conversation,readout,retReadout,record,bookTitle,previewLabel,patientChoice,tip,pdText,pdReading;
  Button next,resume;Text volumeLabel;Slider musicSlider;GameObject abandonConfirm;
  List<Button> prepButtons=new List<Button>();
  List<GameObject> retControls=new List<GameObject>();
  Color pale=new Color(.95f,.96f,.92f),muted=new Color(.66f,.75f,.72f),accent=new Color(.88f,.71f,.40f),dark=new Color(.045f,.085f,.09f,.88f),lighter=new Color(.15f,.25f,.26f,.97f);
  static readonly string[] Striped={"Objective card","Reason card","Preparation","Chart view","Retinoscope view","PD measurement","Pause card","Welcome card"};
  static Sprite roundSprite;
  static Sprite Round(){if(roundSprite)return roundSprite;int n=64;var t=new Texture2D(n,n,TextureFormat.RGBA32,false);t.wrapMode=TextureWrapMode.Clamp;var px=new Color32[n*n];for(int y=0;y<n;y++)for(int x=0;x<n;x++){float dx=Mathf.Max(0,Mathf.Abs(x+.5f-32)-1),dy=Mathf.Max(0,Mathf.Abs(y+.5f-32)-1);float d=Mathf.Sqrt(dx*dx+dy*dy);byte a=(byte)(Mathf.Clamp01(30.5f-d)*255);px[y*n+x]=new Color32(255,255,255,a);}t.SetPixels32(px);t.Apply();roundSprite=Sprite.Create(t,new Rect(0,0,n,n),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(31,31,31,31));return roundSprite;}
  RectTransform Rect(GameObject g,Transform parent,float x,float y,float w,float h,Vector2 anchor){g.transform.SetParent(parent,false);var rt=g.GetComponent<RectTransform>();if(!rt)rt=g.AddComponent<RectTransform>();rt.anchorMin=rt.anchorMax=anchor;rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=new Vector2(x,y);rt.sizeDelta=new Vector2(w,h);return rt;}
  GameObject Panel(string name,Transform parent,float x,float y,float w,float h,Vector2 anchor,Color color){var g=new GameObject(name,typeof(RectTransform),typeof(Image));Rect(g,parent,x,y,w,h,anchor);var img=g.GetComponent<Image>();img.color=color;
   bool fullScreen=w>=1600||h>=900;
   if(!fullScreen&&color.a>.01f){float r=Mathf.Min(w>=300&&h>=150?16:10,Mathf.Min(w,h)*.5f);img.sprite=Round();img.type=Image.Type.Sliced;img.pixelsPerUnitMultiplier=31f/Mathf.Max(1,r);
    if(color.a>.6f&&w>=60&&h>=34){var sh=g.AddComponent<Shadow>();sh.effectColor=new Color(0,0,0,w>=300?.45f:.3f);sh.effectDistance=new Vector2(0,w>=300?-6:-3);}
    if(System.Array.IndexOf(Striped,name)>=0){var st=new GameObject(name+" accent",typeof(RectTransform),typeof(Image));Rect(st,g.transform,0,h*.5f-3,w-36,3,new Vector2(.5f,.5f));st.GetComponent<Image>().color=new Color(accent.r,accent.g,accent.b,.85f);st.GetComponent<Image>().raycastTarget=false;}}
   return g;}
  Text Txt(string name,Transform parent,string text,float x,float y,float w,float h,int size,Color color,Vector2 anchor,TextAnchor align=TextAnchor.MiddleLeft){var g=new GameObject(name,typeof(RectTransform),typeof(Text));Rect(g,parent,x,y,w,h,anchor);var t=g.GetComponent<Text>();t.font=font;t.fontSize=size;t.text=text;t.color=color;t.alignment=align;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.supportRichText=true;t.raycastTarget=false;return t;}
  Button Btn(string name,Transform parent,string text,float x,float y,float w,float h,UnityAction callback,bool main=false){var g=Panel(name,parent,x,y,w,h,new Vector2(.5f,.5f),main?accent:lighter);var b=g.AddComponent<Button>();ColorBlock cb=b.colors;cb.normalColor=Color.white;cb.highlightedColor=new Color(1.22f,1.2f,1.12f);cb.pressedColor=new Color(.75f,.82f,.78f);cb.fadeDuration=.08f;cb.disabledColor=new Color(.38f,.45f,.4f);b.colors=cb;b.onClick.AddListener(callback);b.onClick.AddListener(()=>NoteClick(name));var lt=Txt(name+" label",g.transform,text,0,0,w-12,h,18,main?new Color(.05f,.06f,.06f,1):pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);if(main)lt.fontStyle=FontStyle.Bold;return b;}
  public void Build(Font f,Texture scope){
   font=f;var canvas=new GameObject("Clinic UI",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;root=canvas.transform;
   if(!FindFirstObjectByType<EventSystem>()){new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));}
   menu=Panel("Welcome",root,0,0,1600,900,new Vector2(.5f,.5f),new Color(.025f,.07f,.07f,.25f));
   var card=Panel("Welcome card",menu.transform,-430,0,650,820,new Vector2(.5f,.5f),dark);
   Txt("eyebrow",card.transform,"CLINICAL PRACTICE  /  MIDTERM",0,345,550,36,16,accent,new Vector2(.5f,.5f));
   Txt("title",card.transform,"視 光 室",0,254,550,100,66,pale,new Vector2(.5f,.5f));
   Txt("subtitle",card.transform,"THE REFRACTION ROOM",0,183,550,35,24,muted,new Vector2(.5f,.5f));
   Txt("intro",card.transform,"走進診間，完成一次驗光。\n拿起工具、觀察反射、調整鏡片，\n聽見受測者眼中的清晰與模糊。",0,83,550,130,23,pale,new Vector2(.5f,.5f));
   Txt("scope",card.transform,"期中範圍 · 檢影 / 單眼自覺式驗光 / JCC\n不包含鐘面圖與期末項目",0,-12,550,64,18,muted,new Vector2(.5f,.5f));
   patientChoice=Txt("case",card.transform,"病例 01  /  林同學",0,-83,330,45,22,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   Btn("previous case",card.transform,"−",-226,-83,52,44,()=>{selectedPatient=selectedPatient==0?6:selectedPatient-1;CaseLabel();});
   Btn("next case",card.transform,"+",226,-83,52,44,()=>{selectedPatient=selectedPatient==6?0:selectedPatient+1;CaseLabel();});
   Btn("tutorial",card.transform,"進入診間  /  教學",0,-152,550,58,()=>world.Begin("教學"),true);
   Btn("free",card.transform,"自由練習",-142,-218,266,50,()=>world.Begin("自由"));
   Btn("exam",card.transform,"期中考試 · 18 分鐘",142,-218,266,50,()=>{hints=false;world.Begin("考試");});
   Btn("guide start",card.transform,"新手操作教學",-142,-277,266,45,()=>StartGuide(),true);
   resume=Btn("resume",card.transform,"繼續上次練習",142,-277,266,45,()=>world.Resume());
   Txt("menu help",card.transform,"WASD 移動 · 滑鼠轉頭 · E 互動 · F 坐近\nEsc 釋放滑鼠 · Tab 病歷 · 建議電腦全螢幕",0,-349,550,70,17,muted,new Vector2(.5f,.5f));
   Txt("model label",menu.transform,"UNITY × BLENDER\n第一人稱視光練習",485,-337,450,90,20,pale,new Vector2(.5f,.5f),TextAnchor.LowerRight);

   hud=new GameObject("HUD",typeof(RectTransform));var hrt=Rect(hud,root,0,0,1600,900,new Vector2(.5f,.5f));
   touchPad=Panel("Touch pad",hud.transform,0,0,4000,3000,new Vector2(.5f,.5f),new Color(0,0,0,0));touchPad.AddComponent<ClinicTouchPad>().world=world;
   var top=Panel("Header",hud.transform,0,413,1540,55,new Vector2(.5f,.5f),dark);
   Txt("brand",top.transform,"視光室  /  REFRACTION",-587,0,320,50,19,pale,new Vector2(.5f,.5f));
   patient=Txt("patient",top.transform,"",-250,0,320,50,17,muted,new Vector2(.5f,.5f));
   clock=Txt("clock",top.transform,"",232,0,190,50,18,accent,new Vector2(.5f,.5f));
   // always-visible working-distance selector (opens a picker with every option)
   wdChip=Btn("wd chip",top.transform,"",12,0,190,38,()=>{wdPicker.SetActive(!wdPicker.activeSelf);},true);
   wdPicker=Panel("WD picker",hud.transform,0,300,560,170,new Vector2(.5f,.5f),dark);
   Txt("wd picker title",wdPicker.transform,"檢影工作距離（手臂到病人眼睛的距離）",0,58,520,32,18,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   for(int i=0;i<ClinicWorld.WDOptions.Length;i++){float cm=ClinicWorld.WDOptions[i];var b=Btn("wd opt "+cm,wdPicker.transform,cm+" cm",-195+(i%4)*130,12-(i/4)*48,120,40,()=>{world.SetWD(cm);wdPicker.SetActive(false);});wdOpts.Add(b);}
   wdHint=Txt("wd picker hint",wdPicker.transform,"",0,-72,520,26,14,muted,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   wdPicker.SetActive(false);
   Btn("notebook",top.transform,"病歷  Tab",445,0,130,38,()=>world.ToggleNotebook());
   Btn("voice",top.transform,"語音 開／關",589,0,140,38,()=>{voice=!voice;world.Toast(voice?"受測者語音已開啟。":"受測者語音已關閉。 ");});
   Btn("pause",top.transform,"Esc",703,0,60,38,()=>world.TogglePause());
   var objectives=Panel("Objective card",hud.transform,-598,204,340,330,new Vector2(.5f,.5f),dark);
   step=Txt("step",objectives.transform,"",0,135,300,35,16,accent,new Vector2(.5f,.5f));
   task=Txt("task",objectives.transform,"",0,83,300,70,27,pale,new Vector2(.5f,.5f));
   mission=Txt("mission",objectives.transform,"",0,-32,300,150,18,pale,new Vector2(.5f,.5f),TextAnchor.UpperLeft);
   Btn("hint",objectives.transform,"提示 開／關",0,-137,300,34,()=>{if(world.session.mode=="考試"){world.Toast("考試模式不提供提示。");return;}hints=!hints;});
   var helperCard=Panel("Reason card",hud.transform,-598,-59,340,160,new Vector2(.5f,.5f),dark);
   helper=Txt("helper",helperCard.transform,"",0,0,306,144,18,pale,new Vector2(.5f,.5f),TextAnchor.UpperLeft);
   var cross=Txt("crosshair",hud.transform,"+",0,0,30,30,24,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);crossObj=cross.gameObject;
   context=Txt("context",hud.transform,"",0,-230,780,60,23,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   toast=Txt("toast",hud.transform,"",0,334,800,68,21,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   var bubble=Panel("Patient subtitle",hud.transform,100,-110,740,76,new Vector2(.5f,.5f),new Color(.94f,.94f,.87f,.95f));
   bubble.GetComponent<Image>().raycastTarget=false;
   conversation=Txt("speech",bubble.transform,"",0,0,700,68,21,dark,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   conversation.transform.parent.gameObject.SetActive(false);
   Txt("walk help",hud.transform,"WASD 移動  ·  E 互動  ·  F 坐近  ·  C 受測者視野  ·  H 沉浸模式  ·  Tab 病歷",0,-416,1480,36,18,muted,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   station=Panel("Instrument controls",hud.transform,160,-280,1130,244,new Vector2(.5f,.5f),dark);
   readout=Txt("lens readout",station.transform,"",0,91,1085,45,21,pale,new Vector2(.5f,.5f));
   for(int i=0;i<3;i++){int p=i;Btn("parameter"+i,station.transform,new[]{"球面 S","柱面 C","軸位 A"}[i],-493+i*113,41,105,40,()=>world.SetParameter(p));}
   Btn("minus",station.transform,"−",-136,41,65,40,()=>world.Adjust(-1));Btn("plus",station.transform,"+",-64,41,65,40,()=>world.Adjust(1));
   Btn("eye",station.transform,"換 OD / OS",58,41,166,40,()=>world.SelectEye());
   Btn("occlude",station.transform,"另一眼 OC  O",233,41,174,40,()=>world.Occlude());
   Btn("pinhole",station.transform,"針孔 PH  P",419,41,179,40,()=>world.Pinhole());
   Btn("read",station.transform,"詢問／讀視標  空白鍵",-427,-10,244,41,()=>world.Ask(),true);
   Btn("fog",station.transform,"霧視 +1.00",-214,-10,165,41,()=>world.Fog());
   Btn("jcc",station.transform,"JCC 收／A／P  J",-35,-10,183,41,()=>world.CycleJCC());
   Btn("record",station.transform,"記錄 R",130,-10,130,41,()=>world.Record());
   next=Btn("next",station.transform,"完成此步驟  Enter",358,-10,308,41,()=>world.Next(),true);
   Btn("chart E",station.transform,"E 字",-510,-63,75,39,()=>world.ChangeChart(0));
   Btn("chart RG",station.transform,"紅綠",-429,-63,75,39,()=>world.ChangeChart(1));
   Btn("chart dots",station.transform,"點陣",-348,-63,75,39,()=>world.ChangeChart(2));
   Btn("larger",station.transform,"大一列 ↑",-238,-63,132,39,()=>world.Row(-1));
   Btn("smaller",station.transform,"小一列 ↓",-97,-63,132,39,()=>world.Row(1));
   Btn("hold ret",station.transform,"取／放檢影鏡",62,-63,174,39,()=>world.HoldRet());
   Btn("WD",station.transform,"扣工作距離 WD",244,-63,170,39,()=>world.RemoveWD());
   Btn("leave",station.transform,"回診間 F",433,-63,185,39,()=>world.Focus());
   previewLabel=Txt("station help",station.transform,"直接操作：滑鼠指向儀器上的旋鈕，滾輪轉動、左鍵＋右鍵−（外側大轉盤＝球面、下方內鈕＝柱面、外圈＝軸位、上方小鈕＝輔助鏡片）",0,-105,1080,31,15,muted,new Vector2(.5f,.5f));
   remotePanel=Panel("Projector remote",hud.transform,-670,-220,190,400,new Vector2(.5f,.5f),new Color(.72f,.67f,.67f,1));
   var face=Panel("Remote inset",remotePanel.transform,0,12,166,342,new Vector2(.5f,.5f),new Color(.13f,.14f,.14f,1));
   remoteStatus=Txt("Remote display",face.transform,"",0,140,150,45,14,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   string[] charts={"E 字","紅綠","點陣"};
   for(int i=0;i<3;i++){int c=i;var b=Btn("remote chart "+i,face.transform,charts[i],-52+i*52,89,48,43,()=>world.ChangeChart(c));b.GetComponent<Image>().color=new Color(.68f,.67f,.61f);b.GetComponentInChildren<Text>().color=dark;}
   for(int i=0;i<Optics.Rows.Length;i++){float row=Optics.Rows[i];int col=i%4,r=i/4;var b=Btn("remote VA "+row,face.transform,row.ToString("0.0"),-57+col*38,40-r*32,35,28,()=>world.RemoteRow(row));b.GetComponentInChildren<Text>().fontSize=14;b.GetComponent<Image>().color=new Color(.70f,.70f,.66f);b.GetComponentInChildren<Text>().color=dark;}
   Btn("remote larger",face.transform,"大 ↑",-40,-109,72,34,()=>world.Row(-1));
   Btn("remote smaller",face.transform,"小 ↓",40,-109,72,34,()=>world.Row(1));
   Btn("remote reset",face.transform,"復位",-40,-147,72,30,()=>world.RemoteReset());
   Btn("remote lamp",face.transform,"投影開關",40,-147,72,30,()=>world.RemotePower());
   var shape=RemoteShape();remotePanel.GetComponent<Image>().sprite=shape;remotePanel.GetComponent<Image>().type=Image.Type.Sliced;
   foreach(var b in remotePanel.GetComponentsInChildren<Button>()){b.GetComponent<Image>().sprite=shape;b.GetComponent<Image>().type=Image.Type.Sliced;b.GetComponentInChildren<Text>().fontSize=14;var sh=b.gameObject.AddComponent<Shadow>();sh.effectColor=new Color(0,0,0,.85f);sh.effectDistance=new Vector2(1,-3);}
   var bodyShadow=remotePanel.AddComponent<Shadow>();bodyShadow.effectColor=new Color(0,0,0,.65f);bodyShadow.effectDistance=new Vector2(5,-6);
   Txt("remote label",remotePanel.transform,"CHART PROJECTOR",0,-181,170,24,12,dark,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   var prep=Panel("Preparation",hud.transform,585,60,360,500,new Vector2(.5f,.5f),dark);
   Txt("prep title",prep.transform,"診間準備",0,223,315,43,23,pale,new Vector2(.5f,.5f));
   for(int i=0;i<9;i++){int p=i;prepButtons.Add(Btn("prep"+i,prep.transform,ClinicSession.PrepNames[i],0,179-i*39,318,34,()=>world.Prepare(p)));}
   Txt("wd title",prep.transform,"檢影工作距離",-80,-178,160,30,16,muted,new Vector2(.5f,.5f));
   Btn("wd prev",prep.transform,"◀",-118,-212,60,40,()=>world.ShiftWD(-1));Btn("wd next",prep.transform,"▶",118,-212,60,40,()=>world.ShiftWD(1));
   wdPrepText=Txt("wd value",prep.transform,"",0,-212,170,40,20,accent,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   chartPanel=Panel("Chart view",hud.transform,600,240,344,284,new Vector2(.5f,.5f),dark);
   chartTitle=Txt("chart title",chartPanel.transform,"6 m 視標",0,124,320,30,16,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   var chartImg=new GameObject("Chart image",typeof(RectTransform),typeof(RawImage));Rect(chartImg,chartPanel.transform,0,-14,320,240,new Vector2(.5f,.5f));chartImg.GetComponent<RawImage>().texture=world.ChartTexture;chartImg.GetComponent<RawImage>().raycastTarget=false;chartRaw=chartImg.GetComponent<RawImage>();
   retPanel=Panel("Retinoscope view",hud.transform,600,136,332,472,new Vector2(.5f,.5f),dark);
   Txt("ret title",retPanel.transform,"檢影鏡窺孔 · 放大觀察",0,211,306,40,18,pale,new Vector2(.5f,.5f));
   var raw=new GameObject("Pupil camera",typeof(RectTransform),typeof(RawImage));Rect(raw,retPanel.transform,0,45,290,290,new Vector2(.5f,.5f));raw.GetComponent<RawImage>().texture=scope;raw.GetComponent<RawImage>().raycastTarget=false;
   retReadout=Txt("reflex description",retPanel.transform,"",0,-129,306,55,18,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   Btn("streak minus",retPanel.transform,"−15° Z",-110,-175,92,35,()=>world.Streak(-1));
   Btn("streak 90",retPanel.transform,"+90° Q",0,-175,110,35,()=>world.Streak90(),true);
   Btn("streak plus",retPanel.transform,"+15° X",110,-175,92,35,()=>world.Streak(1));
   Btn("sweep",retPanel.transform,"自動掃動 E",-78,-216,148,35,()=>world.Sweep(),true);
   Btn("wd minus",retPanel.transform,"◀",28,-216,44,35,()=>world.ShiftWD(-1));Btn("wd plus",retPanel.transform,"▶",132,-216,44,35,()=>world.ShiftWD(1));
   wdText=Txt("wd ret",retPanel.transform,"",80,-216,60,35,15,accent,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   // sweep indicator drawn over the eyepiece view: the axis of the sweep and where the streak is now
   var axisGo=new GameObject("Sweep axis",typeof(RectTransform),typeof(Image));sweepAxis=Rect(axisGo,retPanel.transform,0,45,270,3,new Vector2(.5f,.5f));axisGo.GetComponent<Image>().color=new Color(1,.85f,.4f,.55f);axisGo.GetComponent<Image>().raycastTarget=false;
   var dotGo=new GameObject("Sweep dot",typeof(RectTransform),typeof(Image));sweepDot=Rect(dotGo,retPanel.transform,0,45,16,16,new Vector2(.5f,.5f));dotGo.GetComponent<Image>().color=new Color(1,.92f,.55f,.95f);dotGo.GetComponent<Image>().raycastTarget=false;
   sweepText=Txt("sweep text",retPanel.transform,"",0,182,300,26,15,accent,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);

   pdPanel=Panel("PD measurement",hud.transform,-560,-150,420,330,new Vector2(.5f,.5f),dark);
   Txt("pd title",pdPanel.transform,"量瞳距 PD · 對應邊緣法",0,138,380,36,21,accent,new Vector2(.5f,.5f));
   pdText=Txt("pd steps",pdPanel.transform,"",0,-10,384,250,17,pale,new Vector2(.5f,.5f),TextAnchor.UpperLeft);
   pdReading=Txt("pd reading",pdPanel.transform,"",0,-138,380,40,24,accent,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   tipBox=Panel("Control tip",hud.transform,0,0,330,58,new Vector2(.5f,.5f),new Color(.04f,.08f,.08f,.9f));tipBox.GetComponent<Image>().raycastTarget=false;
   tip=Txt("tip",tipBox.transform,"",0,0,314,54,15,pale,new Vector2(.5f,.5f),TextAnchor.MiddleLeft);
   // ---- touch / tablet controls ----
   stickObj=Panel("Walk stick",hud.transform,-610,-290,230,230,new Vector2(.5f,.5f),new Color(.05f,.1f,.1f,.45f));
   var knob=Panel("Stick knob",stickObj.transform,0,0,96,96,new Vector2(.5f,.5f),new Color(.93f,.94f,.88f,.75f));knob.GetComponent<Image>().raycastTarget=false;
   stick=stickObj.AddComponent<ClinicStick>();stick.knob=(RectTransform)knob.transform;
   Txt("stick help",stickObj.transform,"走路",0,-138,200,30,16,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   walkButtons=new GameObject("Walk buttons",typeof(RectTransform));Rect(walkButtons,hud.transform,0,0,10,10,new Vector2(.5f,.5f));
   Btn("t interact",walkButtons.transform,"互動（點物件）",600,-215,210,64,()=>world.Interact(),true);
   Btn("t focus",walkButtons.transform,"坐近驗光儀",600,-290,210,64,()=>world.Focus());
   Btn("t pov",walkButtons.transform,"受測者視野",600,-365,210,56,()=>world.PatientPOV());
   Btn("t look",walkButtons.transform,"工具列 開／關",380,-365,210,56,()=>{immersive=!immersive;});
   knobBar=Panel("Knob bar",hud.transform,160,-118,700,74,new Vector2(.5f,.5f),dark);
   Btn("knob minus",knobBar.transform,"−",-300,0,90,62,()=>world.TouchStep(-1));
   Btn("knob plus",knobBar.transform,"+",205,0,90,62,()=>world.TouchStep(1),true);
   Btn("knob close",knobBar.transform,"關",305,0,62,62,()=>world.touchSel="");
   knobLabel=Txt("knob label",knobBar.transform,"",-45,0,400,70,16,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   // closed-eye cue while measuring PD: the side of the examiner's closed eye is dimmed
   shadeL=Panel("Closed left eye",hud.transform,-660,0,340,3000,new Vector2(.5f,.5f),new Color(0,0,0,.55f));shadeL.GetComponent<Image>().raycastTarget=false;
   shadeR=Panel("Closed right eye",hud.transform,660,0,340,3000,new Vector2(.5f,.5f),new Color(0,0,0,.55f));shadeR.GetComponent<Image>().raycastTarget=false;
   Txt("closed L",shadeL.transform,"你的左眼閉上",40,0,240,40,20,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter).raycastTarget=false;
   Txt("closed R",shadeR.transform,"你的右眼閉上",-40,0,240,40,20,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter).raycastTarget=false;
   pdButtons=Panel("PD buttons",hud.transform,-560,-375,420,96,new Vector2(.5f,.5f),dark);
   Btn("pd say",pdButtons.transform,"①「請看我的眼睛」",-104,22,200,40,()=>world.PDInstruct());
   Btn("pd right",pdButtons.transform,"② 閉右眼 Q",104,22,200,40,()=>world.PDClose(1));
   Btn("pd left",pdButtons.transform,"④ 閉左眼 E",-154,-24,100,40,()=>world.PDClose(2));
   Btn("pd minus",pdButtons.transform,"◀",-70,-24,58,40,()=>world.PDNudge(-1));
   Btn("pd plus",pdButtons.transform,"▶",-8,-24,58,40,()=>world.PDNudge(1));
   Btn("pd ok",pdButtons.transform,"確認",76,-24,100,40,()=>world.PDConfirm(),true);
   Btn("pd exit",pdButtons.transform,"取消",168,-24,72,40,()=>world.PDExit(false));
   book=Panel("Clinical notes",root,0,0,1600,900,new Vector2(.5f,.5f),new Color(.02f,.05f,.04f,.84f));
   var sheet=Panel("Paper",book.transform,0,0,1050,800,new Vector2(.5f,.5f),new Color(.94f,.92f,.84f));
   bookTitle=Txt("book title",sheet.transform,"驗 光 紀 錄",0,338,930,60,35,dark,new Vector2(.5f,.5f));
   var noteViewport=Panel("Scroll paper",sheet.transform,0,25,930,560,new Vector2(.5f,.5f),new Color(1,1,1,0));noteViewport.AddComponent<RectMask2D>();var scroll=noteViewport.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;scroll.viewport=noteViewport.GetComponent<RectTransform>();
   record=Txt("records",noteViewport.transform,"",0,0,920,1000,22,dark,new Vector2(.5f,1),TextAnchor.UpperLeft);var noteContent=record.GetComponent<RectTransform>();noteContent.pivot=new Vector2(.5f,1);noteContent.anchoredPosition=Vector2.zero;scroll.content=noteContent;
   Btn("download",sheet.transform,"下載本次紀錄",-305,-345,280,50,()=>world.Export(),true);
   Btn("close notebook",sheet.transform,"返回診間 Tab",0,-345,280,50,()=>world.ToggleNotebook());
   Btn("new session",sheet.transform,"回到選單",305,-345,280,50,()=>world.ReturnToMenu(false));
   pausedPanel=Panel("Pause",root,0,0,1600,900,new Vector2(.5f,.5f),new Color(.02f,.05f,.04f,.84f));
   var pauseRect=(RectTransform)pausedPanel.transform;pauseRect.anchorMin=Vector2.zero;pauseRect.anchorMax=Vector2.one;pauseRect.sizeDelta=Vector2.zero;
   var pcard=Panel("Pause card",pausedPanel.transform,0,0,650,590,new Vector2(.5f,.5f),dark);
   Txt("pause title",pcard.transform,"暫 停",0,225,530,60,40,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   Txt("music track",pcard.transform,"Blue Oiia - Uiia Cat · 循環播放",0,162,530,38,19,muted,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   volumeLabel=Txt("music volume",pcard.transform,"",0,117,530,32,21,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   var sliderObj=Panel("Music volume slider",pcard.transform,0,70,460,36,new Vector2(.5f,.5f),lighter);musicSlider=sliderObj.AddComponent<Slider>();musicSlider.minValue=0;musicSlider.maxValue=1;
   var fill=Panel("Volume fill",sliderObj.transform,0,0,460,10,new Vector2(.5f,.5f),accent);musicSlider.fillRect=fill.GetComponent<RectTransform>();musicSlider.fillRect.sizeDelta=Vector2.zero;
   var handle=Panel("Volume handle",sliderObj.transform,0,0,26,36,new Vector2(.5f,.5f),pale);musicSlider.handleRect=handle.GetComponent<RectTransform>();musicSlider.handleRect.sizeDelta=new Vector2(26,0);musicSlider.targetGraphic=handle.GetComponent<Image>();musicSlider.SetValueWithoutNotify(world.musicVolume);musicSlider.onValueChanged.AddListener(v=>world.SetMusicVolume(v));
   Btn("continue",pcard.transform,"繼續練習",0,6,530,52,()=>world.TogglePause(),true);
   Btn("save and menu",pcard.transform,"保存進度，回主選單",0,-59,530,52,()=>world.ReturnToMenu(false));
   Btn("abandon",pcard.transform,"放棄這輪",0,-124,530,48,()=>abandonConfirm.SetActive(true));
   Txt("pause help",pcard.transform,"音量 0% 為靜音，設定會自動記住。\n回主選單後可選「繼續上次練習」。",0,-217,530,72,18,muted,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   abandonConfirm=Panel("Confirm abandon",pcard.transform,0,0,620,590,new Vector2(.5f,.5f),new Color(.035f,.07f,.075f,1));
   Txt("abandon question",abandonConfirm.transform,"確定放棄這輪？\n這輪的病例進度與紀錄將清除。",0,65,540,100,24,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   Btn("cancel abandon",abandonConfirm.transform,"保留這輪",-140,-80,260,54,()=>abandonConfirm.SetActive(false));
   Btn("confirm abandon",abandonConfirm.transform,"確定放棄",140,-80,260,54,()=>{abandonConfirm.SetActive(false);world.ReturnToMenu(true);},true);abandonConfirm.SetActive(false);
   // Pin HUD groups to the top / bottom edges so taller tablet screens are not letterboxed.
   var hr=(RectTransform)hud.transform;hr.anchorMin=Vector2.zero;hr.anchorMax=Vector2.one;hr.sizeDelta=Vector2.zero;hr.anchoredPosition=Vector2.zero;
   ((RectTransform)walkButtons.transform).anchoredPosition=new Vector2(0,0);
   foreach(Transform child in hud.transform){var c=(RectTransform)child;if(child.gameObject==touchPad||child==conversation.transform.parent)continue;float y=c.anchoredPosition.y;
    if(child.gameObject==walkButtons){c.anchorMin=c.anchorMax=new Vector2(.5f,0);c.anchoredPosition=new Vector2(0,450);continue;}
    if(y>150){c.anchorMin=c.anchorMax=new Vector2(.5f,1);c.anchoredPosition=new Vector2(c.anchoredPosition.x,y-450);}
    else if(y<-100){c.anchorMin=c.anchorMax=new Vector2(.5f,0);c.anchoredPosition=new Vector2(c.anchoredPosition.x,y+450);}}
   Refresh();
  }
  void Update(){
   GuideUpdate();
   if(!world||!retPanel||!retPanel.activeSelf||sweepAxis==null)return;var s=world.session;
   float ang=s.streak+90;sweepAxis.localRotation=Quaternion.Euler(0,0,ang);
   Vector2 d=new Vector2(Mathf.Cos(ang*Mathf.Deg2Rad),Mathf.Sin(ang*Mathf.Deg2Rad));sweepDot.anchoredPosition=new Vector2(0,45)+d*world.sweep*120;
   sweepText.text=Mathf.Abs(world.sweep)<.08f?"光帶在中央 · 拖曳掃動":"光帶正往 "+(Mathf.Abs(d.x)>Mathf.Abs(d.y)?(d.x*world.sweep>0?"右 →":"← 左"):(d.y*world.sweep>0?"上 ↑":"↓ 下"))+" 掃";
  }
  Sprite RemoteShape(){int n=64,r=12;var tex=new Texture2D(n,n,TextureFormat.RGBA32,false);for(int y=0;y<n;y++)for(int x=0;x<n;x++){float dx=Mathf.Max(r-x,0,x-(n-1-r)),dy=Mathf.Max(r-y,0,y-(n-1-r));tex.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(r-Mathf.Sqrt(dx*dx+dy*dy))));}tex.Apply();return Sprite.Create(tex,new UnityEngine.Rect(0,0,n,n),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(r,r,r,r));}
  void Place(string n,float x,float y){((RectTransform)station.transform.Find(n)).anchoredPosition=new Vector2(x,y);}
  void CaseLabel(){if(selectedPatient==0){patientChoice.text="隨機病患（19–21 歲）";return;}var s=new ClinicSession();s.patient=selectedPatient;patientChoice.text="病例 "+selectedPatient.ToString("00")+"  /  "+s.PatientName;}
  // On tablets the keyboard shortcuts in labels are noise: strip them once touch is detected.
  bool touchLabels;Text portraitHint;
  void ApplyTouchLabels(){touchLabels=true;var key=new System.Text.RegularExpressions.Regex(@"\s+(空白鍵|Enter|Tab|[A-Z])$");
   foreach(var b in root.GetComponentsInChildren<Button>(true)){var t=b.GetComponentInChildren<Text>(true);if(!t)continue;if(t.text=="Esc"){t.text="選單";continue;}t.text=key.Replace(t.text,"");}
   foreach(var t in root.GetComponentsInChildren<Text>(true)){if(t.name=="walk help")t.text="左下搖桿走路 · 拖曳畫面轉頭 · 點物件互動 · 右下按鈕坐近驗光儀";if(t.name=="menu help")t.text="平板：左下搖桿走路、拖曳轉頭、點物件互動；建議橫放並開全螢幕";if(t.name=="station help")t.text="直接點儀器上的旋鈕，下方出現 − ／ ＋ 調整（外側大轉盤＝球面、內鈕＝柱面、外圈＝軸位、上方小鈕＝輔助鏡片）";}
   portraitHint=Txt("portrait hint",root,"建議把平板橫放，按鈕會比較大、比較好點",0,360,900,40,22,accent,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);}
  public void Refresh(){if(!world||!root)return;if(world.touchMode&&!touchLabels)ApplyTouchLabels();if(portraitHint)portraitHint.gameObject.SetActive(Screen.height>Screen.width);scaler.matchWidthOrHeight=Screen.height>0&&(float)Screen.width/Screen.height<1.7f?0:1;var s=world.session;bool play=world.begun;hud.SetActive(play);book.SetActive(play&&world.notebook);pausedPanel.SetActive(play&&world.paused);resume.interactable=canResume;
   if(!world.paused&&abandonConfirm)abandonConfirm.SetActive(false);if(volumeLabel)volumeLabel.text="背景音樂音量  "+Mathf.RoundToInt(world.musicVolume*100)+"%";
   if(!play)return;
   task.text=s.StageTitle;step.text="STEP "+(s.stage+1).ToString("00")+" / 17  ·  "+s.mode;
   task.transform.parent.gameObject.SetActive(!immersive&&!world.cinematic);
   patient.text=s.PatientName+(s.patient==0?"":" · 病例 "+s.patient.ToString("00"))+" · "+s.EyeName;
   string wdLabel=s.wdCm+" cm · "+Optics.WD(s.wdCm).ToString("0.00")+" D";if(wdPrepText)wdPrepText.text=wdLabel;if(wdChip){wdChip.GetComponentInChildren<Text>().text="工作距離 "+s.wdCm+" cm";foreach(var b in wdOpts){bool on=b.name=="wd opt "+s.wdCm;b.GetComponent<Image>().color=on?accent:lighter;b.GetComponentInChildren<Text>().color=on?new Color(.05f,.06f,.06f,1):pale;b.GetComponentInChildren<Text>().fontStyle=on?FontStyle.Bold:FontStyle.Normal;}if(wdHint)wdHint.text=s.wdRemoved?"已從兩眼扣除工作距離，要改請重新開始病例":"目前 "+wdLabel+"，檢影後從兩眼球面扣除";}if(wdText)wdText.text=s.wdCm+"cm";
   int sec=Mathf.FloorToInt(s.mode=="考試"?Mathf.Max(0,1080-s.seconds):s.seconds);clock.text=(s.mode=="考試"?"剩餘 ":"練習 ")+string.Format("{0:00}:{1:00}",sec/60,sec%60)+"  /  六公尺視標";
   var missing=s.Missing();mission.text=s.mode=="考試"?"依期中檢查流程獨立操作。\n完成時按 Enter 前往下一階段。":missing.Count==0?"✓ 這一步的條件已完成。\n按 Enter 前往下一步。":string.Join("\n",missing.GetRange(0,Mathf.Min(4,missing.Count)).ConvertAll(x=>"・"+x));
   next.interactable=!world.busy&&(missing.Count==0||s.mode=="自由"||s.mode=="考試");
   bool showHints=hints&&s.mode!="考試";helper.transform.parent.gameObject.SetActive(showHints&&!immersive&&!world.cinematic&&world.pdStep<0);helper.text=showHints?Hint():"";
   context.text=!world.focused&&!world.patientView?(world.focusLabel!=""?(world.touchMode?"點一下：":"E  ")+world.focusLabel:(world.touchMode?"左下搖桿走路、拖曳畫面轉頭、點物件互動":"走近工具或受測者 · F 坐近驗光儀")):world.patientView?"受測者視野（模糊示意） · C 返回":"";
   toast.text=Time.time<toastUntil?world.lastAction:"";
   conversation.transform.parent.gameObject.SetActive(Time.time<world.speechUntil&&!world.cinematic);conversation.text="受測者  /  "+world.speech;
   bool pd=world.pdStep>=0;
   bool touchUI=world.touchMode&&!world.notebook&&!world.paused&&!world.cinematic;
   touchPad.SetActive(touchUI);stickObj.SetActive(touchUI&&!world.focused&&!pd&&!world.patientView);walkButtons.SetActive(touchUI&&!world.focused&&!pd);
   knobBar.SetActive(touchUI&&world.focused&&!pd&&world.touchSel!="");if(knobBar.activeSelf)knobLabel.text=world.hoverLabel;
   pdButtons.SetActive(pd&&!world.paused);
   shadeR.SetActive(pd&&world.examinerEye==1);shadeL.SetActive(pd&&world.examinerEye==2);
   ((RectTransform)conversation.transform.parent).anchoredPosition=new Vector2(pd?0:100,pd?-345:-210);
   station.SetActive(world.focused&&!pd&&!world.cinematic&&!world.patientView&&!world.notebook&&!world.paused&&!immersive);
   remotePanel.SetActive(world.remoteOwned&&!pd&&!world.cinematic&&!world.notebook&&!world.paused&&!world.patientView&&(!world.touchMode||world.focused));
   remoteStatus.text=world.projectorOn?(new[]{"E 字","紅綠","點陣"}[s.chart]+"  VA "+s.row.ToString("0.0")):"投影已關閉";
   bool compact=world.held!=1&&s.stage!=1&&s.stage!=2&&s.stage!=3;
   foreach(Transform t in station.transform){string n=t.name;bool keep=n=="lens readout"||n=="read"||n=="record"||n=="next"||n=="leave"||n=="eye"||n=="fog"||n=="hold ret"||n=="station help";t.gameObject.SetActive(!compact||keep);}
   station.GetComponent<RectTransform>().anchoredPosition=new Vector2(160,compact?102:170);
   station.GetComponent<RectTransform>().sizeDelta=new Vector2(1130,compact?142:244);
   foreach(string n in new[]{"chart E","chart RG","chart dots","larger","smaller"})station.transform.Find(n).gameObject.SetActive(false);
   if(compact){Place("lens readout",0,49);Place("read",-427,5);Place("eye",-207,5);Place("record",-48,5);Place("next",184,5);Place("leave",433,-43);Place("fog",-427,-43);Place("hold ret",-237,-43);Place("station help",0,-75);previewLabel.gameObject.SetActive(false);}
   else{Place("lens readout",0,91);Place("read",-427,-10);Place("eye",58,41);Place("record",130,-10);Place("next",358,-10);Place("leave",433,-63);Place("fog",-214,-10);Place("hold ret",62,-63);Place("station help",0,-105);}
   helper.transform.parent.gameObject.SetActive(showHints&&!immersive&&!world.cinematic&&!pd&&!world.remoteOwned);
   crossObj.SetActive(!world.focused&&!pd&&!world.cinematic);
   pdPanel.SetActive(pd&&!world.paused);
   if(pd){string[] st={"① 空白鍵：「請看我的這隻眼睛。」","② Q：閉上你的右眼，用左眼看（受測者看你的左眼）","③ ← →：移動 PD 尺，0 對準他右眼瞳孔右緣（顳側），Enter","④ E：閉上你的左眼、張開右眼（受測者看你的右眼）","⑤ 讀他左眼瞳孔靠右眼側（鼻側）的刻度，← → 選數值，Enter"};
    int cur=world.pdStep==0?0:world.pdStep==1?1:world.pdStep==2?2:world.pdStep==3?3:4;string t="";for(int i=0;i<st.Length;i++)t+=(i<cur?"<color=#8fb39a>✓ ":i==cur?"<color=#e8d29a>▶ ":"<color=#8a958f>   ")+st[i]+"</color>\n";
    pdText.text=t+"\n你的眼睛："+(world.examinerEye==1?"右眼閉 · 左眼看":world.examinerEye==2?"左眼閉 · 右眼看":"雙眼張開")+"　 Esc 取消";
    pdReading.text=world.pdStep==4?"讀數 "+world.pdReading.ToString("0.0")+" mm":world.pdStep>=2?"PD 尺移動 0.25 mm／格":"";}
   bool showTip=!world.touchMode&&world.focused&&!pd&&world.hoverLabel!=""&&!world.notebook&&!world.paused;tipBox.SetActive(showTip);
   if(showTip){tip.text=world.hoverLabel;Vector2 lp;RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)hud.transform,Input.mousePosition,null,out lp);((RectTransform)tipBox.transform).anchoredPosition=lp+new Vector2(185,-40);}
   if(immersive&&world.focused&&!world.patientView)context.text=s.EyeName+" "+s.H.lens+"  ·  滾輪調鏡 / V 切換參數 / H 工具列";
   readout.text=s.EyeName+"  "+s.H.lens+"    PD "+s.pd+" mm   VA 列 "+s.row.ToString("0.0")+"    "+new[]{"◀ 球面","◀ 柱面","◀ 軸位"}[world.parameter];
   var prep=prepButtons[0].transform.parent.gameObject;prep.SetActive(s.stage==0&&!pd&&!world.cinematic&&!world.notebook&&!world.paused&&!immersive);
   for(int i=0;i<9;i++)prepButtons[i].GetComponentInChildren<Text>().text=(s.prepared[i]?"✓  ":"")+ClinicSession.PrepNames[i];
   retPanel.SetActive(world.focused&&world.held==1&&!world.patientView&&!world.notebook&&!world.paused);
   chartPanel.SetActive(world.focused&&world.held!=1&&s.stage>=4&&world.pdStep<0&&!world.patientView&&!world.notebook&&!world.paused&&!world.cinematic);
   if(chartPanel.activeSelf){float z=world.ChartZoom;chartRaw.uvRect=new Rect(.5f-z/2,.5f-z/2,z,z);chartTitle.text=s.chart==1?"6 m 視標 · 紅綠":s.chart==2?"6 m 視標 · 點陣（JCC）":"6 m 視標 · 目前這一列 "+s.row.ToString("0.0")+"（放大顯示）";}
   if(pd||world.cinematic)context.text="";
   float r=Optics.RetResidual(s.Truth(s.eye),s.H.lens,Optics.WD(s.wdCm),s.streak);
   retReadout.text="光帶 "+world.StreakName()+" · WD "+s.wdCm+"cm · 按住左鍵拖曳掃動\n"+(showHints?(Mathf.Abs(r)<.125f?"中和":r<0?"順動 → 加正鏡":"逆動 → 加負鏡"):"觀察反射光與光帶的相對移動");
   if(world.notebook)Records();
  }
  string Hint(){var s=world.session;if(s.mode=="自由")return "自由練習參考（近似病例）\nOD "+s.Truth(0)+"\nOS "+s.Truth(1)+"\nPD "+s.TruePD+" mm";
   if(s.stage==0)return !s.prepared[0]?"左側工具櫃：拿酒精棉布（E），走到受測者旁按 E 消毒額托、面罩和 PD 尺。":!s.prepared[1]?"拿 PD 尺走近受測者按 E。閉右眼對 0（他右眼瞳孔右緣），換閉左眼讀他左眼鼻側緣。":"F 坐近後直接點儀器：水平鈕、額托鈕（頂點 12 mm）、支臂高度、鎖定桿；或點右側清單。";
   if(s.stage==1||s.stage==2)return "拿起檢影鏡 → F 坐近。按住左鍵拖曳掃動，Z/X 轉光帶。\n順動加正、逆動加負；反射帶和光帶不平行（破裂）時轉光帶找主子午線。兩條都中和後記錄。";
   if(s.stage==3)return "Gross − 1 / 工作距離（公尺）= Net。只扣球面，柱面與軸位不變。扣完按 R 記錄。";
   if(s.Phase==0)return "① 以扣完工作距離的淨檢影值測 "+s.EyeName+" 視力：另一眼 OC，換大小列找出讀得出的最小一列（VA < 0.8 補 PH），按 R 記錄度數與 VA。右眼做完換左眼。";
   if(s.Phase==1)return "② 第一次 MPMVA（"+s.EyeName+"）：以淨檢影值起始，先 +1.00 霧視，再逐格減正並讀表；視力不再改善時保留最多正度數，按 R 記錄度數與 VA，接著做紅綠。雙眼都完成後才進 JCC。";
   if(s.Phase==2)return "比較紅綠的字是否一樣清楚。紅清加負，綠清加正。重看視力，不追求顏色亮度。";
   if(s.Phase==3)return "本眼路線 "+s.H.route+"："+(s.H.route=="APA"?"軸 → 度數 → 軸":"初步度數 → 複核度數 → 軸 → 度數")+"。\n本輪 "+(s.JCCPattern[s.H.routeIndex]==1?"A：負柱面軸朝較清楚面的紅點移。":"P：紅加白減；每 -0.50DC 補 +0.25DS。")+"\n等清並比較至少兩次後按 Enter。";
   if(s.Phase==4)return "③ "+s.EyeName+" APA／PPAP 完成。收回 JCC，按 R 記錄 JCC 後度數。雙眼 JCC 都記錄後才做第二次 MPMVA。";
   if(s.Phase==5)return "④ 第二次 MPMVA（"+s.EyeName+"）：切回 E 字，先 +0.50D 再霧視並逐格退霧，確認最大正度數最佳視力。按 R 記錄度數與最佳 VA，改鏡後須重記。";
   return "雙眼流程完成後，按 Enter 完成並保存本次紀錄。";
  }
  void Records(){var s=world.session;bookTitle.text=s.completed?"本 次 練 習 回 饋":"驗 光 紀 錄  /  "+s.PatientName;
   System.Func<int,string> eyeTag=e=>e==0?"OD":"OS";
   string text="工作距離  "+s.wdCm+" cm    受測者  "+s.PatientName+"    PD  "+s.pd.ToString("0.#")+" mm    Date  "+System.DateTime.Now.ToString("yyyy / MM / dd")+"    模式 "+s.mode+"\n\n";
   text+="① Retinoscopy（淨檢影值）\n";for(int e=0;e<2;e++){var h=s.eyes[e];text+="   "+eyeTag(e)+":  "+(h.netSaved?h.net.ToString():"待記錄")+"      VA:  "+(h.vaSaved?h.vaText+(h.phRead?"  (PH "+h.phVAText+")":""):"待記錄")+(h.grossSaved?"      Gross "+h.gross:"")+"\n";}
   text+="\n② 1st MPMVA\n";for(int e=0;e<2;e++){var h=s.eyes[e];text+="   "+eyeTag(e)+":  "+(h.mp1Saved?h.mp1.ToString():"待記錄")+"      VA:  "+(h.mp1Saved?h.mp1VAText:"待記錄")+(h.duoAsked?"      紅綠 ✓":"")+"\n";}
   text+="\n③ JCC\n";for(int e=0;e<2;e++){var h=s.eyes[e];text+="   "+eyeTag(e)+":  "+(h.jccSaved?h.jccResult.ToString()+"   ("+h.route+")":"待記錄")+"\n";}
   text+="\n④ 2nd MPMVA\n";for(int e=0;e<2;e++){var h=s.eyes[e];text+="   "+eyeTag(e)+":  "+(h.finalSaved?h.final.ToString():"待記錄")+"      VA:  "+(h.finalSaved?h.finalVAText:"待記錄")+"\n";if(h.finalSaved&&h.finalKey!=s.LensKey(e))text+="   ＊"+eyeTag(e)+" 鏡片已變更，需重新記錄。\n";}
   if(s.completed){text+="\n流程回饋  "+s.Score()+" / 100    ·    "+s.events.Count+" 次操作\n";text+="參考病例：OD "+s.Truth(0)+"  /  OS "+s.Truth(1)+"\n";if(s.examErrors.Count>0)text+="待改進項目\n"+string.Join("\n",s.examErrors)+"\n";}
   text+="\n本次只涵蓋期中：檢影、單眼自覺式驗光、JCC。\n受測者回答及模糊是教學近似模型；此紀錄不是臨床處方。";record.text=text;var rt=record.GetComponent<RectTransform>();rt.sizeDelta=new Vector2(rt.sizeDelta.x,Mathf.Max(560,record.preferredHeight+24));
  }

 }
}
