using System;
using System.Collections.Generic;
using MxR.Common;
using Oculus.Interaction;
using UnityEngine;

namespace MxR.Printer
{
    /// <summary>
    /// A part produced by the printer that can be picked up once the print is finished.
    ///
    /// The prefab is set up with the Interaction SDK Quick Action "Add Grab Interaction"
    /// (Rigidbody + collider + Grabbable + hand/controller grab interactables). This component
    /// - disables the grab interactables while the part is being printed (it is fixed to the bed),
    /// - enables them when the print is done; the part stays kinematic on the bed until it is grabbed,
    /// - detaches the part from the printer bed when it is released and lets physics take over
    ///   (gravity, throwing), so it lands on the real floor/table (Effect Mesh colliders).
    /// </summary>
    public class PrintedPart : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float grabHapticAmplitude = 0.3f;

        /// <summary>True once the part has been picked up from the printer bed.</summary>
        public bool TakenFromBed { get; private set; }
        public bool CanBeGrabbed => grabbable != null;
        /// <summary>Raised when the part is picked up from the bed for the first time.</summary>
        public event Action<PrintedPart> TakenFromPrinter;

        Rigidbody body;
        Grabbable grabbable;
        readonly List<MonoBehaviour> interactables = new List<MonoBehaviour>();

        void Awake()
        {
            body = GetComponentInChildren<Rigidbody>(true);
            grabbable = GetComponentInChildren<Grabbable>(true);
            foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
                if (mb is IInteractable) interactables.Add(mb);   // HandGrabInteractable, GrabInteractable, ...
        }

        void OnEnable()
        {
            if (grabbable) grabbable.WhenPointerEventRaised += OnPointerEvent;
        }

        void OnDisable()
        {
            if (grabbable) grabbable.WhenPointerEventRaised -= OnPointerEvent;
        }

        /// <summary>Called by the printer when printing starts: fixed to the bed, not grabbable.</summary>
        public void SetPrinting()
        {
            if (body)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
            SetGrabEnabled(false);
        }

        /// <summary>Called by the printer when the print is finished: can be picked up now.</summary>
        public void Release()
        {
            if (body) body.useGravity = true;
            // Grabbable keeps the body kinematic while held; on release it restores the old state
            // (kinematic) unless this flag is set -> the part falls / can be thrown after letting go.
            if (grabbable) grabbable.ForceKinematicDisabled = true;
            SetGrabEnabled(true);
        }

        void SetGrabEnabled(bool on)
        {
            foreach (var i in interactables) i.enabled = on;
        }

        void OnPointerEvent(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Select:
                    ControllerHaptics.Pulse(this, grabHapticAmplitude, 0.05f);
                    if (!TakenFromBed)
                    {
                        TakenFromBed = true;
                        TakenFromPrinter?.Invoke(this);
                    }
                    break;
                case PointerEventType.Unselect:
                    // detach from the moving bed only on release; re-parenting while held could make
                    // the grab transformer jump
                    if (transform.parent != null) transform.SetParent(null, true);
                    break;
            }
        }
    }
}
