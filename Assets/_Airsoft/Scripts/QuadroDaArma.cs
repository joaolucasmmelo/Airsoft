using TMPro;
using UnityEngine;

namespace Airsoft
{
    /// <summary>
    /// Quadro na parede do START que mostra os dados de uma arma.
    /// Atualiza o texto sozinho (munição muda a cada tiro).
    /// </summary>
    public class QuadroDaArma : MonoBehaviour
    {
        public AirsoftWeapon arma;
        [Tooltip("1 = pistola, 2 = AK, 3 = sniper")]
        public int tecla = 1;
        public TextMeshPro texto;

        void Start()
        {
            // Se alguma referência ficou vazia (por exemplo, as armas foram montadas de novo), acha sozinho
            if (texto == null)
                texto = GetComponentInChildren<TextMeshPro>();

            if (arma == null)
            {
                ArmasDoJogador armas = FindAnyObjectByType<ArmasDoJogador>();
                if (armas != null && tecla >= 1 && tecla <= armas.armas.Length)
                    arma = armas.armas[tecla - 1];
            }

            if (arma == null || texto == null)
                Debug.LogWarning("[Airsoft] " + name + ": sem arma ou sem texto ligado.", this);
        }

        void Update()
        {
            if (arma == null || texto == null)
                return;

            string municao;
            if (arma.carregador == null)
                municao = "sem carregador";
            else if (arma.municaoInfinita)
                municao = "infinita (treino)";
            else
                municao = arma.carregador.quantidade + " / " + arma.carregador.capacidade;

            texto.text =
                "<b>" + arma.modelo.ToUpper() + "</b>   (tecla " + tecla + ")\n" +
                "Munição: " + municao + "\n" +
                "Capacidade do carregador: " + arma.capacidadeDoCarregador + "\n" +
                "BB: " + (arma.BbMassKg * 1000f).ToString("0.00") + " g\n" +
                "Potência da mola: " + arma.muzzleEnergyJoules.ToString("0.00") + " J\n" +
                "Velocidade da BB: " + arma.MuzzleSpeed.ToString("0") + " m/s\n" +
                "Cadência: " + arma.ROF.ToString("0.0") + " BB/s\n" +
                "Modo: " + (arma.automatico ? "AUTO" : "SEMI") + "\n" +
                "Alcance: ~" + arma.alcance.ToString("0") + " m";
        }
    }
}
