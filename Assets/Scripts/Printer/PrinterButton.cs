using MxR.Common;
using Oculus.Interaction;
using TMPro;
using UnityEngine;

namespace MxR.Printer
{
    /// <summary>
    /// Poke button next to the printer (Interaction SDK): pressing it starts, pauses or resumes a print.
    ///
    /// The button is the Interaction SDK sample "OculusInteractionSamplePokeButton" (PokeInteractable +
    /// press animation + click sound). It is spawned as a child of the printer, so it moves with it.
    /// We listen to the PokeInteractable's pointer events: Select = the finger/controller pushed the
    /// button surface in far enough. Poking needs the "Interactions Rig" building block (poke interactors
    /// for hands and controllers) on the camera rig.
    /// </summary>
    [RequireComponent(typeof(PrinterController))]
    public class PrinterButton : MonoBehaviour
    {
        const string SampleButtonPath =
            "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Prefabs/OculusInteractionSamplePokeButton.prefab";

        [Tooltip("Prefab with a PokeInteractable. Default: Interaction SDK sample poke button.")]
        [SerializeField] GameObject buttonPrefab;
        [Tooltip("Position relative to the printer (x = left/right, y = up, z = front). Negative x = user's right.")]
        [SerializeField] Vector3 localPosition = new Vector3(-0.27f, 0.05f, 0.12f);
        [Tooltip("The sample button faces -Z: y = 180 turns it to the printer's front, x tilts it up towards the user.")]
        [SerializeField] Vector3 localEuler = new Vector3(40f, 180f, 0f);
        [SerializeField] float buttonScale = 1f;

        [Header("Labels")]
        [SerializeField] string labelIdle = "Print";
        [SerializeField] string labelPrinting = "Pause";
        [SerializeField] string labelPaused = "Resume";
        [SerializeField] string labelFinished = "Print again";

        [Header("Haptics (controllers only)")]
        [SerializeField, Range(0f, 1f)] float hapticAmplitude = 0.6f;
        [SerializeField] float hapticDuration = 0.08f;

        PrinterController printer;
        PokeInteractable poke;
        TMP_Text label;

#if UNITY_EDITOR
        void Reset() => buttonPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SampleButtonPath);
#endif

        void Awake()
        {
            printer = GetComponent<PrinterController>();
            if (buttonPrefab == null)
            {
                Debug.LogError("[PrinterButton] No button prefab assigned.", this);
                return;
            }

            GameObject button = Instantiate(buttonPrefab, transform);
            button.name = "PrintButton";
            button.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(localEuler));
            button.transform.localScale *= buttonScale;

            poke = button.GetComponentInChildren<PokeInteractable>(true);
            label = button.GetComponentInChildren<TMP_Text>(true);
            if (poke == null) Debug.LogError("[PrinterButton] Button prefab has no PokeInteractable.", this);
        }

        void OnEnable()
        {
            printer.onStateChanged.AddListener(UpdateLabel);
            if (poke) poke.WhenPointerEventRaised += OnPointerEvent;
        }

        void OnDisable()
        {
            printer.onStateChanged.RemoveListener(UpdateLabel);
            if (poke) poke.WhenPointerEventRaised -= OnPointerEvent;
        }

        void Start() => UpdateLabel(printer.State);

        void OnPointerEvent(PointerEvent evt)
        {
            if (evt.Type != PointerEventType.Select) return;
            printer.TogglePrint();
            ControllerHaptics.Pulse(this, hapticAmplitude, hapticDuration);
        }

        void UpdateLabel(PrinterController.PrinterState state)
        {
            if (!label) return;
            label.text = state switch
            {
                PrinterController.PrinterState.Printing => labelPrinting,
                PrinterController.PrinterState.Paused => labelPaused,
                PrinterController.PrinterState.Finished => labelFinished,
                _ => labelIdle,
            };
        }
    }
}
