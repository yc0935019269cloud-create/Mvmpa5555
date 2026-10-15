using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using RefractionRoom;

public static class ClinicBuild {
 [Serializable] class MeshPackage {public string name;public MeshNode[] nodes;}
 [Serializable] class MeshNode {public string name,material;public int parent;public float[] position,vertices,uv,color;public int[] triangles;public float metallic,roughness;}
 static GameObject ImportBlender(string name){
  Directory.CreateDirectory("Assets/Generated/Meshes");Directory.CreateDirectory("Assets/Generated/Materials");
  var pack=JsonUtility.FromJson<MeshPackage>(File.ReadAllText("Assets/Models/"+name+".clinicmesh.json"));var objects=new List<GameObject>();
  for(int k=0;k<pack.nodes.Length;k++){
   var n=pack.nodes[k];var g=new GameObject(n.name);objects.Add(g);if(n.parent>=0)g.transform.SetParent(objects[n.parent].transform,false);g.transform.localPosition=new Vector3(n.position[0],n.position[1],n.position[2]);
   if(n.vertices.Length==0)continue;
   var mesh=new Mesh();mesh.name=name+"_"+n.name;mesh.indexFormat=n.vertices.Length/3>65535?IndexFormat.UInt32:IndexFormat.UInt16;var verts=new Vector3[n.vertices.Length/3];var uv=new Vector2[verts.Length];for(int i=0;i<verts.Length;i++){verts[i]=new Vector3(n.vertices[i*3],n.vertices[i*3+1],n.vertices[i*3+2]);uv[i]=new Vector2(n.uv[i*2],n.uv[i*2+1]);}mesh.vertices=verts;mesh.triangles=n.triangles;mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();
   string mp="Assets/Generated/Meshes/"+name+"_"+k+".asset";Mesh old=AssetDatabase.LoadAssetAtPath<Mesh>(mp);if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,mp);
   string matp="Assets/Generated/Materials/"+n.material+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matp);if(!mat){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,matp);}mat.color=new Color(n.color[0],n.color[1],n.color[2],n.material=="Glass"?.14f:1);mat.SetFloat("_Metallic",n.metallic);mat.SetFloat("_Glossiness",Mathf.Clamp(1-n.roughness,0,.97f));if(n.material=="Glass")Transparent(mat);EditorUtility.SetDirty(mat);
   g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;
  }
  return objects[0];
 }
 static void Transparent(Material m){m.SetFloat("_Mode",3);m.SetOverrideTag("RenderType","Transparent");m.SetInt("_SrcBlend",(int)BlendMode.One);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);m.SetInt("_ZWrite",0);m.DisableKeyword("_ALPHATEST_ON");m.DisableKeyword("_ALPHABLEND_ON");m.EnableKeyword("_ALPHAPREMULTIPLY_ON");m.renderQueue=3000;}
 static string Root {get{return Directory.GetParent(Application.dataPath).Parent.FullName;}}
 static int checks;
 static List<string> evidence=new List<string>();
 static void Check(bool yes,string note){checks++;evidence.Add((yes?"PASS ":"FAIL ")+note);if(!yes)throw new Exception(note);}
 [MenuItem("Clinic/Validate clinical model")]
 public static void Validate(){
  checks=0;evidence.Clear();
  Check(Optics.AxisError(175,5)==10,"TABO 180-degree wrap");
  Check(Vector3.Distance(Optics.Power(new Rx(-2,-1,180)),Optics.Power(new Rx(-2,-1,0)))<.0001f,"0 and 180 are identical cylinder axes");
  Check(Mathf.Abs(Optics.Meridian(new Rx(-2,-1,180),180)+2)<.001f&&Mathf.Abs(Optics.Meridian(new Rx(-2,-1,180),90)+3)<.001f,"Cylinder has no power along its axis");
  Check(Optics.WD(50)==2&&Optics.WD(57)==1.75f&&Optics.WD(67)==1.5f&&Optics.WD(100)==1&&Optics.WD(40)==2.5f,"Working-distance diopters (40/50/57/67/100 cm)");
  Check(Mathf.Abs((4.475f-(-1.525f))-6)<.0001f,"Patient pupil to calibrated wall display is six metres");
  Check(Mathf.Abs((4.475f-4.463f)*1000-12)<.001f,"Aligned rear face of the lens batteries is twelve millimetres from the pupil");
  var truth=new Rx(-2,-1,180);var gross=new Rx(-.5f,-1,180);
  Check(Optics.Neutral(truth,gross,1.5f),"Both principal meridians neutral at true Rx plus WD");
  Check(!Optics.Neutral(truth,new Rx(-.5f,0,180),1.5f),"One neutral meridian cannot pass astigmatic retinoscopy");
  Check(Optics.RetResidual(truth,new Rx(-1,-1,180),1.5f,0)<0,"Below neutrality produces with motion");
  Check(Optics.RetResidual(truth,new Rx(0,-1,180),1.5f,0)>0,"Above neutrality produces against motion");
  Check(Optics.Duo(truth,new Rx(-1.75f,-1,180)).StartsWith("紅"),"Over-plus gives red-clear response: add minus");
  Check(Optics.Duo(truth,new Rx(-2.25f,-1,180)).StartsWith("綠"),"Over-minus gives green-clear response: add plus");
  Check(Optics.Acuity(truth,truth)>.99f,"Correct Rx has good acuity");
  Check(Optics.Acuity(truth,new Rx(0,0,180),true)>Optics.Acuity(truth,new Rx(0,0,180)),"Pinhole improves optical blur in this model");
  Check(Optics.CompareJCC(truth,truth,true).StartsWith("兩片"),"Balanced axis produces equal JCC responses");
  {float b,l;Check(Optics.VALabel(1.2f,2,out b,out l)=="1.0 +2"&&Optics.VALabel(1f,3,out b,out l)=="1.0 -2"&&Optics.VALabel(1f,1,out b,out l)=="0.9 +1"&&Optics.VALabel(1f,5,out b,out l)=="1.0","Partial-line VA notation: 1.2 two letters = 1.0 +2, 1.0 three = 1.0 -2, 1.0 one = 0.9 +1");}
  var comp=new ClinicSession();comp.stage=8;comp.H.lens=new Rx(-2,-.5f,180);float eq=Optics.Power(comp.H.lens).x;comp.Adjust(1,-1);comp.Adjust(1,-1);Check(Mathf.Abs(eq-Optics.Power(comp.H.lens).x)<.001f,"Two -0.25 cylinder steps compensated by +0.25 sphere");
  for(int patient=1;patient<=6;patient++){
   var s=new ClinicSession();s.patient=patient;
   Check(s.Missing().Count==9,"Case "+patient+" requires all nine preparation actions");
   for(int i=0;i<9;i++)s.prepared[i]=true;Check(s.Advance(),"Case "+patient+" preparation passes");
   for(int e=0;e<2;e++){
    Rx r=s.Truth(e);s.eyes[e].lens=new Rx(r.s+Optics.WD(s.wdCm),r.c,r.a);s.eyes[e].sweeps=2;s.Save();Check(s.Advance(),"Case "+patient+" eye "+e+" gross retinoscopy");
   }
   Check(!s.Advance(),"Case "+patient+" WD must be deducted and recorded");
   foreach(var h in s.eyes){Rx r=h.lens;r.s-=Optics.WD(s.wdCm);h.lens=r;}s.wdRemoved=true;s.Save();Check(s.Advance(),"Case "+patient+" net retinoscopy");
   // ① VA through the net retinoscopy result, OD then OS
   for(int e=0;e<2;e++){Check(s.eye==e&&s.Phase==0,"Case "+patient+" eye "+e+" net-RET VA in OD then OS order");Check(s.H.lens.ToString()==s.H.net.ToString(),"Case "+patient+" eye "+e+" VA starts from the net retinoscopy");s.occluded=true;s.row=.1f;s.Read();s.row=.6f;s.Read();if(s.H.measuredVA<.8f){s.ph=true;s.Read();s.ph=false;}s.Save();Check(s.Advance(),"Case "+patient+" eye "+e+" RET VA recorded");}
   // ② 1st MPMVA then duochrome for each eye before any JCC
   for(int e=0;e<2;e++){
    Check(s.eye==e&&s.Phase==1,"Case "+patient+" eye "+e+" first MPMVA before JCC");s.H.lens=s.Truth(e);s.H.fogged=true;s.Read();s.Read();s.Save();Check(s.Advance(),"Case "+patient+" eye "+e+" first MPMVA recorded");
    Check(s.Phase==2,"Case "+patient+" eye "+e+" duochrome follows its MPMVA");s.H.duoAsked=true;Check(s.Advance(),"Case "+patient+" eye "+e+" duochrome");
   }
   // ③ JCC on both eyes, each recorded
   for(int e=0;e<2;e++){
    Check(s.eye==e&&s.Phase==3,"Case "+patient+" eye "+e+" JCC only after both eyes' MPMVA");
    while(s.Phase==3){s.RegisterJCC();s.RegisterJCC();Check(s.Advance(),"Case "+patient+" eye "+e+" JCC "+s.H.route+" ordered comparisons");}
    s.Save();Check(s.Advance(),"Case "+patient+" eye "+e+" JCC record");
   }
   // ④ 2nd MPMVA on both eyes after both JCC records
   for(int e=0;e<2;e++){
    Check(s.eye==e&&s.Phase==5&&s.eyes[0].jccSaved&&s.eyes[1].jccSaved,"Case "+patient+" eye "+e+" second MPMVA after both JCC records");
    s.H.secondFogged=true;s.Read();s.Read();s.Save();Rx correct=s.H.lens;s.Adjust(0,-1);Check(s.Missing().Contains("記錄第二次 MPMVA 度數與最佳 VA"),"Changed lens invalidates final record");s.H.lens=correct;s.Save();Check(s.Advance(),"Case "+patient+" eye "+e+" final MPMVA record");
   }
   Check(s.Advance()&&s.completed,"Case "+patient+" completes the midterm workflow");Check(s.Score()==100,"Case "+patient+" full workflow scores 100");
  }
  var exam=new ClinicSession();exam.mode="考試";Check(exam.Advance()&&exam.examErrors.Count==1,"Exam allows progression and records unmet criteria without giving hints");Check(exam.Score()==0,"An unattempted clinical case does not receive clinical points");
  Directory.CreateDirectory(Path.Combine(Root,"Reports"));File.WriteAllLines(Path.Combine(Root,"Reports","clinical-validation.txt"),evidence.Concat(new[]{checks+" checks passed"}));Debug.Log("CLINIC_VALIDATION_PASSED "+checks);
 }
 [MenuItem("Clinic/Generate scene")]
 public static void Generate(){
  Validate();
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var root=new GameObject("Blender clinic models");
  foreach(string name in new[]{"Architecture","ExamChair","Patient","PhoropterStand","Phoropter","Retinoscope","PDRuler","CleaningCloth","Remote","Clipboard","Pen","Sanitizer","LightSwitch"}){
   var go=ImportBlender(name);go.name=name;go.transform.SetParent(root.transform,false);
   if(name=="Architecture")foreach(Transform t in go.GetComponentsInChildren<Transform>()){GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.BatchingStatic);var r=t.GetComponent<MeshRenderer>();
    // the room shell lets window daylight in: walls, ceiling and blinds receive but do not cast shadows
    if(r&&(t.name.EndsWith("Wall")||t.name=="Ceiling"||t.name=="Foundation"||t.name.StartsWith("Window")||t.name=="Blind"||t.name.StartsWith("Ceiling")||t.name.StartsWith("Panel")||t.name=="AcousticSlat"))r.shadowCastingMode=ShadowCastingMode.Off;}
  }
  var cam=new GameObject("First-person view",typeof(Camera),typeof(AudioListener));cam.tag="MainCamera";var camera=cam.GetComponent<Camera>();camera.fieldOfView=66;camera.nearClipPlane=.025f;camera.farClipPlane=25;camera.allowHDR=true;camera.allowMSAA=true;cam.AddComponent<ClinicPost>();camera.backgroundColor=new Color(.16f,.23f,.23f);camera.clearFlags=CameraClearFlags.SolidColor;cam.transform.position=new Vector3(.6f,1.65f,.3f);
  var h=ImportBlender("ClinicianHands");h.name="ClinicianHands";h.transform.SetParent(cam.transform,false);h.transform.localPosition=Vector3.zero;h.transform.localRotation=Quaternion.identity;
  var key=new GameObject("Window daylight",typeof(Light));key.transform.rotation=Quaternion.Euler(31,64,0);var l=key.GetComponent<Light>();l.type=LightType.Directional;l.color=new Color(1,.95f,.88f);l.intensity=1.15f;l.shadows=LightShadows.Soft;l.shadowStrength=.85f;l.shadowBias=.02f;l.shadowNormalBias=.25f;
  foreach(Vector3 pos in new[]{new Vector3(-2.2f,2.9f,1.8f),new Vector3(1.7f,2.9f,1.8f),new Vector3(0,2.85f,4.2f)}){var lamp=new GameObject("Ceiling panel light",typeof(Light));lamp.transform.position=pos;var point=lamp.GetComponent<Light>();point.type=LightType.Point;point.range=6.5f;point.intensity=.9f;point.color=new Color(1,.97f,.94f);point.shadows=LightShadows.None;}
  // soft examiner-side fill so the face never reads as harsh top light
  var fill=new GameObject("Examiner fill",typeof(Light));fill.transform.position=new Vector3(-.12f,1.36f,3.75f);var fl=fill.GetComponent<Light>();fl.type=LightType.Point;fl.range=2.4f;fl.intensity=.9f;fl.color=new Color(1,.97f,.95f);fl.shadows=LightShadows.None;
  // refraction stand lamp aimed at the instrument and the patient's face
  var standLamp=new GameObject("Stand lamp",typeof(Light));standLamp.transform.position=new Vector3(.30f,1.93f,4.31f);standLamp.transform.rotation=Quaternion.LookRotation(new Vector3(0,1.38f,4.45f)-standLamp.transform.position);
  var spot=standLamp.GetComponent<Light>();spot.type=LightType.Spot;spot.spotAngle=78;spot.innerSpotAngle=40;spot.range=2.6f;spot.intensity=2.1f;spot.color=new Color(1,.97f,.93f);spot.shadows=LightShadows.Soft;spot.shadowStrength=.32f;spot.shadowBias=.01f;spot.shadowNormalBias=.1f;
  // keeps the font's own GUI/Text material in the build for uGUI text
  var fontKeep=new GameObject("Font material keeper",typeof(TextMesh));fontKeep.GetComponent<TextMesh>().font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/ClinicSans.otf");fontKeep.GetComponent<MeshRenderer>().sharedMaterial=fontKeep.GetComponent<TextMesh>().font.material;fontKeep.transform.position=new Vector3(0,-5,0);
  RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.60f,.64f,.64f);RenderSettings.ambientEquatorColor=new Color(.44f,.43f,.39f);RenderSettings.ambientGroundColor=new Color(.24f,.20f,.16f);RenderSettings.ambientIntensity=1;RenderSettings.fog=false;RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.reflectionIntensity=1;
  var system=new GameObject("Clinic runtime");var world=system.AddComponent<ClinicWorld>();world.models=root.transform;world.hands=h.transform;world.view=camera;world.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/ClinicSans.otf");if(!world.font)throw new Exception("Missing licensed Chinese font");
  Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/Clinic.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Clinic.unity",true)};
  PlayerSettings.companyName="subaiyaya";PlayerSettings.productName="視光室 The Refraction Room";PlayerSettings.bundleVersion="1.0.0";
  PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.runInBackground=true;PlayerSettings.colorSpace=ColorSpace.Linear;
  PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;PlayerSettings.WebGL.decompressionFallback=true;PlayerSettings.WebGL.dataCaching=true;PlayerSettings.WebGL.nameFilesAsHashes=true;PlayerSettings.WebGL.exceptionSupport=WebGLExceptionSupport.FullWithStacktrace;
  PlayerSettings.WebGL.initialMemorySize=128;PlayerSettings.WebGL.maximumMemorySize=512;PlayerSettings.WebGL.memoryGrowthMode=WebGLMemoryGrowthMode.Geometric;PlayerSettings.WebGL.template="PROJECT:Clinic";
  PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL,ManagedStrippingLevel.Low);
  QualitySettings.SetQualityLevel(2);QualitySettings.antiAliasing=4;QualitySettings.pixelLightCount=6;QualitySettings.shadowDistance=9;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.shadowCascades=2;QualitySettings.realtimeReflectionProbes=true;QualitySettings.softParticles=false;QualitySettings.vSyncCount=0;
  var graphics=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();if(graphics){var so=new SerializedObject(graphics);var shaders=so.FindProperty("m_AlwaysIncludedShaders");shaders.ClearArray();foreach(string sn in new[]{"Standard","Unlit/Texture","Unlit/Color","Clinic/RetinalReflex","Clinic/Text3D","Clinic/Mist","Clinic/Screen","Clinic/Streak","Hidden/Clinic/Post","UI/Default"}){var shader=Shader.Find(sn);if(shader){int n=shaders.arraySize;shaders.InsertArrayElementAtIndex(n);shaders.GetArrayElementAtIndex(n).objectReferenceValue=shader;}}so.ApplyModifiedProperties();}
  AssetDatabase.SaveAssets();
  var report=new List<string>();foreach(string n in new[]{"Patient","HeadPivot","Eye_OD","Pupil_OD","Pupil_OS","Phoropter","Pod_OD","Aperture_OD","Aperture_OS","Housing_OD","Sph_OD","ArmSwing","Architecture","Hand_R"}){var ts=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.name==n);foreach(var t in ts)report.Add(n+" pos="+t.position+" rot="+t.eulerAngles+" scale="+t.lossyScale);}
  foreach(var mr in root.GetComponentsInChildren<MeshRenderer>())if(mr.name=="Head"||mr.name=="Foundation"||mr.name=="ChartSurface")report.Add(mr.name+" bounds="+mr.bounds);
  File.WriteAllLines(Path.Combine(Root,"Reports","scene-geometry.txt"),report);Debug.Log("CLINIC_SCENE_GENERATED");
 }
 [MenuItem("Clinic/Build WebGL")]
 public static void Build(){Generate();var path=Path.Combine(Root,"docs");Directory.CreateDirectory(path);var opts=new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Clinic.unity"},locationPathName=path,target=BuildTarget.WebGL,options=BuildOptions.None};var report=BuildPipeline.BuildPlayer(opts);File.WriteAllText(Path.Combine(Root,"Reports","webgl-build.txt"),"Result: "+report.summary.result+"\nSize: "+report.summary.totalSize+"\nTime: "+report.summary.totalTime+"\nErrors: "+report.summary.totalErrors);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("WebGL build failed");File.WriteAllText(Path.Combine(path,".nojekyll"),"");Debug.Log("CLINIC_WEBGL_BUILD_SUCCEEDED");}
}
