from pathlib import Path
p=Path('UnityProject/Assets/Scripts/ClinicUI.cs');s=p.read_text(encoding='utf-8').replace('   conversation=Txt("speech",bubble.transform,','   bubble.GetComponent<Image>().raycastTarget=false;\n   conversation=Txt("speech",bubble.transform,').replace('pd?-345:-110','pd?-345:-210');p.write_text(s,encoding='utf-8')
