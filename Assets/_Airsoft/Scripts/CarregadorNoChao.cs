using System.Collections.Generic;
using UnityEngine;

namespace Airsoft
{
    /// <summary>
    /// Carregador caído no chão, esperando o jogador apertar E perto dele.
    /// É só um cubo colorido girando: amarelo = pistola, azul = AK, vermelho = sniper.
    /// </summary>
    public class CarregadorNoChao : MonoBehaviour
    {
        // Lista com todos os carregadores no chão (assim ninguém precisa procurar na cena)
        public static List<CarregadorNoChao> todos = new List<CarregadorNoChao>();

        public Carregador carregador;

        void OnEnable()
        {
            todos.Add(this);
        }

        void OnDisable()
        {
            todos.Remove(this);
        }

        void Update()
        {
            // Gira devagar para chamar atenção
            transform.Rotate(0f, 90f * Time.deltaTime, 0f);
        }

        /// <summary>Cria um carregador no chão na posição indicada.</summary>
        public static CarregadorNoChao Criar(Carregador carregador, Vector3 posicao)
        {
            GameObject cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubo.name = "Carregador_" + carregador.tipo;
            Destroy(cubo.GetComponent<BoxCollider>());   // sem colisão: a BB não bate nele

            posicao.y = 0.4f;
            cubo.transform.position = posicao;
            cubo.transform.localScale = new Vector3(0.2f, 0.45f, 0.3f);
            cubo.GetComponent<Renderer>().material.color = CorDoTipo(carregador.tipo);

            CarregadorNoChao noChao = cubo.AddComponent<CarregadorNoChao>();
            noChao.carregador = carregador;
            return noChao;
        }

        /// <summary>Apaga todos os carregadores do chão.</summary>
        public static void ApagarTodos()
        {
            // Copia a lista porque Destroy tira itens dela
            foreach (CarregadorNoChao c in new List<CarregadorNoChao>(todos))
                Destroy(c.gameObject);
        }

        static Color CorDoTipo(TipoDeCarregador tipo)
        {
            if (tipo == TipoDeCarregador.PistolGlock) return Color.yellow;
            if (tipo == TipoDeCarregador.Rifle) return new Color(0.2f, 0.6f, 1f);
            if (tipo == TipoDeCarregador.Sniper) return Color.red;
            return Color.white;
        }
    }
}
