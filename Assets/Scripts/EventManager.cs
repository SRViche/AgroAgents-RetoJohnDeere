using UnityEngine;

namespace AgroAgents.Presentation
{

    public class EventManager : MonoBehaviour
    {
        [SerializeField] private ParticleSystem rainParticle;


        void Start()
        {
            rainParticle.Stop();
        }
        public void controlRain()
        {
            if (rainParticle != null)
            {
                if (rainParticle.isPlaying)
                {
                    rainParticle.Stop();
                }
                if (rainParticle.isStopped)
                {
                    rainParticle.Play();
                }
            }
        }
    }
}
