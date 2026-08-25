using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Airsoft
{
    /// <summary>
    /// HUD do simulador, desenhada com IMGUI (OnGUI).
    ///
    /// Escolha deliberada: em primeira pessoa o cursor fica travado, então um Slider
    /// clicável de Canvas seria inútil. Aqui o hop-up é ajustado pela roda do mouse e
    /// a barra na tela é um MEDIDOR. IMGUI também dispensa Canvas, EventSystem e
    /// assets de fonte — menos peças para quebrar.
    /// </summary>
    public class AirsoftHUD : MonoBehaviour
    {
        [Header("Referências")]
        public AirsoftWeapon weapon;
        public PlayerController player;
        public CameraToggle cameraToggle;

        [Header("Opções")]
        public int historySize = 8;
        public bool showHelp = true;

        readonly List<ShotResult> history = new List<ShotResult>();
        ShotResult last;
        bool hasLast;

        // --- recursos de desenho, criados uma vez ---
        Texture2D texPanel, texBar, texFill, texLine;
        GUIStyle sTitle, sLabel, sValue, sBig, sSmall, sHint;
        bool stylesReady;

        static readonly Color Neon = new Color(0.20f, 0.80f, 1f);
        static readonly Color Dim = new Color(0.65f, 0.75f, 0.85f);
        static readonly Color Warn = new Color(1f, 0.72f, 0.25f);
        static readonly Color Gold = new Color(1f, 0.85f, 0.35f);

        void OnEnable() => BBProjectile.ShotResolved += OnShot;
        void OnDisable() => BBProjectile.ShotResolved -= OnShot;

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.hKey.wasPressedThisFrame) showHelp = !showHelp;
            if (kb.rKey.wasPressedThisFrame) ClearShots();
        }

        void OnShot(ShotResult r)
        {
            last = r;
            hasLast = true;
            history.Insert(0, r);
            while (history.Count > historySize) history.RemoveAt(history.Count - 1);

            Debug.Log(r.landed
                ? $"[Airsoft] Hop-up {r.hopUpPercent:0}%  |  BackspinDrag {r.backspinDrag:E3}  |  " +
                  $"{r.hitLabel} a {r.distance:0.0} m" + (r.hitTarget ? $" ({r.ring}, +{r.points})" : "") +
                  $"  |  voo {r.flightTime:0.00} s  |  ápice {r.apexHeight:0.00} m  |  " +
                  $"impacto a {r.impactSpeed:0.0} m/s"
                : $"[Airsoft] Hop-up {r.hopUpPercent:0}%  |  a BB NÃO tocou o solo em {r.flightTime:0.0} s " +
                  $"(hop-up excessivo). Já havia percorrido {r.distance:0.0} m.");
        }

        void ClearShots()
        {
            history.Clear();
            hasLast = false;
            Target.ResetScore();
            foreach (ImpactMarker m in FindObjectsByType<ImpactMarker>(FindObjectsInactive.Exclude))
                Destroy(m.gameObject);
            foreach (TrailRenderer t in FindObjectsByType<TrailRenderer>(FindObjectsInactive.Exclude))
                if (t.GetComponentInParent<BBProjectile>() == null) Destroy(t.gameObject);
            foreach (BBProjectile b in FindObjectsByType<BBProjectile>(FindObjectsInactive.Exclude))
                Destroy(b.gameObject);
        }

        // ------------------------------------------------------------------

        void OnGUI()
        {
            EnsureStyles();

            // Escala tudo por uma altura de referência de 1080px, para a HUD ficar
            // proporcional em qualquer resolução.
            float s = Mathf.Max(0.5f, Screen.height / 1080f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));

            float W = Screen.width / s;
            float H = Screen.height / s;

            bool sideView = cameraToggle != null && cameraToggle.SideActive;

            if (!sideView) DrawCrosshair(W, H);
            DrawWeaponPanel(16f, 16f);
            DrawHopUpGauge(16f, H - 150f);
            DrawLastShot(W, H);
            DrawHistory(W - 366f, 16f);
            DrawScore(W);
            if (showHelp) DrawHelp(W - 366f, H - 232f);
            if (sideView) DrawBadge(W, "MODO ANÁLISE (lateral) — V para voltar");

            GUI.matrix = old;
        }

        // ------------------------------------------------------------------

        void DrawCrosshair(float W, float H)
        {
            float cx = W * 0.5f, cy = H * 0.5f;
            GUI.color = Neon;
            GUI.DrawTexture(new Rect(cx - 11f, cy - 1f, 8f, 2f), texLine);
            GUI.DrawTexture(new Rect(cx + 3f, cy - 1f, 8f, 2f), texLine);
            GUI.DrawTexture(new Rect(cx - 1f, cy - 11f, 2f, 8f), texLine);
            GUI.DrawTexture(new Rect(cx - 1f, cy + 3f, 2f, 8f), texLine);
            GUI.color = Color.white;

            // Ângulo de elevação: comparar hop-up só é justo com o cano nivelado.
            float elev = weapon != null ? weapon.ElevationDeg : 0f;
            bool level = Mathf.Abs(elev) < 0.35f;
            GUI.color = level ? new Color(0.35f, 1f, 0.5f) : Warn;
            GUI.Label(new Rect(cx - 110f, cy + 20f, 220f, 22f),
                level ? "cano nivelado (0.0°)" : $"elevação {elev:+0.0;-0.0}°   —   F para nivelar",
                sSmall);
            GUI.color = Color.white;
        }

        void DrawWeaponPanel(float x, float y)
        {
            const float w = 356f, h = 196f;
            Panel(new Rect(x, y, w, h));

            float ix = x + 16f, iy = y + 12f;
            GUI.Label(new Rect(ix, iy, w, 22f), "ARMA / PROJÉTIL", sTitle); iy += 26f;

            if (weapon == null)
            {
                GUI.Label(new Rect(ix, iy, w, 22f), "sem referência de arma", sLabel);
                return;
            }

            Row(ix, ref iy, w, "Energia de saída", $"{weapon.muzzleEnergyJoules:0.00} J");
            Row(ix, ref iy, w, "Massa da BB", $"{weapon.BbMassKg * 1000f:0.##} g");
            Row(ix, ref iy, w, "Raio da BB", "3.0 mm");
            iy += 4f;

            GUI.color = Neon;
            GUI.Label(new Rect(ix, iy, w - 32f, 30f),
                $"v₀  {weapon.MuzzleSpeed:0.0} m/s", sBig);
            GUI.color = Dim;
            GUI.Label(new Rect(ix + 186f, iy + 6f, w, 24f),
                $"= {weapon.MuzzleSpeedFps:0} fps", sLabel);
            GUI.color = Color.white;
            iy += 32f;

            GUI.color = Dim;
            GUI.Label(new Rect(ix, iy, w - 32f, 20f), "v₀ = √(2E/m)", sSmall);
            GUI.color = Color.white;
        }

        void DrawHopUpGauge(float x, float y)
        {
            const float w = 356f, h = 134f;
            Panel(new Rect(x, y, w, h));

            float ix = x + 16f, iy = y + 12f;
            GUI.Label(new Rect(ix, iy, w, 22f), "HOP-UP", sTitle);

            if (weapon == null) return;

            float pct = weapon.hopUpPercent;
            Color c = AirsoftWeapon.HopUpColor(pct);

            GUI.color = c;
            GUI.Label(new Rect(ix + 180f, iy - 6f, 150f, 34f), $"{pct:0}%", sBig);
            GUI.color = Color.white;
            iy += 34f;

            // barra
            Rect bar = new Rect(ix, iy, w - 32f, 16f);
            GUI.color = new Color(1f, 1f, 1f, 0.10f);
            GUI.DrawTexture(bar, texBar);

            // faixa "ideal" (calibrada por simulação: ~50-60% deixa a trajetória plana)
            GUI.color = new Color(0.3f, 1f, 0.45f, 0.22f);
            GUI.DrawTexture(new Rect(bar.x + bar.width * 0.50f, bar.y, bar.width * 0.10f, bar.height), texBar);

            GUI.color = c;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * (pct / 100f), bar.height), texFill);
            GUI.color = Color.white;
            iy += 22f;

            GUI.color = Dim;
            GUI.Label(new Rect(ix, iy, 120f, 18f), "pouco", sSmall);
            GUI.Label(new Rect(ix + bar.width * 0.5f - 24f, iy, 90f, 18f), "ideal", sSmall);
            GUI.Label(new Rect(ix + bar.width - 46f, iy, 60f, 18f), "muito", sSmall);
            GUI.color = Color.white;
            iy += 22f;

            string modelo = weapon.magnusModel == MagnusModel.Simplificado ? "simplificado" : "completo";
            string val = weapon.magnusModel == MagnusModel.Simplificado
                ? $"BackspinDrag {weapon.BackspinDrag:E2}"
                : $"ω {weapon.SpinRate:0} rad/s ({weapon.SpinRate * 9.5493f:0} rpm)";
            GUI.color = Dim;
            GUI.Label(new Rect(ix, iy, w - 32f, 20f), $"Magnus {modelo} · {val}", sSmall);
            GUI.color = Color.white;
        }

        void DrawLastShot(float W, float H)
        {
            if (!hasLast) return;

            const float w = 420f, h = 92f;
            Rect r = new Rect(W * 0.5f - w * 0.5f, H - h - 20f, w, h);
            Panel(r);

            float ix = r.x + 18f, iy = r.y + 10f;
            string titulo = !last.landed ? "ÚLTIMO DISPARO — não aterrissou"
                          : last.hitTarget ? $"ACERTOU O {last.hitLabel.ToUpper()}"
                          : "ÚLTIMO DISPARO";
            GUI.Label(new Rect(ix, iy, w, 20f), titulo, sTitle);
            iy += 22f;

            GUI.color = !last.landed ? Warn : last.hitTarget ? Gold : Neon;
            GUI.Label(new Rect(ix, iy, 260f, 36f), $"{last.distance:0.0} m", sBig);
            GUI.color = Color.white;

            GUI.color = Dim;
            if (last.hitTarget)
            {
                GUI.color = Gold;
                GUI.Label(new Rect(ix + 150f, iy + 2f, 280f, 20f),
                    $"{last.ring}   +{last.points} pontos", sSmall);
                GUI.color = Dim;
                GUI.Label(new Rect(ix + 150f, iy + 20f, 280f, 20f),
                    $"hop-up {last.hopUpPercent:0}%   ·   voo {last.flightTime:0.00} s", sSmall);
            }
            else
            {
                GUI.Label(new Rect(ix + 150f, iy + 2f, 280f, 20f),
                    $"hop-up {last.hopUpPercent:0}%   ·   voo {last.flightTime:0.00} s", sSmall);
                GUI.Label(new Rect(ix + 150f, iy + 20f, 280f, 20f),
                    $"ápice {last.apexHeight:0.00} m   ·   impacto {last.impactSpeed:0.0} m/s", sSmall);
            }
            GUI.color = Color.white;
        }

        void DrawHistory(float x, float y)
        {
            if (history.Count == 0) return;

            float h = 66f + history.Count * 20f;
            Panel(new Rect(x, y, 350f, h));

            float ix = x + 16f, iy = y + 12f;
            GUI.Label(new Rect(ix, iy, 320f, 20f), "HISTÓRICO", sTitle); iy += 24f;

            GUI.color = Dim;
            GUI.Label(new Rect(ix, iy - 2f, 90f, 18f), "hop-up", sSmall);
            GUI.Label(new Rect(ix + 92f, iy - 2f, 100f, 18f), "distância", sSmall);
            GUI.Label(new Rect(ix + 202f, iy - 2f, 110f, 18f), "ápice", sSmall);
            GUI.color = Color.white;
            iy += 18f;

            for (int i = 0; i < history.Count; i++)
            {
                ShotResult r = history[i];
                GUI.color = i == 0 ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                GUI.Label(new Rect(ix, iy, 90f, 18f), $"{r.hopUpPercent:0}%", sSmall);
                GUI.color = i == 0 ? AirsoftWeapon.HopUpColor(r.hopUpPercent)
                                   : new Color(1f, 1f, 1f, 0.55f);
                GUI.Label(new Rect(ix + 92f, iy, 100f, 18f),
                    r.landed ? $"{r.distance:0.0} m" : "—", sSmall);
                GUI.color = i == 0 ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                GUI.Label(new Rect(ix + 202f, iy, 110f, 18f), $"{r.apexHeight:0.00} m", sSmall);
                iy += 20f;
            }
            GUI.color = Color.white;
        }

        void DrawHelp(float x, float y)
        {
            Panel(new Rect(x, y, 350f, 216f));
            float ix = x + 16f, iy = y + 12f;
            GUI.Label(new Rect(ix, iy, 320f, 20f), "CONTROLES  (H oculta)", sTitle); iy += 26f;

            string[,] rows =
            {
                { "Mouse",              "olhar" },
                { "W A S D / Shift",    "andar / correr" },
                { "Botão esquerdo",     "atirar" },
                { "Roda do mouse",      "regular o hop-up" },
                { "↑ ↓",                "hop-up fino (1%)" },
                { "F",                  "nivelar o cano" },
                { "M",                  "alternar modelo de Magnus" },
                { "V",                  "câmera lateral (análise)" },
                { "R",                  "limpar rastros e marcadores" },
                { "Esc",                "soltar o cursor" },
            };

            for (int i = 0; i < rows.GetLength(0); i++)
            {
                GUI.color = Neon;
                GUI.Label(new Rect(ix, iy, 150f, 18f), rows[i, 0], sSmall);
                GUI.color = Dim;
                GUI.Label(new Rect(ix + 150f, iy, 190f, 18f), rows[i, 1], sSmall);
                iy += 18f;
            }
            GUI.color = Color.white;
        }

        void DrawScore(float W)
        {
            if (Target.TotalHits == 0) return;

            Rect r = new Rect(W * 0.5f - 150f, 16f, 300f, 34f);
            Panel(r);
            GUI.color = Gold;
            GUI.Label(new Rect(r.x, r.y + 8f, r.width, 22f),
                $"ALVOS  {Target.TotalHits} acertos  ·  {Target.TotalPoints} pontos", sHint);
            GUI.color = Color.white;
        }

        void DrawBadge(float W, string text)
        {
            float y = Target.TotalHits > 0 ? 58f : 18f;   // não sobrepor o placar
            Rect r = new Rect(W * 0.5f - 190f, y, 380f, 30f);
            Panel(r);
            GUI.color = Warn;
            GUI.Label(new Rect(r.x, r.y + 6f, r.width, 20f), text, sHint);
            GUI.color = Color.white;
        }

        // ------------------------------------------------------------------

        void Row(float x, ref float y, float w, string label, string value)
        {
            GUI.color = Dim;
            GUI.Label(new Rect(x, y, 200f, 20f), label, sLabel);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, w - 48f, 20f), value, sValue);
            y += 20f;
        }

        void Panel(Rect r)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.92f);
            GUI.DrawTexture(r, texPanel);
            GUI.color = new Color(Neon.r, Neon.g, Neon.b, 0.30f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), texLine);
            GUI.color = Color.white;
        }

        void EnsureStyles()
        {
            if (stylesReady) return;

            texPanel = Solid(new Color(0.03f, 0.05f, 0.08f, 0.80f));
            texBar = Solid(Color.white);
            texFill = Solid(Color.white);
            texLine = Solid(Color.white);

            sTitle = new GUIStyle(GUI.skin.label)
            { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.55f, 0.75f, 0.9f) } };
            sLabel = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            sValue = new GUIStyle(GUI.skin.label)
            { fontSize = 14, alignment = TextAnchor.UpperRight, normal = { textColor = Color.white } };
            sBig = new GUIStyle(GUI.skin.label)
            { fontSize = 27, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            sSmall = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
            sHint = new GUIStyle(GUI.skin.label)
            { fontSize = 13, alignment = TextAnchor.UpperCenter, normal = { textColor = Color.white } };

            stylesReady = true;
        }

        static Texture2D Solid(Color c)
        {
            Texture2D t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
    }
}
