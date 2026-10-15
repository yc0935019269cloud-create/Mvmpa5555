from pathlib import Path
p=Path('UnityProject/Assets/Scripts/ClinicUI.cs');s=p.read_text(encoding='utf-8').replace('musicSlider.fillRect=fill.GetComponent<RectTransform>();','musicSlider.fillRect=fill.GetComponent<RectTransform>();musicSlider.fillRect.sizeDelta=Vector2.zero;').replace('musicSlider.handleRect=handle.GetComponent<RectTransform>();','musicSlider.handleRect=handle.GetComponent<RectTransform>();musicSlider.handleRect.sizeDelta=new Vector2(26,0);')
s=s.replace('   var pcard=Panel("Pause card"', '   var pauseRect=(RectTransform)pausedPanel.transform;pauseRect.anchorMin=Vector2.zero;pauseRect.anchorMax=Vector2.one;pauseRect.sizeDelta=Vector2.zero;\n   var pcard=Panel("Pause card"')
p.write_text(s,encoding='utf-8')
