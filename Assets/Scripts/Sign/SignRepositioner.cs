using Meta.XR.MRUtilityKit;
using MxR.Common;
using UnityEngine;

namespace MxR.Sign
{
    /// <summary>
    /// Lets the user move the wall sign with the ray (controller trigger / hand pinch on the board).
    ///
    /// The sign stays on the walls: every frame the pointing ray is cast against the room's wall
    /// anchors (MRUKRoom.Raycast). The board slides along the wall under the ray, is kept inside the
    /// wall's rectangle and jumps to another wall as soon as the ray points at it (with a haptic tick).
    /// Without room data (fallback placement) it slides on its current plane instead.
    /// </summary>
    [RequireComponent(typeof(WallSign), typeof(WallSignPlacer), typeof(RayDraggable))]
    public class SignRepositioner : MonoBehaviour
    {
        [Tooltip("Distance between wall and sign (m), same as WallSignPlacer.")]
        [SerializeField] float wallOffset = 0.01f;
        [Tooltip("Free space kept between the board and the wall edges (m).")]
        [SerializeField] float edgeMargin = 0.03f;
        [Tooltip("How quickly the board follows the ray (higher = snappier).")]
        [SerializeField] float followSpeed = 18f;

        const float MaxRayDistance = 20f;
        const float HandleDepth = 0.04f;
        static readonly LabelFilter Walls =
            new LabelFilter(MRUKAnchor.SceneLabels.WALL_FACE | MRUKAnchor.SceneLabels.INVISIBLE_WALL_FACE);

        WallSign sign;
        WallSignPlacer placer;
        RayDraggable draggable;

        Vector2 grabOffset;          // where on the board it was grabbed (board-local x/y)
        MRUKAnchor wall;             // wall the board is on (null = no room data)
        Vector3 targetPosition;
        Quaternion targetRotation;
        bool ready;

        void Awake()
        {
            sign = GetComponent<WallSign>();
            placer = GetComponent<WallSignPlacer>();
            draggable = GetComponent<RayDraggable>();
        }

        void OnEnable()
        {
            draggable.DragStarted += OnDragStarted;
            draggable.Dragging += OnDragging;
            placer.Placed += OnPlaced;
        }

        void OnDisable()
        {
            draggable.DragStarted -= OnDragStarted;
            draggable.Dragging -= OnDragging;
            placer.Placed -= OnPlaced;
        }

        void OnPlaced() => wall = placer.Wall;   // placed again, e.g. in a new room

        void Update()
        {
            if (!ready)
            {
                if (!placer.IsPlaced) return;
                // handle = the whole board, slightly thicker so the ray hits it easily
                draggable.SetHandle(new Bounds(new Vector3(0f, 0f, HandleDepth * 0.25f),
                    new Vector3(sign.Size.x, sign.Size.y, HandleDepth)));
                draggable.SetInteractable(true);
                wall = placer.Wall;
                ready = true;
                return;
            }

            if (!draggable.IsDragging) return;
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);   // frame-rate independent smoothing
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, targetPosition, t),
                Quaternion.Slerp(transform.rotation, targetRotation, t));
        }

        void OnDragStarted(Vector3 hitPoint)
        {
            Vector3 local = transform.InverseTransformPoint(hitPoint);
            grabOffset = new Vector2(local.x, local.y);
            targetPosition = transform.position;
            targetRotation = transform.rotation;
        }

        void OnDragging(Ray ray)
        {
            MRUKRoom room = MRUK.Instance ? MRUK.Instance.GetCurrentRoom() : null;
            if (room != null)
            {
                if (room.Raycast(ray, MaxRayDistance, Walls, out RaycastHit hit, out MRUKAnchor hitWall)
                    && hitWall.PlaneRect.HasValue)
                {
                    if (wall != null && hitWall != wall) draggable.Tick();   // moved to another wall
                    wall = hitWall;
                    PlaceOnWall(hit.point, hitWall);
                }
                return;   // ray points at the ceiling, floor or outside: keep the last position
            }

            // no room data: slide on the board's current plane
            var plane = new Plane(transform.forward, transform.position);
            if (plane.Raycast(ray, out float distance))
                targetPosition = ray.GetPoint(distance) - transform.rotation * (Vector3)grabOffset;
        }

        void PlaceOnWall(Vector3 point, MRUKAnchor anchor)
        {
            Transform w = anchor.transform;           // forward = into the room, PlaneRect in local x/y
            // the board's +Z points into the wall, its texts face into the room
            Quaternion rotation = Quaternion.LookRotation(-w.forward, Vector3.up);
            Vector3 centre = point - rotation * (Vector3)grabOffset;

            // keep the whole board on the wall
            Rect rect = anchor.PlaneRect.Value;
            Vector3 local = w.InverseTransformPoint(centre);
            local.x = ClampCentred(local.x, rect.xMin, rect.xMax, sign.Size.x * 0.5f + edgeMargin);
            local.y = ClampCentred(local.y, rect.yMin, rect.yMax, sign.Size.y * 0.5f + edgeMargin);
            local.z = 0f;

            targetPosition = w.TransformPoint(local) + w.forward * wallOffset;
            targetRotation = rotation;
        }

        /// <summary>Clamps v so that [v - half, v + half] fits into [min, max]; centres it if it is too big.</summary>
        static float ClampCentred(float v, float min, float max, float half) =>
            max - min < 2f * half ? (min + max) * 0.5f : Mathf.Clamp(v, min + half, max - half);
    }
}
