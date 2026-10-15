using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace RefractionRoom {
 public class ClinicTarget : MonoBehaviour {public string id,label;}
 public class ClinicWorld : MonoBehaviour {
  public static ClinicWorld Main;
  public ClinicSession session=new ClinicSession();
  public Camera view,scope;
  public Transform models,hands;
  public Font font;
  public ClinicUI ui;
  public bool begun,focused,busy,notebook,patientView,paused;
  public int parameter=0,held=0;
  public string speech="您好，我最近看遠的時候有點模糊。",lastAction="歡迎來到視光室。",focusLabel="";
  public float speechUntil=0;
  public RenderTexture scopeTexture;
  public AudioSource audioOut;
  public List<string> animationLog=new List<string>();
  Dictionary<string,Transform> nodes=new Dictionary<string,Transform>();
  Dictionary<string,Vector3> homePos=new Dictionary<string,Vector3>();
  Dictionary<string,Quaternion> homeRot=new Dictionary<string,Quaternion>();
  Dictionary<string,Vector3> homeScale=new Dictionary<string,Vector3>();
  Dictionary<string,GameObject> tools=new Dictionary<string,GameObject>();
  List<Light> lamps=new List<Light>();
  Material[] reflex=new Material[2];
  Texture2D chartTex;
  Material chartMat,monitorMat;
  TextMesh[] sphereNumbers=new TextMesh[2],cylNumbers=new TextMesh[2];
  Vector3 walkPos=new Vector3(.6f,1.65f,.3f),returnPos;
  float yaw=0,pitch=0,stepBob,sweep,talkTime;
  float tweenTime,shake;Vector3 fromPos,toPos;Quaternion fromRot,toRot;
  bool tweening,readyToSave;
  ClinicTarget target;
  AudioClip tick,footstep;
  Vector3[] eyePoints={new Vector3(-.032f,1.445f,4.475f),new Vector3(.032f,1.445f,4.475f)};
  [DllImport("__Internal")] static extern void ClinicSpeak(string text);
  [DllImport("__Internal")] static extern void ClinicDownload(string filename,string text);
  [DllImport("__Internal")] static extern void ClinicTelemetry(string json);
  [DllImport("__Internal")] static extern void ClinicCapture(int enabled);
  bool lookCaptured;
  public Transform N(string name){Transform t;return nodes.TryGetValue(name,out t)?t:null;}
  void Awake(){Main=this;}
  void Start(){
   foreach(var t in models.GetComponentsInChildren<Transform>(true))if(!nodes.ContainsKey(t.name)){nodes.Add(t.name,t);homePos[t.name]=t.localPosition;homeRot[t.name]=t.localRotation;homeScale[t.name]=t.localScale;}
   foreach(var t in hands.GetComponentsInChildren<Transform>(true))if(!nodes.ContainsKey(t.name)){nodes.Add(t.name,t);homePos[t.name]=t.localPosition;homeRot[t.name]=t.localRotation;homeScale[t.name]=t.localScale;}
   foreach(string n in new[]{"Retinoscope","PDRuler","CleaningCloth","Remote","Clipboard","Pen"}){var t=N(n);if(t)tools[n]=t.gameObject;}
   SetupTool("Retinoscope",new Vector3(-2.63f,1.05f,3.5f),new Vector3(90,0,0));
   SetupTool("PDRuler",new Vector3(-2.58f,.994f,3.83f),Vector3.zero);
   SetupTool("CleaningCloth",new Vector3(-2.54f,.994f,4.2f),Vector3.zero);
   SetupTool("Remote",new Vector3(2.3f,.91f,1.1f),new Vector3(90,0,0));
   SetupTool("Clipboard",new Vector3(2.65f,.902f,1.18f),new Vector3(90,0,0));
   SetupTool("Pen",new Vector3(2.87f,.92f,1.12f),new Vector3(90,0,15));
   if(N("Sanitizer"))N("Sanitizer").position=new Vector3(-2.65f,1.07f,4.55f);
   foreach(var l in FindObjectsByType<Light>(FindObjectsSortMode.None))if(l.type!=LightType.Spot)lamps.Add(l);
   MakeTarget("patient","與受測者說話",new Vector3(0,1.44f,4.56f),new Vector3(.32f,.35f,.2f));
   MakeTarget("phoropter","坐近綜合驗光儀",new Vector3(0,1.46f,4.21f),new Vector3(.51f,.36f,.13f));
   MakeTarget("sanitize","拿取清潔棉布",new Vector3(-2.54f,1.04f,4.2f),new Vector3(.24f,.17f,.25f));
   MakeTarget("pd","拿取 PD 尺",new Vector3(-2.58f,1.05f,3.83f),new Vector3(.28f,.17f,.24f));
   MakeTarget("retino","拿取檢影鏡",new Vector3(-2.63f,1.07f,3.5f),new Vector3(.23f,.2f,.23f));
   MakeTarget("remote","拿取視標遙控器",new Vector3(2.3f,.98f,1.1f),new Vector3(.23f,.18f,.22f));
   MakeTarget("paper","查看板夾紀錄",new Vector3(2.65f,.99f,1.18f),new Vector3(.29f,.17f,.35f));
   MakeTarget("light","調整診間燈光",new Vector3(3.7f,1.28f,3.2f),new Vector3(.18f,.2f,.2f));
   MakeTarget("chart","查看 6 公尺視標",new Vector3(0,1.5f,-1.45f),new Vector3(1.1f,.85f,.1f));
   for(int e=0;e<2;e++)for(int p=0;p<3;p++){
    var t=N(new[]{"Sph_","Cyl_","Axis_"}[p]+(e==0?"OD":"OS"));if(!t)continue;
    var proxy=new GameObject("Direct knob "+e+" "+p);proxy.transform.SetParent(t,false);proxy.layer=9;var col=proxy.AddComponent<SphereCollider>();col.radius=p==0?.045f:.025f;var tag=proxy.AddComponent<ClinicTarget>();tag.id=e+":"+p;tag.label=new[]{"球面","柱面","軸位"}[p];
   }
   // Rendering the actual modelled pupil into the inspection eyepiece, not an answer graphic.
   scopeTexture=new RenderTexture(512,512,16,RenderTextureFormat.ARGB32);scopeTexture.Create();
   var sco=new GameObject("Retinoscope inspection camera");scope=sco.AddComponent<Camera>();scope.targetTexture=scopeTexture;scope.fieldOfView=18;scope.nearClipPlane=.002f;scope.farClipPlane=1;scope.clearFlags=CameraClearFlags.SolidColor;scope.backgroundColor=Color.black;scope.enabled=false;
   for(int e=0;e<2;e++){
    var g=DisplayQuad("PupilReflex_"+e);g.transform.position=eyePoints[e]+new Vector3(0,0,-.002f);g.transform.localScale=Vector3.one*.0093f;
    reflex[e]=new Material(Shader.Find("Clinic/RetinalReflex"));g.GetComponent<Renderer>().material=reflex[e];g.SetActive(false);nodes[g.name]=g.transform;
    var pupil=N(e==0?"Pupil_OD":"Pupil_OS");if(pupil){g.transform.SetParent(pupil,true);g.transform.localPosition=new Vector3(0,0,-.0022f);}
   }
   chartTex=new Texture2D(1024,768,TextureFormat.RGB24,false);chartTex.filterMode=FilterMode.Bilinear;
   foreach(string chartPart in new[]{"ChartFrame","ChartSurface","ChartStand","ChartFoot"})if(N(chartPart))N(chartPart).position+=new Vector3(0,0,-.06f);
   chartMat=new Material(Shader.Find("Unlit/Texture"));chartMat.mainTexture=chartTex;
   // Use quads to keep UV orientation independent of FBX box UV islands.
   var surf=DisplayQuad("Calibrated chart display");surf.transform.position=new Vector3(0,1.5f,-1.525f);surf.transform.rotation=Quaternion.Euler(0,180,0);surf.transform.localScale=new Vector3(1.08f,.81f,1);surf.GetComponent<Renderer>().sharedMaterial=chartMat;
   var mon=DisplayQuad("Chart monitor repeat");mon.transform.position=new Vector3(2.5f,1.22f,1.85f);mon.transform.localScale=new Vector3(.70f,.4f,1);mon.GetComponent<Renderer>().sharedMaterial=chartMat;
   BuildSigns();
   for(int e=0;e<2;e++){string en=e==0?"OD":"OS";float x=e==0?-.102f:.102f;sphereNumbers[e]=InstrumentNumber("Sph readout "+en,new Vector3(x,1.478f,4.177f),N("Pod_"+en));cylNumbers[e]=InstrumentNumber("Cyl readout "+en,new Vector3(x,1.42f,4.177f),N("Pod_"+en));}
   audioOut=gameObject.AddComponent<AudioSource>();audioOut.spatialBlend=0;tick=Tone(780,.035f,.14f);footstep=Tone(92,.065f,.10f);
   view.transform.position=walkPos;view.transform.rotation=Quaternion.Euler(0,0,0);view.fieldOfView=66;
   hands.gameObject.SetActive(false);
   ui=gameObject.AddComponent<ClinicUI>();ui.world=this;ui.Build(font,scopeTexture);
   LoadProgress();DrawChart();SyncMechanics(true);Emit();
   if(Application.absoluteURL.Contains("qa=1"))Debug.Log("CLINIC_QA_READY");
  }
  void SetupTool(string name,Vector3 p,Vector3 e){var t=N(name);if(!t)return;t.position=p;t.rotation=Quaternion.Euler(e);homePos[name]=t.position;homeRot[name]=t.rotation;}
  GameObject DisplayQuad(string name){var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(.5f,-.5f,0)};mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();g.GetComponent<MeshFilter>().mesh=mesh;return g;}
  void MakeTarget(string id,string label,Vector3 p,Vector3 size){var g=new GameObject("Interact_"+id);g.transform.position=p;g.layer=8;g.AddComponent<BoxCollider>().size=size;var t=g.AddComponent<ClinicTarget>();t.id=id;t.label=label;}
  void BuildSigns(){
   Sign("視 光 室",new Vector3(0,2.55f,5.78f),new Vector3(0,0,0),.078f,new Color(.89f,.87f,.76f));
   Sign("THE REFRACTION ROOM",new Vector3(0,2.32f,5.78f),Vector3.zero,.03f,new Color(.70f,.72f,.62f));
   Sign("6 m  /  DISTANCE ACUITY",new Vector3(0,2.07f,-1.50f),new Vector3(0,180,0),.027f,new Color(.19f,.28f,.25f));
   Sign("清潔與檢查工具",new Vector3(-2.0f,1.17f,4.55f),new Vector3(0,90,0),.024f,new Color(.25f,.32f,.28f));
   Sign("01  /  診間",new Vector3(2.65f,2.12f,-2.30f),new Vector3(0,180,0),.032f,new Color(.88f,.89f,.81f));
  }
  void Sign(string s,Vector3 p,Vector3 e,float size,Color c){var g=new GameObject(s);g.transform.position=p;g.transform.rotation=Quaternion.Euler(e);var t=g.AddComponent<TextMesh>();t.text=s;t.font=font;t.fontSize=64;t.characterSize=size/6.4f;t.anchor=TextAnchor.MiddleCenter;t.color=c;g.GetComponent<MeshRenderer>().sharedMaterial=font.material;}
  TextMesh InstrumentNumber(string name,Vector3 pos,Transform parent){var g=new GameObject(name);g.transform.position=pos;g.transform.SetParent(parent,true);var text=g.AddComponent<TextMesh>();text.font=font;text.fontSize=64;text.characterSize=.0027f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.055f,.10f,.09f);g.GetComponent<MeshRenderer>().sharedMaterial=font.material;return text;}
  void Update(){
   if(!begun){view.transform.position=Vector3.Lerp(view.transform.position,new Vector3(1.18f,1.57f,2.05f),Time.deltaTime);view.transform.rotation=Quaternion.Slerp(view.transform.rotation,Quaternion.Euler(0,-26,0),Time.deltaTime);hands.gameObject.SetActive(false);if(scope)scope.enabled=false;if(ui)ui.Refresh();return;}
   if(!paused&&!session.completed)session.seconds+=Time.deltaTime;
   if(session.mode=="考試"&&session.seconds>=1080&&!session.completed){session.completed=true;Say("時間到了，請查看這次的紀錄與回饋。",false);notebook=true;Release();}
   if(Input.GetKeyDown(KeyCode.Escape)){paused=!paused;if(paused)Release();else if(!focused&&!notebook)Lock();}
   if(Input.GetKeyDown(KeyCode.Tab)){ToggleNotebook();}
   if(Input.GetKeyDown(KeyCode.H)){ui.immersive=!ui.immersive;Toast(ui.immersive?"沉浸模式 · H 顯示教學與工具列，鍵盤和實體旋鈕仍可操作。":"已顯示教學與工具列。");}
   if(!paused&&!notebook){
    if(Input.GetKeyDown(KeyCode.F))Focus();
    if(Input.GetKeyDown(KeyCode.C))PatientPOV();
    if(Input.GetKeyDown(KeyCode.E)&&!busy){if(focused)Ask();else Interact();}
    if(Input.GetKeyDown(KeyCode.Space)&&focused&&!busy)Ask();
    if(Input.GetKeyDown(KeyCode.V))parameter=(parameter+1)%3;
    if(Input.GetKeyDown(KeyCode.LeftArrow)&&focused)Adjust(-1);
    if(Input.GetKeyDown(KeyCode.RightArrow)&&focused)Adjust(1);
    if(Input.GetKeyDown(KeyCode.O)&&focused)Occlude();
    if(Input.GetKeyDown(KeyCode.J)&&focused)CycleJCC();
    if(Input.GetKeyDown(KeyCode.R)&&focused)Record();
    if(Input.GetKeyDown(KeyCode.Return)&&focused)Next();
    if(Input.GetKeyDown(KeyCode.UpArrow)&&focused)Row(-1);
    if(Input.GetKeyDown(KeyCode.DownArrow)&&focused)Row(1);
    float sc=Input.mouseScrollDelta.y;if(focused&&Mathf.Abs(sc)>.1f&&!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())Adjust(sc>0?1:-1);
    if(focused&&!patientView&&!busy&&!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()&&(Input.GetMouseButtonDown(0)||Input.GetMouseButtonDown(1))){RaycastHit knobHit;if(Physics.Raycast(view.ScreenPointToRay(Input.mousePosition),out knobHit,2,1<<9)){var tag=knobHit.collider.GetComponent<ClinicTarget>();string[] parts=tag.id.Split(':');session.eye=int.Parse(parts[0]);parameter=int.Parse(parts[1]);Adjust(Input.GetMouseButtonDown(1)?-1:1);}}
    if(!focused&&!patientView&&!tweening){
     if(lookCaptured||Cursor.lockState==CursorLockMode.Locked||Input.GetMouseButton(1)){yaw+=Input.GetAxis("Mouse X")*2;pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*1.5f,-72,72);view.transform.rotation=Quaternion.Euler(pitch,yaw,0);}
     if(Input.GetMouseButtonDown(0)&&Cursor.lockState!=CursorLockMode.Locked&&!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())Lock();
     Vector3 delta=(Quaternion.Euler(0,yaw,0)*new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical"))).normalized;
     if(delta.sqrMagnitude>.01f){Vector3 proposed=walkPos+delta*Time.deltaTime*(Input.GetKey(KeyCode.LeftShift)?2.6f:1.7f);proposed.x=Mathf.Clamp(proposed.x,-3.35f,3.35f);proposed.z=Mathf.Clamp(proposed.z,-1.8f,5.35f);
      // Keep the first-person capsule out of the patient, desk and cabinet.
      if(!Blocked(proposed))walkPos=proposed;stepBob+=Time.deltaTime*9;if(Mathf.Sin(stepBob)>.99f&&!audioOut.isPlaying)audioOut.PlayOneShot(footstep,.3f);
     }
     view.transform.position=walkPos+Vector3.up*(delta.sqrMagnitude>.01f?Mathf.Sin(stepBob)*.012f:0);
    }
    target=null;RaycastHit hit;if(!focused&&Physics.Raycast(view.ViewportPointToRay(new Vector3(.5f,.5f)),out hit,2.3f,1<<8))target=hit.collider.GetComponent<ClinicTarget>();focusLabel=target?target.label:"";
   }
   if(tweening){tweenTime+=Time.deltaTime*1.5f;float t=Mathf.SmoothStep(0,1,tweenTime);view.transform.position=Vector3.Lerp(fromPos,toPos,t);view.transform.rotation=Quaternion.Slerp(fromRot,toRot,t);if(t>=1)tweening=false;}
   AnimatePatient();
   if(held==1){sweep=Mathf.Sin(Time.time*2.4f);var rt=N("Retinoscope");if(rt)rt.localRotation=Quaternion.Euler(0,0,90-session.streak+sweep*5);for(int e=0;e<2;e++){reflex[e].SetFloat("_Residual",Optics.RetResidual(session.Truth(e),session.eyes[e].lens,Optics.WD(session.wdCm),session.streak));reflex[e].SetFloat("_Sweep",sweep);reflex[e].SetFloat("_Angle",(session.streak+90)*Mathf.Deg2Rad);N("PupilReflex_"+e).gameObject.SetActive(true);}scope.transform.position=eyePoints[session.eye]+new Vector3(0,0,-.105f);scope.transform.rotation=Quaternion.identity;scope.enabled=focused&&!notebook&&!patientView;}
   else{scope.enabled=false;for(int e=0;e<2;e++)if(N("PupilReflex_"+e))N("PupilReflex_"+e).gameObject.SetActive(false);}
   if(patientView){view.transform.position=eyePoints[session.eye]+new Vector3(0,0,-.01f);view.transform.rotation=Quaternion.Euler(0,180,0);view.fieldOfView=40;}
   if(Time.frameCount%12==0){ui.Refresh();Emit();}
  }
  bool Blocked(Vector3 p){return (p.x<-1.7f&&p.z>2.65f)||(p.x>1.7f&&p.z>.35f&&p.z<3)||(Mathf.Abs(p.x)<.66f&&p.z>4.0f);}
  void AnimatePatient(){
   var head=N("HeadPivot");if(head)head.localRotation=homeRot[head.name]*Quaternion.Euler(Mathf.Sin(Time.time*.7f)*.8f,Mathf.Sin(Time.time*.35f)*.6f,0);
   float blink=Mathf.Pow(Mathf.Max(0,Mathf.Sin(Time.time*1.37f)),60);
   foreach(string n in new[]{"Lid_OD","Lid_OS"}){var l=N(n);if(l){Vector3 original=homeScale[n];l.localScale=new Vector3(original.x,original.y*Mathf.Lerp(1,7,blink),original.z);}}
   var mouth=N("MouthOpening");if(mouth){Vector3 sc=mouth.localScale;sc.y=(Time.time<talkTime?1+Mathf.Abs(Mathf.Sin(Time.time*18))*4:1)*.0028f; // original FBX has baked mesh radius and transform scale; preserve below
    Vector3 original=homeScale[mouth.name];mouth.localScale=new Vector3(original.x,original.y*(Time.time<talkTime?1+Mathf.Abs(Mathf.Sin(Time.time*18))*3:1),original.z);}
   foreach(string n in new[]{"Pupil_OD","Pupil_OS"}){var p=N(n);if(p)p.localScale=Vector3.Lerp(p.localScale,homeScale[n]*(session.prepared[8]?1.3f:1),Time.deltaTime*2);}
  }
  public void Begin(string mode){
   ReturnTools();held=0;session=new ClinicSession();session.patient=ui.selectedPatient;session.mode=mode;session.EnterStage();begun=true;paused=false;notebook=false;focused=false;patientView=false;walkPos=new Vector3(.6f,1.65f,.3f);view.transform.position=walkPos;yaw=0;pitch=0;view.transform.rotation=Quaternion.identity;view.fieldOfView=66;hands.gameObject.SetActive(true);ui.menu.SetActive(false);SyncMechanics(true);DrawChart();Lock();Say("您好，我看遠有點模糊，今天想檢查度數。",true);SaveProgress();}
  public void Resume(){if(!PlayerPrefs.HasKey("ClinicSession"))return;session=JsonUtility.FromJson<ClinicSession>(PlayerPrefs.GetString("ClinicSession"));begun=true;walkPos=new Vector3(.6f,1.65f,.3f);view.transform.position=walkPos;view.transform.rotation=Quaternion.identity;yaw=0;pitch=0;hands.gameObject.SetActive(true);ui.menu.SetActive(false);SyncMechanics(true);DrawChart();Lock();Toast("已恢復病例與教學進度。");}
  void LoadProgress(){ui.canResume=PlayerPrefs.HasKey("ClinicSession");}
  public void SaveProgress(){if(begun){PlayerPrefs.SetString("ClinicSession",JsonUtility.ToJson(session));PlayerPrefs.Save();if(ui)ui.canResume=true;}}
  public void PointerState(string state){lookCaptured=state=="1";}
  public void Lock(){
   #if UNITY_WEBGL && !UNITY_EDITOR
   if(!focused&&!notebook&&!paused&&!Application.absoluteURL.Contains("qa=1"))ClinicCapture(1);
   #else
   if(!focused&&!notebook&&!paused)Cursor.lockState=CursorLockMode.Locked;Cursor.visible=Cursor.lockState!=CursorLockMode.Locked;
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
   if(busy)return;if(patientView){PatientPOV();return;}
   if(!focused){if(Vector3.Distance(view.transform.position,new Vector3(0,1.445f,4.23f))>3.2f){Toast("先走近受測者與驗光儀，再按 F 坐近。");return;}returnPos=walkPos;focused=true;Release();GoTo(FocusPoint(),Quaternion.Euler(0,0,0));Toast("坐近操作 · 滾輪調鏡，V 切換球面／柱面／軸位，F 回診間。");}
   else{focused=false;GoTo(returnPos,Quaternion.Euler(pitch,yaw,0));Lock();}
   DrawChart();
  }
  Vector3 FocusPoint(){return held==1?new Vector3(session.eye==0?-.032f:.032f,1.445f,4.475f-session.wdCm/100):new Vector3(0,1.475f,3.78f);}
  public void GoTo(Vector3 p,Quaternion r){fromPos=view.transform.position;fromRot=view.transform.rotation;toPos=p;toRot=r;tweenTime=0;tweening=true;}
  public void PatientPOV(){if(busy)return;patientView=!patientView;Release();if(!patientView){view.fieldOfView=66;GoTo(focused?FocusPoint():walkPos,Quaternion.Euler(focused?0:pitch,focused?0:yaw,0));}else Toast("受測者視野：畫面模糊為示意；C 回驗光師。");DrawChart();}
  public void Interact(){
   if(!target){Toast("走近桌面工具或受測者，對準後按 E；F 坐近驗光儀。");return;}
   string id=target.id;
   if(id=="phoropter"){if(held==3)Prepare(0);else if(held==4)Prepare(1);else Focus();}else if(id=="patient"){if(held==3)Prepare(0);else if(held==4)Prepare(1);else StartCoroutine(Action("greet",()=>Say("我平常看遠有點糊，沒有突然看不見，也沒有疼痛。",true)));}
   else if(id=="sanitize"){HoldTool("CleaningCloth");held=3;Toast("已拿棉布，走近受測者後按 E 擦拭儀器。");}else if(id=="pd"){HoldTool("PDRuler");held=4;Toast("已拿 PD 尺，走近受測者後按 E 量瞳距。");}else if(id=="light")Prepare(8);else if(id=="retino")HoldRet();else if(id=="paper")ToggleNotebook();else if(id=="remote"){HoldTool("Remote");held=2;Toast("已拿視標遙控器。F 坐近儀器後可換表。");}else if(id=="chart")PatientPOV();
  }
  public void Prepare(int i){if(busy)return;if(!focused&&i!=8&&Vector3.Distance(view.transform.position,new Vector3(0,1.445f,4.23f))>1.4f){Toast("先走近受測者，按 F 坐近驗光儀，再進行這項操作。");return;}
   StartCoroutine(Action("prep"+i,()=>{
    session.prepared[i]=true;if(i==1)session.pd=session.TruePD;if(i==2)session.level=0;if(i==5){session.height=0;session.vertex=12;}if(i==6)session.chart=1;
    SyncMechanics();DrawChart();Toast(ClinicSession.PrepNames[i]+"完成"+(i==1?" · PD "+session.TruePD+" mm":""));session.Log(ClinicSession.PrepNames[i]);SaveProgress();
   }));
  }
  public void HoldRet(){if(busy)return;if(held==1){ReturnTools();held=0;Toast("已放回檢影鏡。");}else{HoldTool("Retinoscope");held=1;Toast("檢影鏡已拿起；按 F 靠近，在兩條主子午線掃動。");if(focused)GoTo(FocusPoint(),Quaternion.identity);}session.Log("拿取／放回檢影鏡");}
  void HoldTool(string name){ReturnTools();var t=N(name);if(!t)return;t.SetParent(view.transform,true);t.localPosition=new Vector3(.16f,-.19f,.37f);t.localRotation=Quaternion.Euler(0,180,8);}
  void ReturnTools(){foreach(var kv in tools){var t=kv.Value.transform;t.SetParent(models,true);t.position=homePos[kv.Key];t.rotation=homeRot[kv.Key];}}
  public void Adjust(int sign){if(busy||!focused)return;string eye=session.eye==0?"OD":"OS";string n=(parameter==0?"Sph_":parameter==1?"Cyl_":"Axis_")+eye;StartCoroutine(Action("knob:"+n,()=>{session.Adjust(parameter,sign);var t=N(n);if(t)t.localRotation*=Quaternion.Euler(0,0,sign*(parameter==2?5:16));SyncMechanics();DrawChart();audioOut.PlayOneShot(tick);SaveProgress();}));}
  public void SetParameter(int p){parameter=p;Toast(new[]{"球面 · 每格 0.25 D","負柱面 · 每格 0.25 DC（每 -0.50DC 自動 +0.25DS）","散光軸 · 每格 5°"}[p]);}
  public void SelectEye(){if(busy)return;session.eye=1-session.eye;session.jccMode=0;session.ph=false;SyncMechanics();if(focused&&held==1)GoTo(FocusPoint(),Quaternion.identity);DrawChart();Toast("目前測試 "+session.EyeName);}
  public void Occlude(){if(busy)return;StartCoroutine(Action("occluder",()=>{session.occluded=!session.occluded;SyncMechanics();Toast(session.occluded?"已遮蓋非測試眼。":"已打開雙眼。 ");}));}
  public void Pinhole(){if(busy)return;StartCoroutine(Action("pinhole",()=>{session.ph=!session.ph;SyncMechanics();DrawChart();Toast(session.ph?"針孔已擺入測試眼光路。":"針孔已收回。 ");}));}
  public void ChangeChart(int c){if(busy)return;StartCoroutine(Action("remote",()=>{session.chart=c;DrawChart();Toast(new[]{"E 字視標","紅綠視標","JCC 蜂巢點陣"}[c]);}));}
  public void Row(int sign){int k=0;for(int i=0;i<Optics.Rows.Length;i++)if(Mathf.Abs(session.row-Optics.Rows[i])<.01f)k=i;k=Mathf.Clamp(k+sign,0,Optics.Rows.Length-1);session.row=Optics.Rows[k];DrawChart();audioOut.PlayOneShot(tick);}
  public void Fog(){if(busy)return;StartCoroutine(Action("knob:Sph_"+(session.eye==0?"OD":"OS"),()=>{float amount=session.Phase==5?.5f:1;Rx r=session.H.lens;r.s+=amount;session.H.lens=r;if(session.Phase==5)session.H.secondFogged=true;else session.H.fogged=true;session.H.highestFog=amount;session.Log("霧視 +"+amount.ToString("0.00")+"D");SyncMechanics();DrawChart();Toast("加 +"+amount.ToString("0.00")+" D 霧視；逐格退正並反覆確認視力。");SaveProgress();}));}
  public void CycleJCC(){if(busy)return;StartCoroutine(Action("jcc-swing",()=>{session.jccMode=(session.jccMode+1)%3;SyncMechanics();Toast(new[]{"JCC 收回","JCC 軸位 · 手柄沿目前柱面軸","JCC 度數 · 紅白點對準目前柱面軸"}[session.jccMode]);}));}
  public void Streak(int sign){session.streak=Optics.Axis(session.streak+sign*15);session.Log("光帶旋轉 "+session.streak);}
  public void Sweep(){if(busy)return;if(held!=1){Toast("請先拿起檢影鏡。");return;}StartCoroutine(Action("retino-sweep",()=>{if(Mathf.Abs(session.H.lastStreak-session.streak)>25){session.H.sweeps++;session.H.lastStreak=session.streak;}float r=Optics.RetResidual(session.Truth(session.eye),session.H.lens,Optics.WD(session.wdCm),session.streak);Say(Mathf.Abs(r)<.125f?"（光帶中和，瞳孔反射明亮而均勻）":r<0?"（反射光與掃動同向：順動）":"（反射光與掃動反向：逆動）",false);session.Log("檢影掃動 "+session.streak);}));}
  public void WorkingDistance(){if(busy)return;session.wdCm=session.wdCm<60?67:session.wdCm<80?100:50;if(focused&&held==1)GoTo(FocusPoint(),Quaternion.identity);Toast("工作距離 "+session.wdCm+" cm / "+Optics.WD(session.wdCm).ToString("0.00")+" D");}
  public void RemoveWD(){if(busy)return;if(session.wdRemoved){Toast("已扣除工作距離，避免重複扣除。");return;}StartCoroutine(Action("knob:Sph_"+(session.eye==0?"OD":"OS"),()=>{float wd=Optics.WD(session.wdCm);foreach(var h in session.eyes){Rx r=h.lens;r.s-=wd;h.lens=r;}session.wdRemoved=true;session.Log("雙眼扣 WD "+wd);SyncMechanics();Toast("雙眼球面各扣 "+wd.ToString("0.00")+"D；柱面與軸不變。");}));}
  public void Ask(){
   if(busy||!focused)return;
   if(!session.occluded&&session.stage>=4){Toast("先遮住非測試眼，受測者才會只用測試眼比較。");return;}
   if(session.stage==1||session.stage==2){Sweep();return;}
   if(session.chart==1){StartCoroutine(Action("ask",()=>{session.H.duoAsked=true;Say(Optics.Duo(session.Truth(session.eye),session.H.lens),true);session.Log("紅綠問答");}));return;}
   if(session.jccMode>0){StartCoroutine(CompareJCC());return;}
   StartCoroutine(Action("ask",()=>{session.Read();float best=Optics.BestRow(session.Truth(session.eye),session.H.lens,session.ph);string[] dirs={"上","右","下","左","上"};string s="";for(int i=0;i<5;i++)s+=(session.row<=best+.025f?dirs[i]:i<2?dirs[i]:"看不清楚")+(i<4?"、":"。");Say("這一排："+s+(session.row>best+.025f?"字有點糊。":""),true);SaveProgress();}));
  }
  IEnumerator CompareJCC(){
   busy=true;animationLog.Add("jcc-compare");string en=session.eye==0?"OD":"OS";Transform flip=N("JCCFlip_"+en);Quaternion baseQ=flip?flip.localRotation:Quaternion.identity;
   Say("請比較第一片和第二片，哪一片較清楚？",false);
   for(int i=0;i<2;i++){if(flip){Quaternion start=flip.localRotation,end=baseQ*Quaternion.AngleAxis(i==0?0:180,Vector3.up);for(float t=0;t<1;t+=Time.deltaTime*3){flip.localRotation=Quaternion.Slerp(start,end,t);yield return null;}flip.localRotation=end;}audioOut.PlayOneShot(tick);yield return new WaitForSeconds(.4f);}
   if(flip)flip.localRotation=baseQ;session.RegisterJCC();
   Say(Optics.CompareJCC(session.Truth(session.eye),session.H.lens,session.jccMode==1),true);busy=false;session.Log("JCC 兩片比較");SaveProgress();
  }
  public void Record(){if(busy)return;StartCoroutine(Action("record",()=>{session.Save();Toast("已寫入紙本紀錄："+session.EyeName+" "+session.H.lens);SaveProgress();}));}
  public void Next(){if(busy)return;var missing=session.Missing();if(session.mode=="教學"&&missing.Count>0){Toast("還需要："+missing[0]);return;}ReturnTools();held=0;session.Advance();SyncMechanics();DrawChart();SaveProgress();if(session.completed){notebook=true;Release();Toast("期中流程完成，請查看雙眼紀錄與回饋。");}else{Toast(session.StageTitle);if(focused)GoTo(FocusPoint(),Quaternion.identity);}Emit();}
  public void ToggleNotebook(){if(!begun||busy)return;notebook=!notebook;if(notebook)Release();else if(!focused)Lock();}
  public void Say(string text,bool voice){speech=text;speechUntil=Time.time+10;talkTime=Time.time+Mathf.Clamp(text.Length*.10f,1,5);if(voice&&ui.voice){
   #if UNITY_WEBGL && !UNITY_EDITOR
    ClinicSpeak(text);
   #endif
   }lastAction=text;}
  public void Toast(string text){lastAction=text;ui.toastUntil=Time.time+6;}
  IEnumerator Action(string kind,Action effect){
   busy=true;animationLog.Add(kind);if(animationLog.Count>100)animationLog.RemoveAt(0);
   Transform hand=N("Hand_R");Vector3 hp=hand?hand.localPosition:Vector3.zero;Quaternion hq=hand?hand.localRotation:Quaternion.identity;
   Vector3 dest=hp+new Vector3(-.07f,.12f,.14f);string prop=null;
   if(kind=="prep0")prop="CleaningCloth";if(kind=="prep1")prop="PDRuler";if(kind=="remote"||kind=="prep6"||kind=="prep8")prop="Remote";if(kind=="record")prop="Clipboard";
   Transform tool=prop!=null?N(prop):null;Transform parent=tool?tool.parent:null;Vector3 tp=tool?tool.position:Vector3.zero;Quaternion tq=tool?tool.rotation:Quaternion.identity;
   if(tool){tool.SetParent(view.transform,true);tool.localPosition=new Vector3(.08f,-.14f,.38f);tool.localRotation=kind=="record"?Quaternion.Euler(25,0,0):Quaternion.Euler(0,0,0);}
   Transform pen=kind=="record"?N("Pen"):null;if(pen){pen.SetParent(view.transform,true);pen.localPosition=new Vector3(.17f,-.12f,.33f);pen.localRotation=Quaternion.Euler(30,0,-35);}
   if(focused){
    Transform contact=kind.StartsWith("knob:")?N(kind.Substring(5)):kind=="prep1"?N("PDWheel"):kind=="prep2"?N("LevelKnob"):kind=="prep7"?N("LockLever"):kind=="jcc-swing"?N("JCC_"+(session.eye==0?"OD":"OS")):kind=="occluder"||kind=="pinhole"?N("Occluder_"+(session.eye==0?"OS":"OD")):null;
    if(contact)dest=view.transform.InverseTransformPoint(contact.position)+new Vector3(.025f,-.035f,-.045f);
    if(kind=="prep0")dest=new Vector3(.04f,.025f,.51f);
   }
   float duration=kind.StartsWith("knob")?.20f:kind=="record"?1.2f:kind=="prep0"?1.6f:.65f;
   for(float t=0;t<1;t+=Time.deltaTime/duration){
    float a=Mathf.Sin(t*Mathf.PI);
    if(hand){hand.localPosition=Vector3.Lerp(hp,dest,a);hand.localRotation=hq*Quaternion.Euler(-20*a,0,-25*a);}
    if(tool&&kind=="prep0")tool.localPosition=new Vector3(.04f+Mathf.Sin(t*25)*.08f,.07f,.58f);
    if(tool&&kind=="prep1")tool.localPosition=Vector3.Lerp(new Vector3(.08f,-.14f,.38f),new Vector3(0,-.025f,.44f),a);
    if(pen)pen.localPosition=new Vector3(.12f+Mathf.Sin(t*48)*.025f,-.09f,.33f+t*.035f);
    if(kind=="retino-sweep"&&N("Retinoscope"))N("Retinoscope").localPosition=new Vector3(.16f+Mathf.Sin(t*mathPI2)*.025f,-.19f,.37f);
    foreach(string f in new[]{"Finger_R0","Finger_R1","Finger_R2","Finger_R3"}){Transform ft=N(f);if(ft)ft.localRotation=homeRot[f]*Quaternion.Euler(a*30,0,0);}
    yield return null;
   }
   if(hand){hand.localPosition=hp;hand.localRotation=hq;}
   if(tool){tool.SetParent(parent,true);tool.position=tp;tool.rotation=tq;}
   if(pen){pen.SetParent(models,true);pen.position=homePos["Pen"];pen.rotation=homeRot["Pen"];}
   effect();busy=false;ui.Refresh();Emit();
  }
  const float mathPI2=6.283185f;
  void SyncMechanics(bool instant=false){
   for(int e=0;e<2;e++){
    if(sphereNumbers[e])sphereNumbers[e].text=session.eyes[e].lens.s.ToString("+0.00;-0.00;0.00");if(cylNumbers[e])cylNumbers[e].text=session.eyes[e].lens.c.ToString("0.00")+" / "+session.eyes[e].lens.a.ToString("0");
    string eye=e==0?"OD":"OS";Transform pod=N("Pod_"+eye);if(pod){Vector3 p=homePos[pod.name];p.x+=(e==0?-1:1)*(session.pd-64)*.0005f;pod.localPosition=p;}
    Transform occ=N("Occluder_"+eye);if(occ){bool closed=session.occluded&&e!=session.eye;Motion(occ,occ.localPosition,closed?Quaternion.identity:Quaternion.Euler(0,0,e==0?-130:130),instant);}
    Transform ph=N("Pinhole_"+eye);if(ph){foreach(var r in ph.GetComponentsInChildren<Renderer>(true))r.enabled=true;bool inserted=session.ph&&e==session.eye;Motion(ph,homePos[ph.name]+(inserted?Vector3.zero:new Vector3(e==0?-.052f:.052f,.05f,0)),inserted?Quaternion.identity:Quaternion.Euler(0,0,95),instant);}
    Transform jcc=N("JCC_"+eye);if(jcc){Motion(jcc,jcc.localPosition,session.jccMode>0&&e==session.eye?Quaternion.identity:Quaternion.Euler(0,0,e==0?-115:115),instant);}
    Transform jccFlip=N("JCCFlip_"+eye);if(jccFlip)jccFlip.localRotation=Quaternion.Euler(0,0,session.eyes[e].lens.a+(session.jccMode==1?45:0));
    Transform axis=N("Axis_"+eye);if(axis)axis.localRotation=homeRot[axis.name]*Quaternion.Euler(0,0,-session.eyes[e].lens.a);
   }
   var bubble=N("Bubble");if(bubble){Vector3 p=homePos["Bubble"];p.x+=session.level*.004f;bubble.localPosition=p;}
   var lockN=N("LockLever");if(lockN)lockN.localRotation=homeRot[lockN.name]*Quaternion.Euler(0,session.prepared[7]?45:0,0);
   var wheel=N("PDWheel");if(wheel)wheel.localRotation=homeRot[wheel.name]*Quaternion.Euler(0,0,(session.pd-60)*25);
   var lev=N("LevelKnob");if(lev)lev.localRotation=homeRot[lev.name]*Quaternion.Euler(0,0,session.level*18);
   // Rear optical surface is 4.24 + .043 + .18 = 4.463 m; pupil is 4.475 m.
   var phoro=N("Phoropter");if(phoro){Vector3 p=homePos[phoro.name];p.y+=session.height;p.z+=.18f-(session.vertex-12)*.001f;Motion(phoro,p,Quaternion.Euler(0,0,session.level),instant);}
   var sw=N("SwitchLever");if(sw)sw.localRotation=homeRot[sw.name]*Quaternion.Euler(0,0,session.prepared[8]?16:-16);
   foreach(var l in lamps){if(l.type==LightType.Directional)l.intensity=session.prepared[8]?.65f:1.05f;else l.intensity=session.prepared[8]?1.0f:2.3f;}
  }
  void Motion(Transform t,Vector3 p,Quaternion q,bool instant){if(instant){t.localPosition=p;t.localRotation=q;}else StartCoroutine(Mechanism(t,p,q));}
  IEnumerator Mechanism(Transform t,Vector3 p,Quaternion q){Vector3 start=t.localPosition;Quaternion r=t.localRotation;for(float a=0;a<1;a+=Time.deltaTime*4){if(!t)yield break;t.localPosition=Vector3.Lerp(start,p,Mathf.SmoothStep(0,1,a));t.localRotation=Quaternion.Slerp(r,q,Mathf.SmoothStep(0,1,a));yield return null;}t.localPosition=p;t.localRotation=q;}
  void DrawChart(){if(!chartTex)return;int w=chartTex.width,h=chartTex.height;Color32[] pix=new Color32[w*h];Color32 bg=new Color32(244,243,228,255),ink=new Color32(16,24,23,255);
   for(int yy=0;yy<h;yy++)for(int x=0;x<w;x++)pix[yy*w+x]=session.chart==1?(x<w/2?new Color32(196,39,35,255):new Color32(44,158,74,255)):bg;
   if(session.chart==2){for(int y=190;y<590;y+=54)for(int x=290;x<760;x+=54)Disc(pix,w,h,x,y,7,ink);}
   else{
    // E height subtends five arcminutes / decimal VA at 6m; each stroke one fifth.
    float eh=(2*6*Mathf.Tan(2.5f*Mathf.Deg2Rad/60)/Mathf.Max(.1f,session.row))/1.08f*w;
    float scale=eh/5;int count=5;float gap=eh*2;float start=(w-(count-1)*gap-eh)/2;
    for(int k=0;k<count;k++)E(pix,w,h,start+k*gap,h/2-eh/2,scale,new int[]{1,0,3,2,1}[k],ink);
   }
   if(patientView){float b=Optics.Blur(session.Truth(session.eye),session.H.lens,session.ph);int radius=Mathf.Clamp(Mathf.RoundToInt(b*3),0,12);if(radius>0)pix=BlurTexture(pix,w,h,radius);}
   chartTex.SetPixels32(pix);chartTex.Apply(false);
  }
  static void Disc(Color32[] p,int w,int h,int cx,int cy,int r,Color32 c){for(int y=-r;y<=r;y++)for(int x=-r;x<=r;x++)if(x*x+y*y<=r*r&&cx+x>=0&&cx+x<w&&cy+y>=0&&cy+y<h)p[(cy+y)*w+cx+x]=c;}
  static void E(Color32[] p,int w,int h,float xx,float yy,float sc,int rot,Color32 c){for(int y=0;y<5;y++)for(int x=0;x<5;x++){if(x!=0&&y!=0&&y!=2&&y!=4)continue;int rx=x,ry=y;for(int r=0;r<rot;r++){int tmp=rx;rx=4-ry;ry=tmp;}int loX=Mathf.RoundToInt(xx+rx*sc),hiX=Mathf.RoundToInt(xx+(rx+1)*sc),loY=Mathf.RoundToInt(yy+ry*sc),hiY=Mathf.RoundToInt(yy+(ry+1)*sc);for(int py=loY;py<hiY;py++)for(int px=loX;px<hiX;px++)if(px>=0&&px<w&&py>=0&&py<h)p[py*w+px]=c;}}
  static Color32[] BlurTexture(Color32[] p,int w,int h,int r){Color32[] a=new Color32[p.Length],b=new Color32[p.Length];for(int y=0;y<h;y++){int sr=0,sg=0,sb=0;for(int x=-r;x<=r;x++){var c=p[y*w+Mathf.Clamp(x,0,w-1)];sr+=c.r;sg+=c.g;sb+=c.b;}for(int x=0;x<w;x++){int n=2*r+1;a[y*w+x]=new Color32((byte)(sr/n),(byte)(sg/n),(byte)(sb/n),255);var rem=p[y*w+Mathf.Clamp(x-r,0,w-1)];var add=p[y*w+Mathf.Clamp(x+r+1,0,w-1)];sr+=add.r-rem.r;sg+=add.g-rem.g;sb+=add.b-rem.b;}}for(int x=0;x<w;x++){int sr=0,sg=0,sb=0;for(int y=-r;y<=r;y++){var c=a[Mathf.Clamp(y,0,h-1)*w+x];sr+=c.r;sg+=c.g;sb+=c.b;}for(int y=0;y<h;y++){int n=2*r+1;b[y*w+x]=new Color32((byte)(sr/n),(byte)(sg/n),(byte)(sb/n),255);var rem=a[Mathf.Clamp(y-r,0,h-1)*w+x];var add=a[Mathf.Clamp(y+r+1,0,h-1)*w+x];sr+=add.r-rem.r;sg+=add.g-rem.g;sb+=add.b-rem.b;}}return b;}
  AudioClip Tone(float hz,float secs,float amp){int samples=(int)(22050*secs);float[] d=new float[samples];for(int i=0;i<samples;i++)d[i]=Mathf.Sin(i*hz*2*Mathf.PI/22050)*Mathf.Exp(-i/(samples*.22f))*amp;var clip=AudioClip.Create("clinic click",samples,1,22050,false);clip.SetData(d,0);return clip;}
  public void Export(){string text="視光室｜期中練習紀錄\n病例 "+session.patient+" "+session.PatientName+"\n模式 "+session.mode+"\nPD "+session.pd+"mm / WD "+session.wdCm+"cm\n";for(int e=0;e<2;e++){var h=session.eyes[e];text+=(e==0?"OD":"OS")+"\n裸眼 VA "+h.measuredVA.ToString("0.0")+" PH="+h.phRead+"\nGross "+h.gross+"\nNet "+h.net+"\nFinal "+h.final+" VA "+h.finalVA.ToString("0.0")+"\n";}text+="\n練習模型，非臨床處方。\n\n操作紀錄\n"+string.Join("\n",session.events);
   #if UNITY_WEBGL && !UNITY_EDITOR
   ClinicDownload("視光室-病例"+session.patient+".txt",text);
   #else
   Debug.Log(text);
   #endif
  }
  public void RunQA(){var qa=GetComponent<ClinicQA>();if(!qa)qa=gameObject.AddComponent<ClinicQA>();qa.Run();}
  public string Evidence(){return JsonUtility.ToJson(new EvidenceData{stage=session.stage,eye=session.eye,mode=session.mode,begun=begun,focused=focused,busy=busy,missing=session.Missing().Count,animationCount=animationLog.Count,x=view.transform.position.x,z=view.transform.position.z,rx=session.H.lens.ToString(),action=lastAction,completed=session.completed});}
  [Serializable] public class EvidenceData{public int stage,eye,missing,animationCount;public string mode,rx,action;public bool begun,focused,busy,completed;public float x,z;}
  void Emit(){
   #if UNITY_WEBGL && !UNITY_EDITOR
   ClinicTelemetry(Evidence());
   #endif
  }
 }
}
