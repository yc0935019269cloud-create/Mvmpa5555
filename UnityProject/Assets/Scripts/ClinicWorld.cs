using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace RefractionRoom {
 public class ClinicTarget : MonoBehaviour {public string id,label;public Transform highlight;}
 public class ClinicWorld : MonoBehaviour {
  public static ClinicWorld Main;
  public ClinicSession session=new ClinicSession();
  public Camera view,scope;
  public Transform models,hands;
  public Font font;
  public ClinicUI ui;
  public bool begun,focused,busy,notebook,patientView,paused,cinematic,touchMode;
  public string touchSel="";
  public int parameter=0,held=0;
  public bool remoteOwned,projectorOn=true;
  int jccDrag=-1; float dragStartAngle,dragStartRotation; bool[] jccFace=new bool[2];
  float PointerAngle(Transform t){Vector3 q=view.WorldToScreenPoint(t.position);return Mathf.Atan2(Input.mousePosition.y-q.y,Input.mousePosition.x-q.x)*Mathf.Rad2Deg;}
  void DragJCC(){if(jccDrag<0)return;var t=N("Turret_"+EN[jccDrag]);if(paused||notebook||!focused||busy){jccDrag=-1;SyncMechanics();return;}
   float a=dragStartRotation+Mathf.DeltaAngle(dragStartAngle,PointerAngle(t));Spin(t.name,Quaternion.Euler(0,0,a),false,35);
   if(Input.GetMouseButtonUp(0)){int e=jccDrag;jccDrag=-1;float target=TurretInAngle(e);session.jccMode=Mathf.Abs(Mathf.DeltaAngle(a,target))<Mathf.Abs(Mathf.DeltaAngle(a,0))?Mathf.Max(1,session.jccMode):0;SyncMechanics();MarkSave();}}
  public void RemoteRow(float row){if(busy)return;projectorOn=true;session.row=row;DrawChart();MarkSave();}
  public void RemoteReset(){if(busy)return;projectorOn=true;session.chart=0;session.row=.6f;DrawChart();MarkSave();}
  public void RemotePower(){if(busy)return;projectorOn=!projectorOn;DrawChart();}

  public string speech="您好，我最近看遠的時候有點模糊。",lastAction="歡迎來到視光室。",focusLabel="",hoverLabel="";
  public float speechUntil=0;
  public RenderTexture scopeTexture;
  public AudioSource audioOut;
  public float musicVolume=.2f; AudioSource musicSource; float pauseScale=1;
  [DllImport("__Internal")] static extern void ClinicMusicVolume(float volume);
  public void SetMusicVolume(float value){musicVolume=Mathf.Clamp01(value);PlayerPrefs.SetFloat("ClinicMusicVolume",musicVolume);PlayerPrefs.Save();if(musicSource)musicSource.volume=musicVolume;
   #if UNITY_WEBGL && !UNITY_EDITOR
   ClinicMusicVolume(musicVolume);
   #endif
  }
  IEnumerator LoadMusic(){
   using(var req=UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(new Uri(System.IO.Path.Combine(Application.streamingAssetsPath,"Audio/blue-oiia-uiia-cat.mp3")).AbsoluteUri,AudioType.MPEG)){
    yield return req.SendWebRequest();if(req.result==UnityEngine.Networking.UnityWebRequest.Result.Success){musicSource=gameObject.AddComponent<AudioSource>();musicSource.clip=UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(req);musicSource.loop=true;musicSource.spatialBlend=0;musicSource.volume=musicVolume;musicSource.Play();}else Debug.LogWarning("Music: "+req.error);
   }
  }
  public void TogglePause(){if(!begun)return;paused=!paused;if(paused){pauseScale=Time.timeScale;Time.timeScale=0;Release();}else{Time.timeScale=pauseScale>0?pauseScale:1;if(!focused&&!notebook)Lock();}if(ui)ui.Refresh();}
  public void ReturnToMenu(bool abandon){Time.timeScale=1;StopAllCoroutines();var qa=GetComponent<ClinicQA>();if(qa)qa.StopAllCoroutines();busy=false;saveDirty=false;handScripted=false;cinematic=false;jccDrag=-1;pdStep=-1;examinerEye=0;tweening=false;ReturnTools();held=0;
   if(abandon){PlayerPrefs.DeleteKey("ClinicSession");PlayerPrefs.DeleteKey("ClinicRemote");PlayerPrefs.Save();ui.canResume=false;remoteOwned=false;}else SaveProgress();
   begun=false;paused=false;notebook=false;focused=false;patientView=false;if(mist)mist.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(sheen)sheen.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(streakLight)streakLight.enabled=false;if(streakStrip)streakStrip.gameObject.SetActive(false);foreach(string n in new[]{"PupilReflex_0","PupilReflex_1"})if(N(n))N(n).gameObject.SetActive(false);
   Release();ui.menu.SetActive(true);ui.Refresh();}

  public List<string> animationLog=new List<string>();
  // PD measurement (Viktorin): pdStep 0 instruct, 1 close own right eye, 2 align zero, 3 close own left eye, 4 read, -1 off
  public int pdStep=-1,examinerEye=0;
  public float pdReading=62;
  public bool pdInstructed;
  public float sweep;             // -1..1 position of the streak intercept across the pupil
  Dictionary<string,Transform> nodes=new Dictionary<string,Transform>();
  Dictionary<string,Vector3> homePos=new Dictionary<string,Vector3>();
  Dictionary<string,Quaternion> homeRot=new Dictionary<string,Quaternion>();
  Dictionary<string,Vector3> homeScale=new Dictionary<string,Vector3>();
  Dictionary<string,GameObject> tools=new Dictionary<string,GameObject>();
  class Drive {public Vector3 p;public Quaternion q;public float rate;}
  Dictionary<Transform,Drive> drives=new Dictionary<Transform,Drive>();
  List<Light> lamps=new List<Light>();
  Light streakLight;Transform streakStrip;Material streakMat;
  ClinicPost post;
  ParticleSystem mist,sheen;
  Material[] reflex=new Material[2];
  Material textMat;
  Texture2D chartTex;
  Material chartMat;
  TextMesh[] sphereNumbers=new TextMesh[2],cylNumbers=new TextMesh[2];
  TextMesh pdNumber;
  Vector3 walkPos=new Vector3(.6f,1.65f,.3f),returnPos;
  float yaw=0,pitch=0,stepBob,talkTime,blinkAt,blinkT=-1,saveAt,lookYaw,lookPitch;
  float tweenTime;Vector3 fromPos,toPos;Quaternion fromRot,toRot;
  bool tweening,saveDirty,handScripted,returnFocused;
  float targetFov=66;
  ClinicTarget target,hover;
  List<Renderer> lit=new List<Renderer>();
  MaterialPropertyBlock block;
  Transform reachTarget;float reachUntil;bool reachLeft;
  AudioClip tick,footstep,click,spray;
  [DllImport("__Internal")] static extern void ClinicSpeak(string text);
  [DllImport("__Internal")] static extern void ClinicDownload(string filename,string text);
  [DllImport("__Internal")] static extern void ClinicTelemetry(string json);
  [DllImport("__Internal")] static extern void ClinicCapture(int enabled);
  bool lookCaptured;
  public int mouseDowns;public string uiHit="";
  const float ZF=4.390f,ZR=4.463f;
  static readonly string[] EN={"OD","OS"};
  public Texture2D ChartTexture{get{return chartTex;}}
  public float ChartZoom=1;
  public Transform N(string name){Transform t;return nodes.TryGetValue(name,out t)?t:null;}
  void Awake(){Main=this;}
  static Transform Deep(Transform t,string name){if(t.name==name)return t;foreach(Transform c in t){var r=Deep(c,name);if(r)return r;}return null;}
  void Register(Transform root){foreach(var t in root.GetComponentsInChildren<Transform>(true))if(!nodes.ContainsKey(t.name)){nodes.Add(t.name,t);homePos[t.name]=t.localPosition;homeRot[t.name]=t.localRotation;homeScale[t.name]=t.localScale;}}
  void Start(){
   // The refractor hangs from the stand arm: swinging the arm carries the whole head.
   var arm=Deep(models,"ArmSwing");var head=Deep(models,"Phoropter");if(arm&&head)head.SetParent(arm,true);
   var deck=Deep(models,"LiftDeck");if(deck){foreach(string n in new[]{"ArmSwing","StandColumn","StandCap","LampArm","StandLamp"}){var t=Deep(models,n);if(t)t.SetParent(deck,true);}}
   Register(models);Register(hands);
   foreach(string n in new[]{"Retinoscope","PDRuler","CleaningCloth","Remote","Clipboard","Pen","Sanitizer"}){var t=N(n);if(t)tools[n]=t.gameObject;}
   SetupTool("Retinoscope",new Vector3(1.04f,.962f,3.96f),new Vector3(0,0,90));
   SetupTool("PDRuler",new Vector3(1.27f,.9405f,3.94f),new Vector3(90,0,0));
   SetupTool("CleaningCloth",new Vector3(1.46f,.94f,4.18f),new Vector3(0,30,0));
   SetupTool("Sanitizer",new Vector3(1.47f,.997f,4.43f),Vector3.zero);
   SetupTool("Remote",new Vector3(.76f,.944f,3.96f),new Vector3(90,0,0));
   SetupTool("Clipboard",new Vector3(1.13f,.942f,4.25f),new Vector3(90,0,0));
   SetupTool("Pen",new Vector3(1.36f,.944f,4.25f),new Vector3(0,15,90));
   foreach(var l in FindObjectsByType<Light>(FindObjectsSortMode.None))lamps.Add(l);
   post=view.GetComponent<ClinicPost>();
   block=new MaterialPropertyBlock();
   textMat=new Material(Shader.Find("Clinic/Text3D"));textMat.mainTexture=font.material.mainTexture;
   Font.textureRebuilt+=f=>{if(f==font&&textMat)textMat.mainTexture=font.material.mainTexture;};
   MakeTarget("patient","與受測者說話",new Vector3(0,1.44f,4.56f),new Vector3(.32f,.35f,.2f));
   MakeTarget("phoropter","坐近綜合驗光儀",new Vector3(0,1.42f,4.40f),new Vector3(.5f,.42f,.14f));
   MakeTarget("sanitize","拿取清潔棉布（消毒）",new Vector3(1.46f,.98f,4.18f),new Vector3(.24f,.14f,.24f));
   MakeTarget("pd","拿取 PD 尺",new Vector3(1.27f,.98f,3.94f),new Vector3(.28f,.14f,.24f));
   MakeTarget("retino","拿取檢影鏡",new Vector3(1.04f,.99f,3.96f),new Vector3(.23f,.2f,.23f));
   MakeTarget("remote","拿取視標遙控器",new Vector3(.76f,.99f,3.96f),new Vector3(.23f,.18f,.22f));
   MakeTarget("paper","查看板夾紀錄",new Vector3(1.13f,.99f,4.25f),new Vector3(.29f,.17f,.35f));
   MakeTarget("light","調整診間燈光",new Vector3(3.7f,1.28f,3.2f),new Vector3(.18f,.2f,.2f));
   MakeTarget("chart","查看 6 公尺視標",new Vector3(0,1.5f,-1.45f),new Vector3(1.1f,.85f,.1f));
   if(deck)foreach(var t in FindObjectsByType<ClinicTarget>(FindObjectsSortMode.None)){if(t.id=="remote"||t.id=="retino"||t.id=="pd"||t.id=="sanitize"||t.id=="paper")t.transform.SetParent(deck,true);}
   BuildControls();
   // The inspection eyepiece renders the modelled pupil; there is no answer graphic.
   scopeTexture=new RenderTexture(512,512,16,RenderTextureFormat.ARGB32);scopeTexture.Create();
   var sco=new GameObject("Retinoscope inspection camera");scope=sco.AddComponent<Camera>();scope.targetTexture=scopeTexture;scope.fieldOfView=14;scope.nearClipPlane=.002f;scope.farClipPlane=2;scope.clearFlags=CameraClearFlags.SolidColor;scope.backgroundColor=Color.black;scope.allowHDR=false;scope.enabled=false;
   for(int e=0;e<2;e++){
    var g=DisplayQuad("PupilReflex_"+e);reflex[e]=new Material(Shader.Find("Clinic/RetinalReflex"));g.GetComponent<Renderer>().material=reflex[e];g.SetActive(false);nodes[g.name]=g.transform;
    var pupil=N("Pupil_"+EN[e]);if(pupil){g.transform.SetParent(pupil,false);g.transform.localPosition=new Vector3(0,0,-.00035f);g.transform.localScale=Vector3.one*.0042f;}
   }
   var sl=new GameObject("Retinoscope streak");streakLight=sl.AddComponent<Light>();streakLight.type=LightType.Spot;streakLight.spotAngle=12;streakLight.range=1.4f;streakLight.intensity=.9f;streakLight.color=new Color(1,.93f,.78f);streakLight.shadows=LightShadows.None;streakLight.enabled=false;
   var strip=DisplayQuad("Retinoscope streak on face");streakMat=new Material(Shader.Find("Clinic/Streak"));strip.GetComponent<Renderer>().sharedMaterial=streakMat;strip.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;streakStrip=strip.transform;strip.SetActive(false);
   mist=Particles("Alcohol mist",new Color(.85f,.95f,1f,.5f));sheen=Particles("Wipe sheen",new Color(.9f,.97f,1f,.6f));
   chartTex=new Texture2D(1024,768,TextureFormat.RGB24,false);chartTex.filterMode=FilterMode.Bilinear;
   foreach(string chartPart in new[]{"ChartFrame","ChartSurface","ChartStand","ChartFoot"})if(N(chartPart))N(chartPart).position+=new Vector3(0,0,-.06f);
   chartMat=new Material(Shader.Find("Clinic/Screen"));chartMat.mainTexture=chartTex;
   var surf=DisplayQuad("Calibrated chart display");surf.transform.position=new Vector3(0,1.5f,-1.525f);surf.transform.rotation=Quaternion.Euler(0,180,0);surf.transform.localScale=new Vector3(1.08f,.81f,1);surf.GetComponent<Renderer>().sharedMaterial=chartMat;
   var mon=DisplayQuad("Chart monitor repeat");mon.transform.position=new Vector3(1.16f,1.14f,4.555f);mon.transform.localScale=new Vector3(.42f,.27f,1);mon.GetComponent<Renderer>().sharedMaterial=chartMat;if(deck)mon.transform.SetParent(deck,true);
   BuildSigns();BuildScales();
   audioOut=gameObject.AddComponent<AudioSource>();audioOut.spatialBlend=0;tick=Tone(1250,.022f,.11f);click=Tone(520,.05f,.16f);footstep=Tone(92,.065f,.10f);spray=Noise(.32f,.12f);
   SetMusicVolume(PlayerPrefs.GetFloat("ClinicMusicVolume",.2f));
   #if !UNITY_WEBGL || UNITY_EDITOR
   StartCoroutine(LoadMusic());
   #endif
   view.transform.position=walkPos;view.transform.rotation=Quaternion.Euler(0,0,0);view.fieldOfView=targetFov=66;
   hands.gameObject.SetActive(false);
   touchMode=Application.isMobilePlatform||Application.absoluteURL.Contains("touch=1");
   if(Application.isMobilePlatform){QualitySettings.shadowResolution=ShadowResolution.Medium;QualitySettings.antiAliasing=2;if(post)post.bloom=0;}
   ui=gameObject.AddComponent<ClinicUI>();ui.world=this;ui.Build(font,scopeTexture);
   LoadProgress();ApplyPatient();DrawChart();SyncMechanics(true);StartCoroutine(CaptureReflections());Emit();
   if(Application.absoluteURL.Contains("qa=1"))Debug.Log("CLINIC_QA_READY");
  }
  // One-off room capture used as the specular environment for chrome, brushed aluminium and glass.
  IEnumerator CaptureReflections(){
   yield return null;yield return null;
   var rt=new RenderTexture(128,128,16,RenderTextureFormat.ARGBHalf){dimension=UnityEngine.Rendering.TextureDimension.Cube,useMipMap=true,autoGenerateMips=true};rt.Create();
   var g=new GameObject("Reflection capture");g.transform.position=new Vector3(0,1.6f,3.2f);var c=g.AddComponent<Camera>();c.enabled=false;c.nearClipPlane=.05f;c.farClipPlane=20;c.allowHDR=true;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.5f,.52f,.5f);
   hands.gameObject.SetActive(false);bool ok=false;try{ok=c.RenderToCubemap(rt);}catch(Exception){}hands.gameObject.SetActive(begun);Destroy(g);
   if(ok){RenderSettings.defaultReflectionMode=UnityEngine.Rendering.DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=rt;}
  }
  void SetupTool(string name,Vector3 p,Vector3 e){var t=N(name);if(!t)return;t.position=p;t.rotation=Quaternion.Euler(e);homePos[name]=t.position;homeRot[name]=t.rotation;}
  GameObject DisplayQuad(string name){var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(.5f,-.5f,0)};mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();g.GetComponent<MeshFilter>().mesh=mesh;return g;}
  void MakeTarget(string id,string label,Vector3 p,Vector3 size){var g=new GameObject("Interact_"+id);g.transform.position=p;g.layer=8;g.AddComponent<BoxCollider>().size=size;var t=g.AddComponent<ClinicTarget>();t.id=id;t.label=label;}
  void Control(string id,string label,Transform follow,Vector3 world,float radius,Transform glow){
   if(!follow)return;var g=new GameObject("Control "+id);g.transform.SetParent(follow,false);g.transform.position=world;g.layer=9;
   g.AddComponent<SphereCollider>().radius=radius/Mathf.Max(.0001f,follow.lossyScale.x);var t=g.AddComponent<ClinicTarget>();t.id=id;t.label=label;t.highlight=glow?glow:follow;
  }
  void BuildControls(){
   for(int e=0;e<2;e++){
    string en=EN[e],side=e==0?"右眼 OD":"左眼 OS";var pod=N("Pod_"+en);if(!pod)continue;float o=e==0?-1:1;
    var sph=N("Sph_"+en);Control("sph:"+e,side+" 球面轉盤（每格 0.25D）",pod,sph.position+new Vector3(o*.045f,-.004f,-.012f),.03f,sph);
    var st=N("StrongSph_"+en);Control("strong:"+e,side+" 強球面環（每格 3.00D）",pod,st.position+new Vector3(0,0,.025f),.03f,st);
    var aux=N("Aux_"+en);Control("aux:"+e,side+" 輔助鏡片旋鈕",pod,aux.position+new Vector3(0,0,-.002f),.012f,aux);
    var ax=N("AxisKnob_"+en);Control("axis:"+e,side+" 柱面軸鈕（外圈，每格 5°）",pod,ax.position,.019f,ax);
    var cy=N("Cyl_"+en);Control("cyl:"+e,side+" 柱面度數鈕（內鈕，每格 0.25DC）",pod,cy.position,.012f,cy);
    var cell=N("JCCCell_"+en);if(cell){var tur=N("Turret_"+en);for(int k=1;k<=3;k++)Control("jccarm:"+e,side+" JCC 支桿",tur,Vector3.Lerp(tur.position,cell.position,k*.19f)+new Vector3(0,0,-.004f),.007f,tur);Control("jcc:"+e,side+" JCC 交叉圓柱鏡",cell,cell.position+new Vector3(0,0,-.002f),.0155f,N("Turret_"+en));
     Control("flip:"+e,side+" JCC 翻面滾輪",cell,cell.position+new Vector3(.0215f,0,0),.006f,cell);Control("flip:"+e,side+" JCC 翻面滾輪",cell,cell.position+new Vector3(-.0215f,0,0),.006f,cell);}
   }
   Control("pd","P.D. 旋鈕（瞳距）",N("PDWheel"),N("PDWheel").position,.015f,null);
   Control("level","水平調整鈕",N("LevelKnob"),N("LevelKnob").position,.014f,null);
   Control("vertex","額托旋鈕（頂點距離）",N("ForeheadKnob"),N("ForeheadKnob").position,.014f,null);
   Control("lock","支臂鎖定桿",N("LockLever"),N("LockLever").position+new Vector3(.02f,-.02f,-.005f),.022f,null);
   var collar=N("ArmCollar");if(collar)Control("height","支臂高度（桌高）",N("ArmSwing"),collar.position,.07f,collar);
  }
  TextMesh Text3D(string name,Transform parent,Vector3 world,string text,float size,Color c,TextAnchor anchor=TextAnchor.MiddleCenter){
   var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.position=world;g.transform.rotation=parent.rotation;var t=g.AddComponent<TextMesh>();t.text=text;t.font=font;t.fontSize=64;t.characterSize=size/6.4f;t.anchor=anchor;t.alignment=TextAlignment.Center;t.color=c;g.GetComponent<MeshRenderer>().sharedMaterial=textMat;return t;}
  void BuildSigns(){
   Sign("視 光 室",new Vector3(0,2.55f,5.78f),new Vector3(0,0,0),.078f,new Color(.89f,.87f,.76f));
   Sign("THE REFRACTION ROOM",new Vector3(0,2.32f,5.78f),Vector3.zero,.03f,new Color(.70f,.72f,.62f));
   Sign("6 m  /  DISTANCE ACUITY",new Vector3(0,2.07f,-1.50f),new Vector3(0,180,0),.027f,new Color(.19f,.28f,.25f));
   Sign("升降驗光桌",new Vector3(1.08f,.84f,3.80f),Vector3.zero,.024f,new Color(.25f,.32f,.28f));
   Sign("01  /  診間",new Vector3(2.65f,2.12f,-2.30f),new Vector3(0,180,0),.032f,new Color(.88f,.89f,.81f));
   var bottle=N("Sanitizer");if(bottle){var t=Text3D("Alcohol label",bottle,bottle.position+new Vector3(0,-.012f,.0335f),"75% 酒精",.0085f,new Color(.95f,.97f,.96f));t.transform.rotation=bottle.rotation*Quaternion.Euler(0,180,0);}
  }
  void Sign(string s,Vector3 p,Vector3 e,float size,Color c){var g=new GameObject(s);g.transform.position=p;g.transform.rotation=Quaternion.Euler(e);var t=g.AddComponent<TextMesh>();t.text=s;t.font=font;t.fontSize=64;t.characterSize=size/6.4f;t.anchor=TextAnchor.MiddleCenter;t.color=c;g.GetComponent<MeshRenderer>().sharedMaterial=textMat;}
  void BuildScales(){
   Color ink=new Color(.05f,.05f,.06f),red=new Color(.78f,.08f,.06f);
   for(int e=0;e<2;e++){
    string en=EN[e];var pod=N("Pod_"+en);if(!pod)continue;
    sphereNumbers[e]=Text3D("Sph readout "+en,pod,N("SphWindow_"+en).position,"0.00",.0074f,ink);
    cylNumbers[e]=Text3D("Cyl readout "+en,pod,N("CylWindow_"+en).position,"0.00",.0056f,ink);
    // axis reference scale around the main aperture (TABO: 0 at the examiner's right, counter-clockwise)
    var ar=N("AxisRefAnchor_"+en).position;for(int a=0;a<=180;a+=30)Text3D("Axis ref "+a,pod,ar+new Vector3(Mathf.Cos(a*Mathf.Deg2Rad),Mathf.Sin(a*Mathf.Deg2Rad),0)*.0153f,a.ToString(),.0021f,ink);
    var pr=N("ProtractorAnchor_"+en).position;for(int a=0;a<360;a+=30){int lab=a<=180?a:a-180;Text3D("Protractor "+a,pod,pr+new Vector3(Mathf.Cos(a*Mathf.Deg2Rad),Mathf.Sin(a*Mathf.Deg2Rad),0)*.0222f,lab.ToString(),.0027f,ink);}
    var aux=N("AuxScaleAnchor_"+en).position;
    string[] lab2={"O","R","P",e==0?"RMV":"WMV",e==0?"RMH":"WMH",e==0?"RL":"GL","O","+.12","PH",e==0?"6U":"10I","±.50","OC"};
    for(int k=0;k<12;k++){float a=(90-30*k)*Mathf.Deg2Rad;Text3D("Aux "+lab2[k],pod,aux+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.0172f,lab2[k],.0026f,lab2[k]=="OC"||lab2[k]=="PH"?red:ink);}
   }
   var pdA=N("PDScaleAnchor");if(pdA)pdNumber=Text3D("PD readout",pdA.parent,pdA.position,"64",.0066f,ink);
   var zero=N("RulerZero");var ruler=N("PDRuler");
   if(zero&&ruler)for(int mm=0;mm<=100;mm+=10)Text3D("Ruler "+mm,ruler,zero.position+ruler.rotation*new Vector3(mm/1000f,-.0092f,0),mm.ToString(),.0030f,ink);
  }
  Texture2D StreakCookie(){var t=new Texture2D(128,128,TextureFormat.RGBA32,false);t.wrapMode=TextureWrapMode.Clamp;var px=new Color32[128*128];
   for(int y=0;y<128;y++)for(int x=0;x<128;x++){float u=(x-63.5f)/63.5f,v=(y-63.5f)/63.5f;float a=Mathf.Exp(-u*u*260)*Mathf.Clamp01(1-v*v)*Mathf.Clamp01((1-Mathf.Sqrt(u*u+v*v))*4);byte b=(byte)(a*255);px[y*128+x]=new Color32(255,255,255,b);}
   t.SetPixels32(px);t.Apply();return t;}
  ParticleSystem Particles(string name,Color c){
   var g=new GameObject(name);var ps=g.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var m=ps.main;m.playOnAwake=false;m.loop=true;m.startLifetime=.7f;m.startSpeed=0;m.startSize=.008f;m.startColor=c;m.simulationSpace=ParticleSystemSimulationSpace.World;m.maxParticles=400;m.gravityModifier=.02f;
   var em=ps.emission;em.rateOverTime=0;var col=ps.colorOverLifetime;col.enabled=true;var gr=new Gradient();gr.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});col.color=gr;
   var sz=ps.sizeOverLifetime;sz.enabled=true;sz.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.6f,1,1.6f));
   var r=g.GetComponent<ParticleSystemRenderer>();r.material=new Material(Shader.Find("Clinic/Mist"));ps.Play();return ps;}
  void Emit(ParticleSystem ps,Vector3 p,Vector3 vel,float size,float life,int count){var ep=new ParticleSystem.EmitParams();for(int i=0;i<count;i++){ep.position=p+UnityEngine.Random.insideUnitSphere*size*.4f;ep.velocity=vel+UnityEngine.Random.insideUnitSphere*vel.magnitude*.35f;ep.startSize=size*UnityEngine.Random.Range(.6f,1.3f);ep.startLifetime=life*UnityEngine.Random.Range(.7f,1.2f);ps.Emit(ep,1);}}

  // ---------------- per-frame ----------------
  void Update(){
   if(!begun){view.transform.position=Vector3.Lerp(view.transform.position,new Vector3(1.05f,1.58f,2.25f),Time.deltaTime);view.transform.rotation=Quaternion.Slerp(view.transform.rotation,Quaternion.Euler(4,-24,0),Time.deltaTime);hands.gameObject.SetActive(false);if(scope)scope.enabled=false;if(ui)ui.Refresh();if(Input.GetMouseButtonDown(0)){mouseDowns++;}if(Time.frameCount%12==0)Emit();return;}
   if(!paused&&!session.completed)session.seconds+=Time.deltaTime;
   if(session.mode=="考試"&&session.seconds>=1080&&!session.completed){session.completed=true;Say("時間到了，請查看這次的紀錄與回饋。",false);notebook=true;Release();}
   if(Input.GetKeyDown(KeyCode.Escape))TogglePause();
   if(!paused&&Input.GetKeyDown(KeyCode.Tab)){ToggleNotebook();}
   if(!paused&&Input.GetKeyDown(KeyCode.H)){ui.immersive=!ui.immersive;Toast(ui.immersive?"沉浸模式 · H 顯示教學與工具列，鍵盤和實體旋鈕仍可操作。":"已顯示教學與工具列。");}
   if(Input.touchCount>0)touchMode=true;
   if(Input.GetMouseButtonDown(0)){mouseDowns++;var es=UnityEngine.EventSystems.EventSystem.current;uiHit="none";if(es){var pe=new UnityEngine.EventSystems.PointerEventData(es){position=Input.mousePosition};var hits=new List<UnityEngine.EventSystems.RaycastResult>();es.RaycastAll(pe,hits);uiHit=hits.Count>0?hits[0].gameObject.name+"/"+hits.Count:"no-hit";}else uiHit="no-eventsystem";}
   bool overUI=UnityEngine.EventSystems.EventSystem.current&&UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
   hover=null;hoverLabel="";
   if(touchMode&&touchSel!=""&&focused)hoverLabel=TouchSelLabel();
   if(!paused&&!notebook&&!cinematic){
    if(pdStep>=0)PDInput();
    else{
     if(Input.GetKeyDown(KeyCode.F))Focus();
     if(Input.GetKeyDown(KeyCode.C))PatientPOV();
     if(Input.GetKeyDown(KeyCode.E)&&!busy){if(focused)Ask();else Interact();}
     if(Input.GetKeyDown(KeyCode.Space)&&focused&&!busy)Ask();
     if(Input.GetKeyDown(KeyCode.V))SetParameter((parameter+1)%3);
     if(Input.GetKeyDown(KeyCode.LeftArrow)&&focused)Adjust(-1);
     if(Input.GetKeyDown(KeyCode.RightArrow)&&focused)Adjust(1);
     if(Input.GetKeyDown(KeyCode.O)&&focused)Occlude();
     if(Input.GetKeyDown(KeyCode.P)&&focused)Pinhole();
     if(Input.GetKeyDown(KeyCode.J)&&focused)CycleJCC();
     if(Input.GetKeyDown(KeyCode.R)&&focused)Record();
     if(Input.GetKeyDown(KeyCode.Return)&&focused)Next();
     if(Input.GetKeyDown(KeyCode.UpArrow)&&focused)Row(-1);
     if(Input.GetKeyDown(KeyCode.DownArrow)&&focused)Row(1);
     if(focused&&held==1){if(Input.GetKeyDown(KeyCode.Z))Streak(-1);if(Input.GetKeyDown(KeyCode.X))Streak(1);if(Input.GetKeyDown(KeyCode.Q))Streak90();}
     if(focused&&!patientView&&!touchMode&&jccDrag<0){
      RaycastHit kh;if(!overUI&&Physics.Raycast(view.ScreenPointToRay(Input.mousePosition),out kh,1.5f,1<<9))hover=kh.collider.GetComponent<ClinicTarget>();
      if(hover){hoverLabel=hover.label+"\n"+ControlState(hover.id);
       float sc=Input.mouseScrollDelta.y;if(Mathf.Abs(sc)>.1f)UseControl(hover.id,sc>0?1:-1,false);
       if(Input.GetMouseButtonDown(0))UseControl(hover.id,1,true);if(Input.GetMouseButtonDown(1))UseControl(hover.id,-1,true);}
      else{
       float sc=Input.mouseScrollDelta.y;if(Mathf.Abs(sc)>.1f&&!overUI)Adjust(sc>0?1:-1);
       // retinoscopy: drag the mouse to sweep the streak across the pupil
       if(held==1&&!overUI&&Input.GetMouseButton(0)){float ang=(session.streak+90)*Mathf.Deg2Rad;float d=Input.GetAxis("Mouse X")*Mathf.Cos(ang)+Input.GetAxis("Mouse Y")*Mathf.Sin(ang);sweep=Mathf.Clamp(sweep+d*.22f,-1,1);if(Mathf.Abs(sweep)>.8f)CountSweep();}
       // hold the right button to glance around without leaving the seat
       if(Input.GetMouseButton(1)&&!overUI){lookYaw=Mathf.Clamp(lookYaw+Input.GetAxis("Mouse X")*1.6f,-35,35);lookPitch=Mathf.Clamp(lookPitch-Input.GetAxis("Mouse Y")*1.2f,-25,20);}
      }
     }
     if(!focused&&!patientView&&!tweening){
      if(lookCaptured||Cursor.lockState==CursorLockMode.Locked||Input.GetMouseButton(1)){yaw+=Input.GetAxis("Mouse X")*2;pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*1.5f,-72,72);view.transform.rotation=Quaternion.Euler(pitch,yaw,0);}
      if(!touchMode&&Input.GetMouseButtonDown(0)&&Cursor.lockState!=CursorLockMode.Locked&&!overUI)Lock();
      Vector2 sv=ui&&ui.stick&&ui.stick.isActiveAndEnabled?ui.stick.value:Vector2.zero;
      Vector3 delta=Vector3.ClampMagnitude(Quaternion.Euler(0,yaw,0)*new Vector3(Input.GetAxisRaw("Horizontal")+sv.x,0,Input.GetAxisRaw("Vertical")+sv.y),1);
      if(delta.sqrMagnitude>.01f){Vector3 proposed=walkPos+delta*Time.deltaTime*(Input.GetKey(KeyCode.LeftShift)?2.6f:1.7f);proposed.x=Mathf.Clamp(proposed.x,-3.35f,3.35f);proposed.z=Mathf.Clamp(proposed.z,-1.8f,5.35f);
       if(!Blocked(proposed))walkPos=proposed;else{Vector3 px=new Vector3(proposed.x,walkPos.y,walkPos.z),pz=new Vector3(walkPos.x,walkPos.y,proposed.z);if(!Blocked(px))walkPos=px;else if(!Blocked(pz))walkPos=pz;}
       stepBob+=Time.deltaTime*9;if(Mathf.Sin(stepBob)>.99f&&!audioOut.isPlaying)audioOut.PlayOneShot(footstep,.3f);}
      view.transform.position=walkPos+Vector3.up*(delta.sqrMagnitude>.01f?Mathf.Sin(stepBob)*.01f:0);
     }
    }
    target=null;RaycastHit hit;if(!focused&&pdStep<0&&Physics.Raycast(view.ViewportPointToRay(new Vector3(.5f,.5f)),out hit,2.3f,1<<8))target=hit.collider.GetComponent<ClinicTarget>();focusLabel=target?target.label:"";
   }
   DragJCC();
   if(!Input.GetMouseButton(1)||!focused){lookYaw=Mathf.Lerp(lookYaw,0,Time.deltaTime*4);lookPitch=Mathf.Lerp(lookPitch,0,Time.deltaTime*4);}
   if(held==1&&!Input.GetMouseButton(0)&&!touchDragging)sweep=Mathf.Lerp(sweep,0,Time.deltaTime*1.2f);
   if(tweening){tweenTime+=Time.deltaTime*1.4f;float t=Mathf.SmoothStep(0,1,tweenTime);view.transform.position=Vector3.Lerp(fromPos,toPos,t);view.transform.rotation=Quaternion.Slerp(fromRot,toRot,t);if(t>=1)tweening=false;}
   else if(focused&&!patientView&&pdStep<0&&!cinematic)view.transform.rotation=Quaternion.Euler(lookPitch,lookYaw,0);
   if(patientView){Vector3 ep=PupilWorld(session.eye);view.transform.position=ep+new Vector3(0,0,-.012f);view.transform.rotation=Quaternion.Euler(0,180,0);targetFov=40;}
   view.fieldOfView=Mathf.Lerp(view.fieldOfView,targetFov,1-Mathf.Exp(-6*Time.deltaTime));
   if(saveDirty&&Time.time>saveAt){saveDirty=false;SaveProgress();}
   if(Time.frameCount%12==0){ui.Refresh();Emit();}
  }
  bool Blocked(Vector3 p){return (p.x<-1.7f&&p.z>2.65f)||(p.x>.55f&&p.x<1.65f&&p.z>3.7f&&p.z<4.7f)||(Mathf.Abs(p.x)<.66f&&p.z>4.0f)||(p.x>.3f&&p.x<.95f&&p.z>4.05f&&p.z<4.8f);}
  void LateUpdate(){
   if(!models)return;float dt=Time.deltaTime;
   foreach(var kv in drives){var t=kv.Key;if(!t)continue;float k=1-Mathf.Exp(-kv.Value.rate*dt);t.localPosition=Vector3.Lerp(t.localPosition,kv.Value.p,k);t.localRotation=Quaternion.Slerp(t.localRotation,kv.Value.q,k);}
   AnimatePatient();
   // Cross cylinder loupes are geared to the cylinder axis: handle on the axis (A) or red dots on it (P).
   var phoro=N("Phoropter");
   for(int e=0;e<2;e++){var cell=N("JCCCell_"+EN[e]);if(!cell||!phoro)continue;float a=session.JCCRef(e);bool power=session.jccMode==2&&e==session.eye;cell.rotation=phoro.rotation*Quaternion.Euler(0,0,power?a-45:a);}
   HandPose(dt);
   Retinoscopy();
   for(int i=0;i<lit.Count;i++)if(lit[i])lit[i].SetPropertyBlock(null);lit.Clear();
   if(hover&&hover.highlight){foreach(var r in hover.highlight.GetComponentsInChildren<Renderer>()){if(r is ParticleSystemRenderer||r.sharedMaterial==null||!r.sharedMaterial.HasProperty("_Color"))continue;block.Clear();Color c=r.sharedMaterial.color;block.SetColor("_Color",Color.Lerp(c,new Color(1,.86f,.45f),.38f+.12f*Mathf.Sin(Time.time*7)));r.SetPropertyBlock(block);lit.Add(r);}}
  }
  void AnimatePatient(){
   var head=N("HeadPivot");bool still=session.prepared[5]&&session.phoropterIn||pdStep>=0;
   if(head)head.localRotation=homeRot[head.name]*Quaternion.Euler(Mathf.Sin(Time.time*.7f)*(still?.25f:.8f),Mathf.Sin(Time.time*.35f)*(still?.2f:.6f),0);
   if(Time.time>blinkAt){blinkT=0;blinkAt=Time.time+UnityEngine.Random.Range(2.5f,5.5f);}
   float blink=0;if(blinkT>=0){blinkT+=Time.deltaTime/.16f;blink=Mathf.Sin(Mathf.Clamp01(blinkT)*Mathf.PI);if(blinkT>=1)blinkT=-1;}
   foreach(string n in new[]{"Lid_OD","Lid_OS"}){var l=N(n);if(l)l.localRotation=homeRot[n]*Quaternion.Euler(-40*blink,0,0);}
   var jaw=N("Jaw");if(jaw)jaw.localRotation=homeRot[jaw.name]*Quaternion.Euler(Time.time<talkTime?-(1.5f+Mathf.Abs(Mathf.Sin(Time.time*17))*4.5f):0,0,0);
   foreach(string n in new[]{"Pupil_OD","Pupil_OS"}){var p=N(n);if(p)p.localScale=Vector3.Lerp(p.localScale,homeScale[n]*(session.prepared[8]?1.35f:1),Time.deltaTime*2);}
   // fixation: the distance chart, or the examiner's open eye while measuring PD
   Vector3 fix=new Vector3(0,1.5f,-1.5f);
   if(pdStep>=0)fix=pdInstructed?PDEye(examinerEye):PDEye(0)+new Vector3(0,-.03f,0);
   for(int e=0;e<2;e++){var eye=N("Eye_"+EN[e]);if(!eye||!head)continue;Vector3 d=fix-eye.position;Quaternion want=Quaternion.FromToRotation(head.rotation*Vector3.back,d)*head.rotation;eye.rotation=Quaternion.Slerp(eye.rotation,want,1-Mathf.Exp(-22*Time.deltaTime));}
  }
  public Vector3 PupilWorld(int e){var p=N("Pupil_"+EN[e]);return p?p.position:new Vector3(e==0?-.032f:.032f,1.445f,4.475f);}
  void ApplyPatient(){for(int e=0;e<2;e++){var rig=N("EyeRig_"+EN[e]);if(!rig)continue;Vector3 p=homePos[rig.name];p.x=(e==0?-1:1)*session.TruePD/2000f-(rig.parent?rig.parent.position.x:0);rig.localPosition=p;}}

  // ---------------- mechanisms ----------------
  void DriveTo(Transform t,Vector3 p,Quaternion q,bool instant,float rate=16){if(!t)return;if(instant){t.localPosition=p;t.localRotation=q;}Drive d;if(!drives.TryGetValue(t,out d)){d=new Drive();drives[t]=d;}d.p=p;d.q=q;d.rate=rate;}
  void Spin(string name,Quaternion q,bool instant,float rate=16){var t=N(name);if(t)DriveTo(t,homePos[name],homeRot[name]*q,instant,rate);}
  int AuxState(int e){return session.occluded&&e!=session.eye?1:session.ph&&e==session.eye?2:0;}
  void SyncMechanics(bool instant=false){
   for(int e=0;e<2;e++){
    string en=EN[e];var L=session.eyes[e].lens;float side=e==0?1:-1;
    if(sphereNumbers[e]){sphereNumbers[e].text=L.s.ToString("+0.00;-0.00;0.00");sphereNumbers[e].color=L.s<0?new Color(.78f,.08f,.06f):new Color(.05f,.05f,.06f);}
    if(cylNumbers[e]){cylNumbers[e].text=L.c.ToString("0.00;-0.00;0.00");cylNumbers[e].color=L.c<0?new Color(.78f,.08f,.06f):new Color(.05f,.05f,.06f);}
    Transform pod=N("Pod_"+en);if(pod){Vector3 p=homePos[pod.name];p.x+=(e==0?-1:1)*(session.pd-64)*.0005f;DriveTo(pod,p,homeRot[pod.name],instant,10);}
    // weak sphere dial: rim downward = plus (counter-clockwise right eye, clockwise left eye)
    Spin("Sph_"+en,Quaternion.Euler(0,0,side*15*L.s/.25f),instant);
    Spin("StrongSph_"+en,Quaternion.Euler(0,0,-side*30*Mathf.Round(L.s/3)),instant);
    Spin("Cyl_"+en,Quaternion.Euler(0,0,60*L.c),instant);
    Spin("AxisKnob_"+en,Quaternion.Euler(0,0,L.a),instant);
    Spin("AxisPointer_"+en,Quaternion.Euler(0,0,L.a),instant);
    int aux=AuxState(e);
    Spin("AuxWheel_"+en,Quaternion.Euler(0,0,aux==1?60:aux==2?-60:0),instant,9);
    Spin("Aux_"+en,Quaternion.Euler(0,0,aux==1?30:aux==2?120:0),instant,12);
    Spin("Turret_"+en,Quaternion.Euler(0,0,session.jccMode>0&&e==session.eye?TurretInAngle(e):0),instant,7);
    var flip=N("JCCFlip_"+en);if(flip&&!busy)DriveTo(flip,homePos[flip.name],homeRot[flip.name]*Quaternion.AngleAxis(jccFace[e]?180:0,Vector3.right),instant,12);
   }
   var bubble=N("Bubble");if(bubble){Vector3 p=homePos["Bubble"];p.x+=Mathf.Clamp(session.level,-5,5)*.0009f;DriveTo(bubble,p,homeRot["Bubble"],instant,6);}
   Spin("LockLever",Quaternion.Euler(0,0,session.prepared[7]?-70:0),instant);
   Spin("PDWheel",Quaternion.Euler((session.pd-60)*25,0,0),instant);
   Spin("LevelKnob",Quaternion.Euler(session.level*24,0,0),instant);
   Spin("ForeheadKnob",Quaternion.Euler(0,0,(session.vertex-12)*30),instant);
   if(pdNumber)pdNumber.text=session.pd.ToString("0.#");
   var arm=N("ArmSwing");if(arm){Vector3 p=homePos[arm.name];DriveTo(arm,p,homeRot[arm.name]*Quaternion.Euler(0,session.phoropterIn?0:-90,0),instant,2.6f);}
   var deck=N("LiftDeck");if(deck)DriveTo(deck,homePos[deck.name]+Vector3.up*session.height,homeRot[deck.name],instant,5);
   foreach(var kv in tools){var t=kv.Value.transform;if(t.parent==models&&!busy)t.position=homePos[kv.Key]+Vector3.up*session.height;}
   // vertex: the head moves toward the eye; the forehead rest keeps touching the brow
   var phoro=N("Phoropter");if(phoro){Vector3 p=homePos[phoro.name];p.z-=(session.vertex-12)*.001f;DriveTo(phoro,p,homeRot[phoro.name]*Quaternion.Euler(0,session.phoropterIn?0:90,session.level),instant,5);}
   var rest=N("ForeheadRest");if(rest){Vector3 p=homePos[rest.name];p.z+=(session.vertex-12)*.001f;DriveTo(rest,p,homeRot[rest.name],instant,5);}
   Spin("SwitchLever",Quaternion.Euler(0,0,session.prepared[8]?16:-16),instant);
   foreach(var l in lamps){if(!l)continue;if(l.type==LightType.Directional)l.intensity=session.prepared[8]?.35f:1.15f;else if(l.name.StartsWith("Ceiling"))l.intensity=session.prepared[8]?.18f:.9f;}
   if(post)post.targetExposure=session.prepared[8]?.9f:1f;
  }
  float TurretInAngle(int e){
   string en=EN[e];var tur=N("Turret_"+en);var cell=N("JCCCell_"+en);var ap=N("Aperture_"+en);if(!tur||!cell||!ap)return 0;
   Vector3 c=homePos[cell.name],a=tur.parent.InverseTransformPoint(ap.position)-homePos[tur.name];
   return Vector2.SignedAngle(new Vector2(c.x,c.y),new Vector2(a.x,a.y));
  }

  // ---------------- direct manipulation ----------------
  string ControlState(string id){
   string[] p=id.Split(':');int e=p.Length>1?int.Parse(p[1]):session.eye;var L=session.eyes[e].lens;
   switch(p[0]){
    case "sph":case "strong":return "球面 "+L.s.ToString("+0.00;-0.00;0.00")+" D · 滾輪/左右鍵調整";
    case "cyl":return "柱面 "+L.c.ToString("0.00")+" DC · 左鍵 +、右鍵 −";
    case "axis":return "軸位 "+Mathf.RoundToInt(L.a)+"° · 滾輪轉動";
    case "aux":int s=AuxState(e);return "目前 "+(s==1?"OC 遮蓋":s==2?"PH 針孔":"O 開放")+(e==session.eye?" · 點擊：O ↔ PH":" · 點擊：O ↔ OC");
    case "jcc":return session.jccMode==0?"拖曳支桿轉入；點擊選 A／P":session.jccMode==1?"軸位 A · 點擊轉 45° 到度數 P · 滾輪翻面 · 右鍵收回":"度數 P · 點擊轉回軸位 A · 滾輪翻面 · 右鍵收回";
    case "flip":return "滾輪／點擊翻面比較";
    case "jccarm":return "按住支桿拖曳：轉入窺孔／收回";
    case "pd":return "PD "+session.pd.ToString("0.#")+" mm";
    case "level":return "傾斜 "+session.level.ToString("0")+"°";
    case "vertex":return "頂點距離 "+session.vertex.ToString("0")+" mm（目標 12）";
    case "height":return "高度偏差 "+(session.height*100).ToString("0")+" cm";
    case "lock":return session.prepared[7]?"已鎖定":"未鎖定";
   }
   return "";
  }
  void UseControl(string id,int sign,bool clicked){
   if(busy&&!id.StartsWith("sph")&&!id.StartsWith("cyl")&&!id.StartsWith("axis"))return;
   string[] p=id.Split(':');int e=p.Length>1?int.Parse(p[1]):session.eye;
   switch(p[0]){
    case "sph":AdjustControl(e,0,sign,1);break;
    case "strong":AdjustControl(e,0,sign,12);break;
    case "cyl":AdjustControl(e,1,sign,1);break;
    case "axis":AdjustControl(e,2,sign,1);break;
    case "aux":if(!clicked)return;if(e==session.eye)Pinhole();else Occlude();break;
    case "jcc":if(!clicked){if(e==session.eye&&session.jccMode>0)FlipJCC();return;}if(e!=session.eye){Toast("JCC 只放在目前測試眼 "+session.EyeName+" 前。");return;}if(sign<0){if(session.jccMode!=0)SetJCC(0);}else SetJCC(session.jccMode==1?2:1);break;
    case "flip":if(e==session.eye&&session.jccMode>0)FlipJCC();break;
    case "jccarm":if(touchMode&&clicked&&e==session.eye){SetJCC(session.jccMode==0?1:0);break;}if(clicked&&sign>0&&e==session.eye&&session.phoropterIn){jccDrag=e;var t=N("Turret_"+EN[e]);dragStartAngle=PointerAngle(t);dragStartRotation=Mathf.DeltaAngle(0,t.localEulerAngles.z-homeRot[t.name].eulerAngles.z);}break;
    case "pd":if(!session.prepared[1]&&session.mode=="教學"){Toast("先用 PD 尺量出瞳距，再轉 P.D. 旋鈕設定。");return;}session.pd=Mathf.Clamp(session.pd+sign*.5f,50,76);Knob("PDWheel");break;
    case "level":session.level=Mathf.Clamp(session.level-sign,-6,6);if(Mathf.Abs(session.level)<.1f&&!session.prepared[2]){session.prepared[2]=true;Toast("水平氣泡已置中。");session.Log("調整水平");}Knob("LevelKnob");break;
    case "vertex":session.vertex=Mathf.Clamp(session.vertex-sign,8,22);CheckFit();Knob("ForeheadKnob");break;
    case "height":session.height=Mathf.Clamp(session.height+sign*.01f,-.06f,.14f);if(Mathf.Abs(session.height)<.005f)session.height=0;CheckFit();Knob("ArmCollar");break;
    case "lock":if(!clicked)return;Prepare(7);break;
   }
  }
  void CheckFit(){if(!session.prepared[5]&&Mathf.Abs(session.height)<.001f&&Mathf.Abs(session.vertex-12)<.1f){session.prepared[5]=true;Toast("窺孔對準瞳孔，頂點距離 12 mm。");session.Log(ClinicSession.PrepNames[5]);}}
  void Knob(string n){Reach(N(n));audioOut.PlayOneShot(tick,.8f);SyncMechanics();DrawChart();MarkSave();}
  void AdjustControl(int e,int p,int sign,int steps){
   if(!focused||jccDrag>=0)return;if(!session.phoropterIn){Toast("綜合驗光儀還在旁邊，先把它轉到受測者眼前（坐近後點任一準備動作）。");return;}
   session.Adjust(p,sign,steps,e);parameter=p;
   Reach(N((p==0?(steps>1?"StrongSph_":"Sph_"):p==1?"Cyl_":"AxisKnob_")+EN[e]));
   audioOut.PlayOneShot(steps>1?click:tick,.8f);SyncMechanics();DrawChart();MarkSave();
  }
  void MarkSave(){saveDirty=true;saveAt=Time.time+.6f;}
  void Reach(Transform t){if(!t)return;reachTarget=t;reachUntil=Time.time+.7f;reachLeft=t.position.x<-.005f;}
  void HandPose(float dt){
   if(handScripted||!hands.gameObject.activeSelf)return;
   foreach(string side in new[]{"R","L"}){
    var h=N("Hand_"+side);if(!h)continue;bool active=reachTarget&&Time.time<reachUntil&&(side=="L")==reachLeft;
    Vector3 want=homePos[h.name];Quaternion wq=homeRot[h.name];
    if(active){want=view.transform.InverseTransformPoint(reachTarget.position)+new Vector3(side=="R"?.012f:-.012f,-.012f,-.088f);wq=homeRot[h.name]*Quaternion.Euler(-12,0,side=="R"?-18:18);}
    h.localPosition=Vector3.Lerp(h.localPosition,want,1-Mathf.Exp(-14*dt));h.localRotation=Quaternion.Slerp(h.localRotation,wq,1-Mathf.Exp(-14*dt));
    for(int f=0;f<4;f++){var ft=N("Finger_"+side+f);if(ft)ft.localRotation=Quaternion.Slerp(ft.localRotation,homeRot[ft.name]*Quaternion.Euler(active?28+f*4:6,0,0),1-Mathf.Exp(-12*dt));}
   }
  }

  // ---------------- touch ----------------
  bool touchDragging;
  public void TouchDrag(Vector2 d){
   if(cinematic||paused||notebook||pdStep>=0||tweening)return;touchDragging=true;
   if(!focused&&!patientView){yaw+=d.x*.16f;pitch=Mathf.Clamp(pitch-d.y*.12f,-72,72);view.transform.rotation=Quaternion.Euler(pitch,yaw,0);}
   else if(focused&&held==1){float ang=(session.streak+90)*Mathf.Deg2Rad;sweep=Mathf.Clamp(sweep+(d.x*Mathf.Cos(ang)+d.y*Mathf.Sin(ang))*.006f,-1,1);if(Mathf.Abs(sweep)>.8f)CountSweep();}
   else if(focused){lookYaw=Mathf.Clamp(lookYaw+d.x*.08f,-35,35);lookPitch=Mathf.Clamp(lookPitch-d.y*.06f,-25,20);}
  }
  public void TouchRelease(){touchDragging=false;}
  public void TouchTap(Vector2 screen){
   if(cinematic||paused||notebook||busy&&!focused)return;RaycastHit h;var ray=view.ScreenPointToRay(screen);
   if(pdStep>=0)return;
   if(focused&&!patientView){
    if(Physics.Raycast(ray,out h,1.5f,1<<9)){var t=h.collider.GetComponent<ClinicTarget>();touchSel=t.id;touchSelName=t.label;string[] p=t.id.Split(':');
     if(p[0]=="flip"||p[0]=="lock"||p[0]=="aux"||p[0]=="jcc"||p[0]=="jccarm"){UseControl(t.id,1,true);}else{var tr=t.highlight?t.highlight:t.transform;Reach(tr);audioOut.PlayOneShot(tick,.4f);}}
    else touchSel="";
    return;
   }
   if(!patientView&&Physics.Raycast(ray,out h,3.2f,1<<8)){target=h.collider.GetComponent<ClinicTarget>();Interact();}
  }
  public void TouchStep(int sign){if(touchSel!="")UseControl(touchSel,sign,true);}
  string touchSelName="";
  string TouchSelLabel(){return touchSelName+"\n"+ControlState(touchSel);}

  // ---------------- retinoscopy ----------------
  void CountSweep(){if(Mathf.Abs(Optics.AxisError(session.H.lastStreak,session.streak))>25||session.H.lastStreak<-900){session.H.sweeps++;session.H.lastStreak=session.streak;session.Log("檢影掃動 "+session.streak);}}
  public float ReflexAngle(int e,out float residual){
   residual=Optics.RetResidual(session.Truth(e),session.eyes[e].lens,Optics.WD(session.wdCm),session.streak);
   // Break phenomenon: off a principal meridian the reflex band is skewed toward that meridian.
   Vector3 r=Optics.Power(session.eyes[e].lens)-Optics.Power(session.Truth(e));float cyl=2*Mathf.Sqrt(r.y*r.y+r.z*r.z);float band=session.streak;
   if(cyl>.2f){float axis=.5f*Mathf.Atan2(r.z,r.y)*Mathf.Rad2Deg;float d=Mathf.DeltaAngle(band,axis);if(Mathf.Abs(d)>45)d=Mathf.DeltaAngle(band,axis+90);band+=d*Mathf.Clamp01(cyl/1.2f)*.55f;}
   return band;
  }
  void Retinoscopy(){
   bool on=held==1&&begun&&!notebook;
   streakLight.enabled=on&&focused&&!patientView;
   for(int e=0;e<2;e++){var g=N("PupilReflex_"+e);if(g)g.gameObject.SetActive(on&&focused);}
   if(!on){if(scope)scope.enabled=false;if(streakStrip)streakStrip.gameObject.SetActive(false);return;}
   float phi=(session.streak+90)*Mathf.Deg2Rad;Vector3 dir=new Vector3(Mathf.Cos(phi),Mathf.Sin(phi),0);
   Vector3 eyeP=PupilWorld(session.eye);
   float pupilR=.0021f*(N("Pupil_"+EN[session.eye])?N("Pupil_"+EN[session.eye]).lossyScale.x:1);
   Vector3 intercept=eyeP+dir*sweep*.018f;
   streakLight.transform.position=view.transform.position+view.transform.TransformDirection(new Vector3(0,-.01f,.02f));
   streakLight.transform.rotation=Quaternion.LookRotation(intercept-streakLight.transform.position,Vector3.up);
   // the streak intercept: a line on the face oriented at the streak angle (0° horizontal, 90° vertical as the examiner sees it)
   streakStrip.gameObject.SetActive(focused&&!patientView);streakStrip.position=intercept+new Vector3(0,0,-.006f);streakStrip.rotation=Quaternion.Euler(0,0,session.streak);streakStrip.localScale=new Vector3(.075f,.0042f,1);
   streakMat.SetVector("_Hole",new Vector4(eyeP.x,eyeP.y,eyeP.z,pupilR));
   var slit=N("RetStreak");if(slit)slit.localRotation=Quaternion.Euler(0,0,90-session.streak);
   // streak orientation comes from turning the sleeve roller on the handle, not from twisting the whole scope
   foreach(string sn in new[]{"RetSleeve","RetSleeveKnurl"}){var sl=N(sn);if(sl)sl.localRotation=Quaternion.Slerp(sl.localRotation,Quaternion.Euler(0,session.streak*2,0),Time.deltaTime*10);}
   for(int e=0;e<2;e++){
    float res;float band=ReflexAngle(e,out res);float abs=Mathf.Abs(res);
    float neutral=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.06f,.14f,abs));
    float motion=res<0?1:-1,speed=Mathf.Clamp(.55f/Mathf.Max(abs,.05f),.25f,3f);
    float units=sweep*.018f/Mathf.Max(pupilR,.001f);
    var m=reflex[e];m.SetFloat("_Shift",Mathf.Clamp(motion*speed*units*.14f*(1-neutral),-2.2f,2.2f));m.SetFloat("_Angle",(band+90)*Mathf.Deg2Rad);
    m.SetFloat("_Width",Mathf.Lerp(.22f,.85f,1/(1+abs*1.6f)));m.SetFloat("_Bright",Mathf.Lerp(.35f,1.1f,1/(1+abs*.9f))*(.55f+.45f*Mathf.Clamp01(1-Mathf.Abs(sweep)*.6f)));m.SetFloat("_Neutral",neutral);
   }
   scope.transform.position=eyeP+new Vector3(0,0,-.11f);scope.transform.rotation=Quaternion.identity;scope.enabled=focused&&!notebook&&!patientView;
   // the scope is held up in view: it shifts and tilts with the sweep so you can see where the beam is going
   var rt=N("Retinoscope");if(rt&&rt.parent==view.transform){Vector3 swp=new Vector3(dir.x,dir.y,0)*sweep;rt.localPosition=Vector3.Lerp(rt.localPosition,new Vector3(.105f,-.165f,.29f)+swp*.03f,Time.deltaTime*14);rt.localRotation=Quaternion.Slerp(rt.localRotation,Quaternion.Euler(-swp.y*14,180+swp.x*14,4),Time.deltaTime*14);}
  }

  // ---------------- session flow ----------------
  public void Begin(string mode){
   string mark="tools";
   try{
    Time.timeScale=1;remoteOwned=false;projectorOn=true;jccDrag=-1;ReturnTools();held=0;mark="session";session=new ClinicSession();session.patient=ui.selectedPatient;if(session.patient==0)session.Randomize(System.Environment.TickCount);session.mode=mode;session.wdCm=PlayerPrefs.GetFloat("ClinicWD",57);session.EnterStage();begun=true;paused=false;notebook=false;focused=false;patientView=false;pdStep=-1;cinematic=false;handScripted=false;
    mark="camera";walkPos=new Vector3(.6f,1.65f,.3f);view.transform.position=walkPos;yaw=0;pitch=0;view.transform.rotation=Quaternion.identity;targetFov=66;hands.gameObject.SetActive(true);ui.menu.SetActive(false);
    mark="patient";ApplyPatient();mark="mechanics";SyncMechanics(true);mark="chart";DrawChart();mark="lock";Lock();mark="say";Say("您好，我看遠有點模糊，今天想檢查度數。",true);mark="save";SaveProgress();
   }catch(Exception ex){Debug.LogError("CLINIC_BEGIN_FAILED at "+mark+": "+ex);begun=true;ui.menu.SetActive(false);hands.gameObject.SetActive(true);}
  }
  public void Resume(){if(!PlayerPrefs.HasKey("ClinicSession"))return;session=JsonUtility.FromJson<ClinicSession>(PlayerPrefs.GetString("ClinicSession"));remoteOwned=PlayerPrefs.GetInt("ClinicRemote",0)==1||session.prepared[6];projectorOn=true;if(session.stage>0)session.phoropterIn=true;begun=true;paused=false;notebook=false;focused=false;patientView=false;busy=false;Time.timeScale=1;pdStep=-1;cinematic=false;walkPos=new Vector3(.6f,1.65f,.3f);view.transform.position=walkPos;view.transform.rotation=Quaternion.identity;yaw=0;pitch=0;targetFov=66;hands.gameObject.SetActive(true);ui.menu.SetActive(false);ApplyPatient();SyncMechanics(true);DrawChart();Lock();Toast("已恢復病例與教學進度。");}
  void LoadProgress(){ui.canResume=PlayerPrefs.HasKey("ClinicSession");}
  public void SaveProgress(){if(begun){PlayerPrefs.SetInt("ClinicRemote",remoteOwned?1:0);PlayerPrefs.SetString("ClinicSession",JsonUtility.ToJson(session));PlayerPrefs.Save();if(ui)ui.canResume=true;}}
  public void PointerState(string state){lookCaptured=state=="1";}
  public void Lock(){
   if(touchMode)return;
   #if UNITY_WEBGL && !UNITY_EDITOR
   if(!focused&&!notebook&&!paused&&pdStep<0&&!Application.absoluteURL.Contains("qa=1"))try{ClinicCapture(1);}catch(Exception){}
   #else
   if(!focused&&!notebook&&!paused&&pdStep<0)Cursor.lockState=CursorLockMode.Locked;Cursor.visible=Cursor.lockState!=CursorLockMode.Locked;
   #endif
  }
  public void Release(){lookCaptured=false;
   #if UNITY_WEBGL && !UNITY_EDITOR
   ClinicCapture(0);
   #else
   Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
   #endif
  }
  public void Focus(){
   if(busy||pdStep>=0)return;if(patientView){PatientPOV();return;}
   if(!focused){if(Vector3.Distance(view.transform.position,new Vector3(0,1.445f,4.23f))>3.2f){Toast("先走近受測者與驗光儀，再按 F 坐近。");return;}returnPos=walkPos;focused=true;Release();GoTo(FocusPoint(),Quaternion.identity);targetFov=FocusFov();Toast(session.phoropterIn?"坐近操作 · 滑鼠指向旋鈕：滾輪轉動、左鍵＋右鍵−；F 回診間。":"驗光儀還轉在一旁；做水平、桌高等準備時會自動轉到受測者眼前。");}
   else{focused=false;GoTo(returnPos,Quaternion.Euler(pitch,yaw,0));targetFov=66;Lock();}
   DrawChart();
  }
  float FocusFov(){return held==1?34:46;}
  Vector3 FocusPoint(){return held==1?PupilWorld(session.eye)-new Vector3(0,0,session.wdCm/100):new Vector3(0,1.455f,3.93f);}
  public void GoTo(Vector3 p,Quaternion r){fromPos=view.transform.position;fromRot=view.transform.rotation;toPos=p;toRot=r;tweenTime=0;tweening=true;}
  public void PatientPOV(){if(busy||pdStep>=0)return;patientView=!patientView;Release();if(!patientView){targetFov=focused?FocusFov():66;GoTo(focused?FocusPoint():walkPos,Quaternion.Euler(focused?0:pitch,focused?0:yaw,0));}else Toast("受測者視野：畫面模糊為示意；C 回驗光師。");DrawChart();}
  public void Interact(){
   if(!target){Toast("走近桌面工具或受測者，對準後按 E；F 坐近驗光儀。");return;}
   string id=target.id;
   if(id=="phoropter"||id=="patient"){if(held==3)Sanitize();else if(held==4)PDBegin();else if(id=="phoropter")Focus();else StartCoroutine(Action("greet",()=>Say("我平常看遠有點糊，沒有突然看不見，也沒有疼痛。",true)));}
   else if(id=="sanitize"){HoldTool("CleaningCloth");held=3;Toast("已拿酒精棉布。走到驗光儀旁按 E：先噴酒精，再擦額托、面罩與 PD 尺。");}
   else if(id=="pd"){HoldTool("PDRuler");held=4;Toast(session.prepared[0]?"已拿 PD 尺。走近受測者按 E 開始量瞳距。":"已拿 PD 尺。PD 尺會碰到受測者臉部，記得先消毒。");}
   else if(id=="light")Prepare(8);else if(id=="retino")HoldRet();else if(id=="paper")ToggleNotebook();else if(id=="remote"){remoteOwned=true;Release();SaveProgress();Toast("已拿視標遙控器。左下角按鍵可直接換表。");}else if(id=="chart")PatientPOV();
  }
  static bool NeedsPhoropter(int i){return i>=2&&i<=7&&i!=6;}
  public void Prepare(int i){
   if(busy||pdStep>=0)return;
   if(i==0){Sanitize();return;}if(i==1){PDBegin();return;}
   if(!focused&&i!=8&&Vector3.Distance(view.transform.position,new Vector3(0,1.445f,4.23f))>1.4f){Toast("先走近受測者，按 F 坐近驗光儀，再進行這項操作。");return;}
   StartCoroutine(PrepareRoutine(i));
  }
  IEnumerator PrepareRoutine(int i){
   if(NeedsPhoropter(i)&&!session.phoropterIn)yield return SwingPhoropter(true);
   yield return Action("prep"+i,()=>{
    session.prepared[i]=true;if(i==2)session.level=0;if(i==3||i==4){session.ph=false;session.occluded=false;}if(i==5){session.height=0;session.vertex=12;}if(i==6){session.chart=1;remoteOwned=true;}
    SyncMechanics();DrawChart();Toast(ClinicSession.PrepNames[i]+"完成");session.Log(ClinicSession.PrepNames[i]);SaveProgress();
   });
  }
  public IEnumerator SwingPhoropter(bool inFront){
   if(session.phoropterIn==inFront)yield break;bool was=busy;busy=true;session.phoropterIn=inFront;SyncMechanics();audioOut.PlayOneShot(click,.6f);
   Toast(inFront?"把綜合驗光儀轉到受測者眼前。":"把綜合驗光儀轉開到一旁。");
   yield return new WaitForSeconds(1.5f);busy=was;
  }
  public void HoldRet(){if(busy||pdStep>=0)return;if(held==1){ReturnTools();held=0;targetFov=focused?FocusFov():66;Toast("已放回檢影鏡。");}else{HoldTool("Retinoscope");held=1;Toast("檢影鏡已拿起。F 坐近後：按住左鍵拖曳滑鼠掃動光帶，Z / X 旋轉光帶。");if(focused){GoTo(FocusPoint(),Quaternion.identity);targetFov=FocusFov();}}session.Log("拿取／放回檢影鏡");}
  void HoldTool(string name){ReturnTools();var t=N(name);if(!t)return;t.SetParent(view.transform,true);t.localPosition=new Vector3(.16f,-.19f,.37f);t.localRotation=name=="PDRuler"?Quaternion.Euler(-60,0,8):name=="CleaningCloth"?Quaternion.Euler(-40,0,0):Quaternion.Euler(0,180,8);}
  void ReturnTools(){foreach(var kv in tools){var t=kv.Value.transform;t.SetParent(models,true);t.position=homePos[kv.Key]+Vector3.up*session.height;t.rotation=homeRot[kv.Key];}var pump=N("SanitizerPump");if(pump)pump.localPosition=homePos["SanitizerPump"];}
  public void Adjust(int sign){if(!focused)return;AdjustControl(session.eye,parameter,sign,1);}
  public void SetParameter(int p){parameter=p;Toast(new[]{"球面 · 每格 0.25 D（外側大轉盤）","負柱面 · 每格 0.25 DC（內鈕；每 -0.50DC 自動 +0.25DS）","散光軸 · 每格 5°（外圈軸鈕）"}[p]);}
  public void SelectEye(){if(busy)return;session.eye=1-session.eye;session.jccMode=0;session.ph=false;SyncMechanics();if(focused&&held==1)GoTo(FocusPoint(),Quaternion.identity);DrawChart();Toast("目前測試 "+session.EyeName);}
  public void Occlude(){if(busy)return;if(!session.phoropterIn){Toast("先把驗光儀轉到受測者眼前。");return;}StartCoroutine(Action("occluder",()=>{session.occluded=!session.occluded;SyncMechanics();Toast(session.occluded?"非測試眼的輔助鏡片轉到 OC（遮蓋）。":"非測試眼轉回 O（開放）。");}));}
  public void Pinhole(){if(busy)return;if(!session.phoropterIn){Toast("先把驗光儀轉到受測者眼前。");return;}StartCoroutine(Action("pinhole",()=>{session.ph=!session.ph;SyncMechanics();DrawChart();Toast(session.ph?"測試眼輔助鏡片轉到 PH（針孔）。":"測試眼輔助鏡片轉回 O。");}));}
  public void ChangeChart(int c){if(busy)return;remoteOwned=true;projectorOn=true;StartCoroutine(Action("remote",()=>{session.chart=c;if(c==1){session.prepared[6]=true;session.Log(ClinicSession.PrepNames[6]);}DrawChart();MarkSave();Toast(new[]{"E 字視標","紅綠視標","JCC 蜂巢點陣"}[c]);}));}
  public void Row(int sign){int k=0;for(int i=0;i<Optics.Rows.Length;i++)if(Mathf.Abs(session.row-Optics.Rows[i])<.01f)k=i;k=Mathf.Clamp(k+sign,0,Optics.Rows.Length-1);session.row=Optics.Rows[k];DrawChart();audioOut.PlayOneShot(tick);}
  public void Fog(){if(busy)return;float amount=session.Phase==5?.5f:1;var e=session.eye;StartCoroutine(Action("knob:Sph_"+EN[e],()=>{Rx r=session.H.lens;r.s+=amount;session.H.lens=r;if(session.Phase==5)session.H.secondFogged=true;else session.H.fogged=true;session.H.highestFog=amount;session.Log("霧視 +"+amount.ToString("0.00")+"D");SyncMechanics();DrawChart();Toast("加 +"+amount.ToString("0.00")+" D 霧視；逐格退正並反覆確認視力。");SaveProgress();}));}
  public void CycleJCC(){if(busy)return;SetJCC((session.jccMode+1)%3);}
  // What the answer means for the lens (tutorial/free modes): face 1 = red dot on the reference axis.
  void JCCTeach(){if(session.mode=="考試")return;string a=session.AskJCC();bool first=a.StartsWith("第一"),same=a.StartsWith("兩片");
   if(session.Searching){float ang=session.H.searchAngle;Toast("紅點在 "+ang.ToString("0")+"°："+(same?"兩面一樣 → 這方向沒有散光，轉下一個 45°":first?"選紅點（第一面）→ 接受這方向的負柱面：柱面加 −0.25、軸放 "+ang.ToString("0")+"°":"選白點（第二面）→ 拒絕這方向的負柱面，轉下一個 45°"));return;}
   if(session.jccMode==2)Toast(same?"P：兩面一樣 → 柱面度數到位":first?"P：第一面（紅點對軸）較清楚 → 柱面加 −0.25（每 −0.50 柱面補 +0.25 球面）":"P：第二面（白點對軸）較清楚 → 柱面減少 0.25（往 + 方向）；柱面已是 0 時改查其他方向");
   else if(session.jccMode==1)Toast(same?"A：兩面一樣 → 軸位到位":first?"A：第一面較清楚 → 追紅點：軸度數加大（+5°）":"A：第二面較清楚 → 追紅點：軸度數減小（−5°）");}
  void SetJCC(int mode){if(!session.phoropterIn){Toast("先把驗光儀轉到受測者眼前。");return;}StartCoroutine(Action("jcc-swing",()=>{session.jccMode=mode;SyncMechanics();Toast(new[]{"轉盤收回 JCC","JCC 轉入 · 軸位 A：滾輪手柄與柱面軸同向，紅白點在兩側 45°","JCC 轉 45° · 度數 P：紅點與柱面軸平行"}[mode]);}));}
  public void Streak(int sign){Reach(N("RetSleeve"));session.streak=Optics.Axis(session.streak+sign*15);if(session.streak>=180)session.streak=0;session.Log("光帶旋轉 "+session.streak);audioOut.PlayOneShot(tick,.6f);}
  public void Streak90(){Reach(N("RetSleeve"));session.streak=Optics.Axis(session.streak+90);if(session.streak>=180)session.streak=0;session.Log("光帶轉 90° → "+session.streak);audioOut.PlayOneShot(click,.5f);Toast("撥動套筒滾輪：光帶轉到 "+StreakName()+"，掃另一條主子午線。");}
  public string StreakName(){float a=session.streak%180;return a+"°"+(a==0?"（水平）":a==90?"（垂直）":"");}
  public void Sweep(){if(busy)return;if(held!=1){Toast("請先拿起檢影鏡。");return;}StartCoroutine(SweepRoutine());}
  IEnumerator SweepRoutine(){
   busy=true;animationLog.Add("retino-sweep");
   for(float t=0;t<1;t+=Time.deltaTime/1.8f){sweep=Mathf.Sin(t*Mathf.PI*4)*.95f;yield return null;}
   sweep=0;CountSweep();float r=Optics.RetResidual(session.Truth(session.eye),session.H.lens,Optics.WD(session.wdCm),session.streak);
   if(session.mode!="考試")Say(Mathf.Abs(r)<.125f?"（光帶中和：瞳孔整片亮起，看不出移動）":r<0?"（反射光與光帶同向移動：順動）":"（反射光與光帶反向移動：逆動）",false);
   busy=false;
  }
  public static readonly float[] WDOptions={40,50,57,60,67,75,80,100};
  public void ShiftWD(int dir){
   if(busy)return;if(session.wdRemoved){Toast("已從兩眼扣除工作距離，不能再改；要換工作距離請重新開始病例。");return;}
   int k=0;float best=999;for(int i=0;i<WDOptions.Length;i++){float d=Mathf.Abs(WDOptions[i]-session.wdCm);if(d<best){best=d;k=i;}}
   k=Mathf.Clamp(k+dir,0,WDOptions.Length-1);session.wdCm=WDOptions[k];PlayerPrefs.SetFloat("ClinicWD",session.wdCm);
   if(focused&&held==1)GoTo(FocusPoint(),Quaternion.identity);audioOut.PlayOneShot(tick,.5f);
   Toast("工作距離 "+session.wdCm+" cm ＝ "+Optics.WD(session.wdCm).ToString("0.00")+" D（檢影後從兩眼球面扣除）");MarkSave();
  }
  public void WorkingDistance(){ShiftWD(1);}
  public void SetWD(float cm){int k=System.Array.IndexOf(WDOptions,cm);if(k<0)return;int cur=0;float best=999;for(int i=0;i<WDOptions.Length;i++){float d=Mathf.Abs(WDOptions[i]-session.wdCm);if(d<best){best=d;cur=i;}}if(k!=cur)ShiftWD(k-cur);else Toast("工作距離 "+cm+" cm ＝ "+Optics.WD(cm).ToString("0.00")+" D");}
  public void RemoveWD(){if(busy)return;if(session.wdRemoved){Toast("已扣除工作距離，避免重複扣除。");return;}StartCoroutine(Action("knob:Sph_OD",()=>{float wd=Optics.WD(session.wdCm);foreach(var h in session.eyes){Rx r=h.lens;r.s-=wd;h.lens=r;}session.wdRemoved=true;session.Log("雙眼扣 WD "+wd);SyncMechanics();Toast("雙眼球面各扣 "+wd.ToString("0.00")+"D；柱面與軸不變。");}));}
  public void Ask(){
   if(busy||!focused)return;
   if(!session.occluded&&session.stage>=4){Toast("先把非測試眼的輔助鏡片轉到 OC，受測者才會只用測試眼比較。");return;}
   if(session.stage==1||session.stage==2){Sweep();return;}
   if(!projectorOn){Toast("請先開啟投影視標。");return;}
   if(session.chart==1){StartCoroutine(Action("ask",()=>{session.H.duoAsked=true;string d=Optics.Duo(session.Truth(session.eye),session.H.lens);Say(d,true);if(session.mode!="考試")Toast(d.StartsWith("紅")?"紅色清楚 → 加 −0.25（加負）":"綠色清楚 → 若剛才還是紅色，這就是第一次綠色：停在這裡記錄；一開始就綠 → 加 +0.25 再問");session.Log("紅綠問答");}));return;}
   if(session.jccMode>0){StartCoroutine(CompareJCC());return;}
   StartCoroutine(Action("ask",()=>{session.Read();int n=session.H.lastLetters;int ri=Optics.RowIndex(session.row);string[] word={"右","上","左","下"};string s="";for(int i=0;i<5;i++)s+=(i<n?word[Optics.RowRot(ri,i)]:"看不清楚")+(i<4?"、":"。");Say("這一排："+s+(n<5?"後面有點糊。":""),true);
    float bv,lm;string lab=Optics.VALabel(session.row,n,out bv,out lm);if(session.mode!="考試")Toast("列 "+session.row.ToString("0.0")+" 讀對 "+n+"/5"+(lab!=""?" → 記為 "+lab:" → 這列讀不到，換大一列")+"（3–4 個記「該列 −錯幾個」，1–2 個記「上一列 +讀對幾個」）");SaveProgress();}));
  }
  void FlipJCC(){if(busy||!session.occluded){Toast("先遮蓋非測試眼，再翻面比較。");return;}StartCoroutine(FlipOnce());}
  IEnumerator FlipOnce(){busy=true;int e=session.eye;var t=N("JCCFlip_"+EN[e]);jccFace[e]=!jccFace[e];Reach(N("JCCCell_"+EN[e]));
   if(t)DriveTo(t,homePos[t.name],homeRot[t.name]*Quaternion.AngleAxis(jccFace[e]?180:0,Vector3.right),false,14);
   audioOut.PlayOneShot(click,.5f);yield return new WaitForSeconds(.28f);
   if(!jccFace[e]){session.RegisterJCC();Say(session.AskJCC(),true);JCCTeach();}
   else Toast("JCC 已翻面；再滾一格看另一面並比較。");busy=false;SaveProgress();}
  IEnumerator CompareJCC(){
   busy=true;animationLog.Add("jcc-compare");string en=EN[session.eye];Transform flip=N("JCCFlip_"+en);Reach(N("JCCCell_"+en));
   Say("第一面……第二面，哪一面比較清楚？",false);
   Quaternion rest=flip?homeRot[flip.name]:Quaternion.identity;
   for(int i=0;i<2;i++){
    if(flip)DriveTo(flip,homePos[flip.name],rest*Quaternion.AngleAxis(i==0?0:179.9f,Vector3.right),false,10);
    for(float t=0;t<.45f;t+=Time.deltaTime){reachUntil=Time.time+.3f;yield return null;}
    audioOut.PlayOneShot(click,.5f);yield return new WaitForSeconds(.5f);
   }
   if(flip)DriveTo(flip,homePos[flip.name],rest,false,10);
   jccFace[session.eye]=false;session.RegisterJCC();Say(session.AskJCC(),true);JCCTeach();busy=false;session.Log("JCC 兩面比較");SaveProgress();
  }
  public void Record(){if(busy)return;StartCoroutine(Action("record",()=>{session.Save();Toast("已寫入紙本紀錄："+session.EyeName+" "+session.H.lens);SaveProgress();}));}
  public void Next(){if(busy||pdStep>=0)return;var missing=session.Missing();if(session.mode=="教學"&&missing.Count>0){Toast("還需要："+missing[0]);return;}ReturnTools();held=0;targetFov=focused?FocusFov():66;session.Advance();if(session.stage>0&&!session.phoropterIn){session.phoropterIn=true;}SyncMechanics();DrawChart();SaveProgress();if(session.completed){notebook=true;Release();Toast("期中流程完成，請查看雙眼紀錄與回饋。");}else{Toast(session.StageTitle);if(focused)GoTo(FocusPoint(),Quaternion.identity);}Emit();}
  public void ToggleNotebook(){if(!begun||busy||pdStep>=0)return;notebook=!notebook;if(notebook)Release();else if(!focused)Lock();}
  public void Say(string text,bool voice){speech=text;speechUntil=Time.time+9;talkTime=Time.time+Mathf.Clamp(text.Length*.10f,1,5);if(voice&&ui.voice){
   #if UNITY_WEBGL && !UNITY_EDITOR
    try{ClinicSpeak(text);}catch(Exception){}
   #endif
   }lastAction=text;}
  public void Toast(string text){lastAction=text;ui.toastUntil=Time.time+6;}
  Transform Contact(string kind){
   string en=EN[session.eye],other=EN[1-session.eye];
   if(kind.StartsWith("knob:"))return N(kind.Substring(5));
   switch(kind){case "prep2":return N("LevelKnob");case "prep3":case "prep4":return N("Aux_"+en);case "prep5":return N("ForeheadKnob");case "prep7":return N("LockLever");
    case "jcc-swing":return N("JCCCell_"+en);case "occluder":return N("Aux_"+other);case "pinhole":return N("Aux_"+en);}
   return null;
  }
  IEnumerator Action(string kind,Action effect){
   busy=true;animationLog.Add(kind);if(animationLog.Count>100)animationLog.RemoveAt(0);
   string prop=null;if(kind=="remote"||kind=="prep6")prop="Remote";if(kind=="record")prop="Clipboard";
   Transform tool=prop!=null?N(prop):null;Transform parent=tool?tool.parent:null;Vector3 tp=tool?tool.position:Vector3.zero;Quaternion tq=tool?tool.rotation:Quaternion.identity;
   if(tool){tool.SetParent(view.transform,true);tool.localPosition=new Vector3(.08f,-.14f,.38f);tool.localRotation=kind=="record"?Quaternion.Euler(25,0,0):Quaternion.Euler(-20,0,0);}
   Transform pen=kind=="record"?N("Pen"):null;if(pen){pen.SetParent(view.transform,true);pen.localPosition=new Vector3(.17f,-.12f,.33f);pen.localRotation=Quaternion.Euler(30,0,-35);}
   Transform contact=focused?Contact(kind):null;
   float duration=kind=="record"?1.2f:kind.StartsWith("knob")?.35f:.6f;bool applied=false;
   for(float t=0;t<1;t+=Time.deltaTime/duration){
    if(contact)Reach(contact);
    if(tool&&kind!="record")tool.localPosition=new Vector3(.08f,-.14f+Mathf.Sin(t*Mathf.PI)*.04f,.38f);
    if(pen)pen.localPosition=new Vector3(.12f+Mathf.Sin(t*48)*.022f,-.09f,.33f+t*.035f);
    if(!applied&&t>.45f){applied=true;effect();}
    yield return null;
   }
   if(!applied)effect();
   if(tool){tool.SetParent(parent,true);tool.position=tp;tool.rotation=tq;}
   if(pen){pen.SetParent(models,true);pen.position=homePos["Pen"];pen.rotation=homeRot["Pen"];}
   busy=false;ui.Refresh();Emit();
  }

  // ---------------- hygiene: alcohol on the cloth, then forehead rest, face shields, PD rule ----------------
  public void Sanitize(){if(busy||pdStep>=0)return;StartCoroutine(SanitizeRoutine());}
  IEnumerator SanitizeRoutine(){
   busy=true;cinematic=true;handScripted=true;Release();animationLog.Add("prep0");
   bool wasFocused=focused;Vector3 camPos=view.transform.position;Quaternion camRot=view.transform.rotation;float camFov=targetFov;
   if(session.phoropterIn){session.phoropterIn=false;SyncMechanics();}
   ReturnTools();held=0;
   Transform phoro=N("Phoropter"),bottle=N("Sanitizer"),cloth=N("CleaningCloth"),ruler=N("PDRuler"),pump=N("SanitizerPump"),nozzle=N("SanitizerNozzle");
   Transform hr=N("Hand_R"),hl=N("Hand_L");
   for(float t=0;t<1.4f;t+=Time.deltaTime)yield return null;   // the arm finishes swinging aside
   Vector3 rear=phoro.forward,up=phoro.up,side=phoro.right,c=phoro.TransformPoint(new Vector3(0,-.2f,0));
   Vector3 shot=c+rear*.78f+up*.13f+side*.22f;GoTo(shot,Quaternion.LookRotation(c+rear*.03f+up*.02f-shot,Vector3.up));targetFov=40;
   Toast("消毒：先把 75% 酒精噴在棉布上，再擦額托、兩側面罩與 PD 尺。");
   while(tweening)yield return null;
   Transform cam=view.transform;
   // 1. right hand on the spray bottle, left hand holding the cloth open; two pumps of alcohol
   bottle.SetParent(cam,true);cloth.SetParent(cam,true);
   Vector3 bFrom=bottle.localPosition,cFrom=cloth.localPosition;Quaternion bq=bottle.localRotation,cq=cloth.localRotation;
   Vector3 bLocal=new Vector3(.10f,-.085f,.56f),cLocal=new Vector3(-.035f,-.075f,.58f);
   for(float t=0;t<1;t+=Time.deltaTime/.55f){float k=Mathf.SmoothStep(0,1,t);
    bottle.localPosition=Vector3.Lerp(bFrom,bLocal,k);bottle.localRotation=Quaternion.Slerp(bq,Quaternion.Euler(0,200,-14),k);
    cloth.localPosition=Vector3.Lerp(cFrom,cLocal,k);cloth.localRotation=Quaternion.Slerp(cq,Quaternion.Euler(-82,0,8),k);
    HoldBottle(hr,bottle,k);HoldFlat(hl,cloth,k);yield return null;}
   for(int press=0;press<2;press++){
    audioOut.PlayOneShot(spray,.9f);
    for(float t=0;t<1;t+=Time.deltaTime/.34f){float d=Mathf.Sin(t*Mathf.PI);if(pump)pump.localPosition=homePos["SanitizerPump"]+Vector3.down*.007f*d;
     if(t<.55f&&nozzle)Emit(mist,nozzle.position,(cloth.position-nozzle.position).normalized*.6f,.007f,.5f,4);HoldBottle(hr,bottle,1);HoldFlat(hl,cloth,1);yield return null;}
    yield return new WaitForSeconds(.14f);
   }
   for(float t=0;t<1;t+=Time.deltaTime/.35f){bottle.localPosition=Vector3.Lerp(bLocal,bLocal+new Vector3(.08f,-.12f,-.05f),t);HoldBottle(hr,bottle,1-t);yield return null;}
   bottle.SetParent(models,true);bottle.position=homePos["Sanitizer"];bottle.rotation=homeRot["Sanitizer"];if(pump)pump.localPosition=homePos["SanitizerPump"];
   // 2. cloth pressed flat under the right palm: forehead rest, then each face shield, inside to out
   cloth.SetParent(models,true);
   Transform pad=N("ForeheadRest");
   var spots=new List<KeyValuePair<string,Vector3>>{new KeyValuePair<string,Vector3>("額托",pad.position+rear*.0065f)};
   foreach(string en in EN){var ap=phoro.InverseTransformPoint(N("Aperture_"+en).position);spots.Add(new KeyValuePair<string,Vector3>(en=="OD"?"右側面罩":"左側面罩",phoro.TransformPoint(ap+new Vector3(0,0,(ZR-ZF)+.0105f+.0028f))));}
   Quaternion clothFlat=Quaternion.LookRotation(up,rear);
   foreach(var s in spots){
    Toast("背面（受測者臉部接觸側）· 擦拭"+s.Key+(s.Key=="額托"?"：額頭接觸面來回擦一遍":"：由窺孔向外繞圈"));
    Vector3 start=cloth.position;Quaternion sq=cloth.rotation;
    for(float t=0;t<1;t+=Time.deltaTime/.45f){float k=Mathf.SmoothStep(0,1,t);cloth.position=Vector3.Lerp(start,s.Value+rear*.012f,k);cloth.rotation=Quaternion.Slerp(sq,clothFlat,k);WipeHand(hr,cloth,up,rear,k,0);if(hl)HoldIdle(hl);yield return null;}
    bool pad1=s.Key=="額托";float dur=pad1?1.2f:1.5f,amp=pad1?.019f:.017f;
    for(float t=0;t<1;t+=Time.deltaTime/dur){float w=t*Mathf.PI*2*(pad1?3:2.2f);float spiral=pad1?1:Mathf.Lerp(.15f,1,t);
     Vector3 o=pad1?side*Mathf.Sin(w)*amp+up*Mathf.Sin(w*2)*.002f:(side*Mathf.Cos(w)+up*Mathf.Sin(w))*amp*spiral;
     cloth.position=s.Value+o+rear*.0042f;cloth.rotation=clothFlat*Quaternion.Euler(0,0,Mathf.Sin(w)*10);WipeHand(hr,cloth,up,rear,1,w);
     if(UnityEngine.Random.value<.55f)Emit(sheen,cloth.position-rear*.004f+side*UnityEngine.Random.Range(-.012f,.012f)+up*UnityEngine.Random.Range(-.01f,.01f),Vector3.zero,.0045f,.8f,1);yield return null;}
   }
   // 3. the PD rule rests on the nose: wipe both faces before it touches the patient
   Toast("擦拭 PD 尺正反兩面（尤其鼻樑凹槽）。");
   ruler.SetParent(cam,true);cloth.SetParent(cam,true);
   Vector3 rFrom=ruler.localPosition,cs=cloth.localPosition;Quaternion rqs=ruler.localRotation,cqs=cloth.localRotation;Vector3 rLocal=new Vector3(-.015f,-.035f,.40f);
   for(float t=0;t<1;t+=Time.deltaTime/.6f){float k=Mathf.SmoothStep(0,1,t);ruler.localPosition=Vector3.Lerp(rFrom,rLocal,k);ruler.localRotation=Quaternion.Slerp(rqs,Quaternion.Euler(0,0,-5),k);cloth.localPosition=Vector3.Lerp(cs,rLocal+new Vector3(.07f,0,-.006f),k);cloth.localRotation=Quaternion.Slerp(cqs,Quaternion.Euler(-90,0,0),k);
    HoldRulerEnd(hl,ruler,k);WipeHand(hr,cloth,cam.up,-cam.forward,k,0);yield return null;}
   for(int face=0;face<2;face++){
    for(float t=0;t<1;t+=Time.deltaTime/1.15f){float x=Mathf.Lerp(.068f,-.06f,t);cloth.position=ruler.TransformPoint(new Vector3(x,Mathf.Sin(t*Mathf.PI*6)*.004f,face==0?-.005f:.005f));cloth.rotation=cam.rotation*Quaternion.Euler(-90,0,0);
     HoldRulerEnd(hl,ruler,1);WipeHand(hr,cloth,cam.up,-cam.forward,1,t*20);if(UnityEngine.Random.value<.6f)Emit(sheen,cloth.position,Vector3.zero,.004f,.8f,1);yield return null;}
    if(face==0){Quaternion a=ruler.localRotation,b=a*Quaternion.Euler(0,180,0);for(float t=0;t<1;t+=Time.deltaTime/.4f){ruler.localRotation=Quaternion.Slerp(a,b,Mathf.SmoothStep(0,1,t));HoldRulerEnd(hl,ruler,1);yield return null;}}
   }
   ReturnTools();session.prepared[0]=true;session.Log(ClinicSession.PrepNames[0]);Toast("消毒完成：額托、雙側面罩與 PD 尺。");
   handScripted=false;cinematic=false;targetFov=camFov;GoTo(camPos,camRot);while(tweening)yield return null;
   busy=false;if(!wasFocused&&!focused)Lock();SaveProgress();Emit();
  }
  // Hand poses in world space: the wrist pivot sits ~7 cm behind the fingertips, fingers along +Z, palm toward -Y.
  void HandWorld(Transform h,Vector3 wrist,Quaternion rot,float k,float curl){
   if(!h)return;h.position=Vector3.Lerp(h.parent.TransformPoint(homePos[h.name]),wrist,k);h.rotation=Quaternion.Slerp(h.parent.rotation*homeRot[h.name],rot,k);
   string side=h.name.EndsWith("R")?"R":"L";for(int f=0;f<4;f++){var ft=N("Finger_"+side+f);if(ft)ft.localRotation=homeRot[ft.name]*Quaternion.Euler(curl*k,0,0);}
  }
  void HoldBottle(Transform h,Transform bottle,float k){var cam=view.transform;Vector3 f=(cam.forward*.75f-cam.right*.66f).normalized;HandWorld(h,bottle.position-f*.05f+cam.right*.02f-cam.up*.01f,Quaternion.LookRotation(f,(cam.up*.8f+cam.right*.6f).normalized),k,70);}
  void HoldFlat(Transform h,Transform cloth,float k){var cam=view.transform;Vector3 f=(cam.forward*.7f+cam.right*.7f).normalized;HandWorld(h,cloth.position-f*.06f+cam.up*.035f,Quaternion.LookRotation(f,cam.up),k,55);}
  void HoldRulerEnd(Transform h,Transform ruler,float k){var cam=view.transform;HandWorld(h,ruler.TransformPoint(new Vector3(-.075f,0,0))-cam.right*.045f-cam.up*.012f,Quaternion.LookRotation(cam.right,cam.up),k,48);}
  void HoldIdle(Transform h){h.localPosition=Vector3.Lerp(h.localPosition,homePos[h.name]+new Vector3(0,-.08f,0),.1f);h.localRotation=Quaternion.Slerp(h.localRotation,homeRot[h.name],.1f);}
  void WipeHand(Transform h,Transform cloth,Vector3 surfaceUp,Vector3 outward,float k,float w){
   Vector3 fingers=(surfaceUp*.87f-outward*.5f).normalized;HandWorld(h,cloth.position-fingers*.055f+outward*.016f,Quaternion.LookRotation(fingers,outward)*Quaternion.Euler(0,0,Mathf.Sin(w)*6),k,10);
  }

  // ---------------- PD rule measurement ----------------
  public Vector3 PDEye(int which){return new Vector3(which==1?-.032f:which==2?.032f:0,1.445f,4.075f);}
  public void PDBegin(){
   if(busy||pdStep>=0)return;
   if(session.mode=="教學"&&!session.prepared[0]){Toast("PD 尺會碰到受測者鼻樑：先拿棉布消毒儀器與 PD 尺。");return;}
   if(Vector3.Distance(view.transform.position,new Vector3(0,1.445f,4.4f))>2.6f&&!focused){Toast("拿著 PD 尺走近受測者，再按 E。");return;}
   StartCoroutine(PDEnter());
  }
  IEnumerator PDEnter(){
   busy=true;Release();returnFocused=focused;if(!focused)returnPos=walkPos;
   if(session.phoropterIn)yield return SwingPhoropter(false);
   ReturnTools();held=4;var ruler=N("PDRuler");ruler.SetParent(models,true);ruler.rotation=Quaternion.identity;
   float odEdge=PupilWorld(0).x-.0021f*N("Pupil_OD").lossyScale.x;ruler.position=new Vector3(odEdge+.0355f+.0032f,1.4283f,4.4498f);
   pdStep=0;examinerEye=0;pdInstructed=false;pdReading=Mathf.Round(session.TruePD)-3;session.pdZeroError=0;
   GoTo(PDEye(0),Quaternion.identity);targetFov=16;focused=false;
   Toast("PD 尺放在鼻樑上。先按空白鍵告訴受測者：「請看我的這隻眼睛。」");
   while(tweening)yield return null;busy=false;animationLog.Add("pd-enter");
  }
  void PDInput(){
   if(busy)return;
   if(Input.GetKeyDown(KeyCode.Space))PDInstruct();
   if(Input.GetKeyDown(KeyCode.Q))PDClose(1);
   if(Input.GetKeyDown(KeyCode.E))PDClose(2);
   int s=Input.GetKeyDown(KeyCode.RightArrow)?1:Input.GetKeyDown(KeyCode.LeftArrow)?-1:0;float sc=Input.mouseScrollDelta.y;if(Mathf.Abs(sc)>.1f)s=sc>0?1:-1;
   if(s!=0)PDNudge(s);
   if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter))PDConfirm();
   if(Input.GetKeyDown(KeyCode.F))PDExit(false);
  }
  public void PDInstruct(){if(pdStep<0)return;pdInstructed=true;Say("（你）請看著我張開的這隻眼睛，不要動。　受測者：好。",false);if(pdStep==0)pdStep=1;Toast("先閉上你的右眼（Q），用左眼對準受測者的右眼。");}
  public void PDClose(int eye){
   if(pdStep<0||busy)return;
   if(session.mode=="教學"){
    if(!pdInstructed){Toast("先按空白鍵請受測者看著你的眼睛。");return;}
    if(eye==1&&pdStep>2){Toast("零點已對好，現在要閉左眼（E）讀左眼。");return;}
    if(eye==2&&pdStep<3){Toast("先閉右眼（Q），把 0 對準受測者右眼瞳孔的右緣。");return;}
   }
   examinerEye=eye;GoTo(PDEye(eye),Quaternion.identity);audioOut.PlayOneShot(tick,.5f);
   if(eye==1){if(pdStep<2)pdStep=2;Toast("你閉右眼、用左眼看；受測者看著你的左眼。← → 移動 PD 尺，讓 0 對準他右眼瞳孔的右緣（顳側），Enter 確認。");}
   if(eye==2){if(pdStep==3)pdStep=4;Toast("你閉左眼、改用右眼看；受測者看著你的右眼。讀他左眼瞳孔靠右眼那一側（鼻側）的刻度，← → 選數值，Enter 記錄。");}
   if(eye==0)Toast("雙眼都張開時受測者會看你兩眼之間，量測會有視差。");
  }
  public void PDNudge(int s){if(pdStep<0)return;if(pdStep<=2||pdStep==3){var r=N("PDRuler");r.position+=new Vector3(s*.00025f,0,0);audioOut.PlayOneShot(tick,.35f);}else if(pdStep==4){pdReading=Mathf.Clamp(pdReading+s*.5f,45,80);audioOut.PlayOneShot(tick,.35f);}}
  float RulerCoord(Vector3 edge,Vector3 viewer){var r=N("PDRuler");var zero=N("RulerZero");float zr=r.position.z-.0008f;Vector3 p=viewer+(edge-viewer)*((zr-viewer.z)/(edge.z-viewer.z));return (p.x-zero.position.x)*1000;}
  Vector3 PupilLeftEdge(int e){var p=N("Pupil_"+EN[e]);return p.position+p.rotation*Vector3.left*.0021f*p.lossyScale.x;}
  public float PDZeroError(){return RulerCoord(PupilLeftEdge(0),PDEye(examinerEye));}
  public float PDExpected(){return RulerCoord(PupilLeftEdge(1),PDEye(examinerEye));}
  public void PDConfirm(){
   if(pdStep<0||busy)return;bool teach=session.mode=="教學";
   if(pdStep<=2){
    if(teach&&examinerEye!=1){Toast("對零點時要閉右眼（Q），用左眼正對受測者右眼。");return;}
    float err=PDZeroError();session.pdZeroError=err;
    if(teach&&Mathf.Abs(err)>.6f){Toast("0 還沒對準右眼瞳孔右緣：目前差 "+Mathf.Abs(err).ToString("0.0")+" mm，往"+(err<0?"左（←）":"右（→）")+"移。");return;}
    pdStep=3;session.Log("PD 零點對準 誤差 "+err.ToString("0.0")+"mm");Toast("零點已對準。換眼：閉上你的左眼、張開右眼（E）。");return;
   }
   if(pdStep==3){Toast("先閉左眼、張開右眼（E），再讀左眼。");return;}
   float expected=PDExpected();
   if(teach&&examinerEye!=2){Toast("讀左眼時要閉左眼、用右眼看（E）。");return;}
   if(teach&&Mathf.Abs(pdReading-expected)>.6f){Toast("你選的 "+pdReading.ToString("0.0")+" mm 和尺上看到的不一樣，再看一次瞳孔鼻側邊緣對到哪一格。");return;}
   session.pd=pdReading;session.pdMeasured=pdReading;session.prepared[1]=true;
   float off=pdReading-session.TruePD;session.Log("PD 量測 "+pdReading.ToString("0.0")+"mm（零點誤差 "+session.pdZeroError.ToString("0.0")+"）");
   if(session.mode=="考試"&&Mathf.Abs(off)>1)session.examErrors.Add("PD 量測偏差 "+off.ToString("+0.0;-0.0")+" mm");
   Toast("PD "+pdReading.ToString("0.#")+" mm 已記錄。之後轉入驗光儀時會用 P.D. 旋鈕設定兩側窺孔。");
   PDExit(true);
  }
  public void PDExit(bool done){
   if(pdStep<0)return;pdStep=-1;examinerEye=0;pdInstructed=false;ReturnTools();held=0;
   if(returnFocused){focused=true;GoTo(FocusPoint(),Quaternion.identity);targetFov=FocusFov();}
   else{focused=false;GoTo(returnPos,Quaternion.Euler(pitch,yaw,0));targetFov=66;Lock();}
   if(!done)Toast("已收起 PD 尺。");SyncMechanics();SaveProgress();Emit();
  }

  // ---------------- chart ----------------
  void DrawChart(){if(!chartTex)return;int w=chartTex.width,h=chartTex.height;Color32[] pix=new Color32[w*h];Color32 bg=new Color32(244,243,228,255),ink=new Color32(16,24,23,255);
   for(int yy=0;yy<h;yy++)for(int x=0;x<w;x++)pix[yy*w+x]=session.chart==1?(x<w/2?new Color32(196,39,35,255):new Color32(44,158,74,255)):bg;
   ChartZoom=1;
   if(session.chart==2){for(int y=190;y<590;y+=54)for(int x=290;x<760;x+=54)Disc(pix,w,h,x,y,7,ink);}
   else{
    // E height subtends five arcminutes / decimal VA at 6m; each stroke one fifth.
    float eh=(2*6*Mathf.Tan(2.5f*Mathf.Deg2Rad/60)/Mathf.Max(.1f,session.row))/1.08f*w;
    float scale=eh/5;int count=5;float gap=eh*2;float start=(w-(count-1)*gap-eh)/2;
    ChartZoom=Mathf.Clamp(eh*13/w,.06f,1);
    int ri=Optics.RowIndex(session.row);for(int k=0;k<count;k++)E(pix,w,h,start+k*gap,h/2-eh/2,scale,Optics.RowRot(ri,k),ink);
   }
   if(!projectorOn)System.Array.Fill(pix,new Color32(10,12,12,255));
   if(patientView){float b=Optics.Blur(session.Truth(session.eye),session.H.lens,session.ph);int radius=Mathf.Clamp(Mathf.RoundToInt(b*3),0,12);if(radius>0)pix=BlurTexture(pix,w,h,radius);}
   chartTex.SetPixels32(pix);chartTex.Apply(false);
  }
  static void Disc(Color32[] p,int w,int h,int cx,int cy,int r,Color32 c){for(int y=-r;y<=r;y++)for(int x=-r;x<=r;x++)if(x*x+y*y<=r*r&&cx+x>=0&&cx+x<w&&cy+y>=0&&cy+y<h)p[(cy+y)*w+cx+x]=c;}
  static void E(Color32[] p,int w,int h,float xx,float yy,float sc,int rot,Color32 c){for(int y=0;y<5;y++)for(int x=0;x<5;x++){if(x!=0&&y!=0&&y!=2&&y!=4)continue;int rx=x,ry=y;for(int r=0;r<rot;r++){int tmp=rx;rx=4-ry;ry=tmp;}int loX=Mathf.RoundToInt(xx+rx*sc),hiX=Mathf.RoundToInt(xx+(rx+1)*sc),loY=Mathf.RoundToInt(yy+ry*sc),hiY=Mathf.RoundToInt(yy+(ry+1)*sc);for(int py=loY;py<hiY;py++)for(int px=loX;px<hiX;px++)if(px>=0&&px<w&&py>=0&&py<h)p[py*w+px]=c;}}
  static Color32[] BlurTexture(Color32[] p,int w,int h,int r){Color32[] a=new Color32[p.Length],b=new Color32[p.Length];for(int y=0;y<h;y++){int sr=0,sg=0,sb=0;for(int x=-r;x<=r;x++){var c=p[y*w+Mathf.Clamp(x,0,w-1)];sr+=c.r;sg+=c.g;sb+=c.b;}for(int x=0;x<w;x++){int n=2*r+1;a[y*w+x]=new Color32((byte)(sr/n),(byte)(sg/n),(byte)(sb/n),255);var rem=p[y*w+Mathf.Clamp(x-r,0,w-1)];var add=p[y*w+Mathf.Clamp(x+r+1,0,w-1)];sr+=add.r-rem.r;sg+=add.g-rem.g;sb+=add.b-rem.b;}}for(int x=0;x<w;x++){int sr=0,sg=0,sb=0;for(int y=-r;y<=r;y++){var c=a[Mathf.Clamp(y,0,h-1)*w+x];sr+=c.r;sg+=c.g;sb+=c.b;}for(int y=0;y<h;y++){int n=2*r+1;b[y*w+x]=new Color32((byte)(sr/n),(byte)(sg/n),(byte)(sb/n),255);var rem=a[Mathf.Clamp(y-r,0,h-1)*w+x];var add=a[Mathf.Clamp(y+r+1,0,h-1)*w+x];sr+=add.r-rem.r;sg+=add.g-rem.g;sb+=add.b-rem.b;}}return b;}
  AudioClip Tone(float hz,float secs,float amp){int samples=(int)(22050*secs);float[] d=new float[samples];for(int i=0;i<samples;i++)d[i]=Mathf.Sin(i*hz*2*Mathf.PI/22050)*Mathf.Exp(-i/(samples*.22f))*amp;var clip=AudioClip.Create("clinic click",samples,1,22050,false);clip.SetData(d,0);return clip;}
  AudioClip Noise(float secs,float amp){int samples=(int)(22050*secs);float[] d=new float[samples];var rng=new System.Random(7);float lp=0;for(int i=0;i<samples;i++){float n=(float)rng.NextDouble()*2-1;lp=lp*.55f+n*.45f;float env=Mathf.Min(1,i/600f)*Mathf.Exp(-i/(samples*.45f));d[i]=(n-lp)*env*amp;}var clip=AudioClip.Create("spray",samples,1,22050,false);clip.SetData(d,0);return clip;}
  public void Export(){string text="視光學實驗（二）期中考練習紀錄\n工作距離 "+session.wdCm+"cm / 受測者 "+session.PatientName+"（病例 "+session.patient+"）/ PD "+session.pd+"mm / 模式 "+session.mode+"\n";string[] eyeN={"OD","OS"};
   text+="① Retinoscopy\n";for(int e=0;e<2;e++)text+=eyeN[e]+": "+session.eyes[e].net+"  VA: "+session.eyes[e].vaText+(session.eyes[e].phRead?" (PH "+session.eyes[e].phVAText+")":"")+"\n";
   text+="② 1st MPMVA\n";for(int e=0;e<2;e++)text+=eyeN[e]+": "+session.eyes[e].mp1+"  VA: "+session.eyes[e].mp1VAText+"\n";
   text+="③ JCC\n";for(int e=0;e<2;e++)text+=eyeN[e]+": "+session.eyes[e].jccResult+"\n";
   text+="④ 2nd MPMVA\n";for(int e=0;e<2;e++)text+=eyeN[e]+": "+session.eyes[e].final+"  VA: "+session.eyes[e].finalVAText+"\n";
   text+="\n練習模型，非臨床處方。\n\n操作紀錄\n"+string.Join("\n",session.events);
   #if UNITY_WEBGL && !UNITY_EDITOR
   ClinicDownload("視光室-病例"+session.patient+".txt",text);
   #else
   Debug.Log(text);
   #endif
  }
  public void QATimeScale(string s){if(Application.absoluteURL.Contains("qa=1")||Application.isEditor)Time.timeScale=Mathf.Clamp(float.Parse(s,System.Globalization.CultureInfo.InvariantCulture),.05f,4);}
  public void QADirectScene(){if(!Application.absoluteURL.Contains("qa=1")&&!Application.isEditor)return;Begin("自由");session.height=0;session.level=0;session.vertex=12;session.pd=64;session.stage=10;session.eye=0;session.EnterStage();session.phoropterIn=true;session.occluded=true;session.jccMode=0;remoteOwned=true;view.transform.position=new Vector3(0,1.6f,3.4f);Focus();Release();SyncMechanics(true);DrawChart();ui.Refresh();Emit();}
  public void RunQA(){var qa=GetComponent<ClinicQA>();if(!qa)qa=gameObject.AddComponent<ClinicQA>();qa.Run();}
  public string Evidence(){return JsonUtility.ToJson(new EvidenceData{jccMode=session.jccMode,remote=remoteOwned,projector=projectorOn,row=session.row,chart=session.chart,jccFace=jccFace[session.eye],stage=session.stage,eye=session.eye,mode=session.mode,begun=begun,focused=focused,busy=busy,missing=session.Missing().Count,animationCount=animationLog.Count,x=view.transform.position.x,z=view.transform.position.z,rx=session.H.lens.ToString(),action=lastAction,completed=session.completed,pd=session.pd,pdStep=pdStep,phoropterIn=session.phoropterIn,mouseDowns=mouseDowns,uiHit=uiHit,examinerEye=examinerEye,odYaw=EyeYaw(0),osYaw=EyeYaw(1)});
  float EyeYaw(int e){var t=N("Eye_"+EN[e]);if(!t)return 0;Vector3 f=t.rotation*Vector3.back;return Mathf.Atan2(f.x,-f.z)*Mathf.Rad2Deg;}}
  [Serializable] public class EvidenceData{public int jccMode,chart;public bool remote,projector,jccFace;public float row;public int stage,eye,missing,animationCount,pdStep,mouseDowns,examinerEye;public float odYaw,osYaw;public string mode,rx,action,uiHit;public bool begun,focused,busy,completed,phoropterIn;public float x,z,pd;}
  void Emit(){
   #if UNITY_WEBGL && !UNITY_EDITOR
   ClinicTelemetry(Evidence());
   #endif
  }
 }
}
