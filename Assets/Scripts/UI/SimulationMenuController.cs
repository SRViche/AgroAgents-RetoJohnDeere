using AgroAgents.Presentation.Authoring;
using UnityEditor.TerrainTools;
using UnityEngine;

namespace AgroAgents.Presentation
{
    public class SimulationMenuController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private WorldBootstrapper worldBootstrapper;
        [SerializeField] private VRGridBrush vRGridBrush;
        [SerializeField] private ParticleSystem particleRain;

        private void Start()
        {
            particleRain.Stop();
        }
        public void OnClickStartWithSeed()
        {
            if (worldBootstrapper != null)
            {
                Debug.Log("Simulación por semilla");
                worldBootstrapper.TriggerSimulationStart(WorldSource.Generated,null);
            }
        }

        public void OnClickStartWithAuthoredGrid()
        {
            if(vRGridBrush != null && worldBootstrapper != null)
            {
                vRGridBrush.ExportAndSaveGrid();

                worldBootstrapper.TriggerSimulationStart(WorldSource.AuthoredText,null);
            }
        }

        public void OnClickRain()
        {
            if (particleRain.isPlaying)
            {
                particleRain.Stop();
            }

            if (particleRain.isStopped)
            {
                particleRain.Play();
            }
        }
        
    
    }
}
