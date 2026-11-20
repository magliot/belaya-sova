Shader "Owl/Festival spark" {
 SubShader {
  Tags {"Queue"="Transparent+8" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
  Pass {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Input {float4 pos:POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
   struct V {float4 p:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
   V vert(Input i){V o;o.p=TransformObjectToHClip(i.pos.xyz);o.color=i.color;o.uv=i.uv*2-1;return o;}
   half4 frag(V i):SV_Target {
    float r=dot(i.uv,i.uv);clip(1.-r);
    half core=1.-smoothstep(.025,.22,r);
    return half4(lerp(i.color.rgb,half3(1,.98,.90),.71*core),
                 i.color.a*(1.-smoothstep(.05,1.,r)));
   }
   ENDHLSL
  }
 }
}
