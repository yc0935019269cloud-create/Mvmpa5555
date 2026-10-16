using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RefractionRoom {
 // Tablet layout while seated at the refractor: the instrument buttons move out of the wide bar that used to
 // cover the cylinder / axis dials into a column at the bottom-right, and the text lines move to the top, so
 // the whole refractor stays visible and every dial can be touched.
 public partial class ClinicUI {
  class Snap {public Vector2 pos,size,labelSize;public int font;}
  readonly Dictionary<RectTransform,Snap> stationSnap=new Dictionary<RectTransform,Snap>();
  bool dockOn;
  struct RectDefault {public Vector2 amin,amax,pos;}
  readonly Dictionary<RectTransform,RectDefault> topDefaults=new Dictionary<RectTransform,RectDefault>();
  static Text LabelOf(Transform c){var t=c.GetComponent<Text>();return t?t:c.GetComponentInChildren<Text>(true);}
  void SnapStation(){
   foreach(Transform c in station.transform){var r=(RectTransform)c;var lt=LabelOf(c);var sn=new Snap{pos=r.anchoredPosition,size=r.sizeDelta,font=lt?lt.fontSize:0};
    if(lt&&lt.transform!=c)sn.labelSize=((RectTransform)lt.transform).sizeDelta;stationSnap[r]=sn;}
  }
  void RestoreStation(){
   foreach(var kv in stationSnap){var r=kv.Key;if(!r)continue;r.anchoredPosition=kv.Value.pos;r.sizeDelta=kv.Value.size;var lt=LabelOf(r);
    if(lt){lt.fontSize=kv.Value.font;if(lt.transform!=r)((RectTransform)lt.transform).sizeDelta=kv.Value.labelSize;}}
   var st=(RectTransform)station.transform;st.anchorMin=st.anchorMax=new Vector2(.5f,0);
  }
  void TopSlot(RectTransform r,bool top,float x,float y){
   if(!topDefaults.ContainsKey(r))topDefaults[r]=new RectDefault{amin=r.anchorMin,amax=r.anchorMax,pos=r.anchoredPosition};
   if(top){r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.anchoredPosition=new Vector2(x,y);}
   else{var d=topDefaults[r];r.anchorMin=d.amin;r.anchorMax=d.amax;r.anchoredPosition=d.pos;}
  }
  void StationDock(bool on){
   // short text lines sit under the header while docked
   TopSlot((RectTransform)context.transform,on,0,-100);TopSlot((RectTransform)toast.transform,on,0,-168);TopSlot((RectTransform)knobBar.transform,on,0,-236);
   if(!on){if(dockOn){RestoreStation();dockOn=false;}return;}
   dockOn=true;
   var s=world.session;int st=s.stage,ph=s.Phase;bool sub=world.held!=1;
   var want=new List<string>();
   if(sub&&st>=4)want.Add("read");
   want.Add("eye");
   if(sub&&(ph==1||ph==5))want.Add("fog");
   if(sub&&st>=4)want.Add("occlude");
   if(sub&&ph==0)want.Add("pinhole");
   if(ph==3||ph==4)want.Add("jcc");
   if(st>=1&&st<=5||world.held==1)want.Add("hold ret");
   if(st==3)want.Add("WD");
   if(st>=1)want.Add("record");
   want.Add("leave");
   const float bw=126,bh=54,gap=8,pad=10,head=62;
   int rows=(want.Count+1)/2;float W=pad*2+bw*2+gap,H=pad*2+head+gap+rows*(bh+gap)+bh;
   foreach(Transform c in station.transform)c.gameObject.SetActive(false);
   var rt=(RectTransform)station.transform;rt.anchorMin=rt.anchorMax=new Vector2(1,0);rt.sizeDelta=new Vector2(W,H);rt.anchoredPosition=new Vector2(-W/2-10,H/2+10);
   Place2("lens readout",0,H/2-pad-head/2,W-pad*2,head,15);readout.alignment=TextAnchor.MiddleLeft;readout.text=s.EyeName+"\n"+s.H.lens+"　VA 列 "+s.row.ToString("0.0");
   for(int i=0;i<want.Count;i++){float x=(i%2==0?-1:1)*(bw+gap)/2,y=H/2-pad-head-gap-bh/2-(i/2)*(bh+gap);Place2(want[i],x,y,bw,bh,want[i]=="WD"||want[i]=="read"?15:16);}
   Place2("next",0,-H/2+pad+bh/2,W-pad*2,bh,18);
  }
  void Place2(string n,float x,float y,float w,float h,int font){
   var t=station.transform.Find(n);if(!t)return;t.gameObject.SetActive(true);var r=(RectTransform)t;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);
   var lt=LabelOf(t);if(lt){lt.fontSize=font;if(lt.transform!=t)((RectTransform)lt.transform).sizeDelta=new Vector2(w-6,h);}
  }
 }
}
