Shader "Owl/Pickup glow" {
    Properties { _Tint("Light colour",Color)=(1,.76,.34,1) }
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Pass {
            Blend SrcAlpha One
            Cull Back ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            CBUFFER_END
            struct Varyings {float4 clip:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float fog:TEXCOORD2;};
            Varyings vert(float3 p:POSITION, float3 normal:NORMAL){
                Varyings o;o.world=TransformObjectToWorld(p);o.clip=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(normal);o.fog=ComputeFogFactor(o.clip.z);return o;
            }
            half4 frag(Varyings i):SV_Target{
                half depth=saturate(dot(normalize(i.normal),GetWorldSpaceNormalizeViewDir(i.world)));
                half strength=.44h*pow(depth,3.0h);
                return half4(MixFogColor(_Tint.rgb,half3(0,0,0),i.fog),strength);
            }
            ENDHLSL
        }
    }
}
