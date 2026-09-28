using UnityEngine;
using UnityEngine.InputSystem;

namespace Airsoft
{
    /// <summary>
    /// Uma arma. Responsável por:
    ///   - disparar (SEMI ou AUTO, tecla F) respeitando a cadência do motor;
    ///   - pegar a massa e a munição do carregador equipado;
    ///   - calcular a velocidade da BB pela energia da mola: v = √(2E/m);
    ///   - aplicar o hop-up (roda do mouse) na BB por meio do BackspinDrag.
    /// </summary>
    public class AirsoftWeapon : MonoBehaviour
    {
        [Header("Referências")]
        [Tooltip("Transform na boca do cano. A BB nasce aqui e voa na direção do +Z dele.")]
        public Transform muzzle;
        public GameObject bbPrefab;

        [Tooltip("Corpo do jogador, para a BB não bater em quem atirou. Se ficar vazio, é procurado sozinho.")]
        public Collider shooterBody;

        [Header("Arma")]
        public string modelo = "AK47";
        [Tooltip("Só carregadores deste tipo servem nesta arma.")]
        public TipoDeCarregador tipoDeCarregador = TipoDeCarregador.Rifle;
        [Tooltip("Distância aproximada, em metros, que esta arma alcança. O GameManager usa para sortear alvos.")]
        public float alcance = 65f;

        [Header("Mola (modelo simplificado: o ajuste é a energia do disparo)")]
        [Tooltip("Energia do disparo em Joules.")]
        public float muzzleEnergyJoules = 1.49f;

        [Header("Motor e cadência")]
        [Tooltip("Rotação do motor em RPM.")]
        public float rpmDoMotor = 15000f;
        [Tooltip("Quantas rotações do motor são necessárias para um disparo.")]
        public float rotacoesPorDisparo = 30f;
        [Tooltip("Desligado = SEMI (um tiro por clique). Ligado = AUTO (atira segurando).")]
        public bool automatico = false;

        [Header("Carregador desta arma")]
        public int capacidadeDoCarregador = 25;
        [Tooltip("Massa da BB em kg (0.0002 = 0,20 g).")]
        public float massaDaBBKg = 0.0002f;

        [Header("Hop-up")]
        [Range(5f, 100f)]
        [Tooltip("Regulagem do hop-up. Ajuste em jogo com a RODA DO MOUSE.")]
        public float hopUpPercent = 55f;

        [Tooltip("BackspinDrag a 100% de hop-up (é o ω da fórmula, em rad/s). 4500 mantém a trajetória rasa.")]
        public float maxBackspinDrag = 4500f;

        [Header("Sensibilidade dos controles")]
        public float hopStepScroll = 5f;
        public float hopStepKey = 1f;

        [Header("Rastro")]
        public bool colorByHopUp = false;
        public Color neonColor = new Color(0f, 0.55f, 1f, 1f);

        // Carregador equipado agora (null = sem carregador). Quem coloca é o ArmasDoJogador.
        [System.NonSerialized] public Carregador carregador;

        // Ligado antes do START (treino): atira sem gastar bala.
        [System.NonSerialized] public bool municaoInfinita;

        // ---------- valores calculados ----------

        /// <summary>Cadência em BBs por segundo: ROF = RPM / (60 × N).</summary>
        public float ROF
        {
            get { return rpmDoMotor / (60f * rotacoesPorDisparo); }
        }

        /// <summary>Tempo mínimo entre dois tiros: intervalo = 1 / ROF.</summary>
        public float IntervaloEntreTiros
        {
            get { return 1f / ROF; }
        }

        /// <summary>Massa da BB em kg: vem do carregador equipado.</summary>
        public float BbMassKg
        {
            get { return carregador != null ? carregador.massaDaBBKg : massaDaBBKg; }
        }

        /// <summary>v0 = √(2E/m)</summary>
        public float MuzzleSpeed
        {
            get { return Mathf.Sqrt(2f * muzzleEnergyJoules / BbMassKg); }
        }

        public float MuzzleSpeedFps
        {
            get { return MuzzleSpeed / 0.3048f; }
        }

        /// <summary>BackspinDrag enviado para a BB com a regulagem atual.</summary>
        public float BackspinDrag
        {
            get { return maxBackspinDrag * (hopUpPercent / 100f); }
        }

        public float ElevationDeg
        {
            get { return muzzle == null ? 0f : Mathf.Asin(Mathf.Clamp(muzzle.forward.y, -1f, 1f)) * Mathf.Rad2Deg; }
        }

        float proximoTiro = 0f;

        void Awake()
        {
            if (shooterBody == null)
                shooterBody = GetComponentInParent<CharacterController>();
        }

        void Start()
        {
            // ROF visível no Console, como pede o enunciado
            Debug.Log("[Airsoft] " + modelo + ": ROF = " + rpmDoMotor + " / (60 × " + rotacoesPorDisparo + ") = " +
                      ROF.ToString("0.00") + " BB/s, intervalo = " + IntervaloEntreTiros.ToString("0.000") + " s" +
                      " | v0 = √(2 × " + muzzleEnergyJoules + " / " + massaDaBBKg + ") = " +
                      Mathf.Sqrt(2f * muzzleEnergyJoules / massaDaBBKg).ToString("0.0") + " m/s");
        }

        void Update()
        {
            ControlarHopUp();
            ControlarModoDeTiro();
            ControlarGatilho();
        }

        void ControlarHopUp()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    SetHopUp(hopUpPercent + Mathf.Sign(scroll) * hopStepScroll);
            }

            Keyboard teclado = Keyboard.current;
            if (teclado != null)
            {
                if (teclado.upArrowKey.wasPressedThisFrame) SetHopUp(hopUpPercent + hopStepKey);
                if (teclado.downArrowKey.wasPressedThisFrame) SetHopUp(hopUpPercent - hopStepKey);
            }
        }

        void ControlarModoDeTiro()
        {
            Keyboard teclado = Keyboard.current;
            if (teclado != null && teclado.fKey.wasPressedThisFrame)
            {
                // Só troca o modo: não recarrega e não mexe no tempo do próximo tiro
                automatico = !automatico;
                AirsoftHUD.Mensagem("Modo " + (automatico ? "AUTO" : "SEMI"));
            }
        }

        void ControlarGatilho()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;   // não atira com o mouse solto

            // SEMI: só no clique. AUTO: enquanto o botão estiver apertado.
            bool querAtirar;
            if (automatico)
                querAtirar = mouse.leftButton.isPressed;
            else
                querAtirar = mouse.leftButton.wasPressedThisFrame;

            // Nos dois modos o tiro espera o intervalo da cadência
            if (querAtirar && Time.time >= proximoTiro)
                Fire();
        }

        public void SetHopUp(float percent)
        {
            hopUpPercent = Mathf.Clamp(percent, 5f, 100f);
        }

        public void Fire()
        {
            // 1) tem carregador?
            if (carregador == null)
            {
                AirsoftHUD.Mensagem("SEM CARREGADOR");
                return;
            }

            // 2) tem munição? Sem bala, não cria BB.
            if (!municaoInfinita && !carregador.TemBala())
            {
                AirsoftHUD.Mensagem("SEM MUNIÇÃO");
                return;
            }

            proximoTiro = Time.time + IntervaloEntreTiros;

            // 3) cria a BB com a massa do carregador
            GameObject go = Instantiate(bbPrefab, muzzle.position, muzzle.rotation, BBProjectile.Container);
            go.GetComponent<Rigidbody>().mass = carregador.massaDaBBKg;

            Collider bbCol = go.GetComponent<Collider>();
            if (bbCol != null && shooterBody != null)
                Physics.IgnoreCollision(bbCol, shooterBody);

            // 4) velocidade pela energia atual: v = √(2E/m)
            BBProjectile bb = go.GetComponent<BBProjectile>();
            bb.backspinDrag = BackspinDrag;
            bb.hopUpPercent = hopUpPercent;
            bb.elevationDeg = ElevationDeg;
            bb.Launch(muzzle.position, muzzle.forward, MuzzleSpeed);

            TrailRenderer trail = go.GetComponentInChildren<TrailRenderer>();
            if (trail != null) ApplyTrailColor(trail);

            // 5) só agora, com o tiro feito, gasta uma bala (no treino não gasta)
            if (!municaoInfinita)
                carregador.GastarUmaBala();
        }

        /// <summary>Cria um carregador cheio do tipo desta arma.</summary>
        public Carregador NovoCarregadorCheio()
        {
            return new Carregador(tipoDeCarregador, capacidadeDoCarregador, capacidadeDoCarregador, massaDaBBKg);
        }

        void ApplyTrailColor(TrailRenderer trail)
        {
            Color c = colorByHopUp ? HopUpColor(hopUpPercent) : neonColor;

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

        /// <summary>Azul (pouco hop) -> verde (ideal) -> vermelho (muito hop).</summary>
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
