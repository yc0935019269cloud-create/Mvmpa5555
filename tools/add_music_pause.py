from pathlib import Path
p=Path('UnityProject/Assets/Scripts/ClinicWorld.cs');s=p.read_text(encoding='utf-8')
s=s.replace('  public AudioSource audioOut;', '''  public AudioSource audioOut;
  public float musicVolume=.2f; AudioSource musicSource; float pauseScale=1;
  [DllImport("__Internal")] static extern void ClinicMusicVolume(float volume);
  public void SetMusicVolume(float value){musicVolume=Mathf.Clamp01(value);PlayerPrefs.SetFloat("ClinicMusicVolume",musicVolume);PlayerPrefs.Save();if(musicSource)musicSource.volume=musicVolume;
   #if UNITY_WEBGL && !UNITY_EDITOR
   ClinicMusicVolume(musicVolume);
   #endif
  }
  IEnumerator LoadMusic(){
   using(var req=UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(new Uri(System.IO.Path.Combine(Application.streamingAssetsPath,"Audio/blue-oiia-uiia-cat.mp3")).AbsoluteUri,AudioType.MPEG)){
    yield return req.SendWebRequest();if(req.result==UnityEngine.Networking.UnityWebRequest.Result.Success){musicSource=gameObject.AddComponent<AudioSource>();musicSource.clip=UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(req);musicSource.loop=true;musicSource.spatialBlend=0;musicSource.volume=musicVolume;musicSource.Play();}else Debug.LogWarning("Music: "+req.error);
   }
  }
  public void TogglePause(){if(!begun)return;paused=!paused;if(paused){pauseScale=Time.timeScale;Time.timeScale=0;Release();}else{Time.timeScale=pauseScale>0?pauseScale:1;if(!focused&&!notebook)Lock();}if(ui)ui.Refresh();}
  public void ReturnToMenu(bool abandon){Time.timeScale=1;StopAllCoroutines();var qa=GetComponent<ClinicQA>();if(qa)qa.StopAllCoroutines();busy=false;saveDirty=false;handScripted=false;cinematic=false;jccDrag=-1;pdStep=-1;examinerEye=0;tweening=false;ReturnTools();held=0;
   if(abandon){PlayerPrefs.DeleteKey("ClinicSession");PlayerPrefs.DeleteKey("ClinicRemote");PlayerPrefs.Save();ui.canResume=false;remoteOwned=false;}else SaveProgress();
   begun=false;paused=false;notebook=false;focused=false;patientView=false;if(mist)mist.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(sheen)sheen.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(streakLight)streakLight.enabled=false;if(streakStrip)streakStrip.gameObject.SetActive(false);foreach(string n in new[]{"PupilReflex_0","PupilReflex_1"})if(N(n))N(n).gameObject.SetActive(false);
   Release();ui.menu.SetActive(true);ui.Refresh();}
''')
s=s.replace('   view.transform.position=walkPos;view.transform.rotation=Quaternion.Euler(0,0,0);view.fieldOfView=targetFov=66;', '''   SetMusicVolume(PlayerPrefs.GetFloat("ClinicMusicVolume",.2f));
   #if !UNITY_WEBGL || UNITY_EDITOR
   StartCoroutine(LoadMusic());
   #endif
   view.transform.position=walkPos;view.transform.rotation=Quaternion.Euler(0,0,0);view.fieldOfView=targetFov=66;''')
s=s.replace('if(Input.GetKeyDown(KeyCode.Escape)){if(pdStep>=0&&!busy)PDExit(false);else{paused=!paused;if(paused)Release();else if(!focused&&!notebook)Lock();}}', 'if(Input.GetKeyDown(KeyCode.Escape))TogglePause();')
s=s.replace('begun=true;pdStep=-1;cinematic=false;walkPos=', 'begun=true;paused=false;notebook=false;focused=false;patientView=false;busy=false;Time.timeScale=1;pdStep=-1;cinematic=false;walkPos=')
s=s.replace('    remoteOwned=false;projectorOn=true;', '    Time.timeScale=1;remoteOwned=false;projectorOn=true;')
p.write_text(s,encoding='utf-8')
p=Path('UnityProject/Assets/Scripts/ClinicUI.cs');s=p.read_text(encoding='utf-8').replace('  Button next,resume;', '  Button next,resume;Text volumeLabel;Slider musicSlider;GameObject abandonConfirm;')
s=s.replace('()=>{world.paused=!world.paused;world.Release();}', '()=>world.TogglePause()')
s=s.replace('()=>{world.begun=false;world.notebook=false;world.focused=false;world.Release();menu.SetActive(true);}', '()=>world.ReturnToMenu(false)')
a=s.index('   var pcard=Panel(');b=s.index('   // Pin HUD',a)
s=s[:a]+'''   var pcard=Panel("Pause card",pausedPanel.transform,0,0,650,590,new Vector2(.5f,.5f),dark);
   Txt("pause title",pcard.transform,"暫 停",0,225,530,60,40,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   Txt("music track",pcard.transform,"Blue Oiia - Uiia Cat · 循環播放",0,162,530,38,19,muted,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   volumeLabel=Txt("music volume",pcard.transform,"",0,117,530,32,21,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   var sliderObj=Panel("Music volume slider",pcard.transform,0,70,460,36,new Vector2(.5f,.5f),lighter);musicSlider=sliderObj.AddComponent<Slider>();musicSlider.minValue=0;musicSlider.maxValue=1;
   var fill=Panel("Volume fill",sliderObj.transform,0,0,460,10,new Vector2(.5f,.5f),accent);musicSlider.fillRect=fill.GetComponent<RectTransform>();
   var handle=Panel("Volume handle",sliderObj.transform,0,0,26,36,new Vector2(.5f,.5f),pale);musicSlider.handleRect=handle.GetComponent<RectTransform>();musicSlider.targetGraphic=handle.GetComponent<Image>();musicSlider.SetValueWithoutNotify(world.musicVolume);musicSlider.onValueChanged.AddListener(v=>world.SetMusicVolume(v));
   Btn("continue",pcard.transform,"繼續練習",0,6,530,52,()=>world.TogglePause(),true);
   Btn("save and menu",pcard.transform,"保存進度，回主選單",0,-59,530,52,()=>world.ReturnToMenu(false));
   Btn("abandon",pcard.transform,"放棄這輪",0,-124,530,48,()=>abandonConfirm.SetActive(true));
   Txt("pause help",pcard.transform,"音量 0% 為靜音，設定會自動記住。\\n回主選單後可選「繼續上次練習」。",0,-217,530,72,18,muted,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   abandonConfirm=Panel("Confirm abandon",pcard.transform,0,0,600,350,new Vector2(.5f,.5f),dark);
   Txt("abandon question",abandonConfirm.transform,"確定放棄這輪？\\n這輪的病例進度與紀錄將清除。",0,65,540,100,24,pale,new Vector2(.5f,.5f),TextAnchor.MiddleCenter);
   Btn("cancel abandon",abandonConfirm.transform,"保留這輪",-140,-80,260,54,()=>abandonConfirm.SetActive(false));
   Btn("confirm abandon",abandonConfirm.transform,"確定放棄",140,-80,260,54,()=>{abandonConfirm.SetActive(false);world.ReturnToMenu(true);},true);abandonConfirm.SetActive(false);
''' +s[b:]
s=s.replace('   if(!play)return;', '   if(!world.paused&&abandonConfirm)abandonConfirm.SetActive(false);if(volumeLabel)volumeLabel.text="背景音樂音量  "+Mathf.RoundToInt(world.musicVolume*100)+"%";\n   if(!play)return;')
p.write_text(s,encoding='utf-8')
p=Path('UnityProject/Assets/Plugins/WebGL/Clinic.jslib');s=p.read_text(encoding='utf-8-sig').replace(' ClinicCapture:', ' ClinicMusicVolume: function(volume){var audio=document.getElementById("clinic-music");if(audio)audio.volume=Math.max(0,Math.min(1,volume));},\n ClinicCapture:');p.write_text(s,encoding='utf-8')
p=Path('UnityProject/Assets/WebGLTemplates/Clinic/index.html');s=p.read_text(encoding='utf-8').replace('<canvas id=', '<audio id="clinic-music" loop preload="metadata" src="StreamingAssets/Audio/blue-oiia-uiia-cat.mp3"></audio>\n<canvas id=',1)
s=s.replace("window.clinicErrors=[];", "const music=document.getElementById('clinic-music');music.volume=.2;function startMusic(){if(music.paused)music.play().catch(()=>{});}document.addEventListener('pointerdown',startMusic);document.addEventListener('keydown',startMusic);\nwindow.clinicErrors=[];")
p.write_text(s,encoding='utf-8')
