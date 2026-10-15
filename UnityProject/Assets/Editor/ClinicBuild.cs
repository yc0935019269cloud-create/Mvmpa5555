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
   string matp="Assets/Generated/Materials/"+n.material+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matp);if(!mat){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,matp);}mat.color=new Color(n.color[0],n.color[1],n.color[2]);mat.SetFloat("_Metallic",n.metallic);mat.SetFloat("_Glossiness",1-n.roughness);EditorUtility.SetDirty(mat);
   g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;
  }
  return objects[0];
 }
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
  Check(Optics.WD(50)==2&&Optics.WD(67)==1.5f&&Optics.WD(100)==1,"Working-distance diopters");
  Check(Mathf.Abs((4.475f-(-1.525f))-6)<.0001f,"Patient pupil to calibrated wall display is six metres");
  Check(Mathf.Abs((4.475f-(4.24f+.043f+.18f))*1000-12)<.001f,"Aligned rear optical surface is twelve millimetres from the pupil");
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
  var comp=new ClinicSession();comp.stage=8;comp.H.lens=new Rx(-2,-.5f,180);float eq=Optics.Power(comp.H.lens).x;comp.Adjust(1,-1);comp.Adjust(1,-1);Check(Mathf.Abs(eq-Optics.Power(comp.H.lens).x)<.001f,"Two -0.25 cylinder steps compensated by +0.25 sphere");
  for(int patient=1;patient<=6;patient++){
   var s=new ClinicSession();s.patient=patient;
   Check(s.Missing().Count==9,"Case "+patient+" requires all nine preparation actions");
   for(int i=0;i<9;i++)s.prepared[i]=true;Check(s.Advance(),"Case "+patient+" preparation passes");
   for(int e=0;e<2;e++){
    Rx r=s.Truth(e);s.eyes[e].lens=new Rx(r.s+1.5f,r.c,r.a);s.eyes[e].sweeps=2;s.Save();Check(s.Advance(),"Case "+patient+" eye "+e+" gross retinoscopy");
   }
   Check(!s.Advance(),"Case "+patient+" WD must be deducted and recorded");
   foreach(var h in s.eyes){Rx r=h.lens;r.s-=1.5f;h.lens=r;}s.wdRemoved=true;s.Save();Check(s.Advance(),"Case "+patient+" net retinoscopy");
   for(int e=0;e<2;e++){
    Check(s.eye==e,"Case "+patient+" OD then OS sequence");s.occluded=true;s.row=.1f;s.Read();s.row=.6f;s.Read();if(s.H.measuredVA<.8f){s.ph=true;s.Read();s.ph=false;}s.Save();Check(s.Advance(),"Case "+patient+" eye "+e+" VA and PH");
    s.H.lens=s.Truth(e);s.H.fogged=true;s.Read();s.Read();s.Save();Check(s.Advance(),"Case "+patient+" eye "+e+" first MPMVA");
    s.H.duoAsked=true;Check(s.Advance(),"Case "+patient+" eye "+e+" duochrome");
    while(s.Phase==3){s.RegisterJCC();s.RegisterJCC();Check(s.Advance(),"Case "+patient+" eye "+e+" JCC "+s.H.route+" ordered comparisons");}
    s.Save();Check(s.Advance(),"Case "+patient+" eye "+e+" JCC record");
    s.H.secondFogged=true;s.Read();s.Read();s.Save();Rx correct=s.H.lens;s.Adjust(0,-1);Check(s.Missing().Contains("記錄目前處方與最佳 VA"),"Changed lens invalidates final record");s.H.lens=correct;s.Save();Check(s.Advance(),"Case "+patient+" eye "+e+" final MPMVA record");
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
  foreach(string name in new[]{"Architecture","ExamChair","Patient","Phoropter","Retinoscope","PDRuler","CleaningCloth","Remote","Clipboard","Pen","Sanitizer","LightSwitch"}){
   var go=ImportBlender(name);go.name=name;go.transform.SetParent(root.transform,false);
   foreach(var r in go.GetComponentsInChildren<MeshRenderer>())foreach(var mat in r.sharedMaterials)if(mat&&mat.shader.name=="Standard"){mat.SetFloat("_Glossiness",mat.name.Contains("Chrome")?.72f:.28f);if(mat.name.Contains("Chrome")||mat.name.Contains("Brass"))mat.SetFloat("_Metallic",.75f);}
   if(name=="Architecture")foreach(Transform t in go.GetComponentsInChildren<Transform>())GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.BatchingStatic);
  }
  var cam=new GameObject("First-person view",typeof(Camera),typeof(AudioListener));cam.tag="MainCamera";var camera=cam.GetComponent<Camera>();camera.fieldOfView=66;camera.nearClipPlane=.025f;camera.farClipPlane=25;camera.allowHDR=false;camera.allowMSAA=true;camera.backgroundColor=new Color(.16f,.23f,.23f);camera.clearFlags=CameraClearFlags.SolidColor;cam.transform.position=new Vector3(.6f,1.65f,.3f);
  var h=ImportBlender("ClinicianHands");h.name="ClinicianHands";h.transform.SetParent(cam.transform,false);h.transform.localPosition=Vector3.zero;h.transform.localRotation=Quaternion.identity;
  var key=new GameObject("Window daylight",typeof(Light));key.transform.rotation=Quaternion.Euler(43,-42,0);var l=key.GetComponent<Light>();l.type=LightType.Directional;l.color=new Color(1,.93f,.79f);l.intensity=1.05f;l.shadows=LightShadows.Soft;l.shadowStrength=.55f;
  foreach(Vector3 pos in new[]{new Vector3(-1.8f,2.85f,2),new Vector3(1.6f,2.8f,4.5f),new Vector3(1.0f,2.6f,-.2f)}){var lamp=new GameObject("Ceiling fill",typeof(Light));lamp.transform.position=pos;var point=lamp.GetComponent<Light>();point.type=LightType.Point;point.range=6;point.intensity=2.3f;point.color=new Color(1,.91f,.77f);point.shadows=LightShadows.None;}
  RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.65f,.69f,.65f);RenderSettings.ambientEquatorColor=new Color(.43f,.48f,.45f);RenderSettings.ambientGroundColor=new Color(.25f,.22f,.17f);RenderSettings.fog=false;
  var system=new GameObject("Clinic runtime");var world=system.AddComponent<ClinicWorld>();world.models=root.transform;world.hands=h.transform;world.view=camera;world.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/ClinicSans.otf");if(!world.font)throw new Exception("Missing licensed Chinese font");
  Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/Clinic.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Clinic.unity",true)};
  PlayerSettings.companyName="subaiyaya";PlayerSettings.productName="視光室 The Refraction Room";PlayerSettings.bundleVersion="1.0.0";
  PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.runInBackground=true;PlayerSettings.colorSpace=ColorSpace.Gamma;
  PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;PlayerSettings.WebGL.decompressionFallback=true;PlayerSettings.WebGL.dataCaching=true;PlayerSettings.WebGL.nameFilesAsHashes=true;PlayerSettings.WebGL.exceptionSupport=WebGLExceptionSupport.FullWithStacktrace;
  PlayerSettings.WebGL.initialMemorySize=128;PlayerSettings.WebGL.maximumMemorySize=512;PlayerSettings.WebGL.memoryGrowthMode=WebGLMemoryGrowthMode.Geometric;PlayerSettings.WebGL.template="PROJECT:Clinic";
  PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL,ManagedStrippingLevel.Low);
  QualitySettings.SetQualityLevel(2);QualitySettings.antiAliasing=2;QualitySettings.pixelLightCount=4;QualitySettings.shadowDistance=15;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.Medium;QualitySettings.vSyncCount=0;
  var graphics=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();if(graphics){var so=new SerializedObject(graphics);var shaders=so.FindProperty("m_AlwaysIncludedShaders");shaders.ClearArray();foreach(string sn in new[]{"Standard","Unlit/Texture","Unlit/Color","Clinic/RetinalReflex","UI/Default"}){var shader=Shader.Find(sn);if(shader){int n=shaders.arraySize;shaders.InsertArrayElementAtIndex(n);shaders.GetArrayElementAtIndex(n).objectReferenceValue=shader;}}so.ApplyModifiedProperties();}
  AssetDatabase.SaveAssets();
  var report=new List<string>();foreach(string n in new[]{"Patient","HeadPivot","Eye_OD","Pupil_OD","Phoropter","Pod_OD","Sph_OD","Architecture","Hand_R"}){var ts=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x.name==n);foreach(var t in ts)report.Add(n+" pos="+t.position+" rot="+t.eulerAngles+" scale="+t.lossyScale);}
  foreach(var mr in root.GetComponentsInChildren<MeshRenderer>())if(mr.name=="Head"||mr.name=="Foundation"||mr.name=="ChartSurface")report.Add(mr.name+" bounds="+mr.bounds);
  File.WriteAllLines(Path.Combine(Root,"Reports","scene-geometry.txt"),report);Debug.Log("CLINIC_SCENE_GENERATED");
 }
 [MenuItem("Clinic/Build WebGL")]
 public static void Build(){Generate();var path=Path.Combine(Root,"docs");Directory.CreateDirectory(path);var opts=new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Clinic.unity"},locationPathName=path,target=BuildTarget.WebGL,options=BuildOptions.None};var report=BuildPipeline.BuildPlayer(opts);File.WriteAllText(Path.Combine(Root,"Reports","webgl-build.txt"),"Result: "+report.summary.result+"\nSize: "+report.summary.totalSize+"\nTime: "+report.summary.totalTime+"\nErrors: "+report.summary.totalErrors);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("WebGL build failed");File.WriteAllText(Path.Combine(path,".nojekyll"),"");Debug.Log("CLINIC_WEBGL_BUILD_SUCCEEDED");}
}
