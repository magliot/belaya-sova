Shader "Owl/MountainRaceSnow" {
 Properties { _MainTex("Artwork",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) }
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent"}
  Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex; fixed4 _Color;
   struct App {float4 pos:POSITION;float2 uv:TEXCOORD0;};
   struct V {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
   V vert(App a){V v;v.pos=UnityObjectToClipPos(a.pos);v.uv=a.uv;return v;}
   fixed4 frag(V v):SV_Target{return tex2D(_MainTex,v.uv)*_Color;}
   ENDCG
  }
 }
}
