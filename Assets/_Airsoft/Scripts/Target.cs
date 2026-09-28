using UnityEngine;

namespace Airsoft
{
    /// <summary>
    /// Alvo do estande. Quem avisa o acerto é a BB (BBProjectile), no momento da colisão.
    /// O alvo calcula os pontos pelo anel atingido, brilha amarelo, avisa o GameManager
    /// e some quando o brilho acaba.
    /// </summary>
    public class Target : MonoBehaviour
    {
        [Tooltip("Distância do alvo em metros. O GameManager usa para escolher alvos mais longe nas fases altas.")]
        public float nominalDistance = 10f;

        [Tooltip("Raio da placa em metros.")]
        public float plateRadius = 0.5f;

        [Header("Brilho ao ser acertado")]
        public float duracaoDoBrilho = 0.4f;
        public Color corDoBrilho = new Color(1f, 0.85f, 0.3f);

        Renderer[] partes;
        MaterialPropertyBlock bloco;
        bool brilhando = false;
        float tempoDoBrilho = 0f;

        void Awake()
        {
            partes = GetComponentsInChildren<Renderer>();
            bloco = new MaterialPropertyBlock();
        }

        /// <summary>Chamado pelo GameManager para mostrar o alvo numa fase.</summary>
        public void Mostrar()
        {
            brilhando = false;
            PintarBrilho(Color.black);
            gameObject.SetActive(true);
        }

        /// <summary>Chamado pela BB quando ela bate no alvo. Devolve o nome do alvo para o HUD.</summary>
        public string RegisterHit(Vector3 pontoDoImpacto)
        {
            // Distância do impacto até o centro da placa
            Vector3 local = transform.InverseTransformPoint(pontoDoImpacto);
            float distanciaDoCentro = new Vector2(local.x, local.y).magnitude;

            // Pontos por anel: centro 10, meio 5, borda 2
            int pontos;
            if (distanciaDoCentro <= plateRadius * 0.22f)
                pontos = 10;
            else if (distanciaDoCentro <= plateRadius * 0.55f)
                pontos = 5;
            else
                pontos = 2;

            // Começa a brilhar
            brilhando = true;
            tempoDoBrilho = 0f;

            if (GameManager.instance != null)
                GameManager.instance.AlvoAcertado(this, pontos);

            return "alvo de " + nominalDistance.ToString("0") + " m";
        }

        void Update()
        {
            if (!brilhando)
                return;

            tempoDoBrilho += Time.deltaTime;
            float forca = 1f - tempoDoBrilho / duracaoDoBrilho;   // vai de 1 até 0

            if (forca <= 0f)
            {
                // Brilho acabou: apaga e esconde o alvo
                brilhando = false;
                PintarBrilho(Color.black);
                if (GameManager.instance != null)
                    gameObject.SetActive(false);
                return;
            }

            PintarBrilho(corDoBrilho * (forca * forca * 4f));
        }

        void PintarBrilho(Color cor)
        {
            foreach (Renderer parte in partes)
            {
                parte.GetPropertyBlock(bloco);
                bloco.SetColor("_EmissionColor", cor);
                parte.SetPropertyBlock(bloco);
            }
        }
    }
}
