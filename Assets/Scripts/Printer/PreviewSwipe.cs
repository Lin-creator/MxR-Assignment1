using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

namespace MxR.Printer
{
    /// <summary>
    /// Choose the model to print by swiping over the ghost previews on the bed.
    ///
    /// Uses the Interaction SDK's poke interactors (index fingertip with hands, the controller tip with
    /// controllers) as fingers: when one enters a box around the previews, the previews light up; moving
    /// it sideways drags the carousel along (direct manipulation), every half spacing a model is passed
    /// and selected (tick + vibration). Leaving the box lets the carousel snap to the selected model.
    ///
    /// Only active while a model can be chosen (<see cref="PrintModelSelector.CanSelect"/>), so reaching
    /// for the finished part or a running print does not change the selection.
    /// </summary>
    [RequireComponent(typeof(PrintModelSelector), typeof(PrinterController))]
    public class PreviewSwipe : MonoBehaviour
    {
        [Tooltip("Size of the swipe box around the previews (m): x along the bed, y up, z depth. x = 0: fit to the previews.")]
        [SerializeField] Vector3 boxSize = new Vector3(0f, 0.11f, 0.14f);
        [Tooltip("Sideways movement that passes one model, as a fraction of the preview spacing.")]
        [SerializeField, Range(0.3f, 1f)] float stepFraction = 0.5f;
        [SerializeField] AudioClip tickClip;
        [SerializeField, Range(0f, 1f)] float tickVolume = 0.35f;

        PrintModelSelector selector;
        PrinterController printer;
        readonly List<PokeInteractor> fingers = new List<PokeInteractor>();
        PokeInteractor active;       // finger that is swiping
        float startX;                // its x (bed space) where the current step started
        float nextSearch;
        AudioSource audioSource;

#if UNITY_EDITOR
        void Reset() => tickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/move_pick.wav");
#endif

        void Awake()
        {
            selector = GetComponent<PrintModelSelector>();
            printer = GetComponent<PrinterController>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 0.5f;
        }

        void Update()
        {
            // the rig can switch between hands and controllers, so look for poke interactors now and then
            if (Time.time >= nextSearch)
            {
                nextSearch = Time.time + 2f;
                fingers.Clear();
                fingers.AddRange(FindObjectsByType<PokeInteractor>(FindObjectsSortMode.None));
            }

            Transform bed = printer.Bed;
            if (!bed || !selector.CanSelect) { Release(); return; }

            // swipe box in bed space, centred on the previews
            Vector3 centre = printer.PrintCentreOnBed + Vector3.up * (boxSize.y * 0.5f - 0.01f);
            Vector3 size = boxSize;
            if (size.x <= 0f) size.x = 2f * selector.Spacing + 0.08f;
            var box = new Bounds(centre, size);

            // keep the finger that started the swipe while it stays in the box, otherwise take a new one
            PokeInteractor finger = active && Inside(active, bed, box) ? active : null;
            if (!finger)
                foreach (var f in fingers)
                    if (Inside(f, bed, box)) { finger = f; break; }

            if (!finger) { Release(); return; }

            float x = bed.InverseTransformPoint(finger.Origin).x;
            if (finger != active)
            {
                active = finger;
                startX = x;
                selector.Highlighted = true;
            }

            // the printer's +X is the user's left: moving to the user's right = decreasing x
            float spacing = Mathf.Max(0.01f, selector.Spacing);
            float slots = (startX - x) / spacing;          // positive = finger moved to the user's right
            float step = stepFraction;
            if (slots >= step)        { selector.Previous(); startX -= spacing; Tick(); }   // content follows the finger
            else if (slots <= -step)  { selector.Next(); startX += spacing; Tick(); }
            selector.SetDrag((startX - x) / spacing);
        }

        static bool Inside(PokeInteractor f, Transform bed, Bounds box) =>
            f && f.isActiveAndEnabled && f.State != InteractorState.Disabled && box.Contains(bed.InverseTransformPoint(f.Origin));

        void Release()
        {
            if (!active) return;
            active = null;
            selector.SetDrag(0f);           // snap to the selected model
            selector.Highlighted = false;
        }

        void Tick()
        {
            if (tickClip) audioSource.PlayOneShot(tickClip, tickVolume);
        }
    }
}
