using System;
using System.Collections;
using System.Threading.Tasks;
using Meta.XR.MRUtilityKit;
using MxR.Printer;
using MxR.Sign;
using UnityEngine;

namespace MxR.Room
{
    /// <summary>
    /// Keeps the virtual content in the room the user is actually in.
    ///
    /// MRUK loads the room model only once at startup. When the user walks into another space
    /// (other boundary / Space Setup), the old room can no longer be located and the sign, printer
    /// and Effect Mesh colliders would stay at the old coordinates. RoomManager therefore
    /// - reloads the room model from the headset whenever the app gets input focus back or the
    ///   view is recentred (the Quest pauses the app while it shows the boundary prompt), and only
    ///   if the user is now in a different room, places the sign and printer again;
    /// - offers <see cref="Replace"/> (reload + place again, also on the left controller's menu button)
    ///   and <see cref="ScanRoom"/> (opens Space Setup, then reloads) for the wrist menu.
    /// </summary>
    public class RoomManager : MonoBehaviour
    {
        [SerializeField] WallSignPlacer sign;
        [SerializeField] PrinterPlacer printer;
        [Tooltip("Room colliders, rebuilt for the new room. Empty = found in the scene.")]
        [SerializeField] EffectMesh effectMesh;

        [Header("Automatic")]
        [Tooltip("Reload the room when the app regains focus / the view is recentred and re-place the content if the room changed.")]
        [SerializeField] bool autoReplaceOnRoomChange = true;
        [Tooltip("Seconds to wait after regaining focus before reloading (tracking needs a moment to relocalise).")]
        [SerializeField] float focusDelay = 1.5f;

        [Header("Controller shortcut")]
        [Tooltip("Left controller menu button (≡) = Re-place.")]
        [SerializeField] bool menuButtonReplaces = true;

        /// <summary>Human-readable state for the wrist menu, e.g. "Room: 4 walls, 1 table".</summary>
        public string Status { get; private set; } = "Loading room…";
        public event Action<string> StatusChanged;
        public bool IsBusy { get; private set; }

        Guid placedRoom = Guid.Empty;   // room the content was placed in
        Coroutine pendingCheck;

        void Reset()
        {
            sign = FindFirstObjectByType<WallSignPlacer>();
            printer = FindFirstObjectByType<PrinterPlacer>();
            effectMesh = FindFirstObjectByType<EffectMesh>();
        }

        void Awake()
        {
            if (!sign) sign = FindFirstObjectByType<WallSignPlacer>();
            if (!printer) printer = FindFirstObjectByType<PrinterPlacer>();
            if (!effectMesh) effectMesh = FindFirstObjectByType<EffectMesh>();
        }

        void OnEnable() => OVRManager.InputFocusAcquired += OnFocusAcquired;

        void OnDisable()
        {
            OVRManager.InputFocusAcquired -= OnFocusAcquired;
            if (display != null) display.RecenteredPose -= OnFocusAcquired;
        }

        OVRDisplay display;

        void Start()
        {
            // OVRManager creates its display in Awake, so subscribe here instead of OnEnable
            display = OVRManager.display;
            if (display != null) display.RecenteredPose += OnFocusAcquired;
            if (MRUK.Instance) MRUK.Instance.RegisterSceneLoadedCallback(OnFirstSceneLoad);
            else SetStatus("No MRUK in the scene");
        }

        void Update()
        {
            if (menuButtonReplaces && OVRInput.GetDown(OVRInput.Button.Start, OVRInput.Controller.LTouch))
                Replace();
        }

        void OnFirstSceneLoad()
        {
            MRUK.Instance.SceneLoadedEvent.RemoveListener(OnFirstSceneLoad);
            placedRoom = CurrentRoomId();
            SetStatus(Describe(MRUK.Instance.GetCurrentRoom()));
        }

        // ---------- public API (wrist menu) ----------

        /// <summary>Reloads the room model and places the sign and printer again, even if the room did not change.</summary>
        public void Replace() => _ = Reload(force: true);

        /// <summary>Opens the headset's Space Setup (scan / edit the room), then reloads and places everything again.</summary>
        public async void ScanRoom()
        {
            if (IsBusy) return;
            IsBusy = true;
            SetStatus("Space Setup…");
            bool ok;
            try { ok = await OVRScene.RequestSpaceSetup(); }
            catch (Exception e) { Debug.LogException(e); ok = false; }
            IsBusy = false;
            if (!ok) Debug.Log("[RoomManager] Space Setup was cancelled or failed, reloading anyway.");
            await Reload(force: true);
        }

        // ---------- automatic check ----------

        void OnFocusAcquired()
        {
            if (!autoReplaceOnRoomChange || !isActiveAndEnabled) return;
            if (pendingCheck != null) StopCoroutine(pendingCheck);
            pendingCheck = StartCoroutine(CheckAfterDelay());
        }

        IEnumerator CheckAfterDelay()
        {
            yield return new WaitForSeconds(focusDelay);
            pendingCheck = null;
            _ = Reload(force: false);
        }

        // ---------- reload ----------

        async Task Reload(bool force)
        {
            if (IsBusy || !MRUK.Instance) return;
            IsBusy = true;
            SetStatus("Loading room…");
            try
            {
                // no Space Setup prompt here (ScanRoom does that); removeMissingRooms drops rooms we can no longer locate
                var result = await MRUK.Instance.LoadSceneFromDevice(requestSceneCaptureIfNoDataFound: false, removeMissingRooms: true);
                MRUKRoom room = MRUK.Instance.GetCurrentRoom();
                Guid id = CurrentRoomId();
                bool changed = id != placedRoom;
                Debug.Log($"[RoomManager] Reload: {result}, room {id} (placed in {placedRoom}), force={force}");

                if (force || changed)
                {
                    RebuildEffectMesh(room);
                    if (printer)
                    {
                        if (changed && printer.TryGetComponent(out PrinterController controller)) controller.ClearPartsInRoom();
                        printer.PlaceAgain();
                    }
                    if (sign) sign.PlaceAgain();
                    placedRoom = id;
                    SetStatus(room ? (changed ? "New room – " : "Placed again – ") + Describe(room)
                                   : "No room found – use Scan room");
                }
                else
                {
                    SetStatus(Describe(room));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetStatus("Loading the room failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        void RebuildEffectMesh(MRUKRoom room)
        {
            if (!effectMesh) return;
            effectMesh.DestroyMesh();               // colliders of rooms that are gone
            if (room) effectMesh.CreateMesh(room);
        }

        static Guid CurrentRoomId()
        {
            MRUKRoom room = MRUK.Instance ? MRUK.Instance.GetCurrentRoom() : null;
            return room ? room.Anchor.Uuid : Guid.Empty;
        }

        static string Describe(MRUKRoom room)
        {
            if (!room) return "No room found – use Scan room";
            int tables = 0;
            foreach (var a in room.Anchors) if (a.HasAnyLabel(MRUKAnchor.SceneLabels.TABLE)) tables++;
            return $"Room: {room.WallAnchors.Count} walls, {tables} table{(tables == 1 ? "" : "s")}";
        }

        void SetStatus(string status)
        {
            Status = status;
            StatusChanged?.Invoke(status);
        }
    }
}
