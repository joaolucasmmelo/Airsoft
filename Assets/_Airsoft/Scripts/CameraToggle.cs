using UnityEngine;
using UnityEngine.InputSystem;

namespace Airsoft
{
    /// <summary>
    /// Alterna entre a câmera de primeira pessoa e uma câmera lateral fixa ("modo análise").
    ///
    /// Em primeira pessoa você olha AO LONGO da trajetória, o que esconde a curvatura.
    /// De lado, as três curvas do enunciado (pouco / ideal / muito hop-up) ficam
    /// óbvias. Útil principalmente para os prints do relatório.
    /// </summary>
    public class CameraToggle : MonoBehaviour
    {
        public Camera fpsCamera;
        public Camera sideCamera;
        public Key toggleKey = Key.V;

        public bool SideActive { get; private set; }

        void Start() => Apply();

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb[toggleKey].wasPressedThisFrame)
            {
                SideActive = !SideActive;
                Apply();
            }
        }

        void Apply()
        {
            if (fpsCamera != null) fpsCamera.enabled = !SideActive;
            if (sideCamera != null) sideCamera.enabled = SideActive;
        }
    }
}
