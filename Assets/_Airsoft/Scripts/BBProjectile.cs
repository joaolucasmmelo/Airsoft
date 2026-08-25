using UnityEngine;

namespace Airsoft
{
    /// <summary>Qual formulação do efeito Magnus a BB usa.</summary>
    public enum MagnusModel
    {
        /// <summary>F = sqrt(v) * BackspinDrag  (a fórmula simplificada pedida no enunciado)</summary>
        Simplificado = 0,
        /// <summary>F = 1/2 * rho * A * Cl * v^2, com Cl proporcional à razão de spin (versão completa)</summary>
        Completo = 1
    }

    /// <summary>Resultado de um disparo, publicado quando a BB toca o solo.</summary>
    public struct ShotResult
    {
        public float hopUpPercent;
        public float backspinDrag;
        public float spinRate;
        public MagnusModel model;
        public float muzzleSpeed;
        public float massKg;
        public float distance;     // distância HORIZONTAL do cano até o impacto
        public float flightTime;
        public float apexHeight;
        public float impactSpeed;
        public float elevationDeg;
        public Vector3 impactPoint;
        public bool landed;        // false = expirou o tempo de vida sem tocar o solo
        public bool hitTarget;     // true = acertou um alvo, não o solo
        public string hitLabel;    // "solo", "parede" ou "alvo de 30 m"
        public string ring;        // anel acertado, quando foi alvo
        public int points;
    }

    /// <summary>
    /// Física de uma BB de airsoft de 6mm.
    ///
    /// Duas forças são somadas à gravidade a cada passo de física:
    ///
    ///   1) ARRASTO (quadrático, o realista para uma esfera):
    ///        F = 1/2 * rho * Cd * A * v^2      , na direção OPOSTA ao movimento
    ///        rho = 1.225 kg/m3 (ar ao nível do mar)
    ///        Cd  = 0.47 (esfera lisa)
    ///        A   = pi * r^2 = 2.827e-5 m2 para r = 3mm
    ///      Na saída do cano isso vale ~0.121 N = ~62x o peso da BB.
    ///      Por isso o `Linear Damping` do Rigidbody fica em 0: o dele é LINEAR (F ~ v)
    ///      e substituí-lo por este é o que o enunciado chama de "o mais realista possível".
    ///
    ///   2) SUSTENTAÇÃO (efeito Magnus gerado pelo backspin do hop-up):
    ///        Simplificado: F = sqrt(v) * BackspinDrag
    ///        Completo:     F = 1/2 * rho * A * Cl * v^2 , com Cl = k * (w*r/v)
    ///                        = 1/2 * rho * A * k * w * r * v
    ///      Aplicada PERPENDICULARMENTE à velocidade (não simplesmente "para cima"):
    ///      é isso que faz a trajetória de muito hop-up curvar e depois estabilizar,
    ///      em vez de subir para sempre em linha reta.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BBProjectile : MonoBehaviour
    {
        /// <summary>Disparado quando a BB toca o solo (ou expira). A HUD escuta este evento.</summary>
        public static event System.Action<ShotResult> ShotResolved;

        [Header("Geometria da BB")]
        [Tooltip("Raio em metros. BB de airsoft tem 6mm de diâmetro -> raio 0.003 m.")]
        public float radius = 0.003f;

        [Header("Arrasto aerodinâmico")]
        [Tooltip("Densidade do ar em kg/m3. Nível do mar, 15 graus C = 1.225")]
        public float airDensity = 1.225f;
        [Tooltip("Coeficiente de arrasto. Esfera lisa = 0.47")]
        public float dragCoefficient = 0.47f;
        [Tooltip("Desligue para comparar a trajetória COM e SEM arrasto (útil no relatório).")]
        public bool enableDrag = true;

        [Header("Hop-up / Efeito Magnus")]
        public MagnusModel magnusModel = MagnusModel.Simplificado;

        [Tooltip("Constante do modelo simplificado. Representa quanto de hop-up foi aplicado. " +
                 "Definida pela arma no momento do disparo.")]
        public float backspinDrag = 0f;

        [Tooltip("Velocidade angular do backspin em rad/s (usada só no modelo Completo).")]
        public float spinRateRadPerSec = 0f;

        [Tooltip("Coeficiente de sustentação por unidade de razão de spin (modelo Completo).")]
        public float magnusLiftFactor = 0.25f;

        [Tooltip("Meia-vida do backspin em segundos: o giro perde metade da força a cada X s. " +
                 "0 = giro constante durante todo o voo.")]
        public float spinHalfLife = 0f;

        [Header("Ciclo de vida")]
        [Tooltip("Se não tocar o solo nesse tempo, a BB desiste e reporta 'não aterrissou'.")]
        public float maxLifetime = 25f;
        public GameObject impactMarkerPrefab;

        // --- preenchidos pela arma no disparo, só para o relatório ---
        [HideInInspector] public float hopUpPercent;
        [HideInInspector] public float elevationDeg;

        static Transform container;

        /// <summary>
        /// Pai comum de tudo que é criado durante os disparos (BBs, rastros soltos e
        /// marcadores), para a Hierarchy não virar uma lista infinita em rajada.
        /// </summary>
        public static Transform Container
        {
            get
            {
                if (container == null)
                {
                    GameObject go = GameObject.Find("Disparos");
                    if (go == null) go = new GameObject("Disparos");
                    container = go.transform;
                }
                return container;
            }
        }

        Rigidbody rb;
        Vector3 launchPoint;
        float crossSection;
        float bornTime;
        float apex;
        float muzzleSpeed;
        bool resolved;

        /// <summary>Área da secção transversal da esfera (pi * r^2), em m2.</summary>
        public float CrossSection => crossSection;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();

            // Garantias: mesmo que alguém mexa no prefab, a física continua correta.
            rb.useGravity = true;
            rb.linearDamping = 0f;   // o arrasto é NOSSO, quadrático (ver FixedUpdate)
            rb.angularDamping = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            crossSection = Mathf.PI * radius * radius;
            bornTime = Time.time;
        }

        /// <summary>Coloca a BB no cano e dá a ela a velocidade inicial.</summary>
        public void Launch(Vector3 position, Vector3 direction, float speed)
        {
            if (rb == null) rb = GetComponent<Rigidbody>();

            transform.position = position;
            launchPoint = position;
            muzzleSpeed = speed;
            apex = position.y;
            bornTime = Time.time;
            resolved = false;

            rb.position = position;
            rb.linearVelocity = direction.normalized * speed;
            rb.angularVelocity = Vector3.zero;
        }

        void FixedUpdate()
        {
            if (resolved) return;

            Vector3 v = rb.linearVelocity;
            float speed = v.magnitude;

            if (transform.position.y > apex) apex = transform.position.y;

            if (speed > 0.001f)
            {
                Vector3 dir = v / speed;   // já normalizado, evita uma raiz quadrada

                // ---- 1) ARRASTO: F = 1/2 * rho * Cd * A * v^2, oposto ao movimento ----
                if (enableDrag)
                {
                    float drag = 0.5f * airDensity * dragCoefficient * crossSection * speed * speed;
                    rb.AddForce(-dir * drag, ForceMode.Force);
                }

                // ---- 2) SUSTENTAÇÃO (Magnus), perpendicular à velocidade ----
                float lift = CurrentLift(speed);
                if (!Mathf.Approximately(lift, 0f))
                    rb.AddForce(LiftDirection(dir) * lift, ForceMode.Force);
            }

            if (Time.time - bornTime >= maxLifetime)
                Resolve(transform.position, false);
        }

        /// <summary>
        /// Direção da força de sustentação: perpendicular à velocidade, no plano vertical.
        ///
        /// O backspin gira em torno de um eixo horizontal transversal ao movimento.
        /// Para uma BB indo em +Z:  eixo = cross(+Z, +Y) = -X
        ///                          F    = cross(-X, +Z) = +Y  (para cima). Correto.
        ///
        /// Como a conta usa a velocidade ATUAL, a força acompanha a curva sozinha:
        /// se a BB sobe, a sustentação inclina junto e para de empurrar direto para cima.
        /// </summary>
        public static Vector3 LiftDirection(Vector3 velocityDir)
        {
            Vector3 spinAxis = Vector3.Cross(velocityDir, Vector3.up);

            // Caso degenerado: tiro exatamente na vertical. Escolhe um eixo qualquer válido.
            if (spinAxis.sqrMagnitude < 1e-10f)
                spinAxis = Vector3.Cross(velocityDir, Vector3.forward);

            return Vector3.Cross(spinAxis.normalized, velocityDir).normalized;
        }

        /// <summary>Módulo da força de sustentação, em Newtons, para a velocidade atual.</summary>
        public float CurrentLift(float speed)
        {
            float decay = 1f;
            if (spinHalfLife > 0f)
                decay = Mathf.Pow(0.5f, (Time.time - bornTime) / spinHalfLife);

            if (magnusModel == MagnusModel.Simplificado)
            {
                // F = sqrt(v) * BackspinDrag
                return Mathf.Sqrt(speed) * backspinDrag * decay;
            }

            // F = 1/2 * rho * A * Cl * v^2   com   Cl = k * S   e   S = w*r/v
            //  => F = 1/2 * rho * A * k * w * r * v
            float spin = spinRateRadPerSec * decay;
            return 0.5f * airDensity * crossSection * magnusLiftFactor * spin * radius * speed;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (resolved) return;
            Vector3 p = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;

            // GetComponentInParent porque o collider fica na raiz do alvo, mas a BB
            // pode encostar em qualquer parte da hierarquia dele.
            Target target = collision.collider != null
                ? collision.collider.GetComponentInParent<Target>()
                : null;

            Resolve(p, true, target, collision.gameObject.name);
        }

        void Resolve(Vector3 point, bool landed, Target target = null, string surface = null)
        {
            if (resolved) return;
            resolved = true;

            // Distância HORIZONTAL: o enunciado pede a distância no solo em relação
            // ao ponto de disparo, então o componente vertical é descartado.
            Vector3 flat = point - launchPoint;
            flat.y = 0f;

            string label = surface != null && surface.StartsWith("Parede") ? "parede" : "solo";
            string ringHit = null;
            int pts = 0;

            if (target != null)
            {
                TargetHit th = target.RegisterHit(point);
                label = th.label;
                ringHit = th.ring;
                pts = th.points;
            }

            ShotResolved?.Invoke(new ShotResult
            {
                hopUpPercent = hopUpPercent,
                backspinDrag = backspinDrag,
                spinRate = spinRateRadPerSec,
                model = magnusModel,
                muzzleSpeed = muzzleSpeed,
                massKg = rb != null ? rb.mass : 0f,
                distance = flat.magnitude,
                flightTime = Time.time - bornTime,
                apexHeight = apex,
                impactSpeed = rb != null ? rb.linearVelocity.magnitude : 0f,
                elevationDeg = elevationDeg,
                impactPoint = point,
                landed = landed,
                hitTarget = target != null,
                hitLabel = label,
                ring = ringHit,
                points = pts
            });

            if (landed && target == null && impactMarkerPrefab != null)
                Instantiate(impactMarkerPrefab, point + Vector3.up * 0.02f, Quaternion.identity, Container);

            // Solta o rastro do corpo da BB para ele terminar de dissolver sozinho no ar
            // (efeito ghosting). Sem isso o rastro morreria junto com a bala.
            TrailRenderer trail = GetComponentInChildren<TrailRenderer>();
            if (trail != null && trail.gameObject != gameObject)
            {
                trail.transform.SetParent(Container, true);
                trail.emitting = false;
                Destroy(trail.gameObject, trail.time + 0.5f);
            }

            Destroy(gameObject);
        }
    }
}
