using System.Collections.Generic;
using Airsoft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AirsoftEditor
{
    /// <summary>
    /// Monta a cena inteira do simulador com um clique: materiais, prefabs, estande de
    /// tiro, rig de primeira pessoa com a AK47 encaixada e as referências todas ligadas.
    ///
    /// É idempotente: pode rodar quantas vezes quiser, ele refaz do zero.
    /// </summary>
    public static class AirsoftSceneBuilder
    {
        const string Root = "Assets/_Airsoft";
        const string MatDir = Root + "/Materials";
        const string TexDir = Root + "/Textures";
        const string PrefabDir = Root + "/Prefabs";
        const string AkPrefabPath = "Assets/PolyOne/Free Gun/Prefabs/SM_Ak47.prefab";
        // O prefab SM_Ak47 aponta para um material EMBUTIDO no .fbx, que é branco liso.
        // O material de verdade (com a textura) é este, solto na pasta Materials.
        const string GunMaterialPath = "Assets/PolyOne/Free Gun/Materials/Props_FreeGun_M.mat";

        const float TargetGunLength = 0.85f;   // comprimento visual da AK, em metros
        const float EyeHeight = 1.6f;
        // O Plane tem UV de 0 a 1 no seu comprimento inteiro (600 m aqui),
        // então precisa de MUITA repetição: 200 = um ladrilho a cada 3 m.
        const float GroundTiles = 200f;
        internal static readonly Vector3 HolderOffset = new Vector3(0.20f, -0.17f, 0.26f);

        // O SM_Ak47 vem com o carregador e uma munição 7.62 como objetos SOLTOS, ambos
        // na origem — o carregador flutua ao lado do poço e a munição fica pendurada na
        // janela de ejeção. O offset abaixo encaixa o carregador; a munição é desligada
        // porque quem voa aqui é a BB de 6mm.
        const string MagazineName = "Ak47_Magazine";
        const string BulletName = "AK47_Bullet";
        static readonly Vector3 MagazineOffset = new Vector3(0f, -0.075f, -0.0125f);

        // Nomes que o builder é dono e pode apagar entre execuções
        static readonly string[] Owned =
        {
            "Player", "Chao", "Estande", "Camera_Analise", "Arma", "Corpo", "Muzzle",
            // sobras da montagem manual: a BB não deve existir NA CENA, só como prefab
            "BB", "Sphere", "Plane", "Cube", "Disparos"
        };

        // =================================================================
        [MenuItem("Airsoft/Ferramentas/Construir cena do ZERO (apaga o mapa)", false, 50)]
        public static void BuildAll()
        {
            bool confirmou = EditorUtility.DisplayDialog(
                "Construir cena do zero",
                "Isso APAGA o estande, as paredes, os alvos e o Player da cena e cria tudo de novo.\n\n" +
                "Qualquer alteração feita à mão no mapa será perdida. Continuar?",
                "Apagar e reconstruir", "Cancelar");
            if (!confirmou) return;

            EnsureFolders();
            CheckTimeSettings();

            Material mChao = MakeGroundMaterial();
            Material mParede = MakeWallMaterial();
            Material mAlvoBranco = MakeTargetMaterial("M_AlvoBranco", new Color(0.90f, 0.90f, 0.86f));
            Material mAlvoVermelho = MakeTargetMaterial("M_AlvoVermelho", new Color(0.75f, 0.11f, 0.10f));
            Material mAlvoCentro = MakeTargetMaterial("M_AlvoCentro", new Color(0.98f, 0.78f, 0.12f));
            Material mPosteAlvo = MakeTargetMaterial("M_PosteAlvo", new Color(0.20f, 0.21f, 0.22f));
            Material mBB = MakeBBMaterial();
            Material mTrail = MakeTrailMaterial();
            Material mMarker = MakeNeonMaterial("M_Marcador", new Color(1f, 0.45f, 0.15f), 3.2f);

            GameObject markerPrefab = BuildMarkerPrefab(mMarker);
            GameObject bbPrefab = BuildBBPrefab(mBB, mTrail, markerPrefab);

            BuildScene(mChao, bbPrefab,
                       mParede, mAlvoBranco, mAlvoVermelho, mAlvoCentro, mPosteAlvo);
            TuneBloom();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[Airsoft] Cena construída. Aperte Play.\n" +
                      "Controles: mouse=olhar · WASD=andar · clique=atirar · roda=hop-up · H=ajuda");
        }

        [MenuItem("Airsoft/Ferramentas/Rolar a arma 90° (se estiver deitada de lado)", false, 21)]
        public static void RollWeapon()
        {
            Transform model = FindWeaponModel();
            if (model == null) return;
            Undo.RecordObject(model, "Rolar arma");
            model.localRotation = Quaternion.Euler(0f, 0f, 90f) * model.localRotation;
            RecenterModel(model.gameObject, model.parent);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Airsoft] Arma rolada 90°.");
        }

        [MenuItem("Airsoft/Ferramentas/Reaplicar o material da arma", false, 22)]
        public static void ReapplyGunMaterial()
        {
            Transform model = FindWeaponModel();
            if (model == null) return;
            ApplyGunMaterial(model.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Airsoft] Material da arma reaplicado.");
        }

        static Transform FindWeaponModel()
        {
            GameObject holder = GameObject.Find("Player/Main Camera/WeaponHolder");
            if (holder == null) { Debug.LogError("[Airsoft] WeaponHolder não encontrado. Rode 'Construir cena completa' primeiro."); return null; }
            foreach (Transform t in holder.transform)
                if (t.name != "Muzzle") return t;
            Debug.LogError("[Airsoft] Modelo da arma não encontrado dentro do WeaponHolder.");
            return null;
        }

        [MenuItem("Airsoft/Ferramentas/Girar a arma 180° (se o cano estiver ao contrário)", false, 20)]
        public static void FlipWeapon()
        {
            Transform model = FindWeaponModel();
            if (model == null) return;

            Undo.RecordObject(model, "Girar arma");
            model.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.localRotation;
            RecenterModel(model.gameObject, model.parent);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Airsoft] Arma girada 180°.");
        }

        // =================================================================
        //  Pastas e configurações
        // =================================================================

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "_Airsoft");
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Root, "Materials");
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder(Root, "Prefabs");
            if (!AssetDatabase.IsValidFolder(TexDir)) AssetDatabase.CreateFolder(Root, "Textures");
        }

        static void CheckTimeSettings()
        {
            // A BB voa a 122 m/s. No Fixed Timestep padrão (0.02s) ela anda 2.44 m por
            // passo de física: atravessa o chão e o erro de integração passa de 5%.
            // Com 0.005s são 0.61 m por passo e o erro cai para ~1%.
            if (!Mathf.Approximately(Time.fixedDeltaTime, 0.005f))
            {
                Time.fixedDeltaTime = 0.005f;
                Debug.LogWarning("[Airsoft] Fixed Timestep ajustado para 0.005. " +
                    "Confirme em Edit > Project Settings > Time que ficou salvo.");
            }

            if (Physics.defaultContactOffset > 0.002f)
                Debug.LogWarning($"[Airsoft] Default Contact Offset está em {Physics.defaultContactOffset}. " +
                    "Recomendado 0.001 (menor que o raio da BB) em Project Settings > Physics.");
        }

        // =================================================================
        //  Materiais
        // =================================================================

        static Material EnsureMaterial(string name, Shader shader)
        {
            string path = $"{MatDir}/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                // Troca de shader: limpa keywords antigas para não sobrar sujeira
                // do shader anterior (o M_TrailNeon nasce como Lit, por exemplo).
                m.shaderKeywords = new string[0];
                m.shader = shader;
            }
            return m;
        }

        static Material MakeGroundMaterial()
        {
            Texture2D grama = MakeTextureAsset("T_Grama", 512, GrassPixel);

            Material m = EnsureMaterial("M_Chao", Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", grama);
            m.SetTextureScale("_BaseMap", new Vector2(GroundTiles, GroundTiles));
            m.SetFloat("_Smoothness", 0.04f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MakeWallMaterial()
        {
            Texture2D concreto = MakeTextureAsset("T_Concreto", 256, ConcretePixel);

            Material m = EnsureMaterial("M_Parede", Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", concreto);
            m.SetFloat("_Smoothness", 0.1f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MakeTargetMaterial(string name, Color color)
        {
            Material m = EnsureMaterial(name, Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.15f);
            m.SetFloat("_Metallic", 0f);

            // Emissão LIGADA com cor preta: a keyword _EMISSION precisa estar ativa no
            // material para o Target conseguir acender a placa via MaterialPropertyBlock
            // quando é acertada (keywords não podem ser trocadas por MPB).
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MakeBBMaterial()
        {
            Material m = EnsureMaterial("M_BB", Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.6f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.white * 2f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// Material do rastro: BRANCO em HDR e aditivo.
        /// Branco de propósito — a cor azul neon vem do degradê do TrailRenderer,
        /// que é o que permite trocar a cor por tiro (modo "colorir por hop-up").
        /// Se a cor viesse do material, o degradê multiplicaria as duas e apagaria o brilho.
        /// </summary>
        static Material MakeTrailMaterial()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Universal Render Pipeline/Unlit");

            Material m = EnsureMaterial("M_TrailNeon", sh);
            MakeAdditive(m);
            m.SetColor("_BaseColor", new Color(2.2f, 2.2f, 2.2f, 1f));   // HDR: passa de 1 -> o Bloom pega
            m.SetColor("_Color", new Color(2.2f, 2.2f, 2.2f, 1f));
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MakeNeonMaterial(string name, Color color, float intensity)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Universal Render Pipeline/Unlit");

            Material m = EnsureMaterial(name, sh);
            MakeAdditive(m);
            Color hdr = color * intensity;
            hdr.a = 1f;
            m.SetColor("_BaseColor", hdr);
            m.SetColor("_Color", hdr);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Transparente + blending aditivo — é o que produz a leitura de "neon".</summary>
        static void MakeAdditive(Material m)
        {
            m.SetFloat("_Surface", 1f);   // 0 = Opaque, 1 = Transparent
            m.SetFloat("_Blend", 2f);     // 2 = Additive
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_ColorMode", 0f);   // Multiply: o degradê do rastro tinge o material
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            m.SetFloat("_ReceiveShadows", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);

            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        // =================================================================
        //  Prefabs
        // =================================================================

        static GameObject BuildMarkerPrefab(Material mMarker)
        {
            GameObject root = new GameObject("Marcador_Impacto");

            GameObject disc = MakePrimitive(PrimitiveType.Cylinder, "Disco", root.transform, mMarker);
            disc.transform.localScale = new Vector3(0.55f, 0.004f, 0.55f);

            GameObject beam = MakePrimitive(PrimitiveType.Cylinder, "Feixe", root.transform, mMarker);
            beam.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            beam.transform.localScale = new Vector3(0.035f, 1.1f, 0.035f);

            root.AddComponent<ImpactMarker>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/Marcador_Impacto.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// Estrutura do prefab da BB:
        ///
        ///   BB      (escala 1) — Rigidbody, SphereCollider r=0.003, BBProjectile
        ///   ├─ Mesh (escala 0.006) — a esfera visível de 6mm
        ///   └─ Trail (escala 1)    — TrailRenderer
        ///
        /// A raiz fica em escala 1 de propósito. Se a esfera de 6mm fosse a raiz,
        /// a escala 0.006 seria herdada pelo TrailRenderer e a largura do rastro
        /// viraria 0.12 * 0.006 = 0.7mm — invisível.
        /// </summary>
        static GameObject BuildBBPrefab(Material mBB, Material mTrail, GameObject markerPrefab)
        {
            GameObject root = new GameObject("BB");

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.0002f;              // 0,20 g
            rb.linearDamping = 0f;          // o arrasto é o nosso, quadrático
            rb.angularDamping = 0f;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            SphereCollider col = root.AddComponent<SphereCollider>();
            col.radius = 0.003f;            // 3 mm

            BBProjectile bb = root.AddComponent<BBProjectile>();
            bb.radius = 0.003f;
            bb.impactMarkerPrefab = markerPrefab;

            GameObject mesh = MakePrimitive(PrimitiveType.Sphere, "Mesh", root.transform, mBB);
            mesh.transform.localScale = Vector3.one * 0.006f;

            GameObject trailGO = new GameObject("Trail");
            trailGO.transform.SetParent(root.transform, false);
            TrailRenderer tr = trailGO.AddComponent<TrailRenderer>();
            tr.time = 8f;
            tr.minVertexDistance = 0.1f;
            tr.sharedMaterial = mTrail;
            tr.alignment = LineAlignment.View;
            tr.textureMode = LineTextureMode.Stretch;
            tr.numCapVertices = 2;
            tr.numCornerVertices = 2;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.autodestruct = false;
            tr.widthMultiplier = 0.12f;
            tr.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(1f, 0.15f));
            tr.colorGradient = NeonGradient(new Color(0f, 0.55f, 1f));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/BB.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Gradient NeonGradient(Color c)
        {
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.65f),
                    new GradientAlphaKey(0f, 1f)
                });
            return g;
        }

        static GameObject MakePrimitive(PrimitiveType type, string name, Transform parent, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;

            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);   // decoração não colide com a BB

            go.transform.SetParent(parent, false);

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        // =================================================================
        //  Cena
        // =================================================================

        static void BuildScene(Material mChao, GameObject bbPrefab,
                               Material mParede, Material mAlvoBranco, Material mAlvoVermelho,
                               Material mAlvoCentro, Material mPosteAlvo)
        {
            Scene scene = SceneManager.GetActiveScene();

            // A câmera principal é reaproveitada: ela carrega a tag MainCamera e os
            // dados de URP. Solta ela da hierarquia antiga antes de limpar.
            GameObject camGO = GameObject.FindWithTag("MainCamera");
            if (camGO != null)
            {
                camGO.transform.SetParent(null, true);

                // O WeaponHolder é FILHO da câmera. Ao soltar a câmera da hierarquia
                // antiga para reaproveitá-la, ele vem junto e escapa da limpeza abaixo
                // — o que produzia DUAS armas na cena a cada reconstrução.
                for (int i = camGO.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(camGO.transform.GetChild(i).gameObject);
            }

            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go == camGO) continue;
                foreach (string owned in Owned)
                    if (go.name == owned) { Object.DestroyImmediate(go); break; }
            }

            // ---------- chão ----------
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Chao";
            ground.transform.localScale = new Vector3(60f, 1f, 60f);   // 600 x 600 m
            ground.GetComponent<MeshRenderer>().sharedMaterial = mChao;

            // ---------- estande ----------
            GameObject range = new GameObject("Estande");

            BuildWalls(range.transform, mParede);
            BuildTargets(range.transform, mAlvoBranco, mAlvoVermelho, mAlvoCentro, mPosteAlvo);

            // ---------- rig de primeira pessoa ----------
            GameObject player = new GameObject("Player");
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            if (camGO == null)
            {
                camGO = new GameObject("Main Camera");
                camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
                camGO.tag = "MainCamera";
            }
            camGO.name = "Main Camera";
            camGO.transform.SetParent(player.transform, false);
            camGO.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
            camGO.transform.localRotation = Quaternion.identity;

            Camera cam = camGO.GetComponent<Camera>();
            cam.enabled = true;
            cam.fieldOfView = 65f;
            // 0.01 em vez de 0.3: sem isso a câmera CORTA a arma segurada na mão,
            // que é o bug clássico de viewmodel em FPS.
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 1500f;

            GameObject holder = new GameObject("WeaponHolder");
            holder.transform.SetParent(camGO.transform, false);
            holder.transform.localPosition = HolderOffset;
            holder.transform.localRotation = Quaternion.identity;

            // ---------- modelo da AK ----------
            Vector3 muzzleLocal = new Vector3(0f, 0f, 0.55f);
            GameObject akAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AkPrefabPath);
            if (akAsset != null)
            {
                GameObject ak = (GameObject)PrefabUtility.InstantiatePrefab(akAsset);
                ak.transform.SetParent(holder.transform, false);

                // ANTES do FitViewmodel: ele mede a caixa envolvente de todas as malhas
                // para achar o cano. Com a munição ligada e o carregador fora do lugar,
                // a medida sai errada e a arma inteira desalinha.
                TidyWeaponParts(ak);

                muzzleLocal = FitViewmodel(ak, holder.transform);
                ApplyGunMaterial(ak);
                Collider[] cols = ak.GetComponentsInChildren<Collider>();
                if (cols.Length > 0)
                {
                    // O Unity não deixa remover componentes de uma INSTÂNCIA de prefab.
                    // O SM_Ak47 não tem colliders, mas se alguém trocar o modelo por um
                    // que tenha, é preciso desconectar antes — senão a BB colidiria com
                    // a própria arma no instante do disparo.
                    PrefabUtility.UnpackPrefabInstance(ak, PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                    foreach (Collider c in ak.GetComponentsInChildren<Collider>())
                        Object.DestroyImmediate(c);
                }
            }
            else
            {
                Debug.LogWarning($"[Airsoft] Não achei {AkPrefabPath}. Usando um cubo no lugar da AK.");
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "ArmaPlaceholder";
                Object.DestroyImmediate(box.GetComponent<BoxCollider>());
                box.transform.SetParent(holder.transform, false);
                box.transform.localScale = new Vector3(0.07f, 0.12f, TargetGunLength);
                box.transform.localPosition = new Vector3(0f, 0f, TargetGunLength * 0.5f - 0.2f);
            }

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(holder.transform, false);
            muzzle.transform.localPosition = muzzleLocal;
            muzzle.transform.localRotation = Quaternion.identity;   // forward == direção do olhar

            // ---------- componentes e ligações ----------
            AirsoftWeapon weapon = holder.AddComponent<AirsoftWeapon>();
            weapon.muzzle = muzzle.transform;
            weapon.bbPrefab = bbPrefab;

            // O CharacterController entra ANTES do PlayerController: o [RequireComponent]
            // adicionaria um com os valores padrão do Unity, e o Awake() do controller
            // é quem dimensiona a cápsula.
            CharacterController body = player.AddComponent<CharacterController>();

            PlayerController pc = player.AddComponent<PlayerController>();
            pc.cameraPivot = camGO.transform;
            pc.eyeHeight = EyeHeight;

            weapon.shooterBody = body;

            ArmasDoJogador armas = player.AddComponent<ArmasDoJogador>();
            armas.armas = new[] { weapon };

            AirsoftHUD hud = player.AddComponent<AirsoftHUD>();
            hud.armas = armas;
            hud.player = pc;

            // ---------- luz ----------
            Light sun = Object.FindAnyObjectByType<Light>();
            if (sun != null && sun.type == LightType.Directional)
            {
                sun.transform.rotation = Quaternion.Euler(42f, 35f, 0f);
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
            }
        }

        /// <summary>
        /// Encaixa o carregador e desliga a munição solta do modelo da AK.
        ///
        /// O prefab do asset já vem corrigido, mas isto fica aqui de propósito: o
        /// modelo é de terceiros e uma reimportação do .fbx devolve os filhos à origem.
        /// Como o `Construir cena completa` recria o Player do zero, qualquer ajuste
        /// feito à mão na CENA seria um override de instância — e sumiria no próximo
        /// rebuild. Corrigir aqui é o único lugar que sobrevive.
        /// </summary>
        internal static void TidyWeaponParts(GameObject ak)
        {
            foreach (Transform t in ak.GetComponentsInChildren<Transform>(true))
            {
                // Carregadores vêm soltos na origem nos modelos do pacote: encaixa cada um
                if (t.name == MagazineName) t.localPosition = MagazineOffset;
                else if (t.name == "FNFiveSeven_Magazine") t.localPosition = new Vector3(0f, 0f, -0.06f);
                else if (t.name == "Barrett_M82A1_Magazine") t.localPosition = new Vector3(0.19f, -0.1f, -0.008f);

                // SetActive em vez de destruir: o Unity não deixa apagar um filho de
                // instância de prefab por script, mas desativar é um override válido.
                else if (t.name == BulletName) t.gameObject.SetActive(false);
            }
        }

        // =================================================================
        //  Encaixe automático da arma
        // =================================================================

        /// <summary>
        /// Ajusta escala, rotação e posição do modelo da arma sozinho, e devolve
        /// onde fica a boca do cano (em coordenadas locais do WeaponHolder).
        ///
        /// Não dá para saber de antemão em que eixo o artista modelou a arma nem para
        /// que lado o cano aponta, então isso é medido na malha:
        ///   1. o eixo MAIS COMPRIDO da malha é o eixo do cano  -> alinha com +Z;
        ///   2. escala para o comprimento alvo;
        ///   3. a ponta com a MENOR espessura é o cano (a outra é a coronha/carregador);
        ///   4. reposiciona e mede a ponta do cano.
        /// </summary>
        internal static Vector3 FitViewmodel(GameObject model, Transform holder, float comprimento = TargetGunLength)
        {
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            List<Vector3> pts = GatherPoints(model, holder);
            if (pts.Count == 0)
            {
                Debug.LogWarning("[Airsoft] A arma não tem malha legível; usando posição padrão.");
                return new Vector3(0f, 0f, 0.55f);
            }

            // 1) alinha o eixo mais comprido com +Z
            Bounds b = BoundsOf(pts);
            Vector3 size = b.size;
            if (size.x >= size.y && size.x >= size.z)
                model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            else if (size.y >= size.x && size.y >= size.z)
                model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // 2) escala para o comprimento alvo
            pts = GatherPoints(model, holder);
            b = BoundsOf(pts);
            if (b.size.z > 0.0001f)
                model.transform.localScale = Vector3.one * (comprimento / b.size.z);

            // 3) escolhe a melhor entre as 8 poses (4 rolagens x 2 sentidos)
            pts = GatherPoints(model, holder);
            Quaternion pose = BestPose(pts);
            if (pose != Quaternion.identity)
            {
                // PRÉ-multiplica: a correção é no espaço do WeaponHolder, não no do
                // modelo. Pós-multiplicar giraria em torno de um eixo já rotacionado.
                model.transform.localRotation = pose * model.transform.localRotation;
                Debug.Log($"[Airsoft] Pose da arma corrigida em {pose.eulerAngles}.");
            }

            return RecenterModel(model, holder);
        }

        static Vector3 RecenterModel(GameObject model, Transform holder)
        {
            List<Vector3> pts = GatherPoints(model, holder);
            if (pts.Count == 0) return new Vector3(0f, 0f, 0.55f);

            Bounds b = BoundsOf(pts);

            // Centraliza no eixo do holder e recua a coronha para perto da câmera.
            Vector3 shift = new Vector3(-b.center.x, -b.center.y, -b.min.z - 0.22f);
            model.transform.localPosition += shift;

            // Mede a altura e o lado exatos do CANO (os 8% da frente), não da arma inteira,
            // para a BB nascer no furo do cano e não no meio do corpo da arma.
            pts = GatherPoints(model, holder);
            b = BoundsOf(pts);

            float cut = b.max.z - b.size.z * 0.08f;
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (Vector3 p in pts)
                if (p.z >= cut) { sum += p; n++; }

            Vector3 tip = n > 0 ? sum / n : b.center;
            return new Vector3(tip.x, tip.y, b.max.z + 0.02f);
        }

        static List<Vector3> GatherPoints(GameObject go, Transform space)
        {
            List<Vector3> pts = new List<Vector3>();
            Matrix4x4 w2l = space.worldToLocalMatrix;

            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 m = w2l * mf.transform.localToWorldMatrix;
                foreach (Vector3 v in mf.sharedMesh.vertices)
                    pts.Add(m.MultiplyPoint3x4(v));
            }
            return pts;
        }

        static Bounds BoundsOf(List<Vector3> pts)
        {
            Bounds b = new Bounds(pts[0], Vector3.zero);
            for (int i = 1; i < pts.Count; i++) b.Encapsulate(pts[i]);
            return b;
        }

        /// <summary>
        /// Escolhe a orientação da arma entre as 8 possíveis (4 rolagens em torno do
        /// cano x 2 sentidos), sem depender de como o artista modelou o FBX.
        ///
        /// O sinal usado é anatômico e vale para qualquer arma de fogo: o punho e o
        /// carregador descem bem abaixo do eixo do cano e ficam sempre na metade
        /// TRASEIRA. A pose certa é a que maximiza "o quanto a traseira desce a mais
        /// que a dianteira" — o que resolve rolagem e sentido de uma vez só:
        ///   de cabeça para baixo -> o punho sobe          -> pontuação despenca
        ///   deitada de lado      -> o punho vai para o lado -> pontuação ~0
        ///   ao contrário         -> o punho vai para a frente -> pontuação negativa
        /// </summary>
        static Quaternion BestPose(List<Vector3> pts)
        {
            Quaternion best = Quaternion.identity;
            float bestScore = float.NegativeInfinity;

            for (int yaw = 0; yaw < 2; yaw++)
            {
                for (int roll = 0; roll < 4; roll++)
                {
                    Quaternion q = Quaternion.Euler(0f, yaw * 180f, 0f)
                                 * Quaternion.Euler(0f, 0f, roll * 90f);
                    float score = PoseScore(pts, q);
                    if (score > bestScore) { bestScore = score; best = q; }
                }
            }
            return best;
        }

        /// <summary>Quanto a metade traseira desce a mais que a dianteira. Maior = melhor.</summary>
        static float PoseScore(List<Vector3> pts, Quaternion q)
        {
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < pts.Count; i++)
            {
                float z = (q * pts[i]).z;
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }

            float len = maxZ - minZ;
            if (len < 1e-5f) return float.NegativeInfinity;

            float frontCut = maxZ - len * 0.40f;
            float rearCut = minZ + len * 0.40f;

            float frontMinY = float.MaxValue, rearMinY = float.MaxValue;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 p = q * pts[i];
                if (p.z >= frontCut && p.y < frontMinY) frontMinY = p.y;
                if (p.z <= rearCut && p.y < rearMinY) rearMinY = p.y;
            }

            if (frontMinY == float.MaxValue || rearMinY == float.MaxValue)
                return float.NegativeInfinity;

            return frontMinY - rearMinY;
        }

        internal static void ApplyGunMaterial(GameObject ak)
        {
            Material gunMat = AssetDatabase.LoadAssetAtPath<Material>(GunMaterialPath);
            if (gunMat == null)
            {
                Debug.LogWarning($"[Airsoft] Material {GunMaterialPath} não encontrado — a arma fica branca.");
                return;
            }

            foreach (Renderer r in ak.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = gunMat;
                r.sharedMaterials = mats;
            }
        }

        // =================================================================
        //  Texturas procedurais
        // =================================================================

        /// <summary>
        /// Ruído de Perlin SEM COSTURA. O Mathf.PerlinNoise normal deixa emenda visível
        /// quando a textura se repete 200 vezes no chão; aqui as 4 amostras dos cantos
        /// são misturadas por peso bilinear, o que faz a borda direita casar exatamente
        /// com a esquerda (e o topo com a base).
        /// </summary>
        static float Seamless(float u, float v, float freq, float seed)
        {
            float x = u * freq, y = v * freq;
            float a = Mathf.PerlinNoise(seed + x, seed + y);
            float b = Mathf.PerlinNoise(seed + x - freq, seed + y);
            float c = Mathf.PerlinNoise(seed + x, seed + y - freq);
            float d = Mathf.PerlinNoise(seed + x - freq, seed + y - freq);

            return a * (1f - u) * (1f - v) + b * u * (1f - v)
                 + c * (1f - u) * v + d * u * v;
        }

        /// <summary>
        /// Hash inteiro por texel. Tem duas propriedades boas aqui: ladrilha de graça
        /// (o texel k e o texel k+size SÃO o mesmo texel, logo o mesmo hash) e não sofre
        /// da perda de precisão do truque clássico com seno em ponto flutuante.
        /// </summary>
        static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777215f;
            }
        }

        static Color GrassPixel(float u, float v, int x, int y)
        {
            // Três oitavas de Perlin: manchas grandes, moitas médias e textura fina.
            float n = 0.50f * Seamless(u, v, 5f, 11.3f)
                    + 0.30f * Seamless(u, v, 15f, 53.7f)
                    + 0.20f * Seamless(u, v, 44f, 91.1f);
            n = Mathf.Clamp01((n - 0.5f) * 2.4f + 0.5f);

            Color c = Color.Lerp(new Color(0.09f, 0.17f, 0.06f),
                                 new Color(0.36f, 0.55f, 0.20f), n);

            // Manchas de grama seca, para o verde não ficar chapado.
            float seca = Seamless(u, v, 3f, 211f);
            c = Color.Lerp(c, new Color(0.47f, 0.44f, 0.19f),
                           Mathf.SmoothStep(0.58f, 0.88f, seca) * 0.55f);

            // Granulado por texel + tufos: é isto que diferencia grama de musgo.
            // Só o Perlin, por ser ruído de gradiente, sai liso demais.
            float grao = Hash01(x, y, 1);
            float tufo = Hash01(x / 3, y / 5, 3);
            float brilho = 0.78f + 0.30f * grao + 0.16f * tufo;

            return new Color(Mathf.Clamp01(c.r * brilho),
                             Mathf.Clamp01(c.g * brilho),
                             Mathf.Clamp01(c.b * brilho), 1f);
        }

        static Color ConcretePixel(float u, float v, int x, int y)
        {
            float n = 0.6f * Seamless(u, v, 4f, 7.7f)
                    + 0.4f * Seamless(u, v, 23f, 61.9f);
            n = Mathf.Clamp01((n - 0.5f) * 1.5f + 0.5f);

            Color c = Color.Lerp(new Color(0.26f, 0.26f, 0.27f),
                                 new Color(0.46f, 0.46f, 0.45f), n);

            float grao = 0.90f + 0.18f * Hash01(x, y, 7);
            return new Color(Mathf.Clamp01(c.r * grao),
                             Mathf.Clamp01(c.g * grao),
                             Mathf.Clamp01(c.b * grao), 1f);
        }

        /// <summary>Gera (uma única vez) um PNG na pasta Textures e devolve o asset importado.</summary>
        static Texture2D MakeTextureAsset(string name, int size, System.Func<float, float, int, int, Color> sample)
        {
            string path = $"{TexDir}/{name}.png";

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            {
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[size * size];

                for (int y = 0; y < size; y++)
                {
                    float v = y / (float)size;
                    for (int x = 0; x < size; x++)
                        pixels[y * size + x] = sample(x / (float)size, v, x, y);
                }

                tex.SetPixels(pixels);
                tex.Apply();
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.filterMode = FilterMode.Trilinear;
                imp.mipmapEnabled = true;
                // Sem filtro anisotrópico o chão vira uma papa borrada já a 20 m,
                // porque é visto num ângulo quase rasante.
                imp.anisoLevel = 8;
                imp.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // =================================================================
        //  Paredes e alvos
        // =================================================================

        static void BuildWalls(Transform parent, Material mParede)
        {
            // Fundo: alto o bastante para conter até os tiros de hop-up excessivo.
            MakeWall(parent, mParede, "Parede_Fundo", new Vector3(0f, 6f, 132f), new Vector3(70f, 12f, 1f));

            MakeWall(parent, mParede, "Parede_Esquerda", new Vector3(-14f, 2f, 58f), new Vector3(1f, 4f, 150f));
            MakeWall(parent, mParede, "Parede_Direita", new Vector3(14f, 2f, 58f), new Vector3(1f, 4f, 150f));
            MakeWall(parent, mParede, "Parede_Tras", new Vector3(0f, 2f, -16f), new Vector3(36f, 4f, 1f));
        }

        static void MakeWall(Transform parent, Material mat, string name, Vector3 pos, Vector3 size)
        {
            // CreatePrimitive direto (e não MakePrimitive) para MANTER o BoxCollider:
            // as paredes precisam parar as BBs.
            GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = name;
            w.transform.SetParent(parent, false);
            w.transform.localPosition = pos;
            w.transform.localScale = size;

            MeshRenderer mr = w.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            // Repetição proporcional ao tamanho, senão a textura estica feio numa
            // parede de 150 m de comprimento.
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            mpb.SetVector("_BaseMap_ST",
                new Vector4(Mathf.Max(size.x, size.z) / 4f, size.y / 4f, 0f, 0f));
            mr.SetPropertyBlock(mpb);
        }

        static void BuildTargets(Transform parent, Material mBranco, Material mVermelho,
                                 Material mCentro, Material mPoste)
        {
            float[] distancias = { 10f, 20f, 30f, 40f, 50f, 60f, 65f };

            for (int i = 0; i < distancias.Length; i++)
            {
                float d = distancias[i];

                // Alternando os lados e deixando o corredor central (|x| < 3) livre:
                // assim dá para testar as trajetórias longas sem esbarrar num alvo.
                float x = (i % 2 == 0) ? -6f : 6f;

                // Alvos distantes maiores, como num estande de verdade.
                float raio = 0.35f + d * 0.006f;
                const float altura = 1.5f;

                GameObject alvo = new GameObject($"Alvo_{d:0}m");
                alvo.transform.SetParent(parent, false);
                alvo.transform.localPosition = new Vector3(x, altura, d);
                // +Z local apontando para o atirador (que está em z = 0 olhando para +Z).
                alvo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                GameObject poste = MakePrimitive(PrimitiveType.Cube, "Poste", alvo.transform, mPoste);
                poste.transform.localScale = new Vector3(0.07f, altura, 0.07f);
                poste.transform.localPosition = new Vector3(0f, -altura * 0.5f, -0.06f);

                // Anéis empilhados na direção do atirador (+Z local).
                AddRing(alvo.transform, mBranco, raio, 0f);
                AddRing(alvo.transform, mVermelho, raio * 0.55f, 0.012f);
                AddRing(alvo.transform, mCentro, raio * 0.22f, 0.024f);

                // Um único collider para a placa toda. Os anéis não têm collider, então
                // o ponto de contato cai sempre nesta caixa e o Target calcula o anel
                // pela distância ao centro.
                BoxCollider col = alvo.AddComponent<BoxCollider>();
                col.center = Vector3.zero;
                col.size = new Vector3(raio * 2f, raio * 2f, 0.09f);

                Target t = alvo.AddComponent<Target>();
                t.nominalDistance = d;
                t.plateRadius = raio;
            }
        }

        static void AddRing(Transform parent, Material mat, float raio, float z)
        {
            GameObject g = MakePrimitive(PrimitiveType.Cylinder, "Anel", parent, mat);
            // O cilindro do Unity é ao longo de Y e tem 2 unidades de altura por 1 de
            // diâmetro. Girando 90 graus em X o eixo vai para Z e ele vira um disco.
            g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            g.transform.localScale = new Vector3(raio * 2f, 0.008f, raio * 2f);
            g.transform.localPosition = new Vector3(0f, 0f, z);
        }

        // =================================================================

        static void TuneBloom()
        {
            Volume volume = Object.FindAnyObjectByType<Volume>();
            if (volume == null || volume.sharedProfile == null)
            {
                Debug.LogWarning("[Airsoft] Nenhum Global Volume na cena — sem brilho de Bloom.");
                return;
            }

            VolumeProfile profile = volume.sharedProfile;
            if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);

            bloom.active = true;
            bloom.threshold.overrideState = true; bloom.threshold.value = 0.95f;
            bloom.intensity.overrideState = true; bloom.intensity.value = 1.05f;
            bloom.scatter.overrideState = true; bloom.scatter.value = 0.72f;
            bloom.highQualityFiltering.overrideState = true; bloom.highQualityFiltering.value = true;

            EditorUtility.SetDirty(profile);
        }
    }
}
