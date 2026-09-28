using UnityEngine;

namespace Airsoft
{
    /// <summary>
    /// Botão START na parede. Quando a BB acerta ele, a partida começa.
    /// </summary>
    public class StartButton : MonoBehaviour
    {
        public void Acertado()
        {
            if (GameManager.instance != null)
                GameManager.instance.ComecarPartida();
        }
    }
}
