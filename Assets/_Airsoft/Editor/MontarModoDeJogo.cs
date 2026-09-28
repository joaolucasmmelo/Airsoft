using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Airsoft
{
    /// <summary>
    /// Menu Airsoft → Montar modo de jogo.
    /// Cria na cena o GameManager, o placar na parede do fundo e o botão START
    /// na parede de trás. Pode rodar de novo: apaga os antigos e cria outra vez.
    /// Não mexe em paredes nem em alvos.
    /// </summary>
    public static class MontarModoDeJogo
    {
        [MenuItem("Airsoft/Montar modo de jogo", false, 1)]
        public static void Montar()
        {
            // Apaga o que foi criado antes
            foreach (string nome in new[] { "GameManager", "Placar", "Botao_Start" })
            {
                GameObject antigo = GameObject.Find(nome);
                if (antigo != null) Object.DestroyImmediate(antigo);
            }

            // ---------- placar na parede do fundo (na frente do jogador) ----------
            GameObject placar = new GameObject("Placar");
            placar.transform.position = new Vector3(13.6f, 6f, 131.8f);

            TextMeshPro textoPlacar = placar.AddComponent<TextMeshPro>();
            textoPlacar.text = "ATIRE NO START";
            textoPlacar.alignment = TextAlignmentOptions.Center;
            textoPlacar.color = Color.white;
            textoPlacar.enableAutoSizing = true;
            textoPlacar.fontSizeMin = 10;
            textoPlacar.fontSizeMax = 40;
            textoPlacar.rectTransform.sizeDelta = new Vector2(50f, 11f);

            // ---------- botão START na parede de trás ----------
            GameObject botao = new GameObject("Botao_Start");
            botao.transform.position = new Vector3(0f, 2.5f, -15.8f);

            BoxCollider colisor = botao.AddComponent<BoxCollider>();
            colisor.size = new Vector3(5f, 2f, 0.3f);
            StartButton startButton = botao.AddComponent<StartButton>();

            // Placa vermelha (só visual, sem collider)
            GameObject placa = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placa.name = "Placa";
            Object.DestroyImmediate(placa.GetComponent<BoxCollider>());
            placa.transform.SetParent(botao.transform, false);
            placa.transform.localScale = new Vector3(5f, 2f, 0.3f);
            Material vermelho = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Airsoft/Materials/M_AlvoVermelho.mat");
            if (vermelho != null) placa.GetComponent<MeshRenderer>().sharedMaterial = vermelho;

            // Texto START virado para o jogador (que olha para trás, -Z)
            GameObject texto = new GameObject("Texto");
            texto.transform.SetParent(botao.transform, false);
            texto.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            texto.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            TextMeshPro textoStart = texto.AddComponent<TextMeshPro>();
            textoStart.text = "START";
            textoStart.alignment = TextAlignmentOptions.Center;
            textoStart.color = Color.white;
            textoStart.enableAutoSizing = true;
            textoStart.fontSizeMin = 4;
            textoStart.fontSizeMax = 20;
            textoStart.rectTransform.sizeDelta = new Vector2(4.6f, 1.8f);

            // ---------- GameManager ----------
            GameObject gm = new GameObject("GameManager");
            GameManager gameManager = gm.AddComponent<GameManager>();
            gameManager.botaoStart = startButton;
            gameManager.textoPlacar = textoPlacar;
            gameManager.armasDoJogador = Object.FindAnyObjectByType<ArmasDoJogador>();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = gm;
            Debug.Log("[Airsoft] Modo de jogo montado. Salve a cena (Ctrl+S) e aperte Play.");
        }
    }
}
