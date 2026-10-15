using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RefractionRoom {
 // New-player walkthrough: a card with the instruction, a bobbing arrow and a pulsing frame on the
 // exact button / object to use next. Each step finishes when the player does the action (or presses
 // the card's 下一步 for read-only steps). Desktop and touch get their own wording.
 public partial class ClinicUI {
  class GuideStep {
   public string title,desk,touch;
   public string ui;                  // name of the UI element to frame (optional)
   public Func<Vector3?> world;        // world point to point at (optional)
   public Func<bool> done;             // finished when true; null = needs 下一步
   public Action enter;
  }
  public bool guideOn;
  List<GuideStep> guide;int guideIndex;float guideStepTime;
  GameObject guideCard,guideArrow,guideFrame;Text guideTitle,guideBody,guideCount;Button guideNextBtn,guideBackBtn;
  Dictionary<string,RectTransform> uiIndex;
  Vector3 guideStartPos,guideStartFwd;float guideStartSph,guideDoneAt=-1;bool guideStartOcc;int guideStartReads;bool guideSeenPicker;

  public void StartGuide(){
   selectedPatient=1;CaseLabel();guideOn=true;world.Begin("自由");world.session.mode="自由";hints=true;
   BuildGuideSteps();guideIndex=-1;GuideGo(0);
  }
  public void StopGuide(){if(guideOn&&world)world.GuideResetScene();guideOn=false;if(guideCard){guideCard.SetActive(false);guideArrow.SetActive(false);guideFrame.SetActive(false);}}

  void BuildGuideOverlay(){
   if(guideCard)return;
   guideFrame=new GameObject("Guide frame",typeof(RectTransform),typeof(Image));guideFrame.transform.SetParent(root,false);
   var fi=guideFrame.GetComponent<Image>();fi.sprite=RingSprite();fi.type=Image.Type.Sliced;fi.color=new Color(1,.83f,.35f,1);fi.raycastTarget=false;
   guideArrow=new GameObject("Guide arrow",typeof(RectTransform),typeof(Image));guideArrow.transform.SetParent(root,false);
   var ai=guideArrow.GetComponent<Image>();ai.sprite=ArrowSprite();ai.color=new Color(1,.83f,.35f,1);ai.raycastTarget=false;((RectTransform)guideArrow.transform).sizeDelta=new Vector2(70,86);
   var sh=guideArrow.AddComponent<Shadow>();sh.effectColor=new Color(0,0,0,.6f);sh.effectDistance=new Vector2(3,-4);
   guideCard=Panel("Guide card",root,0,250,700,210,new Vector2(.5f,.5f),new Color(.03f,.07f,.075f,.96f));
   var stripe=new GameObject("Guide stripe",typeof(RectTransform),typeof(Image));Rect(stripe,guideCard.transform,0,102,664,4,new Vector2(.5f,.5f));stripe.GetComponent<Image>().color=accent;stripe.GetComponent<Image>().raycastTarget=false;
   guideCount=Txt("guide count",guideCard.transform,"",-180,76,300,30,15,accent,new Vector2(.5f,.5f));
   guideTitle=Txt("guide title",guideCard.transform,"",0,44,640,40,25,pale,new Vector2(.5f,.5f));
   guideBody=Txt("guide body",guideCard.transform,"",0,-18,640,88,19,pale,new Vector2(.5f,.5f),TextAnchor.UpperLeft);
   guideBackBtn=Btn("guide back",guideCard.transform,"上一步",-262,-76,120,40,()=>GuideGo(guideIndex-1));
   Btn("guide quit",guideCard.transform,"結束教學",-122,-76,130,40,()=>{world.ReturnToMenu(false);StopGuide();});
   guideNextBtn=Btn("guide next",guideCard.transform,"下一步",230,-76,190,40,()=>GuideGo(guideIndex+1),true);
   guideCard.SetActive(false);guideArrow.SetActive(false);guideFrame.SetActive(false);
  }
  static Sprite arrowSprite,ringSprite;
  static Sprite ArrowSprite(){if(arrowSprite)return arrowSprite;int w=64,h=80;var t=new Texture2D(w,h,TextureFormat.RGBA32,false);var px=new Color32[w*h];
   for(int y=0;y<h;y++)for(int x=0;x<w;x++){float cx=x+.5f-w/2f,a=0;
    if(y<38){float half=(y+.5f)/38f*30;a=Mathf.Clamp01(half-Mathf.Abs(cx)+.5f);}          // head (tip at the bottom)
    else{a=Mathf.Clamp01(11-Mathf.Abs(cx)+.5f)*Mathf.Clamp01(h-1-y+.5f);}                 // shaft
    px[y*w+x]=new Color32(255,255,255,(byte)(a*255));}
   t.SetPixels32(px);t.Apply();arrowSprite=Sprite.Create(t,new UnityEngine.Rect(0,0,w,h),new Vector2(.5f,0),100);return arrowSprite;}
  static Sprite RingSprite(){if(ringSprite)return ringSprite;int n=64;var t=new Texture2D(n,n,TextureFormat.RGBA32,false);var px=new Color32[n*n];
   for(int y=0;y<n;y++)for(int x=0;x<n;x++){float dx=Mathf.Max(0,Mathf.Abs(x+.5f-32)-14),dy=Mathf.Max(0,Mathf.Abs(y+.5f-32)-14);float d=Mathf.Sqrt(dx*dx+dy*dy);float a=Mathf.Clamp01(18.5f-d)*Mathf.Clamp01(d-12.5f);px[y*n+x]=new Color32(255,255,255,(byte)(a*255));}
   t.SetPixels32(px);t.Apply();ringSprite=Sprite.Create(t,new UnityEngine.Rect(0,0,n,n),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(24,24,24,24));return ringSprite;}

  RectTransform FindUI(string name){
   if(uiIndex==null||!uiIndex.ContainsKey(name)||!uiIndex[name]){uiIndex=new Dictionary<string,RectTransform>();foreach(var r in root.GetComponentsInChildren<RectTransform>(true))if(!uiIndex.ContainsKey(r.name))uiIndex[r.name]=r;}
   RectTransform rt;return uiIndex.TryGetValue(name,out rt)?rt:null;
  }
  void GuideGo(int i){
   if(i<0)i=0;if(guide==null)return;
   if(i>=guide.Count){world.ReturnToMenu(false);StopGuide();return;}
   guideIndex=i;guideStepTime=Time.unscaledTime;var g=guide[i];
   guideStartPos=world.view.transform.position;guideStartFwd=world.view.transform.forward;guideStartSph=world.session.H.lens.s;guideStartReads=world.session.H.reads;guideSeenPicker=false;guideDoneAt=-1;guideClicks.Clear();guideStartOcc=world.session.occluded;
   if(g.enter!=null)g.enter();
   BuildGuideOverlay();guideCard.SetActive(true);guideCard.transform.SetAsLastSibling();guideFrame.transform.SetAsLastSibling();guideArrow.transform.SetAsLastSibling();
   guideCount.text="新手教學  "+(i+1)+" / "+guide.Count;guideTitle.text=g.title;guideBody.text=world.touchMode?g.touch:g.desk;
   guideBackBtn.gameObject.SetActive(i>0);
   if(g.done==null)world.Release();
  }
  // called every frame from Update
  void GuideUpdate(){
   if(!guideOn||guide==null||guideIndex<0)return;
   if(!world.begun){StopGuide();return;}
   var g=guide[guideIndex];
   bool finished=guideDoneAt>0||g.done!=null&&Time.unscaledTime-guideStepTime>.4f&&g.done();
   guideNextBtn.GetComponentInChildren<Text>().text=guideIndex==guide.Count-1?"完成，回主選單":g.done==null?"下一步":finished?"做到了！下一步":"先照指示操作";
   guideNextBtn.interactable=g.done==null||finished;
   if(finished&&guideDoneAt<0)guideDoneAt=Time.unscaledTime;
   if(guideDoneAt>0&&Time.unscaledTime-guideDoneAt>.7f){GuideGo(guideIndex+1);return;}   // short pause so "做到了" is visible
   // keyboard: N = 下一步 on read-only steps (pointer may be locked while walking)
   if(g.done==null&&Input.GetKeyDown(KeyCode.N)){GuideGo(guideIndex+1);return;}
   // target placement
   Vector2? at=null;Vector2 size=new Vector2(90,90);
   if(!string.IsNullOrEmpty(g.ui)){var rt=FindUI(g.ui);if(rt&&rt.gameObject.activeInHierarchy){Vector3[] c=new Vector3[4];rt.GetWorldCorners(c);Vector2 a,b;var rr=(RectTransform)root;
     RectTransformUtility.ScreenPointToLocalPointInRectangle(rr,c[0],null,out a);RectTransformUtility.ScreenPointToLocalPointInRectangle(rr,c[2],null,out b);at=(a+b)/2;size=new Vector2(Mathf.Abs(b.x-a.x),Mathf.Abs(b.y-a.y));}}
   if(at==null&&g.world!=null){var wp=g.world();if(wp.HasValue){Vector3 sp=world.view.WorldToScreenPoint(wp.Value);if(sp.z>0){Vector2 l;RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)root,sp,null,out l);at=l;size=new Vector2(110,110);}}}
   bool show=at.HasValue&&!world.paused||(at.HasValue&&g.ui!=null&&g.ui.StartsWith("continue"));
   guideFrame.SetActive(show);guideArrow.SetActive(show);
   float bob=Mathf.Sin(Time.unscaledTime*6)*10;
   if(show){var p=at.Value;var fr=(RectTransform)guideFrame.transform;fr.anchoredPosition=p;fr.sizeDelta=size+new Vector2(26,26)*(1+.08f*Mathf.Sin(Time.unscaledTime*6));
    var ar=(RectTransform)guideArrow.transform;var half=((RectTransform)root).rect.size/2;bool below=p.y>half.y-180;   // near the top edge: arrow comes from below
    ar.localRotation=Quaternion.Euler(0,0,below?180:0);ar.anchoredPosition=p+new Vector2(0,below?-(size.y/2+30+bob):size.y/2+30+bob);}
   // keep the card away from the target
   var card=(RectTransform)guideCard.transform;float cy=world.focused?250:at.HasValue&&at.Value.y>60?-120:250;if(world.paused||world.notebook)cy=330;
   card.anchoredPosition=Vector2.Lerp(card.anchoredPosition,new Vector2(at.HasValue&&Mathf.Abs(at.Value.x)<380&&Mathf.Abs(at.Value.y-cy)<170?(at.Value.x>0?-420:420):0,cy),.2f);
   guideCard.transform.SetAsLastSibling();guideFrame.transform.SetAsLastSibling();guideArrow.transform.SetAsLastSibling();
  }

  float Turned(){return Vector3.Angle(Flat(guideStartFwd),Flat(world.view.transform.forward));}
  static Vector3 Flat(Vector3 v){v.y=0;return v.sqrMagnitude>0?v.normalized:Vector3.forward;}
  Vector3? Node(string n){var t=world.N(n);return t?t.position:(Vector3?)null;}
  Vector3? TargetPos(string id){foreach(var t in FindObjectsByType<ClinicTarget>(FindObjectsSortMode.None))if(t.id==id)return t.transform.position;return null;}

  void BuildGuideSteps(){
   var s=new List<GuideStep>();
   s.Add(new GuideStep{title="歡迎來到新手教學",desk="接下來會一步一步帶你操作所有功能。金色箭頭和外框指的就是現在要按的按鈕或要看的東西。\n每一步做完會自動跳下一步；只需要看的步驟按「下一步」（或鍵盤 N）。",touch="接下來會一步一步帶你操作所有功能。金色箭頭和外框指的就是現在要點的按鈕或要看的東西。\n每一步做完會自動跳下一步；只需要看的步驟點「下一步」。"});
   s.Add(new GuideStep{title="頂部資訊列",ui="Header",desk="左邊是病例與目前測試眼，中間是工作距離與計時，右邊是「病歷 Tab」、「語音 開／關」和「Esc」選單。",touch="左邊是病例與目前測試眼，中間是工作距離與計時，右邊是「病歷」、「語音 開／關」和「選單」。"});
   s.Add(new GuideStep{title="任務卡",ui="Objective card",desk="左上角永遠顯示這一步要完成的事。「提示 開／關」可以打開更詳細的做法（考試模式沒有提示）。",touch="左上角永遠顯示這一步要完成的事。「提示 開／關」可以打開更詳細的做法（考試模式沒有提示）。"});
   s.Add(new GuideStep{title="走路",ui="Walk stick",desk="按住 W 往前走（A／D 左右、S 後退），走大約一步的距離。",touch="用左下角的搖桿往前推，走大約一步的距離。",done=()=>Vector3.Distance(Flat2(world.view.transform.position),Flat2(guideStartPos))>.5f});
   s.Add(new GuideStep{title="轉頭",desk="先點一下畫面讓滑鼠鎖定，再移動滑鼠左右看（Esc 可放開滑鼠）。轉大約 30 度。",touch="在畫面空白的地方用手指左右拖曳，轉頭大約 30 度。",done=()=>Turned()>28});
   s.Add(new GuideStep{title="設定工作距離",ui="wd chip",desk="點頂部的「工作距離」按鈕，打開選單。",touch="點頂部的「工作距離」按鈕，打開選單。",done=()=>wdPicker.activeSelf,enter=()=>world.Release()});
   s.Add(new GuideStep{title="選你的工作距離",ui="WD picker",desk="選一個距離（例如 57 cm＝1.75 D）。檢影完會從兩眼球面扣掉這個度數。",touch="點一個距離（例如 57 cm＝1.75 D）。檢影完會從兩眼球面扣掉這個度數。",done=()=>{if(wdPicker.activeSelf)guideSeenPicker=true;return guideSeenPicker&&!wdPicker.activeSelf;}});
   s.Add(new GuideStep{title="診間準備清單",ui="Preparation",desk="開始驗光前要完成九項準備：消毒、量 PD、水平、輔助鏡歸 O、打開窺孔、桌高與頂點距離、紅綠大 E、鎖定支臂、調暗燈光。可以直接點清單，或走到物件旁互動。",touch="開始驗光前要完成九項準備：消毒、量 PD、水平、輔助鏡歸 O、打開窺孔、桌高與頂點距離、紅綠大 E、鎖定支臂、調暗燈光。可以直接點清單，或走到物件旁互動。"});
   s.Add(new GuideStep{title="拿桌上的工具",world=()=>TargetPos("remote"),desk="我帶你走到桌邊了。對準發亮的「視標遙控器」按 E 拿起來。酒精棉布、PD 尺、檢影鏡也都在這張桌上，用一樣的方法拿。",touch="我帶你走到桌邊了。點一下發亮的「視標遙控器」拿起來（或對準後點右下「互動」）。酒精棉布、PD 尺、檢影鏡也都在這張桌上。",enter=()=>world.GuideWalkTo(new Vector3(.62f,1.62f,3.42f),TargetPos("remote")??new Vector3(.76f,.99f,3.96f)),done=()=>world.remoteOwned});
   s.Add(new GuideStep{title="消毒與量 PD",world=()=>TargetPos("pd"),desk="正式練習時：拿酒精棉布走到受測者旁按 E，會播放噴酒精、擦額托與面罩的動畫；再拿 PD 尺按 E 進入量瞳距（請看我的這隻眼睛、閉眼、對 0、讀數）。",touch="正式練習時：拿酒精棉布走到受測者旁點互動，會播放消毒動畫；再拿 PD 尺點互動進入量瞳距，下方會出現專用按鈕。"});
   s.Add(new GuideStep{title="坐到驗光儀前",ui="t focus",desk="按 F 坐近驗光儀（再按一次 F 會回到診間）。",touch="點「坐近驗光儀」。",enter=()=>world.GuidePrepareStage(4),done=()=>world.focused});
   s.Add(new GuideStep{title="轉動旋鈕",world=()=>TargetPos("sph:0"),desk="滑鼠指向發亮的球面大轉盤：滾輪轉動，或左鍵＋0.25、右鍵−0.25。現在把右眼球面轉一格。",touch="點發亮的球面大轉盤，下方會出現「−／＋」按鈕，點一下改 0.25 D。",done=()=>Mathf.Abs(world.session.eyes[0].lens.s-guideStartSph)>.1f||Mathf.Abs(world.session.H.lens.s-guideStartSph)>.1f});
   s.Add(new GuideStep{title="遮蓋另一眼",ui="occlude",world=()=>TargetPos("aux:1"),desk="點「另一眼 OC」（或按 O）：非測試眼的輔助鏡片在 OC（遮蓋）和 O（開放）之間切換。",touch="點「另一眼 OC」，或點左眼（畫面右邊）發亮的小輔助鏡片旋鈕：非測試眼在 OC（遮蓋）和 O（開放）之間切換。",done=()=>world.session.occluded!=guideStartOcc||Clicked("occlude")});
   s.Add(new GuideStep{title="遙控器換視標",ui="Projector remote",desk="左下的遙控器可以換 E 字／紅綠／點陣，也可以直接選視力列。點一個數字換列。",touch="左下的遙控器可以換 E 字／紅綠／點陣，也可以直接選視力列。點一個數字換列。",done=()=>ClickedPrefix("remote VA ")});
   s.Add(new GuideStep{title="詢問受測者",ui="read",desk="點「詢問／讀視標」（或空白鍵）：受測者會念出這一列看到的方向，右上角會顯示記法。",touch="點「詢問／讀視標」：受測者會念出這一列看到的方向。",done=()=>world.session.H.reads>guideStartReads||Clicked("read")});
   s.Add(new GuideStep{title="拿起檢影鏡",ui="hold ret",enter=()=>world.GuidePrepareStage(1),desk="點「取／放檢影鏡」：鏡頭會移到你設定的工作距離。",touch="點「取／放檢影鏡」：鏡頭會移到你設定的工作距離。",done=()=>world.held==1});
   s.Add(new GuideStep{title="掃動光帶",ui="Retinoscope view",desk="按住左鍵，沿著右邊窺孔畫面那條金色線的方向拖曳（光帶水平就上下拖、垂直就左右拖），看反射光是順動還是逆動（也可以按「自動掃動 E」）。",touch="手指沿著右邊窺孔畫面那條金色線的方向拖曳（光帶水平就上下拖、垂直就左右拖），看反射光是順動還是逆動（也可以點「自動掃動」）。",done=()=>Mathf.Abs(world.sweep)>.3f||Clicked("sweep")});
   s.Add(new GuideStep{title="光帶轉 90°",ui="streak 90",desk="點「+90° Q」：撥動檢影鏡套筒滾輪，換掃另一條主子午線。",touch="點「+90°」：撥動檢影鏡套筒滾輪，換掃另一條主子午線。",done=()=>Clicked("streak 90")});
   s.Add(new GuideStep{title="放回檢影鏡",ui="hold ret",desk="再點一次「取／放檢影鏡」放回去。",touch="再點一次「取／放檢影鏡」放回去。",done=()=>world.held!=1});
   s.Add(new GuideStep{title="霧視",ui="fog",enter=()=>world.GuidePrepareStage(6),desk="點「霧視 +1.00」：MPMVA 一開始先加正度數霧視，再一格一格退霧。",touch="點「霧視 +1.00」：MPMVA 一開始先加正度數霧視，再一格一格退霧。",done=()=>Clicked("fog")});
   s.Add(new GuideStep{title="紅綠視標",ui="remote chart 1",desk="在遙控器點「紅綠」。紅色清楚就加負、綠色清楚就加正，停在第一次出現綠色。",touch="在遙控器點「紅綠」。紅色清楚就加負、綠色清楚就加正，停在第一次出現綠色。",done=()=>world.session.chart==1});
   s.Add(new GuideStep{title="放入 JCC",ui="jcc",world=()=>TargetPos("jcc:0")??Node("JCCCell_OD"),desk="點「JCC 收／A／P」（或按 J）：每按一次在 收→A（軸位）→P（度數）之間切換。JCC 框上的 A／P 字對準下方柱軸，就是那一種檢查。",touch="點「JCC 收／A／P」，或點右眼（畫面左邊）發亮的 JCC 鏡框：在 收、A（軸位）、P（度數）之間切換。JCC 框上的 A／P 字對準下方柱軸，就是那一種檢查。",enter=()=>world.GuidePrepareStage(10),done=()=>world.session.jccMode>0});
   s.Add(new GuideStep{title="翻面比較",ui="read",desk="點「詢問／讀視標」：會把 JCC 翻兩面，受測者回答第一面或第二面比較清楚；教學與自由模式會提示該怎麼調。",touch="點「詢問／讀視標」：會把 JCC 翻兩面，受測者回答第一面或第二面比較清楚；教學與自由模式會提示該怎麼調。",done=()=>Clicked("read")});
   s.Add(new GuideStep{title="記錄",ui="record",desk="點「記錄 R」把目前度數寫進紀錄單。",touch="點「記錄」把目前度數寫進紀錄單。",done=()=>Clicked("record")});
   s.Add(new GuideStep{title="完成此步驟",ui="next",desk="正式練習時，這一步做完就點「完成此步驟 Enter」進下一步；教學模式會檢查有沒有漏做，缺什麼會告訴你。這裡先不用按。",touch="正式練習時，這一步做完就點「完成此步驟」進下一步；教學模式會檢查有沒有漏做。這裡先不用按。"});
   s.Add(new GuideStep{title="打開病歷",ui="notebook",desk="點「病歷 Tab」：看雙眼的檢影、MPMVA、JCC 紀錄，也可以下載紀錄。",touch="點「病歷」：看雙眼的檢影、MPMVA、JCC 紀錄，也可以下載紀錄。",done=()=>world.notebook});
   s.Add(new GuideStep{title="關閉病歷",ui="close notebook",desk="點「關閉」回到驗光。",touch="點「關閉」回到驗光。",done=()=>!world.notebook});
   s.Add(new GuideStep{title="回到診間",ui="leave",desk="點「回診間 F」離開驗光儀。在診間按 C 可以切換成受測者視野（看他眼中的模糊）。",touch="點「回診間」離開驗光儀。回到診間後右下有「受測者視野」可以看他眼中的模糊。",done=()=>!world.focused});
   s.Add(new GuideStep{title="選單",ui="pause",desk="點右上「Esc」（或按 Esc）打開選單。",touch="點右上「選單」打開選單。",done=()=>world.paused});
   s.Add(new GuideStep{title="選單功能",ui="continue",desk="這裡可以調背景音樂音量、存檔回主選單、或放棄這次練習。點「繼續練習」關掉選單。",touch="這裡可以調背景音樂音量、存檔回主選單、或放棄這次練習。點「繼續練習」關掉選單。",done=()=>!world.paused});
   s.Add(new GuideStep{title="教學完成！",desk="你已經走過全部操作。回主選單後選「進入診間／教學」照正式流程練一次；熟了再試「期中考試」。也可以在病例那裡往前選「隨機病患」。",touch="你已經走過全部操作。回主選單後選「進入診間／教學」照正式流程練一次；熟了再試「期中考試」。也可以在病例那裡往前選「隨機病患」。"});
   guide=s;
  }
  static Vector3 Flat2(Vector3 p){p.y=0;return p;}
  // click tracking for guide targets
  readonly HashSet<string> guideClicks=new HashSet<string>();
  bool Clicked(string n){return guideClicks.Contains(n)&&guideClicks.Remove(n);}
  bool ClickedPrefix(string p){foreach(var c in guideClicks)if(c.StartsWith(p)){guideClicks.Remove(c);return true;}return false;}
  internal void NoteClick(string n){if(guideOn)guideClicks.Add(n);}
 }
}
