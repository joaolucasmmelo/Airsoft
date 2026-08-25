using UnityEngine;
using UnityEngine.InputSystem;

namespace Airsoft
{
    /// <summary>
    /// A arma. Responsável por:
    ///   - calcular a velocidade inicial da BB a partir da ENERGIA de saída (1.49 J);
    ///   - instanciar o prefab da BB na boca do cano a cada clique;
    ///   - traduzir a regulagem do hop-up (1% a 100%) na constante de backspin.
    ///
    /// A massa NÃO é digitada aqui: ela é lida do Rigidbody do próprio prefab da BB.
    /// Assim, trocar a BB de 0.20g para 0.25g no prefab recalcula a velocidade sozinho,
    /// que é exatamente o comportamento real (mesma mola, mesma energia, massas diferentes).
    /// </summary>
    public class AirsoftWeapon : MonoBehaviour
    {
        [Header("Referências")]
        [Tooltip("Transform na boca do cano. A BB nasce aqui e voa na direção do +Z dele.")]
        public Transform muzzle;
        public GameObject bbPrefab;

        [Header("Disparo")]
        [Tooltip("Energia cinética de saída, em Joules. Enunciado: 1.49 J.")]
        public float muzzleEnergyJoules = 1.49f;
        [Tooltip("Intervalo mínimo entre tiros, em segundos (segurar o botão dispara em rajada).")]
        public float fireInterval = 0.12f;

        [Header("Hop-up")]
        [Range(1f, 100f)]
        [Tooltip("Regulagem do hop-up. Ajuste em jogo com a RODA DO MOUSE.")]
        public float hopUpPercent = 55f;

        [Tooltip("BackspinDrag quando o hop-up está em 100%. Calibrado por simulação: " +
                 "0% -> ~35m (pouco hop) | ~55% -> ~63m plano (ideal) | 100% -> ~88m subindo muito.")]
        public float maxBackspinDrag = 0.00065f;

        [Tooltip("Velocidade angular do backspin a 100%, em rad/s (usada no modelo Completo). " +
                 "4500 rad/s = ~43.000 rpm.")]
        public float maxSpinRate = 4500f;

        public MagnusModel magnusModel = MagnusModel.Simplificado;

        [Header("Sensibilidade dos controles")]
        public float hopStepScroll = 5f;
        public float hopStepKey = 1f;

        [Header("Rastro")]
        [Tooltip("Ligado: o rastro muda de azul->verde->vermelho conforme o hop-up, " +
                 "como no diagrama do enunciado. Desligado: sempre azul neon.")]
        public bool colorByHopUp = false;
        public Color neonColor = new Color(0f, 0.55f, 1f, 1f);

        // ---------- valores derivados ----------

        /// <summary>Massa da BB, lida do prefab (kg).</summary>
        public float BbMassKg { get; private set; } = 0.0002f;

        /// <summary>
        /// v0 = sqrt(2E/m).  Com E = 1.49 J e m = 0.0002 kg  ->  122.07 m/s  =  400.5 fps.
        /// </summary>
        public float MuzzleSpeed => Mathf.Sqrt(2f * muzzleEnergyJoules / Mathf.Max(BbMassKg, 1e-9f));

        public float MuzzleSpeedFps => MuzzleSpeed / 0.3048f;

        /// <summary>Constante do modelo simplificado para a regulagem atual.</summary>
        public float BackspinDrag => maxBackspinDrag * (hopUpPercent / 100f);

        /// <summary>Velocidade angular do backspin para a regulagem atual (rad/s).</summary>
        public float SpinRate => maxSpinRate * (hopUpPercent / 100f);

        /// <summary>Ângulo de elevação do cano em graus. 0 = perfeitamente horizontal.</summary>
        public float ElevationDeg =>
            muzzle == null ? 0f : Mathf.Asin(Mathf.Clamp(muzzle.forward.y, -1f, 1f)) * Mathf.Rad2Deg;

        public int ShotCount { get; private set; }

        float nextFireTime;

        void Awake()
        {
            if (bbPrefab != null)
            {
                Rigidbody prefabRb = bbPrefab.GetComponent<Rigidbody>();
                if (prefabRb != null) BbMassKg = prefabRb.mass;
                else Debug.LogWarning("[Airsoft] O prefab da BB não tem Rigidbody!", this);
            }
        }

        void Start()
        {
            // Verificação pedida no enunciado: conferir a velocidade inicial pelo Console.
            Debug.Log(
                $"[Airsoft] Energia de saída = {muzzleEnergyJoules} J | massa da BB = {BbMassKg * 1000f:0.###} g\n" +
                $"          v0 = sqrt(2E/m) = sqrt(2 * {muzzleEnergyJoules} / {BbMassKg}) = " +
                $"{MuzzleSpeed:0.00} m/s = {MuzzleSpeedFps:0.0} fps   (alvo do enunciado: ~400 fps)");
        }

        void Update()
        {
            HandleHopUpInput();
            HandleFireInput();
        }

        void HandleHopUpInput()
        {
            // A roda do mouse regula o hop-up. Isso imita o dial da arma real e, mais
            // importante, funciona com o cursor travado (obrigatório em primeira pessoa).
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    SetHopUp(hopUpPercent + Mathf.Sign(scroll) * hopStepScroll);
            }

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame) SetHopUp(hopUpPercent + hopStepKey);
                if (kb.downArrowKey.wasPressedThisFrame) SetHopUp(hopUpPercent - hopStepKey);
                if (kb.mKey.wasPressedThisFrame) ToggleMagnusModel();
            }
        }

        void HandleFireInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;   // não atira com o mouse solto

            if (mouse.leftButton.isPressed && Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + fireInterval;
                Fire();
            }
        }

        public void SetHopUp(float percent)
        {
            hopUpPercent = Mathf.Clamp(percent, 1f, 100f);
        }

        public void ToggleMagnusModel()
        {
            magnusModel = magnusModel == MagnusModel.Simplificado
                ? MagnusModel.Completo
                : MagnusModel.Simplificado;
            Debug.Log($"[Airsoft] Modelo de Magnus: {magnusModel}");
        }

        public void Fire()
        {
            if (bbPrefab == null || muzzle == null)
            {
                Debug.LogError("[Airsoft] Arma sem prefab de BB ou sem Muzzle atribuído.", this);
                return;
            }

            GameObject go = Instantiate(bbPrefab, muzzle.position, muzzle.rotation,
                                        BBProjectile.Container);
            go.name = $"BB_{ShotCount:000}";

            BBProjectile bb = go.GetComponent<BBProjectile>();
            if (bb != null)
            {
                bb.magnusModel = magnusModel;
                bb.backspinDrag = BackspinDrag;
                bb.spinRateRadPerSec = SpinRate;
                bb.hopUpPercent = hopUpPercent;
                bb.elevationDeg = ElevationDeg;
                bb.Launch(muzzle.position, muzzle.forward, MuzzleSpeed);
            }

            TrailRenderer trail = go.GetComponentInChildren<TrailRenderer>();
            if (trail != null) ApplyTrailColor(trail);

            ShotCount++;

            if (ShotCount == 1)
            {
                Debug.Log($"[Airsoft] Primeiro disparo: v0 medida no Rigidbody = " +
                          $"{(bb != null ? bb.GetComponent<Rigidbody>().linearVelocity.magnitude : 0f):0.00} m/s");
            }
        }

        void ApplyTrailColor(TrailRenderer trail)
        {
            Color c = colorByHopUp ? HopUpColor(hopUpPercent) : neonColor;

            // O material do rastro é branco HDR (aditivo). A cor vem daqui, e o degradê
            // de alpha (opaco -> transparente) é o que produz o efeito de ghosting.
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.65f),
                    new GradientAlphaKey(0f, 1f)
                });
            trail.colorGradient = g;
        }

        /// <summary>Azul (pouco hop) -> verde (ideal) -> vermelho (muito hop), como no enunciado.</summary>
        public static Color HopUpColor(float percent)
        {
            Color pouco = new Color(0.15f, 0.45f, 1f);
            Color ideal = new Color(0.25f, 1f, 0.45f);
            Color muito = new Color(1f, 0.25f, 0.15f);
            return percent < 55f
                ? Color.Lerp(pouco, ideal, Mathf.InverseLerp(1f, 55f, percent))
                : Color.Lerp(ideal, muito, Mathf.InverseLerp(55f, 100f, percent));
        }
    }
}
