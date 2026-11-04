using System;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;

namespace MxR.Common
{
    /// <summary>
    /// Makes an object selectable with the Interaction SDK's ray (controller trigger or hand pinch)
    /// and reports a drag: <see cref="DragStarted"/> → <see cref="Dragging"/> every frame → <see cref="DragEnded"/>.
    /// It does not move anything itself; a repositioner component decides where the object may go.
    ///
    /// Set up at runtime: a trigger BoxCollider ("handle") + ColliderSurface + RayInteractable.
    /// The handle is a trigger, so it never blocks physics (e.g. printed parts) – rays still hit it,
    /// because ColliderSurface uses Collider.Raycast, which ignores the trigger flag.
    ///
    /// Feedback: tint on hover / while dragging (<see cref="HoverTint"/>), pick-up / drop sounds and
    /// controller vibration.
    /// </summary>
    public class RayDraggable : MonoBehaviour
    {
        [Tooltip("Sound when the object is picked up with the ray.")]
        [SerializeField] AudioClip pickUpClip;
        [Tooltip("Sound when the object is released.")]
        [SerializeField] AudioClip dropClip;
        [SerializeField, Range(0f, 1f)] float volume = 0.6f;

        /// <summary>Hit point on the handle when the drag started (world space).</summary>
        public event Action<Vector3> DragStarted;
        /// <summary>Current pointing ray of the dragging hand / controller, every frame while dragging.</summary>
        public event Action<Ray> Dragging;
        public event Action DragEnded;

        public bool IsDragging { get; private set; }
        public bool IsHovered { get; private set; }
        public BoxCollider Handle { get; private set; }

        RayInteractable interactable;
        HoverTint tint;
        AudioSource audioSource;

#if UNITY_EDITOR
        void Reset()
        {
            pickUpClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/move_pick.wav");
            dropClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/move_drop.wav");
        }
#endif

        void Awake()
        {
            var handle = new GameObject("DragHandle");
            handle.transform.SetParent(transform, false);
            Handle = handle.AddComponent<BoxCollider>();
            Handle.isTrigger = true;
            Handle.enabled = false;                      // until SetHandle() / SetInteractable(true)

            var surface = handle.AddComponent<ColliderSurface>();
            surface.InjectAllColliderSurface(Handle);
            // Inject before Start: RayInteractable asserts its surface in Start
            interactable = handle.AddComponent<RayInteractable>();
            interactable.InjectAllRayInteractable(surface);
            interactable.WhenStateChanged += OnStateChanged;
            interactable.WhenPointerEventRaised += OnPointerEvent;
            interactable.enabled = false;

            tint = gameObject.AddComponent<HoverTint>();

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;               // 3D sound from the object's position
            audioSource.minDistance = 0.5f;
        }

        void OnDestroy()
        {
            if (!interactable) return;
            interactable.WhenStateChanged -= OnStateChanged;
            interactable.WhenPointerEventRaised -= OnPointerEvent;
        }

        /// <summary>Sets the handle box in this object's local space (call after the object has its final shape).</summary>
        public void SetHandle(Bounds localBounds)
        {
            Handle.center = localBounds.center;
            Handle.size = localBounds.size;
        }

        /// <summary>Enables or disables ray interaction, e.g. only after the object was placed in the room.</summary>
        public void SetInteractable(bool on)
        {
            Handle.enabled = on;
            interactable.enabled = on;
            tint.Refresh(gameObject);
        }

        void Update()
        {
            if (!IsDragging) return;
            foreach (RayInteractor interactor in interactable.SelectingInteractors)
            {
                Dragging?.Invoke(interactor.Ray);
                break;                                   // one ray is enough
            }
        }

        void OnPointerEvent(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select && !IsDragging)
            {
                IsDragging = true;
                Play(pickUpClip);
                ControllerHaptics.Pulse(this, 0.5f, 0.05f);
                DragStarted?.Invoke(evt.Pose.position);
            }
            else if ((evt.Type == PointerEventType.Unselect || evt.Type == PointerEventType.Cancel) && IsDragging)
            {
                IsDragging = false;
                Play(dropClip);
                ControllerHaptics.Pulse(this, 0.8f, 0.04f, 2);
                DragEnded?.Invoke();
            }
            UpdateTint();
        }

        void OnStateChanged(InteractableStateChangeArgs args)
        {
            IsHovered = args.NewState == InteractableState.Hover || args.NewState == InteractableState.Select;
            UpdateTint();
        }

        void UpdateTint() =>
            tint.Set(IsDragging ? HoverTint.Mode.Active : IsHovered ? HoverTint.Mode.Hover : HoverTint.Mode.None);

        /// <summary>Short haptic tick + a quiet click, e.g. when the object snaps to a new surface.</summary>
        public void Tick()
        {
            ControllerHaptics.Pulse(this, 0.35f, 0.03f);
            if (pickUpClip) audioSource.PlayOneShot(pickUpClip, volume * 0.4f);
        }

        void Play(AudioClip clip)
        {
            if (clip) audioSource.PlayOneShot(clip, volume);
        }
    }
}
