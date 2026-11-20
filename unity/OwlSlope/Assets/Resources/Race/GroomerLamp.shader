Shader "Owl/Groomer lamp" {
 Properties { _Tint("Light",Color)=(1,.87,.55,1) _Beacon("Rotating beacon",Float)=0 }
 SubShader {
  Tags {"Queue"="Transparent+1" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Pass {
   Blend SrcAlpha One Cull Off ZWrite Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   half4 _Tint;float _Beacon;
   struct Input {float4 pos:POSITION;};
   struct Output {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float fog:TEXCOORD1;float facing:TEXCOORD2;};
   Output vert(Input i) {
    Output o;float3 source=TransformObjectToWorld(float3(0,0,0));
    float radius=length(TransformObjectToWorldDir(float3(1,0,0),false));
    float3 p=TransformWorldToView(source);p.xy+=i.pos.xy*radius;
    // The source is the measured centre of the solid lamp lens.
    p.z+=_Beacon>.5?.20:.12;
    o.pos=mul(UNITY_MATRIX_P,float4(p,1));o.uv=i.pos.xy;
    o.fog=ComputeFogFactor(o.pos.z);
    o.facing=_Beacon>.5?1:smoothstep(-.08,.48,dot(TransformObjectToWorldDir(float3(0,0,1)),GetWorldSpaceNormalizeViewDir(source)));
    return o;
   }
   half4 frag(Output i):SV_Target {
    float r=dot(i.uv,i.uv);clip(1-r);
    float pulse=lerp(1.,.30+.70*pow(.5+.5*cos(_Time.y*7.2),3.),_Beacon);
    float halo=exp(-5*r)*(1-smoothstep(.6,1.,r));
    float core=1.-smoothstep(.016,.069,r);
    half3 color=lerp(_Tint.rgb,half3(1,.98,.88),core*(1.-.73*_Beacon));
    return half4(MixFogColor(color,half3(0,0,0),i.fog),saturate(core+halo*.72)*pulse*i.facing);
   }
   ENDHLSL
  }
 }
}
