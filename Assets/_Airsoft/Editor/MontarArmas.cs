using AirsoftEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Airsoft
{
    /// <summary>
    /// Menu Airsoft → Montar armas.
    /// Cria as 3 armas na mão do jogador (pistola, AK e sniper) com os valores de cada uma
    /// e liga tudo ao Player, à HUD e ao GameManager. Pode rodar de novo: recria as armas.
    /// Não mexe em paredes, alvos, placar nem START.
    /// </summary>
    public static class MontarArmas
    {
        const string PastaPrefabs = "Assets/PolyOne/Free Gun/Prefabs/";

        [MenuItem("Airsoft/Montar armas", false, 2)]
        public static void Montar()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[Airsoft] Não achei o Player na cena.");
                return;
            }
            Transform camera = player.GetComponentInChildren<Camera>().transform;

            // Apaga as armas antigas (o WeaponHolder da versão com uma arma só e as Arma_*)
            for (int i = camera.childCount - 1; i >= 0; i--)
            {
                Transform filho = camera.GetChild(i);
                if (filho.name == "WeaponHolder" || filho.name.StartsWith("Arma_"))
                    Object.DestroyImmediate(filho.gameObject);
            }

            GameObject bb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Airsoft/Prefabs/BB.prefab");

            // ---------- pistola (tecla 1) ----------
            // Posição na mão: X = direita, Y = altura, Z = distância à frente da câmera
            AirsoftWeapon pistola = CriarArma(camera, bb, "Arma_Pistola", "FN_Five_Seven.prefab",
                                              0.24f, new Vector3(0.16f, -0.14f, 0.50f));
            pistola.modelo = "Pistola";
            pistola.tipoDeCarregador = TipoDeCarregador.PistolGlock;
            pistola.alcance = 40f;
            pistola.muzzleEnergyJoules = 0.5f;
            pistola.rpmDoMotor = 5400f;           // 3 BB/s
            pistola.capacidadeDoCarregador = 12;
            pistola.massaDaBBKg = 0.00012f;       // 0,12 g

            // ---------- AK (tecla 2) ----------
            AirsoftWeapon ak = CriarArma(camera, bb, "Arma_AK", "SM_Ak47.prefab",
                                         0.85f, new Vector3(0.20f, -0.17f, 0.18f));
            ak.modelo = "AK47";
            ak.tipoDeCarregador = TipoDeCarregador.Rifle;
            ak.alcance = 65f;
            ak.muzzleEnergyJoules = 1.49f;
            ak.rpmDoMotor = 15000f;               // 8,3 BB/s
            ak.capacidadeDoCarregador = 25;
            ak.massaDaBBKg = 0.0002f;             // 0,20 g

            // ---------- sniper (tecla 3) ----------
            AirsoftWeapon sniper = CriarArma(camera, bb, "Arma_Sniper", "SM_Barrett_M82A1.prefab",
                                             1.25f, new Vector3(0.20f, -0.17f, 0.46f));
            sniper.modelo = "Sniper";
            sniper.tipoDeCarregador = TipoDeCarregador.Sniper;
            sniper.alcance = 100f;
            sniper.muzzleEnergyJoules = 4.5f;
            sniper.rpmDoMotor = 1200f;            // 0,67 BB/s (um tiro a cada 1,5 s)
            sniper.capacidadeDoCarregador = 6;
            sniper.massaDaBBKg = 0.00045f;        // 0,45 g

            // ---------- ligações ----------
            ArmasDoJogador armas = player.GetComponent<ArmasDoJogador>();
            if (armas == null) armas = player.AddComponent<ArmasDoJogador>();
            armas.armas = new[] { pistola, ak, sniper };

            AirsoftHUD hud = player.GetComponent<AirsoftHUD>();
            if (hud != null) hud.armas = armas;

            GameManager gm = Object.FindAnyObjectByType<GameManager>();
            if (gm != null) gm.armasDoJogador = armas;
            else Debug.LogWarning("[Airsoft] Sem GameManager na cena: rode também Airsoft → Montar modo de jogo.");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Airsoft] Armas montadas. Salve a cena (Ctrl+S) e aperte Play.");
        }

        static AirsoftWeapon CriarArma(Transform camera, GameObject bbPrefab, string nome,
                                       string arquivoDoPrefab, float comprimento, Vector3 posicaoNaMao)
        {
            GameObject holder = new GameObject(nome);
            holder.transform.SetParent(camera, false);
            holder.transform.localPosition = posicaoNaMao;

            // Modelo 3D da arma
            Vector3 bocaDoCano = new Vector3(0f, 0f, 0.5f);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PastaPrefabs + arquivoDoPrefab);
            if (prefab != null)
            {
                GameObject modelo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                modelo.transform.SetParent(holder.transform, false);

                // Esconde a munição de verdade que vem solta no modelo (quem voa é a BB)
                foreach (Transform t in modelo.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.Contains("Bullet"))
                        t.gameObject.SetActive(false);
                }
                AirsoftSceneBuilder.TidyWeaponParts(modelo);   // encaixa o carregador da AK

                bocaDoCano = AirsoftSceneBuilder.FitViewmodel(modelo, holder.transform, comprimento);
                AirsoftSceneBuilder.ApplyGunMaterial(modelo);
            }
            else
            {
                Debug.LogWarning("[Airsoft] Não achei " + arquivoDoPrefab);
            }

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(holder.transform, false);
            muzzle.transform.localPosition = bocaDoCano;

            AirsoftWeapon arma = holder.AddComponent<AirsoftWeapon>();
            arma.muzzle = muzzle.transform;
            arma.bbPrefab = bbPrefab;
            return arma;
        }
    }
}
