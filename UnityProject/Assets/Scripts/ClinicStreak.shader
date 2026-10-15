Shader "Clinic/Streak" {
 // Retinoscope streak intercept on the face: a soft additive line along local X, cut out over the pupil
 // so the fundus reflex (drawn separately) stays readable.
 Properties { _Hole ("Pupil centre (xyz) radius (w)", Vector)=(0,0,0,0) _Glow ("Glow", Float)=1.4 }
 SubShader {
  Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Blend SrcAlpha One ZWrite Off Cull Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 wp:TEXCOORD1;};
   float4 _Hole;float _Glow;
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.wp=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
   fixed4 frag(v2f i):SV_Target {
    float across=abs(i.uv.y*2-1),along=abs(i.uv.x*2-1);
    float a=exp(-across*across*9)*(1-smoothstep(.55,1,along));
    float2 d=i.wp.xy-_Hole.xy;a*=smoothstep(_Hole.w*.92,_Hole.w*1.12,length(d));
    return fixed4(float3(1,.93,.78)*_Glow,a*.85);
   }
   ENDCG
  }
 }
}
