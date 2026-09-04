using UnityEngine;

namespace AgroAgents.Presentation
{
    public class TransporterVR : MonoBehaviour
    {
        public Transform puntoDestino; 

        public GameObject xrOrigin;    

        // Esta es la función que llamará tu botón
        public void ViajarAlPunto()
        {
            if (puntoDestino != null && xrOrigin != null)
            {
                xrOrigin.transform.position = puntoDestino.position;
                
                xrOrigin.transform.rotation = puntoDestino.rotation; 
            }
            else
            {
                Debug.LogWarning("Falta asignar el destino o el XR Origin en el inspector.");
            }
        }
    }
}
