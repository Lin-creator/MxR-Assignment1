using System;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace MxR.Printer
{
    /// <summary>
    /// Places the printer in the real room once MR Utility Kit has loaded the scene model:
    /// 1. on the largest table (MRUK label TABLE) if its top is big enough, facing the user, otherwise
    /// 2. on a virtual stand on the floor in front of the key wall (next to the wall sign), otherwise
    /// 3. (no room data) on a stand in front of the user.
    /// The printer is hidden until it has been placed.
    /// </summary>
    [RequireComponent(typeof(PrinterController))]
    public class PrinterPlacer : MonoBehaviour
    {
        [Header("Table")]
        [SerializeField] bool preferTable = true;
        [Tooltip("Minimum table top size (m) needed for the printer and its button.")]
        [SerializeField] Vector2 minTableSize = new Vector2(0.6f, 0.4f);

        [Header("Stand (when there is no table)")]
        [SerializeField] float standHeight = 0.75f;
        [Tooltip("Width (x) and depth (z) of the stand (m).")]
        [SerializeField] Vector2 standFootprint = new Vector2(0.8f, 0.55f);
        [Tooltip("Stand centre relative to the printer along x; negative = more room on the user's right for the button.")]
        [SerializeField] float standOffsetX = -0.12f;
        [SerializeField] Material standMaterial;

        [Header("Position in front of the key wall")]
        [SerializeField] float distanceFromWall = 0.9f;
        [Tooltip("Sideways offset along the wall (m), so the printer does not hide the sign.")]
        [SerializeField] float sideOffset = 0.6f;

        [Header("Fallback")]
        [SerializeField] float fallbackTimeout = 6f;
        [SerializeField] float fallbackDistance = 1.2f;

        public bool IsPlaced { get; private set; }
        /// <summary>Raised once the printer has been placed.</summary>
        public event Action Placed;

        float startTime;

#if UNITY_EDITOR
        void Reset() =>
            standMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PrinterStand.mat");
#endif

        void Start()
        {
            // Start runs after every Awake, so parts created by other components in Awake are hidden too
            SetVisible(false);
            startTime = Time.time;
            if (MRUK.Instance != null)
                MRUK.Instance.RegisterSceneLoadedCallback(OnSceneLoaded);
            else
                Debug.LogWarning("[PrinterPlacer] No MRUK in the scene, using fallback placement.");
        }

        void Update()
        {
            if (!IsPlaced && Time.time - startTime > fallbackTimeout)
                PlaceInFrontOfUser();
        }

        void OnSceneLoaded()
        {
            if (IsPlaced) return;
            MRUKRoom room = MRUK.Instance.GetCurrentRoom();
            if (room == null) { PlaceInFrontOfUser(); return; }

            if (preferTable)
            {
                MRUKAnchor table = room.FindLargestSurface(MRUKAnchor.SceneLabels.TABLE);
                if (table != null && table.PlaneRect.HasValue)
                {
                    Vector2 size = table.PlaneRect.Value.size;
                    bool fits = Mathf.Max(size.x, size.y) >= minTableSize.x && Mathf.Min(size.x, size.y) >= minTableSize.y;
                    if (fits)
                    {
                        // table top plane: position = centre of the top surface
                        Vector3 top = table.transform.position;
                        Finish(top, FacingUser(top), $"on table '{table.name}' ({size.x:0.00} x {size.y:0.00} m)");
                        return;
                    }
                }
            }

            MRUKAnchor wall = room.GetKeyWall(out _);
            if (wall != null)
            {
                Transform w = wall.transform;   // forward = into the room
                Vector3 pos = w.position + w.forward * distanceFromWall + w.right * sideOffset;
                pos.y = FloorHeight(room);
                CreateStand();
                Finish(pos + Vector3.up * standHeight, Quaternion.LookRotation(w.forward, Vector3.up),
                    "on a stand in front of the key wall");
                return;
            }

            PlaceInFrontOfUser();
        }

        void PlaceInFrontOfUser()
        {
            Camera cam = Camera.main;
            Vector3 eye = cam ? cam.transform.position : new Vector3(0f, 1.6f, 0f);
            Vector3 fwd = cam ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;

            // tracking space origin is on the floor when the camera rig uses floor-level tracking
            var rig = FindFirstObjectByType<OVRCameraRig>();
            float floorY = rig ? rig.trackingSpace.position.y : 0f;

            Vector3 pos = eye + fwd * fallbackDistance;
            pos.y = floorY;
            CreateStand();
            Finish(pos + Vector3.up * standHeight, Quaternion.LookRotation(-fwd, Vector3.up),
                "on a stand in front of the user (fallback)");
        }

        static float FloorHeight(MRUKRoom room)
        {
            float y = float.PositiveInfinity;
            foreach (var floor in room.FloorAnchors) y = Mathf.Min(y, floor.transform.position.y);
            return float.IsInfinity(y) ? 0f : y;
        }

        /// <summary>Rotation that turns the printer's front (+Z) towards the user.</summary>
        static Quaternion FacingUser(Vector3 position)
        {
            Camera cam = Camera.main;
            Vector3 toUser = cam ? cam.transform.position - position : Vector3.back;
            toUser = Vector3.ProjectOnPlane(toUser, Vector3.up);
            return Quaternion.LookRotation(toUser.sqrMagnitude > 0.01f ? toUser.normalized : Vector3.back, Vector3.up);
        }

        void CreateStand()
        {
            var stand = GameObject.CreatePrimitive(PrimitiveType.Cube);   // keeps its BoxCollider (things can be put on it)
            stand.name = "Stand";
            stand.transform.SetParent(transform, false);
            stand.transform.localPosition = new Vector3(standOffsetX, -standHeight * 0.5f, 0f);
            stand.transform.localScale = new Vector3(standFootprint.x, standHeight, standFootprint.y);
            if (standMaterial) stand.GetComponent<Renderer>().sharedMaterial = standMaterial;
        }

        void Finish(Vector3 position, Quaternion rotation, string where)
        {
            transform.SetPositionAndRotation(position, rotation);
            IsPlaced = true;
            SetVisible(true);
            enabled = false;
            Debug.Log($"[PrinterPlacer] Printer placed {where}.");
            Placed?.Invoke();
        }

        readonly List<Renderer> hidden = new List<Renderer>();

        // only re-enable what we hid ourselves (some child renderers are disabled on purpose)
        void SetVisible(bool visible)
        {
            if (!visible)
            {
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                    if (r.enabled) { r.enabled = false; hidden.Add(r); }
            }
            else
            {
                foreach (var r in hidden) if (r) r.enabled = true;
                hidden.Clear();
            }
        }
    }
}
