using System;
using UnityEngine;

public static class WeatherFrontChecks {
    static void Need(bool pass,string detail){if(!pass)throw new InvalidOperationException("Weather transition: "+detail);}

    public static void Verify(MountainRace race){
        CheckSlopeEffects.CourseMarkers(race);
        race.StartRace("{\"mode\":\"review\",\"seed\":1911}");
        var flakes=race.transform.Find("Windblown snow").GetComponent<ParticleSystem>();
        foreach(int metre in new[]{284,16120}){
            race.Seek(metre);
            float fullRate=flakes.emission.rateOverTime.constant,fog=RenderSettings.fogEndDistance;
            race.SetLightweightGraphics(1);
            race.Seek(metre);
            float lightRate=flakes.emission.rateOverTime.constant;
            Need(fullRate>0 && Mathf.Approximately(lightRate,fullRate*.75f),"lightweight snowfall");
            Need(Mathf.Approximately(fog,RenderSettings.fogEndDistance),"graphics mode changed course visibility");
            race.SetLightweightGraphics(0);
            race.Seek(metre);
            Need(Mathf.Approximately(flakes.emission.rateOverTime.constant,fullRate),"standard snowfall not restored");
            Debug.Log($"GRAPHICS PASS at {metre} m: {fullRate:F1} / {lightRate:F1} flakes per second, visibility {fog:F1} m");
        }
        foreach(int lap in new[]{0,RaceWorld.LapLength}){
            foreach(int edge in new[]{16000,20000,24000,28000}){
                bool blizzard=edge<24000,entering=edge==16000||edge==24000;
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
        race.Seek(23985);race.RideFromReview();
        for(int i=0;i<50&&race.Status.metres<24005;i++)race.Advance(.05f);
        Need(race.Status.metres>=24005&&race.Status.phase=="play"&&RenderSettings.sun.intensity<.31f,
            "ordinary descent through the night arch");
        Debug.Log("WEATHER FRONTS PASS: storm and night fade across four boundaries on two laps; live night entry.");
    }
}
