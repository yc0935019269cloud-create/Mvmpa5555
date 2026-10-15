Shader "Clinic/Mist" {
 // Soft round particles for the alcohol spray and the wet sheen left by the wipe.
 Properties { }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Blend SrcAlpha One ZWrite Off Cull Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   struct v2f {float4 pos:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv*2-1;return o;}
   fixed4 frag(v2f i):SV_Target {float d=length(i.uv);float a=saturate(1-d);a*=a;return fixed4(i.color.rgb,a*i.color.a);}
   ENDCG
  }
 }
}
