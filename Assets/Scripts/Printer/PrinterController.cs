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
        [Tooltip("Seconds for a complete print.")]
        [SerializeField, Min(1f)] float printDuration = 30f;
        [Tooltip("Height of the printed object in metres.")]
        [SerializeField, Min(0.005f)] float printHeight = 0.05f;
        [Tooltip("Radius of the printed object in metres.")]
        [SerializeField, Min(0.005f)] float printRadius = 0.03f;
        [Tooltip("How many circles the nozzle draws per second.")]
        [SerializeField, Min(0.1f)] float lapsPerSecond = 1f;
        [Tooltip("Seconds to move from the home position to the bed and back.")]
        [SerializeField, Min(0.1f)] float travelTime = 1.5f;
        [Tooltip("Material of the printed object. Empty = use the filament tube's material.")]
        [SerializeField] Material printMaterial;

        [Header("Testing")]
        [Tooltip("Start a print automatically when entering Play mode (to check the animation in the Editor).")]
        [SerializeField] bool startOnPlay;

        [Header("Events")]
        public UnityEvent<PrinterState> onStateChanged;

        public PrinterState State { get; private set; } = PrinterState.Idle;
        /// <summary>Progress of the current job, 0..1.</summary>
        public float Progress { get; private set; }

        // rest pose (as imported)
        Vector3 gantryRest, headRest, bedRest, tubeRest;
        Vector3 cableRestScale, tubeRestScale;
        float cableLength;      // height of the Z cable above its pivot
        float tubeRestSpanX;    // x distance from the tube's fixed end to the print head

        float engage;           // 0 = home position, 1 = at the bed and printing
        float angle;            // current angle of the nozzle circle (radians)
        Transform printedObject;

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
            Progress = 0f;
            angle = 0f;
            CreatePrintedObject();
            SetState(PrinterState.Printing);
        }

        [ContextMenu("Cancel print")]
        public void CancelPrint()
        {
            if (printedObject) Destroy(printedObject.gameObject);
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
                    Progress = Mathf.Min(1f, Progress + dt / printDuration);
                    angle += dt * lapsPerSecond * 2f * Mathf.PI;
                    if (Progress >= 1f) SetState(PrinterState.Finished);
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
            float layerHeight = Progress * printHeight;

            // Z: lower the gantry so the nozzle (pivot of PrintHead) touches the current layer
            float nozzleTargetY = bedRest.y + layerHeight;
            float zOffset = w * (nozzleTargetY - headRest.y);
            gantry.localPosition = gantryRest + Vector3.up * zOffset;

            // X: print head rides on the gantry and moves left/right
            float x = w * printRadius * Mathf.Cos(angle);
            printHead.localPosition = headRest + new Vector3(x, zOffset, 0f);

            // Y (front/back): the bed moves, the head stays -> nozzle draws a circle relative to the bed
            float z = w * printRadius * Mathf.Sin(angle);
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
            if (printedObject) Destroy(printedObject.gameObject);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "PrintedObject";
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = printMaterial;

            printedObject = go.transform;
            printedObject.SetParent(bed, false);
            // centre of the circle = where the nozzle is above the bed in the home position
            Vector3 centre = headRest - bedRest;
            printedObject.localPosition = new Vector3(centre.x, 0f, centre.z);
            printedObject.localRotation = Quaternion.identity;
            UpdatePrintedObject();
        }

        void UpdatePrintedObject()
        {
            if (!printedObject) return;
            // Unity's cylinder primitive is 2 units high and 1 unit wide, pivot in the middle
            float h = Progress * printHeight;
            printedObject.gameObject.SetActive(h > 0.0005f);
            printedObject.localScale = new Vector3(2f * printRadius, h * 0.5f, 2f * printRadius);
            Vector3 p = printedObject.localPosition;
            printedObject.localPosition = new Vector3(p.x, h * 0.5f, p.z);
        }

        void SetState(PrinterState s)
        {
            if (State == s) return;
            State = s;
            onStateChanged?.Invoke(s);
        }
    }
}
