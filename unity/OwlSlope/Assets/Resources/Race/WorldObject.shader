Shader "Owl/Course model" {
    Properties {
        _BaseMap("Base colour",2D)="white"{}
        _Lit("Use daylight",Float)=1
        _Glow("Emission",Float)=0
        _SrcBlend("Source blend",Float)=1
        _DstBlend("Destination blend",Float)=0
        _ZWrite("Depth write",Float)=1
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass {
            Tags {"LightMode"="UniversalForward"}
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local_fragment _ OWL_START_BANNER
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float _Lit,_Glow;
            CBUFFER_END
            struct Input {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;half4 c:COLOR;};
            struct Output {float4 p:SV_POSITION;float3 n:TEXCOORD0;float2 uv:TEXCOORD1;
                           half4 c:COLOR;float fog:TEXCOORD2;float3 world:TEXCOORD3;};
            Output vert(Input v){
                Output o;o.world=TransformObjectToWorld(v.p.xyz);o.p=TransformWorldToHClip(o.world);
                o.n=TransformObjectToWorldNormal(v.n);o.uv=v.uv;o.c=v.c;o.fog=ComputeFogFactor(o.p.z);return o;
            }
            half4 frag(Output i):SV_Target{
                half4 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*i.c;
                #ifndef UNITY_COLORSPACE_GAMMA
                base.rgb=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*SRGBToLinear(i.c.rgb);
                #endif
                #if defined(OWL_START_BANNER)
                base.a*=smoothstep(7.0,14.0,-TransformWorldToView(i.world).z);
                #endif
                clip(base.a-.024);
                half3 n=normalize(i.n);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 daylight=max(half3(.12,.14,.18),SampleSH(n))+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                base.rgb*=lerp(half3(1,1,1),daylight,_Lit)+_Glow;
                base.rgb=MixFog(base.rgb,i.fog);return base;
            }
            ENDHLSL
        }
        Pass {
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On ZTest LEqual Cull Off ColorMask 0
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float3 _LightDirection;
            float _DstBlend;
            struct In {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;half4 c:COLOR;};
            struct Out {float4 p:SV_POSITION;float2 uv:TEXCOORD0;half alpha:TEXCOORD1;};
            Out shadowVert(In v){
                Out o;
                float3 world=TransformObjectToWorld(v.p.xyz);
                o.p=TransformWorldToHClip(ApplyShadowBias(world,TransformObjectToWorldNormal(v.n),_LightDirection));
                o.p=ApplyShadowClamping(o.p);o.uv=v.uv;o.alpha=v.c.a;return o;
            }
            half4 shadowFrag(Out i):SV_Target{
                clip(abs(_DstBlend-1)-.01);
                clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a*i.alpha-.48);return 0;
            }
            ENDHLSL
        }
    }
}
