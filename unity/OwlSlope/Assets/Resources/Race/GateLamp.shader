Shader "Owl/Night gate lamps" {
 Properties {_Tint("Reflector colour", Color)=(.2,.87,1,1)}
 SubShader {
  Tags {"Queue"="Transparent+5" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Pass {
   Cull Off ZWrite Off Blend SrcAlpha One
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _Tint;
   CBUFFER_END
   struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;half visibility:TEXCOORD1;};
   V vert(float3 p:POSITION,float2 uv:TEXCOORD0){
    float3 center=TransformWorldToView(TransformObjectToWorld(p));
    float depth=-center.z;
    // Keep each lens legible at a distance. The hardware depth test still hides it behind terrain.
    float pixel=2.*max(depth,.01)/(_ScreenParams.y*UNITY_MATRIX_P._m11);
    V o;o.uv=uv*2.-1.;center.xy+=o.uv*max(.42,4.5*pixel);
    center.z+=.21;
    o.p=mul(UNITY_MATRIX_P,float4(center,1));o.visibility=1.-smoothstep(44.,80.,depth);return o;
   }
   half4 frag(V i):SV_Target {
    float r=length(i.uv),core=1.-smoothstep(.19,.39,r);
    float halo=pow(saturate(1.-r),2.5);
    return half4(lerp(_Tint.rgb,half3(1,.97,.88),core*.75),i.visibility*(core*.90+halo*.55));
   }
   ENDHLSL
  }
 }
}
