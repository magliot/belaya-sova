Shader "Owl/Mandarin peel" {
 SubShader {
  Tags {"Queue"="Geometry" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
  Pass {
   Cull Off ZWrite On
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;half light:TEXCOORD1;};
   V vert(float3 p:POSITION,float3 normal:NORMAL,float2 uv:TEXCOORD0){
    V o;o.p=TransformObjectToHClip(p);o.uv=uv;
    o.light=dot(TransformObjectToWorldNormal(normal),half3(-.33,.78,-.51));return o;
   }
   half4 frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target {
    half outer=IS_FRONT_VFACE(face,1.,0.);
    half edge=smoothstep(0.,.12,min(i.uv.x,1.-i.uv.x));
    half dimples=sin(i.uv.x*28.)*sin(i.uv.y*63.)*.035;
    half3 rind=lerp(half3(1,.46,.065),half3(1,.22,.015),edge)+dimples;
    half3 pith=half3(1,.80,.46)+dimples;
    half l=.84+.16*i.light*(outer*2.-1.);
    return half4(lerp(pith,rind,outer)*l,1);
   }
   ENDHLSL
  }
 }
}
