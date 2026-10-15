from pathlib import Path
from fontTools import subset
root=Path(__file__).resolve().parents[1]
text=''.join(p.read_text(encoding='utf-8') for p in (root/'UnityProject/Assets').rglob('*.cs'))
text+=''.join(chr(i) for i in range(32,127))+'✓×−°Δ…上下左右清楚第一二片紅綠順逆動中和待記錄0123456789'
font=subset.load_font(str(root/'Blender/NotoSansCJKtc-Regular.otf'),subset.Options())
sub=subset.Subsetter();sub.populate(text=text);sub.subset(font)
subset.save_font(font,str(root/'UnityProject/Assets/Resources/ClinicSans.otf'),subset.Options())
print('Font subset:',(root/'UnityProject/Assets/Resources/ClinicSans.otf').stat().st_size,'bytes')
