from pathlib import Path
p=Path('UnityProject/Assets/Scripts/ClinicUI.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('GameObject chartPanel,hud','GameObject remotePanel; Text remoteStatus;\n  GameObject chartPanel,hud')
start=s.index('   var prep=Panel(')
s=s[:start]+'''   remotePanel=Panel("Projector remote",hud.transform,-670,-220,190,400,new Vector2(.5f,.5f),new Color(.72f,.67f,.67f,1));
   var face=Panel("Remote inset",remotePanel.transform,0,12,166,342,new Vector2(.5f,.5f),new Color(.13f,.14f,.14f,1));
   remoteStatus=Txt("Remote display",face.transform,"",0,140,150,45,14,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   string[] charts={"E 字","紅綠","點陣"};
   for(int i=0;i<3;i++){int c=i;var b=Btn("remote chart "+i,face.transform,charts[i],-52+i*52,89,48,43,()=>world.ChangeChart(c));b.GetComponent<Image>().color=new Color(.68f,.67f,.61f);}
   for(int i=0;i<Optics.Rows.Length;i++){float row=Optics.Rows[i];int col=i%3,r=i/3;var b=Btn("remote VA "+row,face.transform,row.ToString("0.0"),-52+col*52,40-r*32,48,28,()=>world.RemoteRow(row));b.GetComponentInChildren<Text>().fontSize=14;}
   Btn("remote larger",face.transform,"大 ↑",-40,-109,72,34,()=>world.Row(-1));
   Btn("remote smaller",face.transform,"小 ↓",40,-109,72,34,()=>world.Row(1));
   Btn("remote reset",face.transform,"復位",-40,-147,72,30,()=>world.RemoteReset());
   Btn("remote lamp",face.transform,"投影開關",40,-147,72,30,()=>world.RemotePower());
   Txt("remote label",remotePanel.transform,"CHART PROJECTOR",0,-181,170,24,12,dark,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
''' +s[start:]
s=s.replace('   crossObj.SetActive(','''   remotePanel.SetActive(world.remoteOwned&&!pd&&!world.cinematic&&!world.notebook&&!world.paused&&!world.patientView&&(!world.touchMode||world.focused));
   remoteStatus.text=world.projectorOn?(new[]{"E 字","紅綠","點陣"}[s.chart]+"  VA "+s.row.ToString("0.0")):"投影已關閉";
   bool compact=world.held!=1&&s.stage!=1&&s.stage!=2&&s.stage!=3;
   foreach(Transform t in station.transform){string n=t.name;bool keep=n=="lens readout"||n=="read"||n=="record"||n=="next"||n=="leave"||n=="eye"||n=="fog"||n=="hold ret"||n=="station help";t.gameObject.SetActive(!compact||keep);}
   station.GetComponent<RectTransform>().sizeDelta=new Vector2(1130,compact?142:244);
   foreach(string n in new[]{"chart E","chart RG","chart dots","larger","smaller"})station.transform.Find(n).gameObject.SetActive(false);
   if(compact){Place("lens readout",0,49);Place("read",-427,5);Place("eye",-207,5);Place("record",-48,5);Place("next",184,5);Place("leave",433,-43);Place("fog",-427,-43);Place("hold ret",-237,-43);Place("station help",0,-75);previewLabel.gameObject.SetActive(false);}
   else{Place("lens readout",0,91);Place("read",-427,-10);Place("eye",58,41);Place("record",130,-10);Place("next",358,-10);Place("leave",433,-63);Place("fog",-214,-10);Place("hold ret",62,-63);Place("station help",0,-105);}
   helper.transform.parent.gameObject.SetActive(showHints&&!immersive&&!world.cinematic&&!pd&&!world.remoteOwned);
   crossObj.SetActive(''')
s=s.replace('  void CaseLabel()', '  void Place(string n,float x,float y){((RectTransform)station.transform.Find(n)).anchoredPosition=new Vector2(x,y);}\n  void CaseLabel()')
s=s.replace('先走到左側工具櫃取工具','先走到受測者右側升降桌取工具')
p.write_text(s,encoding='utf-8')
p=Path('UnityProject/Assets/Scripts/ClinicWorld.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('  public int parameter=0,held=0;', '''  public int parameter=0,held=0;
  public bool remoteOwned,projectorOn=true;
  int jccDrag=-1; float dragStartAngle,dragStartRotation; bool[] jccFace=new bool[2];
  float PointerAngle(Transform t){Vector3 q=view.WorldToScreenPoint(t.position);return Mathf.Atan2(Input.mousePosition.y-q.y,Input.mousePosition.x-q.x)*Mathf.Rad2Deg;}
  void DragJCC(){if(jccDrag<0)return;var t=N("Turret_"+EN[jccDrag]);if(paused||notebook||!focused||busy){jccDrag=-1;SyncMechanics();return;}
   float a=dragStartRotation+Mathf.DeltaAngle(dragStartAngle,PointerAngle(t));Spin(t.name,Quaternion.Euler(0,0,a),false,35);
   if(Input.GetMouseButtonUp(0)){int e=jccDrag;jccDrag=-1;float target=TurretInAngle(e);session.jccMode=Mathf.Abs(Mathf.DeltaAngle(a,target))<Mathf.Abs(Mathf.DeltaAngle(a,0))?Mathf.Max(1,session.jccMode):0;SyncMechanics();MarkSave();}}
  public void RemoteRow(float row){if(busy)return;projectorOn=true;session.row=row;DrawChart();MarkSave();}
  public void RemoteReset(){if(busy)return;projectorOn=true;session.chart=0;session.row=.6f;DrawChart();MarkSave();}
  public void RemotePower(){if(busy)return;projectorOn=!projectorOn;DrawChart();}
''')
# locations shared between tools and hit targets
repl={'-2.63f,1.012f,3.52f':'1.04f,.962f,3.96f','-2.58f,.9905f,3.80f':'1.27f,.9405f,3.94f','-2.56f,.990f,4.10f':'1.46f,.94f,4.18f','-2.64f,1.047f,4.42f':'1.47f,.997f,4.43f','2.3f,.894f,1.1f':'.76f,.944f,3.96f','2.65f,.892f,1.18f':'1.13f,.942f,4.25f','2.87f,.894f,1.12f':'1.36f,.944f,4.25f','-2.56f,1.03f,4.12f':'1.46f,.98f,4.18f','-2.58f,1.03f,3.80f':'1.27f,.98f,3.94f','-2.63f,1.05f,3.5f':'1.04f,.99f,3.96f','2.3f,.95f,1.1f':'.76f,.99f,3.96f','2.65f,.95f,1.18f':'1.13f,.99f,4.25f'}
for a,b in repl.items():s=s.replace(a,b)
s=s.replace('HoldTool("Remote");held=2;', 'remoteOwned=true;Release();')
s=s.replace('F 坐近儀器後可換表。','左下角按鍵可直接換表。')
s=s.replace('if(i==6)session.chart=1;', 'if(i==6){session.chart=1;remoteOwned=true;}')
s=s.replace('ReturnTools();held=0;mark="session";', 'remoteOwned=false;projectorOn=true;jccDrag=-1;ReturnTools();held=0;mark="session";')
s=s.replace('if(busy)return;StartCoroutine(Action("remote",()=>{session.chart=c;', 'if(busy)return;remoteOwned=true;projectorOn=true;StartCoroutine(Action("remote",()=>{session.chart=c;')
s=s.replace('   if(patientView){float b=Optics.Blur', '   if(!projectorOn)System.Array.Fill(pix,new Color32(10,12,12,255));\n   if(patientView){float b=Optics.Blur')
s=s.replace('if(session.chart==1){StartCoroutine(Action("ask"', 'if(!projectorOn){Toast("請先開啟投影視標。");return;}\n   if(session.chart==1){StartCoroutine(Action("ask"')
s=s.replace('   if(!Input.GetMouseButton(1)||!focused)', '   DragJCC();\n   if(!Input.GetMouseButton(1)||!focused)')
s=s.replace('if(focused&&!patientView&&!touchMode){','if(focused&&!patientView&&!touchMode&&jccDrag<0){')
s=s.replace('    case "jcc":if(!clicked)return;', '    case "jcc":if(!clicked){if(e==session.eye&&session.jccMode>0)Ask();return;}')
s=s.replace('case "flip":if(clicked)Ask();break;', 'case "flip":if(e==session.eye&&session.jccMode>0)Ask();break;\n    case "jccarm":if(clicked&&sign>0&&e==session.eye&&session.phoropterIn){jccDrag=e;var t=N("Turret_"+EN[e]);dragStartAngle=PointerAngle(t);dragStartRotation=Mathf.DeltaAngle(0,t.localEulerAngles.z-homeRot[t.name].eulerAngles.z);}break;')
s=s.replace('    case "flip":return "點擊翻面比較（空白鍵）";', '    case "flip":return "滾輪／點擊翻面比較";\n    case "jccarm":return "按住支桿拖曳：轉入窺孔／收回";')
s=s.replace('    var cell=N("JCCCell_"+en);if(cell){', '    var cell=N("JCCCell_"+en);if(cell){var tur=N("Turret_"+en);for(int k=1;k<=3;k++)Control("jccarm:"+e,side+" JCC 支桿",tur,Vector3.Lerp(tur.position,cell.position,k*.19f)+new Vector3(0,0,-.004f),.007f,tur);')
s=s.replace('session.phoropterIn?0:80','session.phoropterIn?0:90')
s=s.replace('homeRot[phoro.name]*Quaternion.Euler(0,0,session.level)','homeRot[phoro.name]*Quaternion.Euler(0,session.phoropterIn?0:-90,session.level)')
s=s.replace('(p.x>1.7f&&p.z>.35f&&p.z<3)', '(p.x>.55f&&p.x<1.65f&&p.z>3.7f&&p.z<4.7f)')
p.write_text(s,encoding='utf-8')
