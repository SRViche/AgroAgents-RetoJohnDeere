using UnityEngine;
using UnityEngine.UI; 

public class SliderController : MonoBehaviour
{
    public Slider miSlider;
    public TMPro.TextMeshProUGUI textoValor; 

    void Start()
    {
        // Actualiza el texto al iniciar con el valor por defecto del slider
        ActualizarTexto(miSlider.value);

        // Escucha los cambios del slider en tiempo real
        miSlider.onValueChanged.AddListener(ActualizarTexto);
    }

    void ActualizarTexto(float valor)
    {
        // "F0" elimina los decimales. Usa "F1" o "F2" si quieres 1 o 2 decimales.
        textoValor.text = valor.ToString("F0"); 
    }
}