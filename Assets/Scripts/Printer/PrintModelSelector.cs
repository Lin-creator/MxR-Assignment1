using System;
using System.Collections.Generic;
using MxR.Common;
using UnityEngine;
using State = MxR.Printer.PrinterController.PrinterState;

namespace MxR.Printer
{
    /// <summary>
    /// Lets the user choose what to print from five models, like a carousel on the print bed.
    ///
    /// Every model has a "ghost" preview (transparent, slowly turning) on the bed: the selected model
    /// in the middle where it will be printed, the previous one on the user's left and the next one on
    /// the right – smaller and even more faded. Tapping the left / right edge of the display
    /// (<see cref="PrinterTouchscreen"/>) scrolls; the ghosts slide to their new places.
    ///
    /// While printing, only a faint ghost of the selected model stays as the target outline the real
    /// part grows into. Selecting is possible when the printer is idle or finished.
    /// </summary>
    [RequireComponent(typeof(PrinterController))]
    public class PrintModelSelector : MonoBehaviour
    {
        [Serializable]
        public class PrintModel
        {
            [Tooltip("Name shown on the display and the sign.")]
            public string displayName;
            [Tooltip("Name of the mesh object inside the model library (FBX).")]
            public string meshName;
            [Tooltip("Print time in seconds.")]
            [Min(1f)] public float printSeconds = 7f;
        }

        [Tooltip("FBX with one child object per model (Assets/Models/PrintModels/PrintModels.fbx).")]
        [SerializeField] GameObject modelLibrary;
        [SerializeField] Material ghostMaterial;
        [SerializeField] List<PrintModel> models = new List<PrintModel>
        {
            new PrintModel { displayName = "Cube", meshName = "Cube", printSeconds = 5f },
            new PrintModel { displayName = "Gear", meshName = "Gear", printSeconds = 6f },
            new PrintModel { displayName = "Twisted tower", meshName = "Tower", printSeconds = 8f },
            new PrintModel { displayName = "Chess pawn", meshName = "Pawn", printSeconds = 9f },
            new PrintModel { displayName = "Vase", meshName = "Vase", printSeconds = 10f },
        };

        [Header("Ghost look")]
        [SerializeField] Color ghostColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField, Range(0f, 1f)] float selectedAlpha = 0.4f;
        [Tooltip("Previous / next model: more faded than the selected one.")]
        [SerializeField, Range(0f, 1f)] float sideAlpha = 0.14f;
        [Tooltip("Ghost of the selected model while it is being printed (target outline).")]
        [SerializeField, Range(0f, 1f)] float printingAlpha = 0.12f;
        [SerializeField, Range(0.3f, 1f)] float sideScale = 0.7f;
        [Tooltip("Distance between the previews (m). 0 = fit to the bed.")]
        [SerializeField] float spacing = 0f;
        [SerializeField] float turnSpeed = 35f;     // degrees per second
        [SerializeField] float slideSpeed = 6f;     // slots per second
        [SerializeField] float fadeSpeed = 4f;

        /// <summary>Index of the selected model.</summary>
        public int Index { get; private set; }
        public PrintModel Selected => models.Count > 0 ? models[Index] : null;
        public int Count => models.Count;
        /// <summary>Selecting is possible while the printer is idle or finished.</summary>
        public bool CanSelect => printer.State == State.Idle || printer.State == State.Finished;
        /// <summary>Raised after the selection changed.</summary>
        public event Action<PrintModel> SelectionChanged;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        PrinterController printer;
        readonly List<Renderer> ghosts = new List<Renderer>();
        readonly List<float> ghostAlpha = new List<float>();   // current (faded) alpha per ghost
        MaterialPropertyBlock block;
        float scroll;      // animated index, slides towards Index
        float turn;

        void Awake()
        {
            printer = GetComponent<PrinterController>();
            block = new MaterialPropertyBlock();
            BuildGhosts();
            Index = 0;
            scroll = 0f;
            ApplySelection();
        }

        void BuildGhosts()
        {
            if (!modelLibrary || !ghostMaterial || !printer.Bed)
            {
                Debug.LogError("[PrintModelSelector] Needs the model library, the ghost material and the printer's bed.", this);
                models.Clear();
                return;
            }

            // keep only models whose mesh exists
            models.RemoveAll(m => FindMesh(m) == null);

            if (spacing <= 0f && printer.Bed.TryGetComponent(out MeshFilter bedMesh) && bedMesh.sharedMesh)
            {
                float halfBed = bedMesh.sharedMesh.bounds.extents.x;
                spacing = Mathf.Clamp(halfBed - 0.035f, 0.05f, 0.08f);   // side ghosts stay on the bed
            }
            if (spacing <= 0f) spacing = 0.07f;

            foreach (PrintModel m in models)
            {
                var go = new GameObject($"Ghost ({m.displayName})");
                go.transform.SetParent(printer.Bed, false);
                go.AddComponent<MeshFilter>().sharedMesh = FindMesh(m);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = ghostMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                ghosts.Add(r);
                ghostAlpha.Add(0f);
                go.SetActive(false);
            }
        }

        Mesh FindMesh(PrintModel m)
        {
            Transform t = modelLibrary ? modelLibrary.transform.Find(m.meshName) : null;
            return t && t.TryGetComponent(out MeshFilter f) ? f.sharedMesh : null;
        }

        // ---------- selection ----------

        public void Next() => Step(+1);
        public void Previous() => Step(-1);

        void Step(int direction)
        {
            if (models.Count < 2 || !CanSelect) return;
            Index = (Index + direction + models.Count) % models.Count;
            ApplySelection();
            ControllerHaptics.Pulse(this, 0.3f, 0.03f);
        }

        void ApplySelection()
        {
            PrintModel m = Selected;
            if (m == null) return;
            printer.SetJob(FindMesh(m), m.printSeconds, m.displayName);
            SelectionChanged?.Invoke(m);
        }

        // ---------- ghosts ----------

        void Update()
        {
            int n = ghosts.Count;
            if (n == 0) return;

            // slide the carousel along the shortest way round
            float delta = Mathf.Repeat(Index - scroll + n * 0.5f, n) - n * 0.5f;
            scroll = Mathf.Repeat(scroll + Mathf.Clamp(delta, -slideSpeed * Time.deltaTime, slideSpeed * Time.deltaTime), n);

            bool carousel = CanSelect;
            bool printing = !carousel;
            turn = printing ? Mathf.MoveTowardsAngle(turn, 0f, 360f * Time.deltaTime)   // match the real part
                            : turn + turnSpeed * Time.deltaTime;

            Vector3 centre = printer.PrintCentreOnBed;
            for (int i = 0; i < n; i++)
            {
                // slot: 0 = selected, -1 = previous (user's left), +1 = next (user's right)
                float slot = Mathf.Repeat(i - scroll + n * 0.5f, n) - n * 0.5f;
                float d = Mathf.Abs(slot);

                float target;
                if (printing) target = i == Index ? printingAlpha : 0f;
                else if (i == Index && printer.HasPartOnBed && d < 0.5f) target = 0f;   // the real part is there
                else target = d <= 1f ? Mathf.Lerp(selectedAlpha, sideAlpha, d)
                                      : Mathf.Lerp(sideAlpha, 0f, Mathf.Clamp01((d - 1f) * 2f));
                ghostAlpha[i] = Mathf.MoveTowards(ghostAlpha[i], target, fadeSpeed * Time.deltaTime);

                Renderer r = ghosts[i];
                bool visible = ghostAlpha[i] > 0.003f;
                if (r.gameObject.activeSelf != visible) r.gameObject.SetActive(visible);
                if (!visible) continue;

                // the printer's +X is the user's left (the printer faces the user), so previous = +X
                float scale = Mathf.Lerp(1f, sideScale, Mathf.Clamp01(d));
                r.transform.localPosition = centre + new Vector3(-slot * spacing, 0f, 0f);
                r.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
                r.transform.localScale = Vector3.one * scale;

                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, new Color(ghostColor.r, ghostColor.g, ghostColor.b, ghostAlpha[i]));
                r.SetPropertyBlock(block);
            }
        }
    }
}
