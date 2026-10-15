from pathlib import Path
p=Path('UnityProject/Assets/Scripts/ClinicWorld.cs');s=p.read_text(encoding='utf-8').replace('if(Input.GetKeyDown(KeyCode.Tab)){ToggleNotebook();}', 'if(!paused&&Input.GetKeyDown(KeyCode.Tab)){ToggleNotebook();}').replace('if(Input.GetKeyDown(KeyCode.H)){ui.immersive=', 'if(!paused&&Input.GetKeyDown(KeyCode.H)){ui.immersive=');p.write_text(s,encoding='utf-8')
