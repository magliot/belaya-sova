Shader "Owl/Course model" {
    Properties {
        _BaseMap("Base colour",2D)="white"{}
        _Lit("Use daylight",Float)=1
        _Glow("Emission",Float)=0
        _SrcBlend("Source blend",Float)=1
        _DstBlend("Destination blend",Float)=0
        _ZWrite("Depth write",Float)=1
        _LeafCoverage("Foliage alpha to coverage", Float)=0
        _FadeCable("Soften distant lift cable", Float)=0
        _StoneRim("Icy stone gate edge", Float)=0
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass {
            Tags {"LightMode"="UniversalForward"}
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            AlphaToMask [_LeafCoverage]
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local_fragment _ OWL_START_BANNER
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float _Lit,_Glow,_LeafCoverage,_FadeCable,_StoneRim;
            CBUFFER_END
            half _OwlNight;
            struct Input {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;half4 c:COLOR;};
            struct Output {float4 p:SV_POSITION;float3 n:TEXCOORD0;float2 uv:TEXCOORD1;
                           half4 c:COLOR;float fog:TEXCOORD2;float3 world:TEXCOORD3;};
            Output vert(Input v){
                Output o;o.world=TransformObjectToWorld(v.p.xyz);o.n=TransformObjectToWorldNormal(v.n);
                if(_LeafCoverage>.5){
                    float3 root=TransformObjectToWorld(float3(0,0,0));
                    float3 facing=SafeNormalize(float3(_WorldSpaceCameraPos.x-root.x,0,_WorldSpaceCameraPos.z-root.z));
                    // A second intersecting drawing gave bent spruces two trunks and four mismatched crowns.
                    float3 right=float3(-facing.z,0,facing.x);
                    o.world=root+(right*v.p.x+float3(0,v.p.y,0))*length(TransformObjectToWorldDir(float3(0,1,0),false));
                    o.n=facing;
                }
                o.p=TransformWorldToHClip(o.world);o.uv=v.uv;o.c=v.c;o.fog=ComputeFogFactor(o.p.z);
                if(_FadeCable>.5)o.c.a*=lerp(1,.18,smoothstep(16.,82.,o.p.w));
                return o;
            }
            half4 frag(Output i):SV_Target{
                half4 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*i.c;
                #ifndef UNITY_COLORSPACE_GAMMA
                base.rgb=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*SRGBToLinear(i.c.rgb);
                #endif
                #if defined(OWL_START_BANNER)
                base.a*=smoothstep(7.0,14.0,-TransformWorldToView(i.world).z);
                #endif
                if(_LeafCoverage>.5){
                    // Coverage-preserving mips and a one-pixel transition keep distant needles stable.
                    base.a=saturate((base.a-.38)/max(fwidth(base.a),.05)+.5);
                    clip(base.a-.01);
                }else clip(base.a-.024);
                half3 n=SafeNormalize(i.n);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world),i.world,half4(1,1,1,1));
                half3 daylight=max(half3(.12,.14,.18),SampleSH(n))+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                base.rgb*=lerp(half3(1,1,1),daylight,_Lit)+_Glow;
                base.rgb*=lerp(half3(1,1,1),half3(.23,.36,.58),_OwlNight*(1-saturate(_Lit))*(1-saturate(_Glow)));
                if(_StoneRim>.5){
                    half edge=1.-saturate(abs(dot(n,GetWorldSpaceNormalizeViewDir(i.world))));
                    base.rgb+=half3(.09,.62,.85)*edge*edge;
                }
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
