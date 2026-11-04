using TMPro;
using UnityEngine;

namespace MxR.Common
{
    /// <summary>
    /// Creates world-space TextMeshPro labels from code.
    /// 3D TextMeshPro sizes: fontSize 1 = 0.1 m em height, RectTransform size is in metres.
    /// The text is readable when looking along the parent's +Z axis (it faces -Z).
    /// </summary>
    public static class TextFactory
    {
        public static TextMeshPro Create(string name, Transform parent, Vector3 localPos, Vector2 size,
            float fontSize, TextAlignmentOptions alignment, Color color, FontStyles style = FontStyles.Normal,
            TMP_FontAsset font = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var tmp = go.AddComponent<TextMeshPro>();   // also adds RectTransform + MeshRenderer
            if (font) tmp.font = font;                 // null = default font from TMP Settings
            tmp.rectTransform.sizeDelta = size;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Truncate;
            return tmp;
        }

        /// <summary>Same as <see cref="Create"/>, but shrinks the font until the text fits the box.</summary>
        public static TextMeshPro CreateAutoSized(string name, Transform parent, Vector3 localPos, Vector2 size,
            float minSize, float maxSize, TextAlignmentOptions alignment, Color color,
            FontStyles style = FontStyles.Normal, TMP_FontAsset font = null)
        {
            var tmp = Create(name, parent, localPos, size, maxSize, alignment, color, style, font);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = minSize;
            tmp.fontSizeMax = maxSize;
            return tmp;
        }
    }
}
