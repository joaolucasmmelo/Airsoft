using UnityEngine;

namespace Airsoft
{
    /// <summary>Dados de um acerto em alvo, publicados para a HUD.</summary>
    public struct TargetHit
    {
        public string label;
        public float nominalDistance;
        public float offsetFromCenter;   // distância ao centro da placa, em metros
        public string ring;
        public int points;
    }

    /// <summary>
    /// Alvo de estande com anéis concêntricos.
    ///
    /// A detecção do acerto NÃO fica aqui: quem chama é a própria BB, no
    /// <see cref="BBProjectile"/>, no instante da colisão. Fazer assim garante
    /// ordem determinística (a BB já tem o ponto de contato em mãos) e evita
    /// depender de qual OnCollisionEnter o Unity resolve primeiro.
    /// </summary>
    public class Target : MonoBehaviour
    {
        [Tooltip("Distância nominal do alvo em metros — só para o rótulo na HUD.")]
        public float nominalDistance = 10f;

        [Tooltip("Raio da placa em metros.")]
        public float plateRadius = 0.5f;

        public float flashDuration = 0.45f;
        public Color flashColor = new Color(1f, 0.85f, 0.3f);

        public static event System.Action<TargetHit> Hit;
        public static int TotalHits { get; private set; }
        public static int TotalPoints { get; private set; }

        public static void ResetScore()
        {
            TotalHits = 0;
            TotalPoints = 0;
        }

        Renderer[] parts;
        MaterialPropertyBlock mpb;
        float flashStart = -999f;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            parts = GetComponentsInChildren<Renderer>();
            mpb = new MaterialPropertyBlock();
        }

        /// <summary>Chamado pela BB no impacto. Devolve em qual anel ela entrou.</summary>
        public TargetHit RegisterHit(Vector3 worldPoint)
        {
            // O alvo é montado com o +Z local apontando para o atirador e a placa
            // no plano XY local, então a distância ao centro é só a norma de (x, y).
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            float offset = new Vector2(local.x, local.y).magnitude;
            float f = plateRadius > 0f ? offset / plateRadius : 1f;

            string ring;
            int points;
            if (f <= 0.22f) { ring = "MOSCA!"; points = 10; }
            else if (f <= 0.55f) { ring = "anel interno"; points = 5; }
            else { ring = "anel externo"; points = 2; }

            TotalHits++;
            TotalPoints += points;
            flashStart = Time.time;

            TargetHit hit = new TargetHit
            {
                label = $"alvo de {nominalDistance:0} m",
                nominalDistance = nominalDistance,
                offsetFromCenter = offset,
                ring = ring,
                points = points
            };

            Hit?.Invoke(hit);
            return hit;
        }

        void Update()
        {
            float age = Time.time - flashStart;
            if (age > flashDuration + 0.1f) return;   // nada aceso, nada a fazer

            // Termina em 0 (preto) e só então para de escrever, para a placa
            // não ficar acesa para sempre.
            float t = Mathf.Clamp01(1f - age / flashDuration);
            Color emission = flashColor * (t * t * 4f);

            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].GetPropertyBlock(mpb);
                mpb.SetColor(EmissionId, emission);
                parts[i].SetPropertyBlock(mpb);
            }
        }
    }
}
