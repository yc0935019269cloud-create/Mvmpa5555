from pathlib import Path
p=Path('UnityProject/Assets/Scripts/ClinicWorld.cs');s=p.read_text(encoding='utf-8')
s=s.replace('session.phoropterIn?0:90','session.phoropterIn?0:-90').replace('session.phoropterIn?0:-90,session.level','session.phoropterIn?0:90,session.level')
s=s.replace('  public void RunQA(){', '''  public void QADirectScene(){if(!Application.absoluteURL.Contains("qa=1")&&!Application.isEditor)return;Begin("自由");session.stage=10;session.eye=0;session.EnterStage();session.phoropterIn=true;session.occluded=true;session.jccMode=0;remoteOwned=true;view.transform.position=new Vector3(0,1.6f,3.4f);Focus();Release();SyncMechanics(true);DrawChart();ui.Refresh();Emit();}
  public void RunQA(){''')
s=s.replace('stage=session.stage,eye=session.eye,mode=', 'jccMode=session.jccMode,remote=remoteOwned,projector=projectorOn,row=session.row,chart=session.chart,jccFace=jccFace[session.eye],stage=session.stage,eye=session.eye,mode=')
s=s.replace('public int stage,eye,missing,','public int jccMode,chart;public bool remote,projector,jccFace;public float row;public int stage,eye,missing,')
p.write_text(s,encoding='utf-8')
p=Path('UnityProject/Assets/WebGLTemplates/Clinic/index.html');s=p.read_text(encoding='utf-8');s=s.replace('<pre id="qa-state">','<button id="qa-direct">操作檢查</button><pre id="qa-state">');s=s.replace("document.getElementById('qa-flow').onclick=", "document.getElementById('qa-direct').onclick=()=>instance.SendMessage('Clinic runtime','QADirectScene');document.getElementById('qa-flow').onclick=");p.write_text(s,encoding='utf-8')
