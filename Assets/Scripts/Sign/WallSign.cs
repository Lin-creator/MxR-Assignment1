using MxR.Common;
using TMPro;
using UnityEngine;

namespace MxR.Sign
{
    /// <summary>
    /// Information board about the machine: title, author name, description and a live status line.
    /// The board is built from code (backboard, accent bars, TextMeshPro texts) when the scene starts,
    /// so all content can be edited in the Inspector.
    /// Placement on the wall is done by <see cref="WallSignPlacer"/>.
    ///
    /// Local layout: origin = centre of the board, texts face -Z (readable when looking along +Z),
    /// the backboard sits behind the texts towards +Z (towards the wall).
    /// </summary>
    public class WallSign : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] string title = "3D Printer";
        [SerializeField] string authorName = "Linus Pihan";
        [SerializeField, TextArea(4, 8)] string description =
            "An FDM 3D printer melts plastic filament in a heated nozzle and builds objects layer by layer. " +
            "The print head moves left/right (X), the bed moves front/back (Y) and the gantry moves up (Z) " +
            "after every layer.\n\nChoose a model with the arrows on the printer's display, tap its middle to print.";
        [SerializeField] string initialStatus = "Status: ready";

        [Header("Look")]
        [Tooltip("Width and height of the board in metres.")]
        [SerializeField] Vector2 size = new Vector2(0.9f, 0.6f);
        [SerializeField] Material boardMaterial;
        [SerializeField] Material accentMaterial;
        [Tooltip("Empty = default font from TMP Settings (LiberationSans SDF).")]
        [SerializeField] TMP_FontAsset font;
        [SerializeField] Color textColor = new Color(0.92f, 0.95f, 1f);
        [SerializeField] Color accentTextColor = new Color(0.45f, 0.75f, 1f);

        const float BoardDepth = 0.012f;
        const float Padding = 0.04f;

        TextMeshPro statusText;
        bool built;

        public Vector2 Size => size;

        void Awake() => Build();

        /// <summary>Builds the board now if it has not been built yet (safe to call multiple times).</summary>
        public void EnsureBuilt() => Build();

        /// <summary>Shows a status line at the bottom of the board, e.g. the printer state.</summary>
        public void SetStatus(string status)
        {
            Build();
            statusText.text = status;
        }

        void Build()
        {
            if (built) return;
            built = true;

            float w = size.x, h = size.y;

            // backboard and accent bars (primitives without colliders)
            CreateBox("Board", new Vector3(0f, 0f, BoardDepth * 0.5f), new Vector3(w, h, BoardDepth), boardMaterial);
            CreateBox("AccentTop", new Vector3(0f, h * 0.5f - 0.01f, -0.001f), new Vector3(w, 0.02f, 0.004f), accentMaterial);
            CreateBox("AccentLine", new Vector3(0f, h * 0.5f - 0.155f, -0.001f), new Vector3(w - 2f * Padding, 0.004f, 0.002f), accentMaterial);

            float textW = w - 2f * Padding;
            float top = h * 0.5f - 0.03f;
            const float z = -0.003f; // just in front of the board

            // title + name block (height ~0.12 m)
            TextFactory.Create("Title", transform, new Vector3(0f, top - 0.04f, z), new Vector2(textW, 0.08f),
                0.6f, TextAlignmentOptions.Left, textColor, FontStyles.Bold, font).text = title;
            TextFactory.Create("Author", transform, new Vector3(0f, top - 0.1f, z), new Vector2(textW, 0.04f),
                0.3f, TextAlignmentOptions.Left, accentTextColor, FontStyles.Normal, font).text = authorName;

            // description fills the middle, shrinks to fit
            float descTop = h * 0.5f - 0.175f;
            float descBottom = -h * 0.5f + 0.09f;
            TextFactory.CreateAutoSized("Description", transform,
                new Vector3(0f, (descTop + descBottom) * 0.5f, z), new Vector2(textW, descTop - descBottom),
                0.15f, 0.28f, TextAlignmentOptions.TopLeft, textColor, FontStyles.Normal, font).text = description;

            // status line at the bottom
            statusText = TextFactory.Create("Status", transform, new Vector3(0f, -h * 0.5f + 0.05f, z),
                new Vector2(textW, 0.05f), 0.3f, TextAlignmentOptions.Left, accentTextColor, FontStyles.Bold, font);
            statusText.text = initialStatus;
        }

        void CreateBox(string name, Vector3 localPos, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (material) go.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
