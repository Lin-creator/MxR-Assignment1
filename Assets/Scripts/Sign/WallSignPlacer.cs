using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace MxR.Sign
{
    /// <summary>
    /// Anchors the sign to the room's key wall once MR Utility Kit (MRUK) has loaded the scene model.
    ///
    /// Key wall = the longest wall with no other walls behind it (MRUKRoom.GetKeyWall), i.e. the
    /// "stage" wall of the room. The sign is centred on that wall at eye height and faces into the room.
    /// Wall anchors in MRUK: position = centre of the wall plane, forward (+Z) = wall normal into the room.
    ///
    /// Fallback (no room data, e.g. no Space Setup or permission denied): place the sign in front of the user.
    /// </summary>
    [RequireComponent(typeof(WallSign))]
    public class WallSignPlacer : MonoBehaviour
    {
        [Tooltip("Height of the sign centre above the floor (m).")]
        [SerializeField] float centreHeight = 1.5f;
        [Tooltip("Distance between wall and sign (m), avoids z-fighting with the wall.")]
        [SerializeField] float wallOffset = 0.01f;
        [Tooltip("If no room is loaded after this time (s), use the fallback position.")]
        [SerializeField] float fallbackTimeout = 6f;
        [Tooltip("Fallback: distance in front of the user (m).")]
        [SerializeField] float fallbackDistance = 2f;

        /// <summary>The wall the sign is attached to (null when using the fallback).</summary>
        public MRUKAnchor Wall { get; private set; }
        public bool IsPlaced { get; private set; }

        WallSign sign;
        float startTime;

        void Awake()
        {
            sign = GetComponent<WallSign>();
            sign.EnsureBuilt();             // Awake order between components is undefined
            SetVisible(false);              // hidden until we know where it goes
        }

        void Start()
        {
            startTime = Time.time;
            if (MRUK.Instance != null)
                MRUK.Instance.RegisterSceneLoadedCallback(OnSceneLoaded);   // also fires if already loaded
            else
                Debug.LogWarning("[WallSignPlacer] No MRUK in the scene, using fallback placement.");
        }

        void Update()
        {
            if (!IsPlaced && Time.time - startTime > fallbackTimeout)
                PlaceInFrontOfUser();
        }

        void OnSceneLoaded()
        {
            MRUKRoom room = MRUK.Instance.GetCurrentRoom();
            Vector2 wallSize = Vector2.zero;
            MRUKAnchor keyWall = room != null ? room.GetKeyWall(out wallSize) : null;
            if (keyWall == null)
            {
                Debug.LogWarning("[WallSignPlacer] Room has no key wall, using fallback placement.");
                PlaceInFrontOfUser();
                return;
            }
            PlaceOnWall(keyWall, wallSize, FloorHeight(room, keyWall));
        }

        /// <summary>Height of the floor below the wall (lowest floor anchor; wall bottom if there is none).</summary>
        static float FloorHeight(MRUKRoom room, MRUKAnchor wall)
        {
            float y = float.PositiveInfinity;
            foreach (var floor in room.FloorAnchors) y = Mathf.Min(y, floor.transform.position.y);
            if (float.IsInfinity(y))
            {
                room.GetKeyWall(out Vector2 size);
                y = wall.transform.position.y - size.y * 0.5f;
            }
            return y;
        }

        void PlaceOnWall(MRUKAnchor wall, Vector2 wallSize, float floorY)
        {
            Transform w = wall.transform;
            float wallBottom = w.position.y - wallSize.y * 0.5f;

            // eye height, but keep the board inside the wall area
            float half = sign.Size.y * 0.5f + 0.05f;
            float y = Mathf.Clamp(floorY + centreHeight, wallBottom + half, wallBottom + wallSize.y - half);

            Vector3 pos = w.position + w.forward * wallOffset;
            pos.y = y;
            // sign's +Z points into the wall, so its text (facing -Z) faces into the room
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-w.forward, Vector3.up));

            Wall = wall;
            Finish($"on key wall '{wall.name}' ({wallSize.x:0.00} x {wallSize.y:0.00} m)");
        }

        void PlaceInFrontOfUser()
        {
            Camera cam = Camera.main;
            Vector3 eye = cam ? cam.transform.position : new Vector3(0f, 1.5f, 0f);
            Vector3 fwd = cam ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;

            transform.SetPositionAndRotation(eye + fwd * fallbackDistance, Quaternion.LookRotation(fwd, Vector3.up));
            Finish("in front of the user (fallback)");
        }

        void Finish(string where)
        {
            IsPlaced = true;
            SetVisible(true);
            enabled = false;   // stop the timeout check
            Debug.Log($"[WallSignPlacer] Sign placed {where}.");
        }

        void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }
    }
}
