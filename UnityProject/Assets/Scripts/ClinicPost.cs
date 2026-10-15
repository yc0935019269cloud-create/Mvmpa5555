using UnityEngine;

namespace RefractionRoom {
 // Camera grade for the built-in pipeline: HDR bloom, ACES tone curve, room-light exposure, vignette.
 [RequireComponent(typeof(Camera))]
 public class ClinicPost : MonoBehaviour {
  public float exposure=1,targetExposure=1,bloom=.06f,threshold=1.1f,saturation=1.06f,contrast=1.04f,vignette=.28f;
  public Color tint=Color.white;
  Material mat;
  RenderTexture[] chain=new RenderTexture[5];
  void OnEnable(){var s=Shader.Find("Hidden/Clinic/Post");if(s&&s.isSupported)mat=new Material(s){hideFlags=HideFlags.HideAndDontSave};}
  void Update(){exposure=Mathf.Lerp(exposure,targetExposure,1-Mathf.Exp(-3*Time.unscaledDeltaTime));}
  void OnRenderImage(RenderTexture src,RenderTexture dst){
   if(!mat||src.width<32||src.height<32){Graphics.Blit(src,dst);return;}
   mat.SetFloat("_Threshold",threshold);mat.SetFloat("_Knee",threshold*.5f);mat.SetFloat("_Exposure",exposure);mat.SetFloat("_BloomStrength",bloom);
   mat.SetFloat("_Saturation",saturation);mat.SetFloat("_Contrast",contrast);mat.SetFloat("_Vignette",vignette);mat.SetColor("_Tint",tint);
   var fmt=src.format;int w=src.width/2,h=src.height/2,n=0;
   RenderTexture last=chain[0]=RenderTexture.GetTemporary(w,h,0,fmt);Graphics.Blit(src,last,mat,0);
   for(n=1;n<chain.Length;n++){w/=2;h/=2;if(w<8||h<8)break;chain[n]=RenderTexture.GetTemporary(w,h,0,fmt);Graphics.Blit(last,chain[n],mat,1);last=chain[n];}
   for(int i=n-2;i>=0;i--){Graphics.Blit(last,chain[i],mat,2);last=chain[i];}
   mat.SetTexture("_Bloom",last);Graphics.Blit(src,dst,mat,3);
   for(int i=0;i<n;i++){RenderTexture.ReleaseTemporary(chain[i]);chain[i]=null;}
  }
 }
}
