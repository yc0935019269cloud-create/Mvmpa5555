using System;
using System.Collections;
using UnityEngine;

namespace RefractionRoom {
 // Browser-only diagnostic entry point. Exercises the same public physical actions as the UI.
 public class ClinicQA : MonoBehaviour {
  ClinicWorld w;
  bool running;
  public string result="";
  public void Run(){if(running)return;if(!Application.absoluteURL.Contains("qa=1")&&!Application.isEditor)return;w=ClinicWorld.Main;StartCoroutine(Flow());}
  IEnumerator Wait(){while(w.busy)yield return null;yield return null;}
  IEnumerator Lens(Rx desired){
   for(int p=0;p<3;p++){
    w.SetParameter(p);
    for(int guard=0;guard<110;guard++){
     Rx now=w.session.H.lens;float d=p==0?desired.s-now.s:p==1?desired.c-now.c:desired.a-now.a;
     if(p==2){if(d>90)d-=180;if(d< -90)d+=180;}
     if(Mathf.Abs(d)<(p==2?2.6f:.12f))break;
     w.Adjust(d>0?1:-1);yield return Wait();
    }
   }
   // Cylinder compensation can alter sphere while doing a later parameter.
   w.SetParameter(0);for(int i=0;i<40&&Mathf.Abs(w.session.H.lens.s-desired.s)>.12f;i++){w.Adjust(w.session.H.lens.s<desired.s?1:-1);yield return Wait();}
  }
  void Next(){if(w.session.Missing().Count>0)throw new Exception("Runtime QA stage "+w.session.stage+": "+string.Join(", ",w.session.Missing()));w.Next();}
  IEnumerator Flow(){
   running=true;result="RUNNING";w.ui.voice=false;w.ui.selectedPatient=1;w.Begin("教學");w.Release();
   w.view.transform.position=new Vector3(0,1.65f,2.2f);w.Focus();yield return new WaitForSeconds(.8f);
   for(int i=0;i<9;i++){w.Prepare(i);yield return Wait();}
   Next();
   for(int e=0;e<2;e++){
    w.HoldRet();yield return new WaitForSeconds(.8f);
    var r=w.session.Truth(e);yield return Lens(new Rx(r.s+Optics.WD(w.session.wdCm),r.c,r.a));
    w.Sweep();yield return Wait();for(int i=0;i<6;i++)w.Streak(1);w.Sweep();yield return Wait();w.Record();yield return Wait();Next();
   }
   w.RemoveWD();yield return Wait();w.Record();yield return Wait();Next();
   for(int e=0;e<2;e++){
    if(!w.session.occluded){w.Occlude();yield return Wait();}
    w.Ask();yield return Wait();w.Row(1);w.Ask();yield return Wait();
    if(w.session.H.measuredVA<.8f){w.Pinhole();yield return Wait();w.Ask();yield return Wait();w.Pinhole();yield return Wait();}w.Record();yield return Wait();Next();
    w.Fog();yield return Wait();w.Ask();yield return Wait();yield return Lens(w.session.Truth(e));w.Ask();yield return Wait();w.Record();yield return Wait();Next();
    w.Ask();yield return Wait();Next();
    while(w.session.Phase==3){w.Ask();yield return Wait();w.Ask();yield return Wait();Next();}
    w.Record();yield return Wait();Next();
    w.Fog();yield return Wait();w.Ask();yield return Wait();yield return Lens(w.session.Truth(e));w.Ask();yield return Wait();w.Record();yield return Wait();Next();
   }
   Next();result=w.session.completed?"PASS_FULL_MIDTERM_FLOW":"FAIL_NOT_COMPLETED";w.lastAction=result;Debug.Log(result+" "+w.Evidence());running=false;
  }
 }
}
