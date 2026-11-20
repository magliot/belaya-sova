Shader "Owl/MountainRaceSky" {
 Properties {
  _Ridge("Painted landscape",2D)="white"{}
  _LeftRidge("Ridge A",2D)="white"{}
  _RightRidge("Ridge C",2D)="white"{}
  _Route("Tubing route",2D)="black"{}
  _RouteNext("Next tubing landscape",2D)="black"{}
  _RouteBlend("Route transition",Range(0,1))=0
  _Aurora("Aurora",2D)="black"{}
  _AuroraA("Aurora A",2D)="black"{}
  _AuroraC("Aurora C",2D)="black"{}
  _MenuArt("Menu landscape",2D)="white"{}
  _Menu("Menu",Float)=0
  _Tube("Tubing",Float)=0
  _Forward("Direction",Vector)=(0,0,1,0)
  _Horizon("Horizon",Color)=(.82,.90,.97,1)
  _Zenith("Zenith",Color)=(.38,.67,.91,1)
  _Tint("Landscape tint",Color)=(1,1,1,1)
  _Night("Night",Range(0,1))=0
  _Storm("Blowing snow",Range(0,1))=0
  _Clock("Ride clock",Float)=0
 }
 SubShader {
  Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
  Cull Off ZWrite Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   sampler2D _Ridge,_LeftRidge,_RightRidge,_Aurora,_AuroraA,_AuroraC,_MenuArt,_Route,_RouteNext;
   float _RouteBlend;
   float4 _Forward,_Horizon,_Zenith,_Tint;float _Night,_Storm,_Clock,_Menu,_Tube;
   struct V {float4 pos:SV_POSITION;float3 ray:TEXCOORD0;float4 screen:TEXCOORD1;};
   V vert(float4 p:POSITION){V o;o.pos=UnityObjectToClipPos(p);o.screen=ComputeScreenPos(o.pos);o.ray=p.xyz;return o;}
   fixed4 mountainPanel(float panel,float2 uv,float2 du,float2 dv){
    uv=clamp(uv,.002,.998);
    float tile=frac((panel+1.)/3.+.001)*3.;
    if(tile<.5)return tex2Dgrad(_LeftRidge,uv,du,dv);
    if(tile>1.5)return tex2Dgrad(_RightRidge,uv,du,dv);
    return tex2Dgrad(_Ridge,uv,du,dv);
   }
   fixed4 frag(V i):SV_Target{
    if(_Menu>.5){
     float2 screen=i.screen.xy/i.screen.w;
     float crop=(_ScreenParams.x/_ScreenParams.y)/(889./1580.);
     float2 uv=.5+(screen-.5)*float2(min(crop,1.),min(1./crop,1.));
     // Anchor the snow platform under the menu rider when a wide viewport crops the artwork.
     uv.y=.40+(screen.y-.40)*min(1./crop,1.);
     return tex2D(_MenuArt,uv);
    }
    float3 dir=normalize(i.ray);
    float2 right=float2(_Forward.z,-_Forward.x);
    float az=atan2(dot(dir.xz,right),dot(dir.xz,_Forward.xz));
    float3 sky=lerp(_Horizon.rgb,_Zenith.rgb,smoothstep(-.07,.7,dir.y));
    if(_Tube>.5){
     // These paintings already contain sky, ridge and valley. Wrap them around the
     // view and dissolve the far snowfield into the same haze as the 3D terrain.
     float2 uv=float2(.50+.485*sin(az),clamp(.44+dir.y*1.42,.012,.988));
     fixed3 location=lerp(tex2D(_Route,uv).rgb,tex2D(_RouteNext,uv).rgb,_RouteBlend);
     fixed3 distant=lerp(_Horizon.rgb,location,.96*smoothstep(-.175,.035,dir.y));
     return fixed4(lerp(distant,sky,smoothstep(.29,.67,dir.y)),1);
    }
    if(_Night>.01){
     float2 st=float2(az,dir.y)*135;
     float noise=frac(sin(dot(floor(st),float2(12.9898,78.233)))*43758.5);
     sky+=_Night*step(.991,noise)*(1-smoothstep(.02,.24,length(frac(st)-.5)))*.7;
     float group=clamp(floor(az/.60+.5),-1.,1.);
     float2 auruv=float2(.5+(az-group*.60)*1.54,.16+dir.y*1.14);
     auruv.y+=.06*sin(auruv.x*15+_Clock*.55)*sin(auruv.y*3.14);
     auruv.x+=.016*sin(auruv.y*9+_Clock*.3);
     fixed4 aur=tex2D(_Aurora,clamp(auruv,.002,.998));
     if(group<-.5)aur=tex2D(_AuroraA,clamp(auruv,.002,.998));
     if(group>.5)aur=tex2D(_AuroraC,clamp(auruv,.002,.998));
     float mask=smoothstep(0,.12,min(auruv.x,1-auruv.x))*smoothstep(.09,.23,auruv.y)*smoothstep(0,.1,1-auruv.y);
     sky+=aur.rgb*aur.a*mask*_Night*(.64+.22*cos(auruv.x*22-_Clock*.57));
    }
    float bearing=atan2(dir.x,dir.z);
    // Nine overlapping mountain views complete one revolution. Keep the valley floor
    // below the skyline all the way down to the descending foreground geometry.
    float sector=bearing*(9./6.2831853);
    float panel=floor(sector),t=frac(sector),v=.67+dir.y*1.65;
    // UV jumps between panels, but a pixel's footprint stays continuous through each seam
    // (including atan2's wrap). Otherwise the coarsest mip draws a vertical line in clear sky.
    float inv=1.1172677/max(dot(dir.xz,dir.xz),.001); // .78 * 9 / (2*pi)
    float2 du=float2(inv*(dir.z*ddx(dir.x)-dir.x*ddx(dir.z)),ddx(v));
    float2 dv=float2(inv*(dir.z*ddy(dir.x)-dir.x*ddy(dir.z)),ddy(v));
    fixed4 ridgeA=mountainPanel(panel,float2(.5+t*.78,v),du,dv);
    fixed4 ridgeB=mountainPanel(panel+1.,float2(.5+(t-1.)*.78,v),du,dv);
    float blend=smoothstep(.36,.64,t);
    fixed3 a=lerp(sky,lerp(ridgeA.rgb*_Tint.rgb,_Horizon.rgb,.20),ridgeA.a);
    fixed3 b=lerp(sky,lerp(ridgeB.rgb*_Tint.rgb,_Horizon.rgb,.20),ridgeB.a);
    sky=lerp(a,b,blend);
    if(_Storm>.5){
     float ripple=sin(az*5.-_Clock*.65+sin(az*11.+dir.y*18.-_Clock*.31));
     float veil=smoothstep(-.17,.06,dir.y)*(1.-smoothstep(.05,.44,dir.y));
     sky=lerp(sky,_Horizon.rgb,.84+veil*(.105+.045*ripple));
    }
    return fixed4(sky,1);
   }
   ENDCG
  }
 }
}
