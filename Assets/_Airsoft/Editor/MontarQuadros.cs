using AirsoftEditor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Airsoft
{
    /// <summary>
    /// Menu Airsoft → Montar quadros da parede.
    /// Cria na parede do START (atrás do jogador) um quadro de controles e um quadro
    /// para cada arma. Pode rodar de novo: apaga os antigos e cria outra vez.
    /// Rode depois de "Montar armas".
    /// </summary>
    public static class MontarQuadros
    {
        // A parede de trás fica em z = -16. Os quadros ficam colados nela, virados para o jogador.
        const float ZDaParede = -15.9f;

        [MenuItem("Airsoft/Montar quadros da parede", false, 3)]
        public static void Montar()
        {
            GameObject antigo = GameObject.Find("Quadros");
            if (antigo != null) Object.DestroyImmediate(antigo);

            ArmasDoJogador armas = Object.FindAnyObjectByType<ArmasDoJogador>();
            if (armas == null || armas.armas == null || armas.armas.Length < 3)
            {
                Debug.LogError("[Airsoft] Rode primeiro Airsoft → Montar armas.");
                return;
            }

            GameObject quadros = new GameObject("Quadros");
            Material fundo = MaterialDoFundo();

            // Um quadro por arma, em fila, com o modelo 3D da arma em cima.
            // Olhando para a parede, +X fica à ESQUERDA, então a pistola (1) fica em x = +7.
            float[] posicoesX = { 7f, 0f, -7f };
            string[] prefabs = { "FN_Five_Seven.prefab", "SM_Ak47.prefab", "SM_Barrett_M82A1.prefab" };
            float[] tamanhoDoModelo = { 1.6f, 3.8f, 4.6f };   // comprimento do modelo na parede, em metros

            for (int i = 0; i < 3; i++)
            {
                TextMeshPro texto = CriarQuadro(quadros.transform, "Quadro_" + armas.armas[i].modelo,
                    new Vector3(posicoesX[i], 6.2f, ZDaParede), new Vector2(5f, 3.2f), fundo);
                Transform quadroTransform = texto.transform.parent;

                QuadroDaArma quadro = quadroTransform.gameObject.AddComponent<QuadroDaArma>();
                quadro.arma = armas.armas[i];
                quadro.tecla = i + 1;
                quadro.texto = texto;
                texto.text = armas.armas[i].modelo;

                CriarModeloDaArma(quadroTransform, prefabs[i], tamanhoDoModelo[i]);
            }

            // Quadro de controles, à esquerda das armas (largo para cada comando caber numa linha)
            TextMeshPro controles = CriarQuadro(quadros.transform, "Quadro_Controles",
                new Vector3(15.5f, 5f, ZDaParede), new Vector2(8f, 6.5f), fundo);
            controles.text =
                "<b>CONTROLES</b>\n" +
                "Mouse: olhar\n" +
                "W A S D / Shift: andar / correr\n" +
                "Botão esquerdo: atirar\n" +
                "1  2  3: pistola / AK / sniper\n" +
                "F: modo SEMI / AUTO\n" +
                "E: pegar carregador\n" +
                "Roda do mouse: hop-up\n" +
                "Setas cima/baixo: hop-up fino\n" +
                "Esc: soltar o cursor\n\n" +
                "Atire no START para jogar";

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = quadros;
            Debug.Log("[Airsoft] Quadros montados. Salve a cena (Ctrl+S).");
        }

        /// <summary>Cria um quadro (fundo escuro + texto) e devolve o texto.</summary>
        static TextMeshPro CriarQuadro(Transform pai, string nome, Vector3 posicao, Vector2 tamanho, Material fundo)
        {
            GameObject quadro = new GameObject(nome);
            quadro.transform.SetParent(pai, false);
            quadro.transform.position = posicao;
            quadro.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // virado para o jogador

            GameObject placa = GameObject.CreatePrimitive(PrimitiveType.Quad);
            placa.name = "Fundo";
            Object.DestroyImmediate(placa.GetComponent<Collider>());   // a BB atravessa e bate na parede
            placa.transform.SetParent(quadro.transform, false);
            placa.transform.localScale = new Vector3(tamanho.x, tamanho.y, 1f);
            placa.GetComponent<MeshRenderer>().sharedMaterial = fundo;

            GameObject textoGO = new GameObject("Texto");
            textoGO.transform.SetParent(quadro.transform, false);
            textoGO.transform.localPosition = new Vector3(0f, 0f, -0.02f);   // um pouco à frente do fundo

            TextMeshPro texto = textoGO.AddComponent<TextMeshPro>();
            texto.alignment = TextAlignmentOptions.TopLeft;
            texto.color = Color.white;
            texto.enableAutoSizing = true;
            texto.textWrappingMode = TextWrappingModes.NoWrap;   // sem quebrar linha: a fonte diminui para caber
            texto.fontSizeMin = 1f;
            texto.fontSizeMax = 6f;
            texto.rectTransform.sizeDelta = new Vector2(tamanho.x - 0.3f, tamanho.y - 0.3f);
            return texto;
        }

        /// <summary>Coloca o modelo 3D da arma deitado acima do quadro, de perfil para o jogador.</summary>
        static void CriarModeloDaArma(Transform quadro, string arquivoDoPrefab, float comprimento)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolyOne/Free Gun/Prefabs/" + arquivoDoPrefab);
            if (prefab == null)
            {
                Debug.LogWarning("[Airsoft] Não achei " + arquivoDoPrefab);
                return;
            }

            // Suporte acima do quadro. Girado 90° para o cano ficar deitado ao longo da parede.
            GameObject suporte = new GameObject("Modelo");
            suporte.transform.SetParent(quadro, false);
            suporte.transform.localPosition = new Vector3(0f, 2.8f, -0.3f);
            suporte.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            GameObject modelo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            modelo.transform.SetParent(suporte.transform, false);

            foreach (Transform t in modelo.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("Bullet"))
                    t.gameObject.SetActive(false);
            }
            AirsoftSceneBuilder.TidyWeaponParts(modelo);

            // Mesmo encaixe usado na mão do jogador: acerta rotação e escala pelo tamanho
            AirsoftSceneBuilder.FitViewmodel(modelo, suporte.transform, comprimento);
            AirsoftSceneBuilder.ApplyGunMaterial(modelo);

            // O encaixe deixa a coronha perto da origem; centraliza a arma no suporte
            modelo.transform.localPosition += new Vector3(0f, 0f, -(comprimento * 0.5f - 0.22f));
        }

        static Material MaterialDoFundo()
        {
            const string caminho = "Assets/_Airsoft/Materials/M_Quadro.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.SetColor("_BaseColor", new Color(0.04f, 0.06f, 0.09f));
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
