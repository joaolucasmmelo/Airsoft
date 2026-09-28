using UnityEngine;

namespace Airsoft
{
    /// <summary>
    /// Um carregador: tipo, capacidade, quantidade de BBs e massa das BBs.
    /// É um objeto independente — a arma só guarda uma referência para o carregador equipado.
    /// </summary>
    [System.Serializable]
    public class Carregador
    {
        public TipoDeCarregador tipo;
        public int capacidade;
        public int quantidade;

        [Tooltip("Massa de cada BB em kg (0.0002 = 0,20 g). Em gramas só na tela.")]
        public float massaDaBBKg;

        public Carregador() { }

        public Carregador(TipoDeCarregador tipo, int capacidade, int quantidade, float massaDaBBKg)
        {
            this.tipo = tipo;
            this.capacidade = Mathf.Max(1, capacidade);                        // capacidade maior que zero
            this.quantidade = Mathf.Clamp(quantidade, 0, this.capacidade);     // entre zero e a capacidade
            this.massaDaBBKg = massaDaBBKg;
        }

        public bool TemBala()
        {
            return quantidade > 0;
        }

        /// <summary>Tira exatamente uma BB. Só é chamado quando o disparo acontece.</summary>
        public void GastarUmaBala()
        {
            if (quantidade > 0)
                quantidade--;
        }
    }
}
