using System;
using UnityEngine;

public static class WeatherFrontChecks {
    static void Need(bool pass,string detail){if(!pass)throw new InvalidOperationException("Weather transition: "+detail);}

    public static void Verify(MountainRace race){
        race.StartRace("{\"mode\":\"review\",\"seed\":1911}");
        foreach(int lap in new[]{0,20000}){
            foreach(int edge in new[]{10000,12500,15000,17500}){
                bool blizzard=edge<15000,entering=edge==10000||edge==15000;
                string property=blizzard?"_Storm":"_Night";
                int arch=lap+edge;
                float previous=-1,previousFog=0,previousLight=0;
                foreach(int offset in new[]{-221,-150,-55,-1,1,48,81}){
                    race.Seek(arch+offset);
                    var sky=RenderSettings.skybox;
                    float weight=sky.GetFloat(property),fog=RenderSettings.fogEndDistance,light=RenderSettings.sun.intensity;
                    float progress=entering?weight:1-weight;
                    Need(progress>previous,"transition not advancing at "+(arch+offset));
                    if(offset==-221)Need(progress<.001f,"front started early");
                    if(offset==-55)Need(progress>.45f&&progress<.65f,"no intermediate weather before arch");
                    if(offset==81)Need(progress>.999f,"front did not finish");
                    if(offset==1){
                        Need(Mathf.Abs(previousFog-fog)<3.6f,"visibility jumped at arch "+arch);
                        Need(Mathf.Abs(previousLight-light)<.012f,"sunlight jumped at arch "+arch);
                    }
                    if(blizzard)Need(Mathf.Abs(weight-race.Status.blizzard)<.001f,"snow and wind disagree");
                    previous=progress;previousFog=fog;previousLight=light;
                }
            }
        }
        race.Seek(14985);race.RideFromReview();
        for(int i=0;i<50&&race.Status.metres<15005;i++)race.Advance(.05f);
        Need(race.Status.metres>=15005&&race.Status.phase=="play"&&RenderSettings.sun.intensity<.31f,
            "ordinary descent through the night arch");
        Debug.Log("WEATHER FRONTS PASS: storm and night fade across four boundaries on two laps; live night entry.");
    }
}
