using System.Collections.Generic;
using UnityEngine;

namespace Airsoft
{
    /// <summary>
    /// Marcador neon deixado onde a BB tocou o solo. Dá um "pop" ao nascer,
    /// fica um tempo e depois some. Serve para comparar visualmente onde cada
    /// regulagem de hop-up derrubou a bala.
    /// </summary>
    public class ImpactMarker : MonoBehaviour
    {
        public float lifetime = 16f;
        public float fadeStart = 11f;
        public float popDuration = 0.25f;
        public float popScale = 1.6f;

        [Tooltip("Máximo de marcadores vivos ao mesmo tempo. Em rajada eles se acumulam " +
                 "e viram uma parede de luz; os mais antigos somem primeiro.")]
        public int maxLive = 14;

        static readonly List<ImpactMarker> live = new List<ImpactMarker>();

        Renderer[] renderers;
        Color[] baseColors;
        MaterialPropertyBlock mpb;
        Vector3 baseScale;
        float born;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            baseColors = new Color[renderers.Length];
            mpb = new MaterialPropertyBlock();

            for (int i = 0; i < renderers.Length; i++)
            {
                Material m = renderers[i].sharedMaterial;
                baseColors[i] = (m != null && m.HasProperty(BaseColorId))
                    ? m.GetColor(BaseColorId)
                    : Color.white;
            }

            baseScale = transform.localScale;
            born = Time.time;

            live.Add(this);
            while (live.Count > maxLive)
            {
                ImpactMarker oldest = live[0];
                live.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }
        }

        void Update()
        {
            float age = Time.time - born;

            // "Pop" de entrada
            float pop = age < popDuration
                ? Mathf.Lerp(popScale, 1f, age / popDuration)
                : 1f;
            transform.localScale = baseScale * pop;

            // Desvanecimento
            float alpha = age < fadeStart
                ? 1f
                : Mathf.Clamp01(1f - (age - fadeStart) / Mathf.Max(0.01f, lifetime - fadeStart));

            for (int i = 0; i < renderers.Length; i++)
            {
                Color c = baseColors[i];
                c.a *= alpha;
                // O material é aditivo, então reduzir o alpha apaga o brilho suavemente.
                c.r *= alpha; c.g *= alpha; c.b *= alpha;

                renderers[i].GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, c);
                renderers[i].SetPropertyBlock(mpb);
            }

            if (age >= lifetime) Destroy(gameObject);
        }

        void OnDestroy() => live.Remove(this);
    }
}
