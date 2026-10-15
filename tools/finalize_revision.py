from pathlib import Path
p=Path('UnityProject/Assets/Scripts/ClinicWorld.cs');s=p.read_text(encoding='utf-8')
s=s.replace('session.chart=c;DrawChart();Toast(new[]{','session.chart=c;if(c==1){session.prepared[6]=true;session.Log(ClinicSession.PrepNames[6]);}DrawChart();MarkSave();Toast(new[]{')
s=s.replace('Sign("清潔與檢查工具",new Vector3(-2.0f,1.17f,4.55f),new Vector3(0,90,0)', 'Sign("升降驗光桌",new Vector3(1.08f,.84f,3.80f),Vector3.zero')
s=s.replace('   if(!focused)return;if(!session.phoropterIn)', '   if(!focused||jccDrag>=0)return;if(!session.phoropterIn)')
p.write_text(s,encoding='utf-8')
p=Path('UnityProject/Assets/Scripts/ClinicUI.cs');s=p.read_text(encoding='utf-8').replace('b.GetComponent<Image>().color=new Color(.68f,.67f,.61f);','b.GetComponent<Image>().color=new Color(.68f,.67f,.61f);b.GetComponentInChildren<Text>().color=dark;').replace('b.GetComponentInChildren<Text>().fontSize=14;','b.GetComponentInChildren<Text>().fontSize=14;b.GetComponent<Image>().color=new Color(.70f,.70f,.66f);b.GetComponentInChildren<Text>().color=dark;')
s=s.replace('   station.GetComponent<RectTransform>().sizeDelta=', '   station.GetComponent<RectTransform>().anchoredPosition=new Vector2(160,compact?102:170);\n   station.GetComponent<RectTransform>().sizeDelta=')
p.write_text(s,encoding='utf-8')
