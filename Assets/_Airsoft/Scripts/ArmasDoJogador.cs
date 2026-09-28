using UnityEngine;
using UnityEngine.InputSystem;

namespace Airsoft
{
    /// <summary>
    /// As armas que o jogador carrega.
    ///  - teclas 1, 2 e 3 trocam de arma;
    ///  - tecla E pega o carregador que estiver perto (só se servir na arma da mão).
    /// </summary>
    public class ArmasDoJogador : MonoBehaviour
    {
        [Tooltip("0 = pistola (tecla 1), 1 = AK (tecla 2), 2 = sniper (tecla 3)")]
        public AirsoftWeapon[] armas;

        [Tooltip("Distância máxima, em metros, para pegar um carregador com E.")]
        public float distanciaParaPegar = 2f;

        int indiceAtual = 0;

        public AirsoftWeapon ArmaAtual
        {
            get { return armas[indiceAtual]; }
        }

        void Start()
        {
            ModoTreino();
        }

        void Update()
        {
            Keyboard teclado = Keyboard.current;
            if (teclado == null)
                return;

            if (teclado.digit1Key.wasPressedThisFrame) Equipar(0);
            if (teclado.digit2Key.wasPressedThisFrame) Equipar(1);
            if (teclado.digit3Key.wasPressedThisFrame) Equipar(2);

            if (teclado.eKey.wasPressedThisFrame) TentarPegarCarregador();
        }

        public void Equipar(int indice)
        {
            if (indice >= armas.Length)
                return;

            indiceAtual = indice;

            // Só a arma escolhida fica ligada (as outras nem recebem comandos)
            for (int i = 0; i < armas.Length; i++)
                armas[i].gameObject.SetActive(i == indice);
        }

        /// <summary>Antes do START: todas as armas carregadas e com munição infinita.</summary>
        public void ModoTreino()
        {
            foreach (AirsoftWeapon arma in armas)
            {
                arma.carregador = arma.NovoCarregadorCheio();
                arma.municaoInfinita = true;
            }

            // Deixa só a arma da mão visível (no começo do jogo, a pistola)
            Equipar(indiceAtual);
        }

        /// <summary>Começo de partida: só a pistola tem carregador, cheio. Munição passa a acabar.</summary>
        public void PrepararInicioDePartida()
        {
            foreach (AirsoftWeapon arma in armas)
            {
                arma.carregador = null;
                arma.municaoInfinita = false;
            }

            armas[0].carregador = armas[0].NovoCarregadorCheio();
            Equipar(0);
        }

        void TentarPegarCarregador()
        {
            CarregadorNoChao noChao = CarregadorMaisPerto();
            if (noChao == null)
                return;

            AirsoftWeapon arma = ArmaAtual;

            // Tipo diferente: não pega e explica o motivo
            if (noChao.carregador.tipo != arma.tipoDeCarregador)
            {
                int tecla = IndiceDaArmaDoTipo(noChao.carregador.tipo) + 1;
                AirsoftHUD.Mensagem("Esse carregador não serve na " + arma.modelo +
                                    ". Troque para a arma " + tecla + ".");
                return;
            }

            // Tipo certo: troca o carregador
            Carregador antigo = arma.carregador;
            arma.carregador = noChao.carregador;
            Destroy(noChao.gameObject);

            // Se o antigo ainda tinha bala, larga ele no chão para pegar depois
            if (antigo != null && antigo.TemBala())
                CarregadorNoChao.Criar(antigo, transform.position + transform.forward * 1f);

            AirsoftHUD.Mensagem("Carregador trocado: " + arma.carregador.quantidade + " balas");
        }

        /// <summary>O carregador no chão mais perto do jogador, ou null se não houver nenhum perto.</summary>
        public CarregadorNoChao CarregadorMaisPerto()
        {
            CarregadorNoChao maisPerto = null;
            float menorDistancia = distanciaParaPegar;

            foreach (CarregadorNoChao c in CarregadorNoChao.todos)
            {
                Vector3 diferenca = c.transform.position - transform.position;
                diferenca.y = 0f;   // ignora a altura

                if (diferenca.magnitude < menorDistancia)
                {
                    menorDistancia = diferenca.magnitude;
                    maisPerto = c;
                }
            }
            return maisPerto;
        }

        /// <summary>Soma das balas de todas as armas.</summary>
        public int TotalDeBalas()
        {
            int total = 0;
            foreach (AirsoftWeapon arma in armas)
            {
                if (arma.carregador != null)
                    total += arma.carregador.quantidade;
            }
            return total;
        }

        /// <summary>
        /// Maior distância que o jogador consegue atingir com a munição que tem
        /// (nas armas e nos carregadores caídos no chão).
        /// </summary>
        public float AlcanceDisponivel()
        {
            float alcance = 0f;

            foreach (AirsoftWeapon arma in armas)
            {
                if (arma.carregador != null && arma.carregador.TemBala() && arma.alcance > alcance)
                    alcance = arma.alcance;
            }

            foreach (CarregadorNoChao c in CarregadorNoChao.todos)
            {
                AirsoftWeapon arma = armas[IndiceDaArmaDoTipo(c.carregador.tipo)];
                if (arma.alcance > alcance)
                    alcance = arma.alcance;
            }

            return alcance;
        }

        public int IndiceDaArmaDoTipo(TipoDeCarregador tipo)
        {
            for (int i = 0; i < armas.Length; i++)
            {
                if (armas[i].tipoDeCarregador == tipo)
                    return i;
            }
            return 0;
        }
    }
}
