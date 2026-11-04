using MxR.Common;
using Oculus.Interaction;
using UnityEngine;

namespace MxR.Printer
{
    /// <summary>
    /// Makes the printer's display a touchscreen: tapping it (finger or controller) starts, pauses or
    /// resumes a print.
    ///
    /// The poke logic is the Interaction SDK sample poke button (PokeInteractable on a clipped plane
    /// surface + click sound). It is spawned on the screen surface, scaled to the screen size, and its
    /// own visuals are hidden – the display itself is the button. Select = the finger/controller pushed
    /// through the screen surface far enough. Needs the "Interactions Rig" building block (poke interactors).
    ///
    /// Feedback on the display itself: the screen glows brighter while a finger hovers over it and
    /// flashes when it is pressed (emission of the display material), plus the click sound and vibration.
    /// </summary>
    [RequireComponent(typeof(PrinterController))]
    public class PrinterTouchscreen : MonoBehaviour
    {
        const string SampleButtonPath =
            "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Prefabs/OculusInteractionSamplePokeButton.prefab";
        // the sample button's pressable area at scale 1 (3 x 1 units of its 0.05 m root scale)
        static readonly Vector2 SampleButtonSize = new Vector2(0.15f, 0.05f);

        [Tooltip("Prefab with a PokeInteractable. Default: Interaction SDK sample poke button.")]
        [SerializeField] GameObject buttonPrefab;
        [Tooltip("Screen normal in printer space (measured in Blender: faces front, tilted up ~15 deg).")]
        [SerializeField] Vector3 screenNormal = new Vector3(-0.089f, 0.267f, 0.960f);
        [Tooltip("Touch area (m). The screen is 0.079 x 0.049 m; slightly larger is easier to hit.")]
        [SerializeField] Vector2 touchSize = new Vector2(0.09f, 0.06f);

        [Header("Screen feedback")]
        [Tooltip("Emission multiplier while a finger / controller hovers over the screen.")]
        [SerializeField] float hoverGlow = 2f;
        [Tooltip("Emission multiplier right after a press (fades back).")]
        [SerializeField] float pressGlow = 4.5f;
        [SerializeField] float glowFadeSpeed = 6f;

        [Header("Haptics (controllers only)")]
        [SerializeField, Range(0f, 1f)] float hapticAmplitude = 0.6f;
        [SerializeField] float hapticDuration = 0.08f;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        PrinterController printer;
        PokeInteractable poke;
        Material screenMaterial;     // own instance of the display's emissive material
        Color baseEmission;
        float glow = 1f, flash;      // current multiplier, press flash 0..1
        bool hovered;

        /// <summary>The poke surface on the display (null if it could not be created).</summary>
        public PokeInteractable Poke => poke;

#if UNITY_EDITOR
        void Reset() => buttonPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SampleButtonPath);
#endif

        void Awake()
        {
            printer = GetComponent<PrinterController>();
            Transform anchor = transform.Find("DisplayAnchor");
            if (!buttonPrefab || !anchor)
            {
                Debug.LogError("[PrinterTouchscreen] Needs a button prefab and the model's DisplayAnchor.", this);
                return;
            }

            Vector3 n = screenNormal.normalized;
            GameObject touch = Instantiate(buttonPrefab, transform);
            touch.name = "DisplayTouchArea";
            // the sample button is pressed from its -Z side: -Z = screen normal (out of the screen);
            // 2 mm in front of the screen so the surface is not inside the model
            touch.transform.SetLocalPositionAndRotation(anchor.localPosition + n * 0.002f,
                Quaternion.LookRotation(-n, Vector3.up));
            Vector3 s = touch.transform.localScale;   // keep the depth (z) so press distances stay as designed
            touch.transform.localScale = new Vector3(s.x * touchSize.x / SampleButtonSize.x,
                s.y * touchSize.y / SampleButtonSize.y, s.z);

            // invisible: the display is the button (disabled before PrinterPlacer hides/restores renderers)
            foreach (var r in touch.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            poke = touch.GetComponentInChildren<PokeInteractable>(true);
            if (poke == null) Debug.LogError("[PrinterTouchscreen] Button prefab has no PokeInteractable.", this);

            FindScreenMaterial();
        }

        /// <summary>The display mesh's emissive material slot (the glowing screen), as an own instance.</summary>
        void FindScreenMaterial()
        {
            Transform display = transform.Find("Display");
            if (!display || !display.TryGetComponent(out Renderer r)) return;
            Material[] mats = r.materials;           // instances, so the shared FBX material stays unchanged
            foreach (Material m in mats)
            {
                if (m && m.IsKeywordEnabled("_EMISSION") && m.HasProperty(EmissionColor))
                {
                    screenMaterial = m;
                    baseEmission = m.GetColor(EmissionColor);
                    return;
                }
            }
        }

        void OnEnable()
        {
            if (!poke) return;
            poke.WhenPointerEventRaised += OnPointerEvent;
            poke.WhenStateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            if (!poke) return;
            poke.WhenPointerEventRaised -= OnPointerEvent;
            poke.WhenStateChanged -= OnStateChanged;
        }

        void OnStateChanged(InteractableStateChangeArgs args) =>
            hovered = args.NewState == InteractableState.Hover || args.NewState == InteractableState.Select;

        void OnPointerEvent(PointerEvent evt)
        {
            if (evt.Type != PointerEventType.Select) return;
            printer.TogglePrint();
            flash = 1f;
            ControllerHaptics.Pulse(this, hapticAmplitude, hapticDuration);
        }

        void Update()
        {
            if (!screenMaterial) return;
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * glowFadeSpeed * 0.5f);
            float target = Mathf.Max(hovered ? hoverGlow : 1f, Mathf.Lerp(1f, pressGlow, flash));
            float next = Mathf.MoveTowards(glow, target, Time.deltaTime * glowFadeSpeed * pressGlow);
            if (Mathf.Approximately(next, glow)) return;
            glow = next;
            screenMaterial.SetColor(EmissionColor, baseEmission * glow);
        }
    }
}
