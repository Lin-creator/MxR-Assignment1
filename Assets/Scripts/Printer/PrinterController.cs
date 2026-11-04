using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MxR.Printer
{
    /// <summary>
    /// Animates the 3D printer model with a simulated print job.
    ///
    /// The motion is procedural (no AnimationClip), so a job can be paused and resumed at any point:
    /// - Gantry (and the print head on it) moves down so the nozzle sits on the current layer (Z axis).
    /// - Print head moves left/right on the gantry (X axis), the bed moves front/back (Y axis).
    ///   Together the nozzle traces a circle relative to the bed.
    /// - A cylinder in the filament colour grows on the bed as the job progresses.
    /// - The two cables are stretched so they stay attached to the moving parts.
    ///
    /// Put this on the "Printer3D" root of the imported model; parts are found by name.
    /// All parts are direct children of the root, so positions are in the root's local space
    /// (x = left/right, y = up, z = front/back).
    /// </summary>
    public class PrinterController : MonoBehaviour
    {
        public enum PrinterState { Idle, Printing, Paused, Finished }

        [Header("Model parts (auto-assigned by name)")]
        [SerializeField] Transform gantry;
        [SerializeField] Transform printHead;
        [SerializeField] Transform bed;
        [SerializeField] Transform zCable;
        [SerializeField] Transform filamentTube;

        [Header("Print job")]
        [Tooltip("Seconds for a complete print (default; a selected model brings its own time).")]
        [SerializeField, Min(1f)] float printDuration = 7f;
        [Tooltip("Height of the printed object in metres.")]
        [SerializeField, Min(0.005f)] float printHeight = 0.05f;
        [Tooltip("Radius of the printed object in metres.")]
        [SerializeField, Min(0.005f)] float printRadius = 0.03f;
        [Tooltip("How many circles the nozzle draws per second.")]
        [SerializeField, Min(0.1f)] float lapsPerSecond = 1f;
        [Tooltip("Seconds to move from the home position to the bed and back.")]
        [SerializeField, Min(0.1f)] float travelTime = 1f;
        [Tooltip("Material of the printed object. Empty = use the filament tube's material.")]
        [SerializeField] Material printMaterial;

        [Header("Printed part")]
        [Tooltip("Prefab that is printed (set up with 'Add Grab Interaction', pivot at its bottom). " +
                 "Its size replaces Print Height/Radius. Empty = plain cylinder that cannot be grabbed.")]
        [SerializeField] GameObject printedPartPrefab;
        [Tooltip("Colour the printed part with the print material (filament colour).")]
        [SerializeField] bool tintPrintedPart = true;
        [Tooltip("Parts taken from the printer that stay in the room; the oldest is removed beyond this.")]
        [SerializeField, Min(1)] int maxPartsInRoom = 5;

        [Header("Testing")]
        [Tooltip("Start a print automatically when entering Play mode (to check the animation in the Editor).")]
        [SerializeField] bool startOnPlay;

        [Header("Events")]
        public UnityEvent<PrinterState> onStateChanged;

        public PrinterState State { get; private set; } = PrinterState.Idle;
        /// <summary>Progress of the current job, 0..1.</summary>
        public float Progress { get; private set; }
        /// <summary>0 = axes at home position, 1 = nozzle at the bed (printing pose).</summary>
        public float Engage => engage;
        /// <summary>Seconds until the current job is finished (only meaningful while printing/paused).</summary>
        public float RemainingSeconds =>
            (1f - Progress) * (State == PrinterState.Printing || State == PrinterState.Paused ? activeDuration : JobDuration);
        /// <summary>Print time of the selected job (s).</summary>
        public float JobDuration => jobDuration > 0f ? jobDuration : printDuration;
        /// <summary>Name of the selected model (empty = default part).</summary>
        public string JobName { get; private set; } = "";
        /// <summary>The bed transform; printed parts and previews are its children.</summary>
        public Transform Bed => bed;
        /// <summary>Where parts are printed, in the bed's local space (below the nozzle's home position).</summary>
        public Vector3 PrintCentreOnBed { get { Vector3 c = headRest - bedRest; return new Vector3(c.x, 0f, c.z); } }
        /// <summary>The part of the last job while it still sits on the bed (null if none or taken).</summary>
        public PrintedPart PartOnBed => printedObject ? printedPart : null;
        public bool HasPartOnBed => printedObject != null;
        /// <summary>True if finished parts can be picked up (a grabbable part prefab is assigned).</summary>
        public bool PrintsGrabbableParts => printedPartPrefab != null;

        // rest pose (as imported)
        Vector3 gantryRest, headRest, bedRest, tubeRest;
        Vector3 cableRestScale, tubeRestScale;
        float cableLength;      // height of the Z cable above its pivot
        float tubeRestSpanX;    // x distance from the tube's fixed end to the print head

        float engage;           // 0 = home position, 1 = at the bed and printing
        float activeDuration = 7f;  // duration of the running job
        float angle;            // current angle of the nozzle circle (radians)

        // part of the current/last job while it is still on the bed
        Transform printedObject;
        PrintedPart printedPart;
        Vector3 partFullScale;  // local scale at 100 %
        float partBottom;       // local y of the part's bottom (relative to its pivot) at 100 %
        float partHeight, partRadius;
        readonly List<PrintedPart> partsInRoom = new List<PrintedPart>();

        // selected job (set by PrintModelSelector)
        Mesh jobMesh;
        float jobDuration;

        void Reset() => FindParts();

        void Awake()
        {
            FindParts();
            gantryRest = gantry.localPosition;
            headRest = printHead.localPosition;
            bedRest = bed.localPosition;
            tubeRest = filamentTube.localPosition;
            tubeRestScale = filamentTube.localScale;
            cableRestScale = zCable.localScale;
            cableLength = zCable.GetComponent<MeshFilter>().sharedMesh.bounds.max.y;
            tubeRestSpanX = headRest.x - tubeRest.x;
            partHeight = printHeight;
            partRadius = printRadius;

            if (printMaterial == null)
                printMaterial = filamentTube.GetComponent<Renderer>().sharedMaterial;
        }

        void Start()
        {
            if (startOnPlay) StartNewPrint();
        }

        void FindParts()
        {
            if (!gantry) gantry = transform.Find("Gantry");
            if (!printHead) printHead = transform.Find("PrintHead");
            if (!bed) bed = transform.Find("Bed");
            if (!zCable) zCable = transform.Find("ZCable");
            if (!filamentTube) filamentTube = transform.Find("FilamentTube");
        }

        // ---------- public API (used by buttons / interaction) ----------

        /// <summary>
        /// Selects what the next print makes: the printed part prefab gets this mesh (pivot at its bottom)
        /// and the job takes <paramref name="seconds"/>. Does not affect a running print.
        /// </summary>
        public void SetJob(Mesh mesh, float seconds, string displayName)
        {
            jobMesh = mesh;
            jobDuration = seconds;
            JobName = displayName ?? "";
        }

        /// <summary>Idle/Finished -> start new print, Printing -> pause, Paused -> resume.</summary>
        [ContextMenu("Toggle print")]
        public void TogglePrint()
        {
            switch (State)
            {
                case PrinterState.Idle:
                case PrinterState.Finished: StartNewPrint(); break;
                case PrinterState.Printing: SetState(PrinterState.Paused); break;
                case PrinterState.Paused: SetState(PrinterState.Printing); break;
            }
        }

        public void StartNewPrint()
        {
            RemovePartFromBed();
            Progress = 0f;
            angle = 0f;
            activeDuration = JobDuration;          // a new selection does not change a running job
            CreatePrintedObject();
            SetState(PrinterState.Printing);
        }

        [ContextMenu("Cancel print")]
        public void CancelPrint()
        {
            RemovePartFromBed();
            Progress = 0f;
            SetState(PrinterState.Idle);
        }

        // ---------- animation ----------

        void Update()
        {
            float dt = Time.deltaTime;

            if (State == PrinterState.Printing)
            {
                // only advance the job once the nozzle has reached the bed
                if (engage >= 1f)
                {
                    Progress = Mathf.Min(1f, Progress + dt / activeDuration);
                    angle += dt * lapsPerSecond * 2f * Mathf.PI;
                    if (Progress >= 1f)
                    {
                        UpdatePrintedObject();                    // final size before the state changes
                        SetState(PrinterState.Finished);
                        if (printedPart) printedPart.Release();   // can be picked up now
                    }
                }
            }

            // move towards the bed while a job is active (also while paused), home otherwise
            bool atBed = State == PrinterState.Printing || State == PrinterState.Paused;
            engage = Mathf.MoveTowards(engage, atBed ? 1f : 0f, dt / travelTime);

            ApplyPose(Mathf.SmoothStep(0f, 1f, engage));
            UpdatePrintedObject();
        }

        void ApplyPose(float w)
        {
            float layerHeight = Progress * partHeight;

            // Z: lower the gantry so the nozzle (pivot of PrintHead) touches the current layer
            float nozzleTargetY = bedRest.y + layerHeight;
            float zOffset = w * (nozzleTargetY - headRest.y);
            gantry.localPosition = gantryRest + Vector3.up * zOffset;

            // X: print head rides on the gantry and moves left/right
            float x = w * partRadius * Mathf.Cos(angle);
            printHead.localPosition = headRest + new Vector3(x, zOffset, 0f);

            // Y (front/back): the bed moves, the head stays -> nozzle draws a circle relative to the bed
            float z = w * partRadius * Mathf.Sin(angle);
            bed.localPosition = bedRest - Vector3.forward * z;

            // cables: stretch so their free ends follow the gantry / print head
            filamentTube.localPosition = tubeRest + Vector3.up * zOffset;
            Vector3 ts = tubeRestScale;
            ts.x *= (printHead.localPosition.x - tubeRest.x) / tubeRestSpanX;
            filamentTube.localScale = ts;

            Vector3 cs = cableRestScale;
            cs.y *= (cableLength + zOffset) / cableLength;
            zCable.localScale = cs;
        }

        void CreatePrintedObject()
        {
            GameObject go;
            if (printedPartPrefab)
            {
                go = Instantiate(printedPartPrefab);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);   // 2 units high, 1 unit wide
                Destroy(go.GetComponent<Collider>());
                go.transform.localScale = new Vector3(2f * printRadius, printHeight * 0.5f, 2f * printRadius);
            }
            go.name = string.IsNullOrEmpty(JobName) ? "PrintedPart" : $"PrintedPart ({JobName})";
            if (jobMesh) ApplyJobMesh(go, jobMesh);
            if (tintPrintedPart && printMaterial)
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = printMaterial;

            printedObject = go.transform;
            partFullScale = printedObject.localScale;
            printedObject.SetParent(bed, false);
            printedObject.localRotation = Quaternion.identity;
            // centre of the circle = where the nozzle is above the bed in the home position
            Vector3 centre = headRest - bedRest;
            printedObject.localPosition = new Vector3(centre.x, 0f, centre.z);

            // measure the part at full size (in bed space) -> layer height and nozzle circle
            Bounds b = LocalBounds(printedObject, bed);
            partBottom = b.min.y;
            partHeight = Mathf.Max(0.005f, b.size.y);
            partRadius = Mathf.Max(0.005f, 0.5f * Mathf.Min(b.size.x, b.size.z));

            printedPart = go.GetComponent<PrintedPart>();
            if (!printedPart && printedPartPrefab) printedPart = go.AddComponent<PrintedPart>();
            if (printedPart)
            {
                printedPart.SetPrinting();
                printedPart.TakenFromPrinter += OnPartTaken;
            }
            UpdatePrintedObject();
        }

        /// <summary>
        /// Puts the selected model into the part: the first MeshFilter gets the mesh at its original size,
        /// its colliders are replaced by a convex MeshCollider of the same shape (convex: needed for a
        /// non-kinematic Rigidbody). Runs right after Instantiate, before the grab components' Start
        /// collects the colliders.
        /// </summary>
        static void ApplyJobMesh(GameObject part, Mesh mesh)
        {
            var filter = part.GetComponentInChildren<MeshFilter>(true);
            if (!filter) return;
            filter.sharedMesh = mesh;
            filter.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            filter.transform.localScale = Vector3.one;
            foreach (var c in filter.GetComponents<Collider>()) DestroyImmediate(c);
            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
        }

        void UpdatePrintedObject()
        {
            if (!printedObject || State == PrinterState.Finished || State == PrinterState.Idle) return;
            // grow along y; keep the bottom on the bed surface
            float p = Progress;
            printedObject.gameObject.SetActive(p > 0.01f);
            printedObject.localScale = new Vector3(partFullScale.x, partFullScale.y * Mathf.Max(p, 0.001f), partFullScale.z);
            Vector3 pos = printedObject.localPosition;
            printedObject.localPosition = new Vector3(pos.x, -partBottom * p, pos.z);
        }

        /// <summary>Removes all parts the user took from the printer (e.g. when the room changed and they would float).</summary>
        public void ClearPartsInRoom()
        {
            foreach (var part in partsInRoom) if (part) Destroy(part.gameObject);
            partsInRoom.Clear();
        }

        /// <summary>Removes the part of the previous job if nobody took it (the bed must be empty).</summary>
        void RemovePartFromBed()
        {
            if (printedObject && (!printedPart || !printedPart.TakenFromBed))
                Destroy(printedObject.gameObject);
            printedObject = null;
            printedPart = null;
        }

        void OnPartTaken(PrintedPart part)
        {
            part.TakenFromPrinter -= OnPartTaken;
            if (part == printedPart) { printedObject = null; printedPart = null; }
            partsInRoom.Add(part);
            while (partsInRoom.Count > maxPartsInRoom)
            {
                if (partsInRoom[0]) Destroy(partsInRoom[0].gameObject);
                partsInRoom.RemoveAt(0);
            }
        }

        /// <summary>Combined renderer bounds of <paramref name="root"/> in the local space of <paramref name="space"/>.</summary>
        static Bounds LocalBounds(Transform root, Transform space)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 0.05f);
            Bounds result = default;
            bool first = true;
            foreach (var r in renderers)
            {
                Bounds wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = wb.center + Vector3.Scale(wb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 local = space.InverseTransformPoint(corner);
                    if (first) { result = new Bounds(local, Vector3.zero); first = false; }
                    else result.Encapsulate(local);
                }
            }
            // relative to the part's pivot
            result.center -= space.InverseTransformPoint(root.position);
            return result;
        }

        void SetState(PrinterState s)
        {
            if (State == s) return;
            State = s;
            onStateChanged?.Invoke(s);
        }
    }
}
