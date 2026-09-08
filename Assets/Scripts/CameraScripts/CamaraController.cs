using UnityEngine;
using UnityEngine.SceneManagement;

namespace AgroAgents.Presentation
{
    public class CamaraController : MonoBehaviour
    {
        [Header("Cámaras a pausar")]
        public Camera[] camaras;

        // 1. BOTON REINICIAR
        public void ReiniciarSimulacion()
        {
            Time.timeScale = 1f; // Restablecer el tiempo antes de recargar
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            Debug.Log("Simulación Reiniciada.");
        }

        // 2. BOTON PAUSAR
        public void PausarSimulacion()
        {
            Time.timeScale = 0f;

            if (camaras != null)
            {
                foreach (Camera cam in camaras)
                {
                    if (cam != null) cam.enabled = false;
                }
            }
            Debug.Log("Simulación Pausada.");
        }

        // 3. BOTON CONTINUAR
        public void ContinuarSimulacion()
        {
            Time.timeScale = 1f;

            if (camaras != null)
            {
                foreach (Camera cam in camaras)
                {
                    if (cam != null) cam.enabled = true;
                }
            }
            Debug.Log("Simulación Reanudada.");
        }
    }
}
