Shader "Clinic/RetinalReflex" {
 Properties { _Residual ("Residual D", Float)=0 _Sweep ("Sweep", Float)=0 _Angle ("Angle", Float)=0 }
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
   float _Residual,_Sweep,_Angle;
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv*2-1;return o;}
   fixed4 frag(v2f i):SV_Target {
    float d=length(i.uv);clip(1-d);
    float2 axis=float2(cos(_Angle),sin(_Angle));
    float motion=_Residual<0?1:-1;
    float neutral=1-smoothstep(.06,.125,abs(_Residual));
    float width=lerp(.20,.8,1/(1+abs(_Residual)*2));
    float shift=_Sweep*motion*(1-neutral)*.8;
    float band=exp(-pow((dot(i.uv,axis)-shift)/width,2)*3);
    float brightness=lerp(.6,1,1/(1+abs(_Residual)));
    float lit=lerp(band,1,neutral)*brightness;
    return fixed4(.84*lit+.08,.26*lit+.015,.065*lit,1);
   }
   ENDCG
  }
 }
}
