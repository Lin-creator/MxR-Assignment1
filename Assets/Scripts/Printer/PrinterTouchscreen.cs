using System.Collections.Generic;
using MxR.Common;
using Oculus.Interaction;
using TMPro;
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
    /// Feedback on the display itself: the screen "breathes" (slow glow pulse) while it waits for a tap,
    /// glows brighter while a finger hovers over it and flashes when it is pressed (emission of the
    /// display material), plus the click sound and vibration.
    ///
    /// With a <see cref="PrintModelSelector"/> the screen is split into three zones: tapping the left /
    /// right edge (marked with arrows) selects the previous / next model, the middle starts the print.
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
        [Tooltip("Extra emission of the idle pulse (0 = no pulse).")]
        [SerializeField] float pulseAmount = 0.9f;
        [Tooltip("Seconds per pulse.")]
        [SerializeField] float pulsePeriod = 1.6f;

        [Header("Model selection (with PrintModelSelector)")]
        [Tooltip("Width of the left / right scroll zones on the screen (m).")]
        [SerializeField] float sideZoneWidth = 0.022f;
        [SerializeField] Color arrowColor = new Color(0.85f, 0.95f, 1f);

        [Header("Haptics (controllers only)")]
        [SerializeField, Range(0f, 1f)] float hapticAmplitude = 0.6f;
        [SerializeField] float hapticDuration = 0.08f;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        PrinterController printer;
        PrintModelSelector selector;     // optional: left / right zones scroll through the models
        readonly List<PokeInteractable> zones = new List<PokeInteractable>();
        readonly List<System.Action<PointerEvent>> handlers = new List<System.Action<PointerEvent>>();
        readonly HashSet<PokeInteractable> hoveredZones = new HashSet<PokeInteractable>();
        PokeInteractable poke, previousZone, nextZone;
        TextMeshPro previousArrow, nextArrow;
        Material screenMaterial;     // own instance of the display's emissive material
        Color baseEmission;
        float glow = 1f, flash;      // current multiplier, press flash 0..1

        /// <summary>The poke surface of the print button (middle of the display; null if it could not be created).</summary>
        public PokeInteractable Poke => poke;

#if UNITY_EDITOR
        void Reset() => buttonPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SampleButtonPath);
#endif

        void Awake()
        {
            printer = GetComponent<PrinterController>();
            selector = GetComponent<PrintModelSelector>();
            Transform anchor = transform.Find("DisplayAnchor");
            if (!buttonPrefab || !anchor)
            {
                Debug.LogError("[PrinterTouchscreen] Needs a button prefab and the model's DisplayAnchor.", this);
                return;
            }

            if (selector)
            {
                // [ < | print | > ]  – the outer zones scroll through the models
                float middle = touchSize.x - 2f * sideZoneWidth;
                float side = (middle + sideZoneWidth) * 0.5f;
                poke = CreateZone("DisplayTouchArea", anchor, 0f, middle);
                previousZone = CreateZone("DisplayPrevious", anchor, -side, sideZoneWidth);
                nextZone = CreateZone("DisplayNext", anchor, side, sideZoneWidth);
                previousArrow = CreateArrow("ArrowPrevious", anchor, -side, "<");
                nextArrow = CreateArrow("ArrowNext", anchor, side, ">");
            }
            else
            {
                poke = CreateZone("DisplayTouchArea", anchor, 0f, touchSize.x);
            }

            FindScreenMaterial();
        }

        /// <summary>
        /// Spawns the (invisible) sample poke button on the screen surface. x = offset along the screen,
        /// positive = user's right (the zone's local +X; the printer's +X is the user's left).
        /// </summary>
        PokeInteractable CreateZone(string name, Transform anchor, float x, float width)
        {
            Vector3 n = screenNormal.normalized;
            Quaternion rotation = Quaternion.LookRotation(-n, Vector3.up);
            GameObject touch = Instantiate(buttonPrefab, transform);
            touch.name = name;
            // the sample button is pressed from its -Z side: -Z = screen normal (out of the screen);
            // 2 mm in front of the screen so the surface is not inside the model
            touch.transform.SetLocalPositionAndRotation(anchor.localPosition + n * 0.002f + rotation * new Vector3(x, 0f, 0f),
                rotation);
            Vector3 s = touch.transform.localScale;   // keep the depth (z) so press distances stay as designed
            touch.transform.localScale = new Vector3(s.x * width / SampleButtonSize.x,
                s.y * touchSize.y / SampleButtonSize.y, s.z);

            // invisible: the display is the button (disabled before PrinterPlacer hides/restores renderers)
            foreach (var r in touch.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            var zone = touch.GetComponentInChildren<PokeInteractable>(true);
            if (zone == null) Debug.LogError("[PrinterTouchscreen] Button prefab has no PokeInteractable.", this);
            else
            {
                zones.Add(zone);
                handlers.Add(evt => { if (evt.Type == PointerEventType.Select) OnZoneTapped(zone); });
            }
            return zone;
        }

        TextMeshPro CreateArrow(string name, Transform anchor, float x, string text)
        {
            Vector3 n = screenNormal.normalized;
            Quaternion rotation = Quaternion.LookRotation(-n, Vector3.up);
            var arrow = TextFactory.Create(name, transform, anchor.localPosition + n * 0.0012f + rotation * new Vector3(x, 0f, 0f),
                new Vector2(sideZoneWidth, touchSize.y), 0.16f, TextAlignmentOptions.Center, arrowColor, FontStyles.Bold);
            arrow.transform.localRotation = rotation;    // text faces -Z like the touch zones
            arrow.text = text;
            return arrow;
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
            for (int i = 0; i < zones.Count; i++)
            {
                zones[i].WhenPointerEventRaised += handlers[i];
                zones[i].WhenStateChanged += OnStateChanged;
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < zones.Count; i++)
            {
                zones[i].WhenPointerEventRaised -= handlers[i];
                zones[i].WhenStateChanged -= OnStateChanged;
            }
            hoveredZones.Clear();
        }

        void OnStateChanged(InteractableStateChangeArgs args)
        {
            // the event does not say which zone changed, so ask all of them
            hoveredZones.Clear();
            foreach (var zone in zones)
                if (zone.State == InteractableState.Hover || zone.State == InteractableState.Select)
                    hoveredZones.Add(zone);
        }

        void OnZoneTapped(PokeInteractable zone)
        {
            bool scrolling = selector && selector.CanSelect;
            if (scrolling && zone == previousZone) selector.Previous();
            else if (scrolling && zone == nextZone) selector.Next();
            else printer.TogglePrint();            // middle zone – or any zone while a print is running
            flash = 1f;
            ControllerHaptics.Pulse(this, hapticAmplitude, hapticDuration);
        }

        void Update()
        {
            // arrows only while a model can be chosen
            bool arrows = selector && selector.CanSelect;
            if (previousArrow && previousArrow.gameObject.activeSelf != arrows) previousArrow.gameObject.SetActive(arrows);
            if (nextArrow && nextArrow.gameObject.activeSelf != arrows) nextArrow.gameObject.SetActive(arrows);

            if (!screenMaterial) return;
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * glowFadeSpeed * 0.5f);

            // "breathing" glow while the screen waits for a tap (ready, paused, done): shows it is a button
            float idle = 1f;
            if (printer.State != PrinterController.PrinterState.Printing)
                idle += pulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.time * 2f * Mathf.PI / pulsePeriod));

            float target = Mathf.Max(hoveredZones.Count > 0 ? hoverGlow : idle, Mathf.Lerp(1f, pressGlow, flash));
            float next = Mathf.MoveTowards(glow, target, Time.deltaTime * glowFadeSpeed * pressGlow);
            if (Mathf.Approximately(next, glow)) return;
            glow = next;
            screenMaterial.SetColor(EmissionColor, baseEmission * glow);
        }
    }
}
