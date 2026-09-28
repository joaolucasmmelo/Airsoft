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
        public ArmasDoJogador armas;
        public PlayerController player;

        // A arma que está na mão agora
        AirsoftWeapon weapon
        {
            get { return armas != null ? armas.ArmaAtual : null; }
        }

        // Mensagem temporária no meio da tela (ex.: "SEM MUNIÇÃO")
        static string mensagem = "";
        static float mensagemAte = 0f;

        /// <summary>Mostra um aviso no meio da tela por 2 segundos. Qualquer script pode chamar.</summary>
        public static void Mensagem(string texto)
        {
            mensagem = texto;
            mensagemAte = Time.time + 2f;
        }


        ShotResult last;
        bool hasLast;

        // --- recursos de desenho, criados uma vez ---
        Texture2D texPanel, texBar, texFill, texLine;
        GUIStyle sTitle, sLabel, sValue, sBig, sBigRight, sSmall, sSmallCenter, sSmallRight, sAviso;
        bool stylesReady;

        /// <summary>Largura comum dos painéis. Todo o texto interno respeita Pad de cada lado.</summary>
        const float PanelW = 368f;
        const float Pad = 16f;
        const float HopUpH = 128f;     // arma + munição + medidor de hop-up
        const float LastShotH = 152f;  // título + distância + 3 linhas

        static readonly Color Neon = new Color(0.20f, 0.80f, 1f);
        static readonly Color Dim = new Color(0.65f, 0.75f, 0.85f);
        static readonly Color Warn = new Color(1f, 0.72f, 0.25f);
        static readonly Color Gold = new Color(1f, 0.85f, 0.35f);

        void OnEnable() => BBProjectile.ShotResolved += OnShot;
        void OnDisable() => BBProjectile.ShotResolved -= OnShot;

        void OnShot(ShotResult r)
        {
            last = r;
            hasLast = true;

            Debug.Log(r.landed
                ? $"[Airsoft] Hop-up {r.hopUpPercent:0}%  |  BB {r.massKg * 1000f:0.00} g  |  BackspinDrag {r.backspinDrag:0}  |  " +
                  $"{r.hitLabel} a {r.distance:0.0} m  |  voo {r.flightTime:0.00} s  |  " +
                  $"ápice {r.apexHeight:0.00} m  |  impacto a {r.impactSpeed:0.0} m/s"
                : $"[Airsoft] Hop-up {r.hopUpPercent:0}%  |  a BB NÃO tocou o solo em {r.flightTime:0.0} s " +
                  $"(hop-up excessivo). Já havia percorrido {r.distance:0.0} m.");
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

            // Na tela ficam só a mira, o card da arma/hop-up e o último disparo (canto inferior esquerdo).
            // Controles e dados das armas ficam nos quadros da parede do START.
            float yHopUp = H - HopUpH - 16f;

            DrawCrosshair(W, H);
            DrawHopUpGauge(16f, yHopUp);
            DrawLastShot(16f, yHopUp - LastShotH - 12f);
            DrawAvisos(W, H);

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
        }

        void DrawAvisos(float W, float H)
        {
            float y = H * 0.5f + 40f;

            // Mensagem temporária
            if (Time.time < mensagemAte)
            {
                Rect r = new Rect(W * 0.5f - 220f, y, 440f, 34f);
                Panel(r);
                GUI.color = Warn;
                GUI.Label(new Rect(r.x, r.y + 6f, r.width, 24f), mensagem, sAviso);
                GUI.color = Color.white;
                y += 42f;
            }

            // Dica quando está perto de um carregador
            if (armas == null)
                return;

            CarregadorNoChao perto = armas.CarregadorMaisPerto();
            if (perto != null)
            {
                int tecla = armas.IndiceDaArmaDoTipo(perto.carregador.tipo) + 1;
                string nome = armas.armas[tecla - 1].modelo;
                Rect r = new Rect(W * 0.5f - 220f, y, 440f, 34f);
                Panel(r);
                GUI.Label(new Rect(r.x, r.y + 6f, r.width, 24f),
                    "E  pegar carregador de " + nome + " (" + perto.carregador.quantidade + " balas)", sAviso);
            }
        }

        void DrawHopUpGauge(float x, float y)
        {
            Panel(new Rect(x, y, PanelW, HopUpH));

            float ix = x + Pad, iy = y + 12f;
            float inner = PanelW - Pad * 2f;

            if (weapon == null) return;

            // Linha 1: arma e massa da BB à esquerda, modo de tiro à direita
            GUI.Label(new Rect(ix, iy, inner, 20f),
                weapon.modelo.ToUpper() + "   ·   BB " + (weapon.BbMassKg * 1000f).ToString("0.00") + " g", sTitle);
            GUI.color = weapon.automatico ? Gold : Neon;
            GUI.Label(new Rect(ix, iy, inner, 20f), weapon.automatico ? "AUTO" : "SEMI", sValue);
            GUI.color = Color.white;
            iy += 22f;

            // Linha 2: munição grande à esquerda, hop-up grande à direita
            string municao;
            if (weapon.carregador == null)
                municao = "SEM CARREGADOR";
            else if (weapon.municaoInfinita)
                municao = "∞";
            else
                municao = weapon.carregador.quantidade + " / " + weapon.carregador.capacidade;

            bool vazio = weapon.carregador == null || (!weapon.municaoInfinita && !weapon.carregador.TemBala());
            GUI.color = vazio ? Warn : Color.white;
            GUI.Label(new Rect(ix, iy, inner, 34f), municao, sBig);

            float pct = weapon.hopUpPercent;
            Color c = AirsoftWeapon.HopUpColor(pct);
            GUI.color = Dim;
            GUI.Label(new Rect(ix, iy + 12f, inner - 86f, 18f), "hop-up", sSmallRight);
            GUI.color = c;
            GUI.Label(new Rect(ix, iy, inner, 34f), pct.ToString("0") + "%", sBigRight);
            GUI.color = Color.white;
            iy += 40f;

            // barra
            Rect bar = new Rect(ix, iy, inner, 16f);
            GUI.color = new Color(1f, 1f, 1f, 0.10f);
            GUI.DrawTexture(bar, texBar);

            // Faixa "ideal": a regulagem de referência para o alvo do meio do estande.
            // Nesta calibragem a trajetória é rasa na faixa inteira (ápice de 2,9 m no
            // pior caso), então a marca é uma referência, não um limite.
            GUI.color = new Color(0.3f, 1f, 0.45f, 0.22f);
            GUI.DrawTexture(new Rect(bar.x + bar.width * 0.50f, bar.y, bar.width * 0.10f, bar.height), texBar);

            GUI.color = c;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * (pct / 100f), bar.height), texFill);
            GUI.color = Color.white;
            iy += 22f;

            GUI.color = Dim;
            GUI.Label(new Rect(ix, iy, 100f, 18f), "pouco", sSmall);
            GUI.Label(new Rect(ix, iy, inner, 18f), "ideal", sSmallCenter);
            GUI.Label(new Rect(ix, iy, inner, 18f), "muito", sSmallRight);
            GUI.color = Color.white;
        }

        void DrawLastShot(float x, float y)
        {
            if (!hasLast) return;

            Panel(new Rect(x, y, PanelW, LastShotH));

            float ix = x + Pad, iy = y + 12f;
            string titulo = !last.landed ? "ÚLTIMO DISPARO — não aterrissou"
                          : last.hitTarget ? $"ACERTOU O {last.hitLabel.ToUpper()}"
                          : "ÚLTIMO DISPARO";
            GUI.Label(new Rect(ix, iy, PanelW - Pad * 2f, 20f), titulo, sTitle);
            iy += 24f;

            GUI.color = !last.landed ? Warn : last.hitTarget ? Gold : Neon;
            GUI.Label(new Rect(ix, iy, PanelW - Pad * 2f, 34f), $"{last.distance:0.0} m", sBig);
            GUI.color = Color.white;
            iy += 38f;

            Row(ix, ref iy, "hop-up", $"{last.hopUpPercent:0}%");
            Row(ix, ref iy, "tempo de voo", $"{last.flightTime:0.00} s");
            Row(ix, ref iy, "ápice", $"{last.apexHeight:0.00} m");
        }

        // ------------------------------------------------------------------

        /// <summary>Linha "rótulo à esquerda / valor à direita" dentro da largura útil do painel.</summary>
        void Row(float x, ref float y, string label, string value)
        {
            float inner = PanelW - Pad * 2f;
            GUI.color = Dim;
            GUI.Label(new Rect(x, y, inner * 0.62f, 20f), label, sLabel);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, inner, 20f), value, sValue);
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
            sBigRight = new GUIStyle(sBig) { alignment = TextAnchor.UpperRight };
            sSmall = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
            sSmallCenter = new GUIStyle(sSmall) { alignment = TextAnchor.UpperCenter };
            sAviso = new GUIStyle(GUI.skin.label)
            { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, normal = { textColor = Color.white } };
            sSmallRight = new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight };

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
