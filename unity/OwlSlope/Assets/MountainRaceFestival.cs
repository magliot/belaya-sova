using UnityEngine;

public partial class MountainRace {
    const int FestivalParticleLimit=680;
    readonly ParticleSystem.Particle[] festivalStars=new ParticleSystem.Particle[FestivalParticleLimit];
    static readonly Color[] festivalColors={
        new Color(1,.075f,.28f),new Color(.03f,.89f,1),new Color(1,.57f,.045f),
        new Color(.70f,.16f,1),new Color(.12f,1,.45f),new Color(1,.20f,.68f)
    };
    float festivalClock;
    int visibleSparks;

    void LateUpdate(){
        // A visitor can stop the course inspection and watch a complete display.
        if(weatherZone==4&&Review&&reviewRate==0){festivalClock+=Time.deltaTime;DrawFestival(festivalClock);}
    }

    void DrawFestival(float time){
        visibleSparks=0;
        int first=Mathf.FloorToInt((Status.metres+58)/70);
        for(int pad=first;pad<=first+2;pad++){
            float marker=pad*70,range=marker-Status.metres;
            float visible=Mathf.SmoothStep(0,1,Mathf.InverseLerp(45,74,range))
                *Mathf.SmoothStep(0,1,Mathf.InverseLerp(190,156,range));
            if(visible<=0)continue;
            float interval=3.62f+.25f*(pad%3),clock=time+pad*1.31f;
            int shot=Mathf.FloorToInt(clock/interval);float t=Mathf.Repeat(clock,interval);
            int style=(pad+shot)%3,side=(pad&1)==0?-1:1;
            // One fixed firing point in each section of the valley. Rockets climb
            // from the same place throughout the section while the camera moves.
            // Lay out each fixed pad from the sightline of its approach, once in
            // world coordinates. The bends otherwise put bursts behind the HUD or
            // entirely outside the narrow portrait camera.
            float overlook=marker-115;
            Vector3 eye=World.SampleGround(overlook-6.4f,0)+Vector3.up*2.65f;
            Vector3 view=(World.SampleGround(overlook+7.7f,0)+Vector3.up*1.12f-eye).normalized;
            Vector3 right=Vector3.Cross(Vector3.up,view).normalized,up=Vector3.Cross(view,right);
            var burst=eye+view*116+right*(side*24)+up*(51+pad%3*1.6f);
            var basePoint=burst-new Vector3(side*2.3f,0,.7f);
            basePoint.y=World.SampleGround(basePoint.z,side*24).y+1.1f;
            burst.y=Mathf.Max(burst.y,basePoint.y+14);
            var rise=burst-basePoint;
            Color primary=festivalColors[(pad+shot*2)%festivalColors.Length],
                  secondary=festivalColors[(pad+shot*2+(style==0?2:3))%festivalColors.Length];
            const float ascent=1.12f;
            if(t<ascent+.25f){
                for(int i=0;i<15;i++){
                    float f=t/ascent-i*.022f;
                    if(f<0||f>1)continue;
                    float height=f*(1.22f-.22f*f);
                    var p=basePoint+rise*height+new Vector3(.12f*Mathf.Sin(29*f+i),0,.10f*Mathf.Cos(19*f));
                    Spark(p,.60f-i*.025f,new Color(1,.67f+.02f*i,.25f+.035f*i),visible*(1-i/19f));
                }
            }
            float age=t-ascent;
            if(age<0||age>2.02f)continue;
            if(age<.23f)Spark(burst,2.2f-age*5,new Color(1,.98f,.79f),visible);
            int count=style==1?52:64;
            for(int i=0;i<count;i++){
                float a=i*2.39996f+shot*.29f,z=1-2*(i+.5f)/count,r=Mathf.Sqrt(1-z*z);
                var dir=new Vector3(Mathf.Cos(a)*r,z,Mathf.Sin(a)*r);
                if(style==1&&i%3!=0){
                    a=2*Mathf.PI*i/count;dir=new Vector3(Mathf.Cos(a),Mathf.Sin(a)*.91f,Mathf.Sin(a)*.42f);
                }
                float speed=8.5f+1.7f*(i%3)+(style==2?1.1f:0);
                float life=1.3f+.16f*(i%5),alpha=visible*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(life*.55f,life,age)));
                if(alpha<=0)continue;
                var color=i%7==0?new Color(1,.92f,.58f):i%3==0?secondary:primary;
                for(int trail=0;trail<3;trail++){
                    float tt=age-trail*.074f;if(tt<0)break;
                    var p=burst+dir*(speed*(1-Mathf.Exp(-tt*.78f))/.78f)-Vector3.up*(tt*tt*(style==2?3.1f:1.85f));
                    Spark(p,(trail==0?1.19f:.70f)*(1-.22f*tt),color,alpha*(1-trail*.27f));
                }
            }
        }
        fireworks.SetParticles(festivalStars,visibleSparks);
    }
    void Spark(Vector3 p,float size,Color color,float opacity){
        if(visibleSparks>=festivalStars.Length)return;
        color.a=opacity;
        festivalStars[visibleSparks++]=new ParticleSystem.Particle{position=p,startColor=color,startSize=size,startLifetime=3,remainingLifetime=3};
    }
}
