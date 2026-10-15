Shader "Clinic/RetinalReflex" {
 // Pupil reflex of a streak retinoscope. The C# side computes where the reflex band sits
 // (with / against / neutral, speed, width, break angle) from the lens and the eye.
 Properties { _Shift ("Band offset (pupil radii)", Float)=0 _Angle ("Band normal angle", Float)=0 _Width ("Band width", Float)=.4 _Bright ("Brightness", Float)=1 _Neutral ("Neutral fill", Float)=0 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Blend SrcAlpha OneMinusSrcAlpha
  ZWrite Off Cull Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
   float _Shift,_Angle,_Width,_Bright,_Neutral;
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv*2-1;return o;}
   fixed4 frag(v2f i):SV_Target {
    float d=length(i.uv);clip(1-d);
    float2 n=float2(cos(_Angle),sin(_Angle));
    float band=exp(-pow((dot(i.uv,n)-_Shift)/_Width,2)*2.5);
    float lit=lerp(band,1,_Neutral)*_Bright*(1-smoothstep(.82,1,d)*.45);
    float3 c=float3(1.05,.36,.09)*lit+float3(.05,.012,.006);
    return fixed4(c,saturate(.25+lit));
   }
   ENDCG
  }
 }
}
