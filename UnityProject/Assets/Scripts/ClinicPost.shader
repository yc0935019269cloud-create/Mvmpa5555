Shader "Hidden/Clinic/Post" {
 // Small built-in pipeline grade: soft bloom, exposure, ACES filmic curve, saturation, vignette.
 Properties { _MainTex ("Source", 2D)="white" {} _Bloom ("Bloom", 2D)="black" {} }
 CGINCLUDE
 #include "UnityCG.cginc"
 sampler2D _MainTex,_Bloom;float4 _MainTex_TexelSize;
 float _Threshold,_Knee,_Exposure,_BloomStrength,_Saturation,_Contrast,_Vignette;float4 _Tint;
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
 v2f vert(appdata_img v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;return o;}
 half3 Box(float2 uv,float d){float4 o=_MainTex_TexelSize.xyxy*float4(-d,-d,d,d);
  return (tex2D(_MainTex,uv+o.xy).rgb+tex2D(_MainTex,uv+o.zy).rgb+tex2D(_MainTex,uv+o.xw).rgb+tex2D(_MainTex,uv+o.zw).rgb)*.25;}
 half3 Prefilter(half3 c){half b=max(c.r,max(c.g,c.b));half soft=clamp(b-_Threshold+_Knee,0,2*_Knee);soft=soft*soft/(4*_Knee+1e-4);
  return c*max(soft,b-_Threshold)/max(b,1e-4);}
 half3 ACES(half3 x){return saturate((x*(2.51*x+.03))/(x*(2.43*x+.59)+.14));}
 ENDCG
 SubShader {
  Cull Off ZWrite Off ZTest Always
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment f
   half4 f(v2f i):SV_Target{return half4(Prefilter(Box(i.uv,1)*_Exposure),1);}
   ENDCG }
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment f
   half4 f(v2f i):SV_Target{return half4(Box(i.uv,1),1);}
   ENDCG }
  Pass { Blend One One
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment f
   half4 f(v2f i):SV_Target{return half4(Box(i.uv,.5),1);}
   ENDCG }
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment f
   half4 f(v2f i):SV_Target{
    half3 c=tex2D(_MainTex,i.uv).rgb*_Exposure+tex2D(_Bloom,i.uv).rgb*_BloomStrength;
    c*=_Tint.rgb;
    c=ACES(c*.6);
    half l=dot(c,half3(.2126,.7152,.0722));c=lerp(l.xxx,c,_Saturation);
    c=saturate((c-.5)*_Contrast+.5);
    float2 d=(i.uv-.5)*float2(1.25,1);c*=1-_Vignette*smoothstep(.25,.95,dot(d,d)*2.2);
    return half4(c,1);}
   ENDCG }
 }
}
