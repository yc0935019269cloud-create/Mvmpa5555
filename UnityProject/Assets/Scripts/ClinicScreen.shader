Shader "Clinic/Screen" {
 // Self-lit chart display (projector screen / monitor): keeps the white chart white after tone mapping.
 Properties { _MainTex ("Chart", 2D)="white" {} _Boost ("Luminance", Float)=1.7 }
 SubShader {
  Tags { "RenderType"="Opaque" }
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
   sampler2D _MainTex;float _Boost;
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
   fixed4 frag(v2f i):SV_Target {return fixed4(tex2D(_MainTex,i.uv).rgb*_Boost,1);}
   ENDCG
  }
 }
}
