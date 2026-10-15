mergeInto(LibraryManager.library, {
 ClinicCapture: function(enabled){
  window.clinicWantsLook=!!enabled;
  if(!enabled){try{if(document.pointerLockElement)document.exitPointerLock();}catch(e){}return;}
  var c=document.getElementById('unity-canvas');
  try{var p=c.requestPointerLock();if(p&&p.catch)p.catch(function(){});}catch(e){}
 },
 ClinicSpeak: function(ptr) {
  var text=UTF8ToString(ptr);
  try{
  if(!window.speechSynthesis||!window.SpeechSynthesisUtterance)return;
  window.speechSynthesis.cancel();
  var speech=new SpeechSynthesisUtterance(text);speech.lang='zh-TW';speech.rate=1;
  var voices=window.speechSynthesis.getVoices();var voice=voices.find(function(v){return v.lang==='zh-TW';})||voices.find(function(v){return v.lang.indexOf('zh')===0;});if(voice)speech.voice=voice;
  window.speechSynthesis.speak(speech);
  }catch(e){}
 },
 ClinicDownload: function(name,text){
  var blob=new Blob([UTF8ToString(text)],{type:'text/plain;charset=utf-8'}),url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=UTF8ToString(name);a.click();setTimeout(function(){URL.revokeObjectURL(url);},10000);
 },
 ClinicTelemetry: function(ptr){
  var el=document.getElementById('clinic-state');if(el)el.textContent=UTF8ToString(ptr);
 }
});
