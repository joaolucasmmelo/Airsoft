using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Airsoft
{
    /// <summary>
    /// Controla a partida:
    ///  - atirar no START começa o jogo;
    ///  - cada fase mostra de 1 a 4 alvos e dá um tempo para acertar todos;
    ///  - acertou todos: próxima fase (menos tempo e alvos mais longe);
    ///  - o tempo acabou: fim de jogo e o START aparece de novo.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // Jeito simples de qualquer script achar o GameManager: GameManager.instance
        public static GameManager instance;

        [Header("Referências")]
        public StartButton botaoStart;
        public TextMeshPro textoPlacar;
        public ArmasDoJogador armasDoJogador;

        [Header("Dificuldade")]
        [Tooltip("Segundos por alvo na fase 1.")]
        public float tempoPorAlvo = 12f;
        [Tooltip("Quanto o tempo por alvo diminui a cada fase.")]
        public float reducaoPorFase = 0.5f;
        [Tooltip("O tempo por alvo nunca fica menor que isso.")]
        public float tempoPorAlvoMinimo = 5f;
        [Tooltip("Na fase 1 só aparecem alvos até esta distância.")]
        public float distanciaInicial = 30f;
        [Tooltip("Quantos metros a distância máxima aumenta por fase.")]
        public float distanciaPorFase = 10f;

        [Header("Drop de carregador")]
        [Range(0f, 1f)]
        [Tooltip("Chance de cair um carregador ao terminar uma fase (0.5 = 50%).")]
        public float chanceDeDrop = 0.5f;
        [Tooltip("Canto da área onde o carregador pode cair (atrás do parapeito).")]
        public Vector3 areaDoDropMin = new Vector3(-12f, 0f, -14f);
        public Vector3 areaDoDropMax = new Vector3(39f, 0f, 0.5f);

        Target[] todosOsAlvos;
        List<Target> alvosDaFase = new List<Target>();

        bool jogando = false;
        bool esperandoProximaFase = false;   // pausa curta para ver o último alvo brilhar
        int fase = 0;
        int pontos = 0;
        float tempoRestante = 0f;

        void Awake()
        {
            instance = this;
        }

        void Start()
        {
            // Pega todos os alvos da cena uma única vez
            todosOsAlvos = FindObjectsByType<Target>();

            EsconderTodosOsAlvos();
            botaoStart.gameObject.SetActive(true);
            textoPlacar.text = "ATIRE NO START\n(atrás de você)";
        }

        void Update()
        {
            if (!jogando || esperandoProximaFase)
                return;

            // Sem bala nas armas, nenhum carregador no chão e nenhuma BB voando: acabou
            if (armasDoJogador.TotalDeBalas() == 0 && CarregadorNoChao.todos.Count == 0 && BBProjectile.voando == 0)
            {
                FimDeJogo("SEM MUNIÇÃO");
                return;
            }

            tempoRestante -= Time.deltaTime;

            if (tempoRestante <= 0f)
            {
                tempoRestante = 0f;
                FimDeJogo("TEMPO ESGOTADO");
                return;
            }

            AtualizarPlacar();
        }

        public void ComecarPartida()
        {
            if (jogando)
                return;

            jogando = true;
            fase = 0;
            pontos = 0;
            botaoStart.gameObject.SetActive(false);

            CarregadorNoChao.ApagarTodos();
            armasDoJogador.PrepararInicioDePartida();

            ProximaFase();
        }

        void ProximaFase()
        {
            esperandoProximaFase = false;
            fase++;
            EsconderTodosOsAlvos();

            // Quantos alvos: na fase 1 só 1, depois sorteia até no máximo 4
            int maximo = Mathf.Min(fase, 4);
            int quantidade = Random.Range(1, maximo + 1);

            // Até que distância os alvos podem aparecer nesta fase
            float distanciaMaxima = distanciaInicial + (fase - 1) * distanciaPorFase;

            // Nunca sorteia alvo que nenhuma arma com munição consegue alcançar
            float alcance = armasDoJogador.AlcanceDisponivel();
            if (distanciaMaxima > alcance)
                distanciaMaxima = alcance;

            // Lista dos alvos que estão perto o suficiente
            List<Target> candidatos = new List<Target>();
            foreach (Target alvo in todosOsAlvos)
            {
                if (alvo.nominalDistance <= distanciaMaxima)
                    candidatos.Add(alvo);
            }

            // Sorteia os alvos da fase sem repetir
            for (int i = 0; i < quantidade; i++)
            {
                if (candidatos.Count == 0)
                    break;

                int sorteado = Random.Range(0, candidatos.Count);
                Target alvo = candidatos[sorteado];
                candidatos.RemoveAt(sorteado);

                alvo.Mostrar();
                alvosDaFase.Add(alvo);
            }

            // Nenhum alvo ao alcance = não tem mais munição para jogar
            if (alvosDaFase.Count == 0)
            {
                FimDeJogo("SEM MUNIÇÃO");
                return;
            }

            // Tempo da fase: tempo por alvo (que diminui a cada fase) vezes a quantidade
            float tempoDoAlvo = tempoPorAlvo - (fase - 1) * reducaoPorFase;
            if (tempoDoAlvo < tempoPorAlvoMinimo)
                tempoDoAlvo = tempoPorAlvoMinimo;

            tempoRestante = tempoDoAlvo * alvosDaFase.Count;

            Debug.Log("[Airsoft] Fase " + fase + ": " + alvosDaFase.Count + " alvo(s) até " +
                      distanciaMaxima + " m, " + tempoRestante.ToString("0.0") + " s");

            AtualizarPlacar();
        }

        /// <summary>Chamado pelo Target quando uma BB acerta ele.</summary>
        public void AlvoAcertado(Target alvo, int pontosDoAcerto)
        {
            // Ignora alvos que não fazem parte da fase atual
            if (!jogando || !alvosDaFase.Contains(alvo))
                return;

            pontos += pontosDoAcerto;
            alvosDaFase.Remove(alvo);   // o próprio alvo se esconde depois de brilhar
            AtualizarPlacar();

            // Acertou todos: espera meio segundo (o tempo do brilho) e vai para a próxima fase
            if (alvosDaFase.Count == 0)
            {
                TentarDropDeCarregador();
                esperandoProximaFase = true;
                Invoke(nameof(ProximaFase), 0.5f);
            }
        }

        void TentarDropDeCarregador()
        {
            if (Random.value > chanceDeDrop)
                return;

            // Prefere uma arma que está sem bala; se todas têm, sorteia qualquer uma
            List<AirsoftWeapon> semBala = new List<AirsoftWeapon>();
            foreach (AirsoftWeapon arma in armasDoJogador.armas)
            {
                if (arma.carregador == null || !arma.carregador.TemBala())
                    semBala.Add(arma);
            }

            AirsoftWeapon escolhida;
            if (semBala.Count > 0)
                escolhida = semBala[Random.Range(0, semBala.Count)];
            else
                escolhida = armasDoJogador.armas[Random.Range(0, armasDoJogador.armas.Length)];

            // Posição aleatória dentro da área do jogador
            Vector3 posicao = new Vector3(
                Random.Range(areaDoDropMin.x, areaDoDropMax.x),
                0f,
                Random.Range(areaDoDropMin.z, areaDoDropMax.z));

            CarregadorNoChao.Criar(escolhida.NovoCarregadorCheio(), posicao);
            AirsoftHUD.Mensagem("Caiu um carregador de " + escolhida.modelo + "!");
        }

        void FimDeJogo(string motivo)
        {
            jogando = false;
            CancelInvoke(nameof(ProximaFase));
            EsconderTodosOsAlvos();
            CarregadorNoChao.ApagarTodos();

            // Volta ao treino: todas as armas com munição infinita até o próximo START
            armasDoJogador.ModoTreino();
            botaoStart.gameObject.SetActive(true);

            textoPlacar.text = motivo + "\nFase " + fase + "   " + pontos + " pontos\nATIRE NO START";
            Debug.Log("[Airsoft] " + motivo + " na fase " + fase + " com " + pontos + " pontos");
        }

        void EsconderTodosOsAlvos()
        {
            foreach (Target alvo in todosOsAlvos)
                alvo.gameObject.SetActive(false);

            alvosDaFase.Clear();
        }

        void AtualizarPlacar()
        {
            textoPlacar.text = "FASE " + fase + "\n" +
                               tempoRestante.ToString("0.0") + " s\n" +
                               pontos + " PONTOS";
        }
    }
}
