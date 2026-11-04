using UnityEngine;

namespace MxR.Printer
{
    /// <summary>
    /// Adds physics colliders to the printer model at startup so printed parts (and hands) can rest on it:
    /// - Frame: MeshCollider with the exact shape (static, concave is fine for non-moving colliders)
    /// - Bed: BoxCollider fitted to the bed mesh + kinematic Rigidbody, because the bed moves
    /// </summary>
    [RequireComponent(typeof(PrinterController))]
    public class PrinterColliders : MonoBehaviour
    {
        void Awake()
        {
            Transform frame = transform.Find("Frame");
            if (frame && !frame.GetComponent<Collider>())
                frame.gameObject.AddComponent<MeshCollider>();     // uses the MeshFilter's mesh

            Transform bed = transform.Find("Bed");
            if (bed && !bed.GetComponent<Collider>())
            {
                bed.gameObject.AddComponent<BoxCollider>();          // auto-sized to the mesh bounds
                var body = bed.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;                             // moved by PrinterController, not by physics
            }
        }
    }
}
