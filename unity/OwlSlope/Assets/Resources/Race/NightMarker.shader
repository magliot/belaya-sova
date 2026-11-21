Shader "Owl/Night course marker" {
    Properties { _Tint("Light colour",Color)=(1,.49,.12,1) _Ring("Ring band",Float)=0 }
    SubShader {
        Tags {"Queue"="Transparent+5" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Pass {
            Blend SrcAlpha One
            Cull Back ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;half _Ring;
            CBUFFER_END
            half _OwlNight;
            struct Vertex {float3 p:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct Fragment {float4 clip:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float band:TEXCOORD2;};
            Fragment vert(Vertex v){
                Fragment o;o.world=TransformObjectToWorld(v.p);o.clip=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normal);o.band=v.uv.x;return o;
            }
            half4 frag(Fragment i):SV_Target{
                half profile;
                if(_Ring>.5h)profile=1-abs(2*i.band-1);
                else profile=saturate(dot(normalize(i.normal),GetWorldSpaceNormalizeViewDir(i.world)));
                half glow=_Ring>.5h?profile*profile:.42h*profile*profile*(1-.69h*smoothstep(.83h,.97h,profile));
                half core=smoothstep(.83h,1.h,profile);
                half visible=1-smoothstep(44.,72.,distance(i.world,_WorldSpaceCameraPos));
                return half4(lerp(_Tint.rgb,half3(1,.97,.78),core*.48h),glow*visible*_OwlNight);
            }
            ENDHLSL
        }
    }
}
