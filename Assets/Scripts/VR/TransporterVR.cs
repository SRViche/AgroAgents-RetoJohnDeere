using UnityEngine;

namespace AgroAgents.Presentation
{
    public class TransporterVR : MonoBehaviour
    {
        [SerializeField] private Transform puntoMapa;
        [SerializeField] private Transform puntoConstruir;
        [SerializeField] private Transform puntoOffice;
        public GameObject xrOrigin;    

        // Esta es la función que llamará tu botón
        public void ViajarAlPunto()
        {
            if (puntoMapa != null && xrOrigin != null)
            {
                xrOrigin.transform.position = puntoMapa.position;
                
                xrOrigin.transform.rotation = puntoMapa.rotation; 
            }
            else
            {
                Debug.LogWarning("Falta asignar el destino o el XR Origin en el inspector.");
            }
        }

        public void ViajarAConstruir()
        {
            if (puntoConstruir!= null && xrOrigin!=null)
            {
                xrOrigin.transform.position=puntoConstruir.position;
                xrOrigin.transform.rotation=puntoConstruir.rotation;
            }
            else
            {
                Debug.LogWarning("Falta asignar el destino o el XR Origin en el inspector.");
            }
        }

        public void ViajarAOficina()
        {
            if(puntoOffice!=null && xrOrigin != null)
            {
                xrOrigin.transform.position=puntoOffice.position;
                xrOrigin.transform.rotation=puntoOffice.rotation;
            }
            else
            {
                Debug.LogWarning("Falta asignar el destino o el XR Origin en el inspector.");
            }
        }
    }
}
