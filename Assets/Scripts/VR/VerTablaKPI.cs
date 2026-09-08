/* Codigo hiper molon que permite que el gatillo de la mano izquierda despliegue
la tabla de KPIs molones que no tenemos creo */

using UnityEngine;

namespace AgroAgents.Presentation
{
    public class VerTablaKPI : MonoBehaviour
    {
        [Header("Referencias")]
        // Animator de la mano izquierda 
        public Animator handAnimator;

        // Canva con los KPIs
        public GameObject tablaKPIs;

        // Para que sea el gatillo con el que se pueda abrir la tabla
        [Header("Configuración")]
        public string nombreParametroAnimator = "Trigger";

        // Sensibilidad del gatillo para que se pueda ver la tabla
        [Range(0.1f, 1f)]
        public float sensibilidad = 0.5f;

        private void Start()
        {
            // Oculta la tabla al iniciar
            if (tablaKPIs != null) tablaKPIs.SetActive(false);
        }

        private void Update()
        {
            if (handAnimator == null || tablaKPIs == null) return;

            // Aqui se lee la animación 
            float presionGatillo = handAnimator.GetFloat(nombreParametroAnimator);

            // Gatillo presionado mucho o lo suficiente : SE VE LA TABLA SIII
            bool debeMostrarTabla = presionGatillo >= sensibilidad;

            if (tablaKPIs.activeSelf != debeMostrarTabla)
            {
                tablaKPIs.SetActive(debeMostrarTabla);
            }
        }
    }
}
