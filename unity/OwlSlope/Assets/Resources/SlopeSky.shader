Shader "Owl/SlopeSky" {
    Properties {
        _Ridge ("Painted ridge", 2D) = "white" {}
        _Forward ("Valley direction", Vector) = (0,0,1,0)
    }
    SubShader {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _Ridge;
            float4 _Forward;
            struct SkyVertex { float4 position : SV_POSITION; float3 ray : TEXCOORD0; };
            SkyVertex vert(float4 vertex : POSITION){
                SkyVertex o;o.position=UnityObjectToClipPos(vertex);o.ray=vertex.xyz;return o;
            }
            fixed4 frag(SkyVertex i) : SV_Target {
                float3 dir=normalize(i.ray);
                float3 sky=lerp(float3(.82,.90,.97),float3(.38,.67,.91),smoothstep(-.07,.70,dir.y));
                float2 right=float2(_Forward.z,-_Forward.x);
                float bearing=atan2(dot(dir.xz,right),dot(dir.xz,_Forward.xz));
                float2 uv=float2(.50+bearing*1.04,.39+dir.y*2.65);
                fixed4 ridge=tex2D(_Ridge,clamp(uv,.002,.998));
                float mask=smoothstep(0,.10,min(uv.x,1-uv.x))*smoothstep(0,.08,min(uv.y,1-uv.y));
                return fixed4(lerp(sky,lerp(ridge.rgb,float3(.76,.85,.94),.26),ridge.a*mask),1);
            }
            ENDCG
        }
    }
}
