Shader "Owl/MountainRaceFlake" {
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct Input {float4 pos:POSITION;fixed4 tint:COLOR;float2 uv:TEXCOORD0;};
   struct V {float4 clip:SV_POSITION;fixed4 tint:COLOR;float2 uv:TEXCOORD0;};
   V vert(Input v){V o;o.clip=UnityObjectToClipPos(v.pos);o.tint=v.tint;o.uv=v.uv;return o;}
   fixed4 frag(V i):SV_Target {return fixed4(i.tint.rgb,i.tint.a*(1-smoothstep(.26,.5,length(i.uv-.5))));}
   ENDCG
  }
 }
}
