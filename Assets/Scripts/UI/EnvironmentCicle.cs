using UnityEngine;

namespace AgroAgents.Presentation
{
    public class EnvironmentCicle : MonoBehaviour
    {
        public enum DayTime{Morning, Night, Noon, Sunset, Cloudy, Sunrise}

        [SerializeField] Material[] skyboxMaterials;
        DayTime dayTime=DayTime.Morning;
        public void EnvironmentChange()
        {
            switch (dayTime)
            {
                case DayTime.Morning:
                    RenderSettings.skybox = skyboxMaterials[0];
                    RenderSettings.fog=true;
                    RenderSettings.fogColor=new Color(0.4392157f, 0.5019608f, 0.5647059f, 1f);
                    RenderSettings.fogDensity=0.00037f;
                    RenderSettings.fogMode=FogMode.Exponential;
                    RenderSettings.ambientSkyColor=new Color(0.18667853f, 0.53380954f,0.5924529f, 1f);
                    RenderSettings.ambientEquatorColor=new Color(0.12634388f, 0.5671282f, 0.735849f, 1f);
                    RenderSettings.ambientGroundColor=new Color(0.07818743f, 0.045186214f, 0.009134057f,1f);
                    RenderSettings.ambientIntensity=0.73f;
                    RenderSettings.subtractiveShadowColor=new Color(0.2509804f, 0.0f, 0.011764707f,1f);                    
                    break;
                case DayTime.Night:
                    RenderSettings.skybox = skyboxMaterials[1];
                    RenderSettings.fog=true;
                    RenderSettings.fogColor=new Color(0.2676753f, 0.269613f, 0.2754717f, 1f);
                    RenderSettings.fogDensity=0.00037f;
                    RenderSettings.fogMode=FogMode.Exponential;
                    RenderSettings.ambientSkyColor=new Color(0.0074990317f, 0.51269468f,0.23074007f, 1f);
                    RenderSettings.ambientEquatorColor=new Color(0.0018211621f, 0.051269468f, 0.18447503f, 1f);
                    RenderSettings.ambientGroundColor=new Color(0.06124607f, 0.014443844f, 0.0047769533f,1f);
                    RenderSettings.ambientIntensity=0.51f;
                    RenderSettings.subtractiveShadowColor=new Color(0.0f, 0.0f, 0.0f,1f); 
                    break;
                case DayTime.Noon:
                    RenderSettings.skybox = skyboxMaterials[2];
                    RenderSettings.fog=true;
                    RenderSettings.fogColor=new Color(0.41205403f, 0.68834454f, 0.7207546f, 1f);
                    RenderSettings.fogDensity=0.00037f;
                    RenderSettings.fogMode=FogMode.Exponential;
                    RenderSettings.ambientSkyColor=new Color(1f, 0.31764707f,0.0f, 1f);
                    RenderSettings.ambientEquatorColor=new Color(0.6862745f, 0.9333334f, 0.9333334f, 1f);
                    RenderSettings.ambientGroundColor=new Color(0.07818743f, 0.045186214f, 0.009134057f,1f);
                    RenderSettings.ambientIntensity=0.73f;
                    RenderSettings.subtractiveShadowColor=new Color(0.2509804f, 0.0f, 0.011764707f,1f); 
                    break;
                case DayTime.Sunset:
                    RenderSettings.skybox = skyboxMaterials[3];
                    RenderSettings.fog=true;
                    RenderSettings.fogColor=new Color(0.90196085f, 0.0f, 0.14509805f, 1f);
                    RenderSettings.fogDensity=0.00037f;
                    RenderSettings.fogMode=FogMode.Exponential;
                    RenderSettings.ambientSkyColor=new Color(0.49693304f, 0.03071345f,0.0047769533f, 1f);
                    RenderSettings.ambientEquatorColor=new Color(0.21586053f, 0.014443844f, 0.0047769533f, 1f);
                    RenderSettings.ambientGroundColor=new Color(0.06124607f, 0.014443844f, 0.0047769533f,1f);
                    RenderSettings.ambientIntensity=0.51f;
                    RenderSettings.subtractiveShadowColor=new Color(0.2509804f, 0.0f, 0.011764707f,1f); 
                    break;
                case DayTime.Cloudy:
                    RenderSettings.skybox = skyboxMaterials[4];
                    RenderSettings.fog=true;
                    RenderSettings.fogColor=new Color(0.5f, 0.5f, 0.5f, 1f);
                    RenderSettings.fogDensity=0.002f;
                    RenderSettings.fogMode=FogMode.Exponential;
                    RenderSettings.ambientSkyColor=new Color(0.07421358f, 0.10224175f,0.111932434f, 1f);
                    RenderSettings.ambientEquatorColor=new Color(0.17788847f, 0.30054384f, 0.35640025f, 1f);
                    RenderSettings.ambientGroundColor=new Color(0.16513224f, 0.16513224f, 0.18447503f,1f);
                    RenderSettings.ambientIntensity=1f;
                    RenderSettings.subtractiveShadowColor=new Color(0.22313282f, 0.2422177f, 0.2905661f,1f); 
                    break;
                case DayTime.Sunrise:
                    RenderSettings.skybox = skyboxMaterials[5];
                    RenderSettings.fog=true;
                    RenderSettings.fogColor=new Color(0.87169814f, 0.5265803f, 0.28124598f, 1f);
                    RenderSettings.fogDensity=0.00037f;
                    RenderSettings.fogMode=FogMode.Exponential;
                    RenderSettings.ambientSkyColor=new Color(0.0367177f, 0.037869584f,0.10188681f, 1f);
                    RenderSettings.ambientEquatorColor=new Color(1f, 0.7491f, 0.35640025f, 1f);
                    RenderSettings.ambientGroundColor=new Color(0.16513224f, 0.16513224f, 0.18447503f,1f);
                    RenderSettings.ambientIntensity=1f;
                    RenderSettings.subtractiveShadowColor=new Color(0.22313282f, 0.2422177f, 0.2905661f,1f);
                    break;

            }
        }
        public void SetDayTime()
        {
            dayTime = DayTime.Sunset;
            EnvironmentChange();
        }
        public void SetNightTime()
        {
            dayTime = DayTime.Night;
            EnvironmentChange();
        }
        public void SetNoonTime()
        {
            dayTime = DayTime.Noon;
            EnvironmentChange();
        }
        public void SetCloudyTime()
        {
            dayTime = DayTime.Cloudy;
            EnvironmentChange();
        }
        public void SetSunriseTime()
        {
            dayTime = DayTime.Sunrise;
            EnvironmentChange();
        }
        public void SetMorningTime()
        {
            dayTime = DayTime.Morning;
            EnvironmentChange();
        }
    }
}


