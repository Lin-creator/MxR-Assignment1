using System.Collections.Generic;
using UnityEngine;

namespace MxR.Common
{
    /// <summary>
    /// Visual feedback for <see cref="RayDraggable"/>: lightens all renderers of the object while a ray
    /// hovers it and tints them blue while it is being dragged.
    ///
    /// Uses MaterialPropertyBlocks per material slot, so the shared material assets stay unchanged and
    /// every slot keeps its own base colour. Works with URP Lit / Unlit (property _BaseColor).
    /// </summary>
    public class HoverTint : MonoBehaviour
    {
        public enum Mode { None, Hover, Active }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color ActiveColor = new Color(0.35f, 0.7f, 1f);

        const float HoverLighten = 0.25f;     // blend towards white
        const float ActiveBlend = 0.35f;      // blend towards blue
        const float FadeSpeed = 12f;

        struct Slot { public Renderer renderer; public int index; public Material material; }

        readonly List<Slot> slots = new List<Slot>();
        MaterialPropertyBlock block;
        Mode mode;
        float hover, active;   // current fade amounts 0..1

        void Awake() => block = new MaterialPropertyBlock();

        /// <summary>Collects the renderers again (call after children were added, e.g. a stand).</summary>
        public void Refresh(GameObject root)
        {
            Clear();
            slots.Clear();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.GetComponent<TMPro.TMP_Text>()) continue;   // keep text crisp
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    // skip transparent materials (e.g. model previews): they set their own alpha per renderer
                    if (mats[i] && mats[i].HasProperty(BaseColor) && mats[i].renderQueue < 3000)
                        slots.Add(new Slot { renderer = r, index = i, material = mats[i] });
            }
        }

        public void Set(Mode newMode)
        {
            mode = newMode;
            enabled = true;
        }

        void Update()
        {
            float step = FadeSpeed * Time.deltaTime;
            hover = Mathf.MoveTowards(hover, mode == Mode.Hover ? 1f : 0f, step);
            active = Mathf.MoveTowards(active, mode == Mode.Active ? 1f : 0f, step);

            if (hover == 0f && active == 0f) { Clear(); enabled = false; return; }

            foreach (var s in slots)
            {
                if (!s.renderer || !s.material) continue;
                // read the colour every frame: some materials change at runtime (e.g. the status light)
                Color baseColor = s.material.GetColor(BaseColor);
                Color c = Color.Lerp(baseColor, Color.white, hover * HoverLighten);
                c = Color.Lerp(c, ActiveColor * baseColor.maxColorComponent + ActiveColor * 0.3f, active * ActiveBlend);
                c.a = baseColor.a;
                s.renderer.GetPropertyBlock(block, s.index);
                block.SetColor(BaseColor, c);
                s.renderer.SetPropertyBlock(block, s.index);
            }
        }

        void Clear()
        {
            foreach (var s in slots)
                if (s.renderer) s.renderer.SetPropertyBlock(null, s.index);
        }
    }
}
