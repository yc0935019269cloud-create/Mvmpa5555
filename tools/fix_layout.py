from pathlib import Path
p=Path('UnityProject/Assets/Scripts/ClinicWorld.cs');s=p.read_text(encoding='utf-8').replace('GetComponentsInChildren<ClinicTarget>()','FindObjectsByType<ClinicTarget>(FindObjectsSortMode.None)')
s=s.replace('new Vector3(2.5f,1.22f,1.85f)','new Vector3(1.16f,1.14f,4.555f)').replace('new Vector3(.70f,.4f,1)','new Vector3(.42f,.27f,1)')
s=s.replace('mon.GetComponent<Renderer>().sharedMaterial=chartMat;','mon.GetComponent<Renderer>().sharedMaterial=chartMat;if(deck)mon.transform.SetParent(deck,true);')
s=s.replace('if(clicked&&sign>0&&e==session.eye&&session.phoropterIn){jccDrag=e;', 'if(touchMode&&clicked&&e==session.eye){SetJCC(session.jccMode==0?1:0);break;}if(clicked&&sign>0&&e==session.eye&&session.phoropterIn){jccDrag=e;')
s=s.replace('p[0]=="jcc"){UseControl','p[0]=="jcc"||p[0]=="jccarm"){UseControl')
p.write_text(s,encoding='utf-8')
