using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using MxR.Common;
using UnityEngine;

namespace MxR.Printer
{
    /// <summary>
    /// Lets the user move the printer with the ray (controller trigger / hand pinch on the printer or its stand).
    ///
    /// Like carrying it on a stick: the grabbed point stays at the same distance along the ray
    /// (push/pull with the thumbstick). Below that point the room model is probed with a downward
    /// MRUKRoom.Raycast to find what the printer would stand on:
    /// - a table / storage top → the printer stands directly on it, the virtual stand is hidden
    /// - the floor → the printer stands on its virtual stand
    /// - nothing (outside the room) → the printer stays where it was
    /// While moving, the printer turns its front (and the button) towards the user.
    /// Switching between floor and furniture gives a haptic tick.
    /// </summary>
    [RequireComponent(typeof(PrinterPlacer), typeof(RayDraggable))]
    public class PrinterRepositioner : MonoBehaviour
    {
        [Tooltip("How quickly the printer follows the ray (higher = snappier).")]
        [SerializeField] float followSpeed = 14f;
        [Tooltip("Push/pull speed with the thumbstick (m/s).")]
        [SerializeField] float pushPullSpeed = 1.5f;
        [SerializeField] float minDistance = 0.3f;
        [SerializeField] float maxDistance = 5f;
        [Tooltip("Turn the printer's front towards the user while moving it.")]
        [SerializeField] bool faceUser = true;

        const float ProbeHeight = 3f;
        static readonly LabelFilter Supports = new LabelFilter(
            MRUKAnchor.SceneLabels.FLOOR | MRUKAnchor.SceneLabels.TABLE | MRUKAnchor.SceneLabels.STORAGE);

        PrinterPlacer placer;
        RayDraggable draggable;

        Vector3 grabPoint;           // where the printer was hit when the drag started
        float grabDistance = -1f;    // distance along the ray to the grabbed point (-1 = not measured yet)
        Vector3 grabOffset;          // printer origin relative to the grabbed point (world, horizontal)
        MRUKAnchor support;          // what the printer stands on (null = unknown / no room)
        Vector3 targetPosition;
        Quaternion targetRotation;
        bool ready;
        readonly List<RaycastHit> hits = new List<RaycastHit>();
        readonly List<MRUKAnchor> anchors = new List<MRUKAnchor>();

        void Awake()
        {
            placer = GetComponent<PrinterPlacer>();
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

        // placed again (e.g. in a new room): the stand may have appeared or disappeared
        void OnPlaced()
        {
            if (ready) UpdateHandle();
        }

        void Update()
        {
            if (!ready)
            {
                if (!placer.IsPlaced) return;
                UpdateHandle();
                draggable.SetInteractable(true);
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
            grabPoint = hitPoint;
            grabDistance = -1f;      // measured from the ray origin on the first drag frame
            grabOffset = Vector3.ProjectOnPlane(transform.position - hitPoint, Vector3.up);
            targetPosition = transform.position;
            targetRotation = transform.rotation;
            support = FindSupport(transform.position, out _);
        }

        void OnDragging(Ray ray)
        {
            if (!draggable.IsDragging) return;

            if (grabDistance < 0f) grabDistance = Vector3.Distance(ray.origin, grabPoint);

            // push / pull with either thumbstick (controllers only)
            float stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).y + OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).y;
            grabDistance = Mathf.Clamp(grabDistance + stick * pushPullSpeed * Time.deltaTime, minDistance, maxDistance);
            Vector3 candidate = ray.GetPoint(grabDistance) + grabOffset;

            MRUKRoom room = MRUK.Instance ? MRUK.Instance.GetCurrentRoom() : null;
            if (room != null)
            {
                MRUKAnchor newSupport = FindSupport(candidate, out float surfaceY);
                if (newSupport == null) return;                       // outside the room: stay
                bool onFloor = newSupport.HasAnyLabel(MRUKAnchor.SceneLabels.FLOOR);
                candidate.y = surfaceY + (onFloor ? placer.StandHeight : 0f);
                if (onFloor != placer.UsesStand)
                {
                    placer.SetStandVisible(onFloor);
                    UpdateHandle();
                }
                if (newSupport != support) draggable.Tick();
                support = newSupport;
            }
            else
            {
                candidate.y = transform.position.y;                   // no room data: keep the height
            }

            targetPosition = candidate;
            if (faceUser) targetRotation = FacingUser(candidate, targetRotation);
        }

        /// <summary>
        /// Highest floor / table / storage top below a point that is lower than the user's eyes
        /// (so the printer is not put on top of a tall shelf). Null if there is none.
        /// </summary>
        MRUKAnchor FindSupport(Vector3 point, out float surfaceY)
        {
            surfaceY = 0f;
            MRUKRoom room = MRUK.Instance ? MRUK.Instance.GetCurrentRoom() : null;
            if (room == null) return null;

            Camera cam = Camera.main;
            float maxY = cam ? cam.transform.position.y - 0.3f : float.PositiveInfinity;
            var down = new Ray(new Vector3(point.x, point.y + ProbeHeight, point.z), Vector3.down);
            if (!room.RaycastAll(down, ProbeHeight * 2f + 5f, Supports, hits, anchors)) return null;

            MRUKAnchor best = null;
            for (int i = 0; i < hits.Count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.normal.y < 0.7f || hit.point.y > maxY) continue;   // side of a cabinet / too high
                if (best == null || hit.point.y > surfaceY) { best = anchors[i]; surfaceY = hit.point.y; }
            }
            return best;
        }

        static Quaternion FacingUser(Vector3 position, Quaternion fallback)
        {
            Camera cam = Camera.main;
            if (!cam) return fallback;
            Vector3 toUser = Vector3.ProjectOnPlane(cam.transform.position - position, Vector3.up);
            return toUser.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toUser.normalized, Vector3.up) : fallback;
        }

        /// <summary>Ray handle = frame + (visible) stand, in the printer's local space.</summary>
        void UpdateHandle()
        {
            var bounds = new Bounds();
            bool any = false;
            foreach (string part in new[] { "Frame", "Stand" })
            {
                Transform t = transform.Find(part);
                if (!t || !t.gameObject.activeSelf || !t.TryGetComponent(out MeshFilter mf) || !mf.sharedMesh) continue;
                Bounds b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1);
                    Vector3 p = transform.InverseTransformPoint(t.TransformPoint(b.min + Vector3.Scale(b.size, corner)));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            if (any) draggable.SetHandle(bounds);
            draggable.RefreshHighlight();
        }
    }
}
