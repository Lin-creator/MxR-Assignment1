using MxR.Common;
using Oculus.Interaction;
using TMPro;
using UnityEngine;

namespace MxR.Room
{
    /// <summary>
    /// Small room menu bound to the user's left hand / controller.
    ///
    /// Raise the left hand (or controller) in front of your eyes and hold it there briefly: a panel pops
    /// up above it, facing you, with the current room status and two poke buttons (press them with the
    /// other hand):
    /// - "Re-place"  → <see cref="RoomManager.Replace"/>: reload the room and place sign + printer again
    /// - "Scan room" → <see cref="RoomManager.ScanRoom"/>: open Space Setup, then reload and place again
    /// Lower the hand or look away and the panel disappears, so it never gets in the way.
    ///
    /// Built from code like the wall sign; the buttons are the Interaction SDK sample poke button
    /// (same as the printer button). Panel local space: content faces -Z (towards the user).
    /// </summary>
    [RequireComponent(typeof(RoomManager))]
    public class WristMenu : MonoBehaviour
    {
        const string SampleButtonPath =
            "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Prefabs/OculusInteractionSamplePokeButton.prefab";

        [SerializeField] GameObject buttonPrefab;
        [SerializeField] Material panelMaterial;
        [SerializeField] Color textColor = new Color(0.92f, 0.95f, 1f);
        [SerializeField] Color accentTextColor = new Color(0.45f, 0.75f, 1f);

        [Header("Placement")]
        [Tooltip("Offset of the panel centre from the hand anchor (world up, metres).")]
        [SerializeField] float heightAboveHand = 0.13f;
        [SerializeField] Vector2 panelSize = new Vector2(0.17f, 0.17f);
        [SerializeField] float buttonScale = 0.8f;

        [Header("Showing / hiding")]
        [Tooltip("Show when the hand is within this angle of the view direction (degrees).")]
        [SerializeField] float showAngle = 25f;
        [Tooltip("Hide when the hand leaves this angle (degrees); larger than showAngle to avoid flicker.")]
        [SerializeField] float hideAngle = 42f;
        [SerializeField] float maxHandDistance = 0.75f;
        [Tooltip("The hand has to stay in view this long before the panel appears (s).")]
        [SerializeField] float showDelay = 0.35f;
        [SerializeField] float popTime = 0.15f;

        RoomManager room;
        Transform cam, hand;
        Transform panel;
        TextMeshPro statusText;
        PokeInteractable replaceButton, scanButton;
        float inViewTime, scale;
        bool shown;

#if UNITY_EDITOR
        void Reset()
        {
            buttonPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SampleButtonPath);
            panelMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SignBoard.mat");
        }
#endif

        void Awake()
        {
            room = GetComponent<RoomManager>();
            Build();
        }

        void OnEnable()
        {
            room.StatusChanged += OnStatusChanged;
            if (replaceButton) replaceButton.WhenPointerEventRaised += OnReplacePressed;
            if (scanButton) scanButton.WhenPointerEventRaised += OnScanPressed;
        }

        void OnDisable()
        {
            room.StatusChanged -= OnStatusChanged;
            if (replaceButton) replaceButton.WhenPointerEventRaised -= OnReplacePressed;
            if (scanButton) scanButton.WhenPointerEventRaised -= OnScanPressed;
        }

        void Start()
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig)
            {
                cam = rig.centerEyeAnchor;
                hand = rig.leftHandAnchor;    // follows the left controller, or the left hand with hand tracking
            }
            else Debug.LogWarning("[WristMenu] No OVRCameraRig found, the wrist menu is disabled.");
            OnStatusChanged(room.Status);
        }

        void Build()
        {
            panel = new GameObject("WristMenuPanel").transform;
            panel.SetParent(transform, false);

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            Destroy(board.GetComponent<Collider>());
            board.transform.SetParent(panel, false);
            board.transform.localPosition = new Vector3(0f, 0f, 0.004f);
            board.transform.localScale = new Vector3(panelSize.x, panelSize.y, 0.006f);
            if (panelMaterial) board.GetComponent<Renderer>().sharedMaterial = panelMaterial;

            float w = panelSize.x - 0.02f, top = panelSize.y * 0.5f;
            TextFactory.Create("Title", panel, new Vector3(0f, top - 0.02f, -0.002f), new Vector2(w, 0.025f),
                0.2f, TextAlignmentOptions.Left, textColor, FontStyles.Bold).text = "Room";
            statusText = TextFactory.CreateAutoSized("Status", panel, new Vector3(0f, top - 0.05f, -0.002f),
                new Vector2(w, 0.03f), 0.08f, 0.13f, TextAlignmentOptions.TopLeft, accentTextColor);

            replaceButton = CreateButton("ReplaceButton", "Re-place", -0.01f);
            scanButton = CreateButton("ScanButton", "Scan room", -0.06f);

            panel.gameObject.SetActive(false);
        }

        PokeInteractable CreateButton(string name, string text, float y)
        {
            if (!buttonPrefab) { Debug.LogError("[WristMenu] No button prefab assigned.", this); return null; }
            GameObject button = Instantiate(buttonPrefab, panel);
            button.name = name;
            button.transform.SetLocalPositionAndRotation(new Vector3(0f, y, 0f), Quaternion.identity);   // faces -Z like the panel
            button.transform.localScale *= buttonScale;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = text;
            return button.GetComponentInChildren<PokeInteractable>(true);
        }

        void LateUpdate()
        {
            if (!cam || !hand) return;

            bool tracked = OVRInput.GetControllerPositionTracked(OVRInput.Controller.LTouch)
                           || OVRInput.GetControllerPositionTracked(OVRInput.Controller.LHand);
            Vector3 toHand = hand.position - cam.position;
            float angle = Vector3.Angle(cam.forward, toHand);
            bool near = toHand.magnitude < maxHandDistance;

            // hysteresis: easy to keep open, deliberate to open
            bool inView = tracked && near && angle < (shown ? hideAngle : showAngle);
            inViewTime = inView ? inViewTime + Time.deltaTime : 0f;
            bool wantShown = shown ? inView : inViewTime >= showDelay;
            if (wantShown != shown)
            {
                shown = wantShown;
                if (shown)
                {
                    panel.gameObject.SetActive(true);
                    ControllerHaptics.Pulse(this, 0.2f, 0.03f, 1, OVRInput.Controller.LTouch);
                }
            }

            // pop in / out by scaling
            scale = Mathf.MoveTowards(scale, shown ? 1f : 0f, Time.deltaTime / popTime);
            if (scale <= 0f) { if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false); return; }
            panel.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, scale);

            // above the hand, facing the user (content faces -Z, so +Z points away from the eyes)
            Vector3 pos = hand.position + Vector3.up * heightAboveHand;
            Vector3 away = pos - cam.position;
            if (away.sqrMagnitude > 1e-4f)
                panel.SetPositionAndRotation(pos, Quaternion.LookRotation(away, Vector3.up));
        }

        void OnReplacePressed(PointerEvent evt)
        {
            if (evt.Type != PointerEventType.Select) return;
            ControllerHaptics.Pulse(this, 0.6f, 0.06f);
            room.Replace();
        }

        void OnScanPressed(PointerEvent evt)
        {
            if (evt.Type != PointerEventType.Select) return;
            ControllerHaptics.Pulse(this, 0.6f, 0.06f);
            room.ScanRoom();
        }

        void OnStatusChanged(string status)
        {
            if (statusText) statusText.text = status;
        }
    }
}
