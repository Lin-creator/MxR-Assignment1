using MxR.Common;
using TMPro;
using UnityEngine;

namespace MxR.Printer
{
    /// <summary>
    /// Tooltip that tells the user the finished print can be picked up: a small label ("Grab me!")
    /// floats and bobs above the part on the bed, always facing the user, with an arrow pointing down
    /// at it. It pops up when a grabbable print is finished and disappears as soon as the part is taken.
    /// </summary>
    [RequireComponent(typeof(PrinterController))]
    public class GrabHint : MonoBehaviour
    {
        [SerializeField] Material backgroundMaterial;
        [SerializeField] string title = "Grab me!";
        [SerializeField] string subtitle = "pinch or grip button";
        [SerializeField] Color titleColor = new Color(0.92f, 0.95f, 1f);
        [SerializeField] Color subtitleColor = new Color(0.45f, 0.75f, 1f);
        [Tooltip("Gap between the top of the part and the bottom of the label (m).")]
        [SerializeField] float heightAbovePart = 0.035f;
        [SerializeField] float bobAmplitude = 0.005f;
        [SerializeField] float popTime = 0.2f;

        static readonly Vector2 PanelSize = new Vector2(0.11f, 0.042f);

        PrinterController printer;
        Transform hint;
        float scale;

#if UNITY_EDITOR
        void Reset() =>
            backgroundMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SignBoard.mat");
#endif

        void Awake() => printer = GetComponent<PrinterController>();

        void LateUpdate()
        {
            PrintedPart part = printer.PartOnBed;
            bool show = printer.State == PrinterController.PrinterState.Finished && printer.PrintsGrabbableParts
                        && part && !part.TakenFromBed;

            scale = Mathf.MoveTowards(scale, show ? 1f : 0f, Time.deltaTime / popTime);
            if (scale <= 0f)
            {
                if (hint && hint.gameObject.activeSelf) hint.gameObject.SetActive(false);
                return;
            }
            if (!hint) Build();
            if (!hint.gameObject.activeSelf) hint.gameObject.SetActive(true);
            hint.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, scale);

            if (part)
            {
                // above the part's top, bobbing; content faces -Z, so +Z points away from the user's eyes
                Bounds b = WorldBounds(part.transform);
                float bob = Mathf.Sin(Time.time * 3f) * bobAmplitude;
                Vector3 pos = new Vector3(b.center.x, b.max.y + heightAbovePart + PanelSize.y * 0.5f + bob, b.center.z);
                Camera cam = Camera.main;
                Vector3 away = cam ? pos - cam.transform.position : transform.forward * -1f;
                hint.SetPositionAndRotation(pos, away.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(away, Vector3.up) : hint.rotation);
            }
        }

        void Build()
        {
            hint = new GameObject("GrabHint").transform;
            hint.SetParent(transform, false);

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            Destroy(panel.GetComponent<Collider>());
            panel.transform.SetParent(hint, false);
            panel.transform.localPosition = new Vector3(0f, 0f, 0.002f);
            panel.transform.localScale = new Vector3(PanelSize.x, PanelSize.y, 0.003f);
            if (backgroundMaterial) panel.GetComponent<Renderer>().sharedMaterial = backgroundMaterial;

            float w = PanelSize.x - 0.01f;
            TextFactory.Create("Title", hint, new Vector3(0f, 0.008f, -0.001f), new Vector2(w, 0.022f),
                0.17f, TextAlignmentOptions.Center, titleColor, FontStyles.Bold).text = title;
            TextFactory.Create("Subtitle", hint, new Vector3(0f, -0.011f, -0.001f), new Vector2(w, 0.014f),
                0.09f, TextAlignmentOptions.Center, subtitleColor).text = subtitle;
            // arrow pointing down at the part
            TextFactory.Create("Arrow", hint, new Vector3(0f, -PanelSize.y * 0.5f - 0.009f, -0.001f), new Vector2(0.03f, 0.02f),
                0.16f, TextAlignmentOptions.Center, subtitleColor, FontStyles.Bold).text = "v";
        }

        static Bounds WorldBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
