Shader "Owl/Mandarin sparkle" {
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent"}
  Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct V {float4 clip:SV_POSITION;fixed4 tint:COLOR;float2 uv:TEXCOORD0;};
   V vert(float4 p:POSITION,fixed4 tint:COLOR,float2 uv:TEXCOORD0){
       V o;o.clip=UnityObjectToClipPos(p);o.tint=tint;o.uv=uv;return o;
   }
   fixed4 frag(V i):SV_Target {
       float2 xy=abs(i.uv-.5)*2;
       float star=xy.x+xy.y+6*xy.x*xy.y;
       return fixed4(lerp(i.tint.rgb,fixed3(1,.98,.78),1-smoothstep(.04,.36,star)),i.tint.a*(1-smoothstep(.66,1,star)));
   }
   ENDCG
  }
 }
}
