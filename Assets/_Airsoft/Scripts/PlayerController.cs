using UnityEngine;
using UnityEngine.InputSystem;

namespace Airsoft
{
    /// <summary>
    /// Controle de primeira pessoa.
    ///
    /// A rotação é dividida em dois níveis da hierarquia, que é o padrão em FPS:
    ///   - o Player  gira na horizontal (yaw)   -> gira o corpo inteiro, arma junto;
    ///   - a Câmera  gira na vertical  (pitch)  -> só a cabeça sobe e desce.
    /// Como a arma é filha da câmera, o cano aponta para onde você olha sem nenhuma
    /// linha de código extra: a hierarquia resolve.
    ///
    /// O movimento passa por um CharacterController para o jogador esbarrar nas paredes
    /// em vez de atravessar o mapa. A BB do próprio atirador ignora essa cápsula
    /// (ver <see cref="AirsoftWeapon.shooterBody"/>), senão o tiro morreria no peito de
    /// quem puxou o gatilho ao mirar para baixo.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Referências")]
        [Tooltip("A câmera. Recebe a rotação vertical (pitch).")]
        public Transform cameraPivot;

        [Header("Mouse look")]
        public float mouseSensitivity = 0.08f;
        public float minPitch = -85f;
        public float maxPitch = 85f;

        [Header("Movimento")]
        public float walkSpeed = 5f;
        public float runSpeed = 10f;
        [Tooltip("Altura dos olhos, em metros.")]
        public float eyeHeight = 1.6f;

        [Header("Corpo")]
        [Tooltip("Raio da cápsula de colisão, em metros.")]
        public float bodyRadius = 0.35f;
        [Tooltip("Altura da cápsula de colisão, em metros.")]
        public float bodyHeight = 1.8f;

        CharacterController body;
        float fallSpeed;
        float yaw;
        float pitch;

        /// <summary>A cápsula do jogador, para a arma poder ignorá-la ao disparar.</summary>
        public CharacterController Body => body;

        /// <summary>Ângulo de elevação da mira em graus. Positivo = mirando para cima.</summary>
        public float ElevationDeg => -pitch;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            body.radius = bodyRadius;
            body.height = bodyHeight;
            body.center = new Vector3(0f, bodyHeight * 0.5f, 0f);
            body.slopeLimit = 50f;
            body.stepOffset = 0.3f;
        }

        void Start()
        {
            yaw = transform.eulerAngles.y;
            pitch = 0f;

            if (cameraPivot != null)
            {
                Vector3 p = cameraPivot.localPosition;
                cameraPivot.localPosition = new Vector3(p.x, eyeHeight, p.z);
            }

            SetCursorLocked(true);
        }

        void Update()
        {
            HandleCursor();
            HandleLook();
            HandleMove();
        }

        void HandleCursor()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);

            // Clicar na janela com o cursor solto volta a travar (e não dispara,
            // porque a arma ignora o clique enquanto o cursor não está travado).
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(true);
        }

        void HandleLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;

            // Alguns drivers devolvem lixo na primeira leitura do mouse. Sem esta
            // guarda um NaN contamina yaw/pitch para sempre (NaN sobrevive ao
            // Mathf.Clamp, porque toda comparação com NaN é falsa) e a câmera trava
            // com "Input rotation is { NaN, NaN, NaN, NaN }".
            if (float.IsNaN(delta.x) || float.IsNaN(delta.y) ||
                float.IsInfinity(delta.x) || float.IsInfinity(delta.y))
                return;

            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
            ApplyRotation();
        }

        void ApplyRotation()
        {
            if (float.IsNaN(yaw) || float.IsInfinity(yaw)) yaw = 0f;
            if (float.IsNaN(pitch) || float.IsInfinity(pitch)) pitch = 0f;

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void HandleMove()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            Vector3 input = Vector3.zero;
            if (kb.wKey.isPressed) input.z += 1f;
            if (kb.sKey.isPressed) input.z -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed) input.x -= 1f;

            if (input.sqrMagnitude > 1f) input.Normalize();

            float speed = kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            Vector3 move = transform.TransformDirection(input) * speed;

            // Uma velocidade de queda pequena e constante enquanto apoiado: sem componente
            // vertical o CharacterController nunca reporta isGrounded e o jogador passa a
            // "flutuar" na borda de qualquer desnível.
            fallSpeed = body.isGrounded ? -2f : fallSpeed - 9.81f * Time.deltaTime;
            move.y = fallSpeed;

            body.Move(move * Time.deltaTime);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
