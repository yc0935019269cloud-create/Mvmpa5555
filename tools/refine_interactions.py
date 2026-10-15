from pathlib import Path
p=Path('UnityProject/Assets/Scripts/ClinicWorld.cs');s=p.read_text(encoding='utf-8')
s=s.replace('   Register(models);Register(hands);','''   var deck=Deep(models,"LiftDeck");if(deck){foreach(string n in new[]{"ArmSwing","StandColumn","StandCap","LampArm","StandLamp"}){var t=Deep(models,n);if(t)t.SetParent(deck,true);}}
   Register(models);Register(hands);''')
s=s.replace('p.y+=session.height;DriveTo(arm','DriveTo(arm')
s=s.replace('   // vertex: the head moves', '''   var deck=N("LiftDeck");if(deck)DriveTo(deck,homePos[deck.name]+Vector3.up*session.height,homeRot[deck.name],instant,5);
   foreach(var kv in tools){var t=kv.Value.transform;if(t.parent==models&&!busy)t.position=homePos[kv.Key]+Vector3.up*session.height;}
   // vertex: the head moves''')
s=s.replace('t.position=homePos[kv.Key];','t.position=homePos[kv.Key]+Vector3.up*session.height;')
s=s.replace('session=JsonUtility.FromJson<ClinicSession>(PlayerPrefs.GetString("ClinicSession"));','session=JsonUtility.FromJson<ClinicSession>(PlayerPrefs.GetString("ClinicSession"));remoteOwned=PlayerPrefs.GetInt("ClinicRemote",0)==1||session.prepared[6];projectorOn=true;')
s=s.replace('PlayerPrefs.SetString("ClinicSession",JsonUtility.ToJson(session));','PlayerPrefs.SetInt("ClinicRemote",remoteOwned?1:0);PlayerPrefs.SetString("ClinicSession",JsonUtility.ToJson(session));')
s=s.replace('remoteOwned=true;Release();Toast(', 'remoteOwned=true;Release();SaveProgress();Toast(')
s=s.replace('if(e==session.eye&&session.jccMode>0)Ask();','if(e==session.eye&&session.jccMode>0)FlipJCC();')
s=s.replace('  IEnumerator CompareJCC(){', '''  void FlipJCC(){if(busy||!session.occluded){Toast("先遮蓋非測試眼，再翻面比較。");return;}StartCoroutine(FlipOnce());}
  IEnumerator FlipOnce(){busy=true;int e=session.eye;var t=N("JCCFlip_"+EN[e]);jccFace[e]=!jccFace[e];Reach(N("JCCCell_"+EN[e]));
   if(t)DriveTo(t,homePos[t.name],homeRot[t.name]*Quaternion.AngleAxis(jccFace[e]?180:0,Vector3.right),false,14);
   audioOut.PlayOneShot(click,.5f);yield return new WaitForSeconds(.28f);
   if(!jccFace[e]){session.RegisterJCC();Say(Optics.CompareJCC(session.Truth(e),session.H.lens,session.jccMode==1),true);}
   else Toast("JCC 已翻面；再滾一格看另一面並比較。");busy=false;SaveProgress();}
  IEnumerator CompareJCC(){''')
s=s.replace('homeRot[flip.name],instant,12);','homeRot[flip.name]*Quaternion.AngleAxis(jccFace[e]?180:0,Vector3.right),instant,12);')
s=s.replace('   session.RegisterJCC();Say(', '   jccFace[session.eye]=false;session.RegisterJCC();Say(')
s=s.replace('    case "jcc":return session.jccMode==0?', '    case "jcc":return session.jccMode==0?')
s=s.replace('點擊轉入 JCC（軸位 A）','拖曳支桿轉入；點擊選 A／P').replace(' · 右鍵收回',' · 滾輪翻面 · 右鍵收回')
# prevent stale target position after table height adjustment; targets follow the deck
s=s.replace('   BuildControls();','''   if(deck)foreach(var t in GetComponentsInChildren<ClinicTarget>()){if(t.id=="remote"||t.id=="retino"||t.id=="pd"||t.id=="sanitize"||t.id=="paper")t.transform.SetParent(deck,true);}
   BuildControls();''')
p.write_text(s,encoding='utf-8')
