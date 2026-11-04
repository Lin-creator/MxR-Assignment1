using System.Collections;
using UnityEngine;

namespace MxR.Common
{
    /// <summary>
    /// Short vibration pulses on the Touch controllers (OVRInput). Does nothing when hands are used,
    /// because only connected controllers vibrate.
    /// </summary>
    public static class ControllerHaptics
    {
        public static void Pulse(MonoBehaviour host, float amplitude, float duration, int count = 1,
            OVRInput.Controller controllers = OVRInput.Controller.Touch)
        {
            if (host && host.isActiveAndEnabled)
                host.StartCoroutine(Run(amplitude, duration, count, controllers));
        }

        static IEnumerator Run(float amplitude, float duration, int count, OVRInput.Controller controllers)
        {
            for (int i = 0; i < count; i++)
            {
                Set(1f, amplitude, controllers);
                yield return new WaitForSeconds(duration);
                Set(0f, 0f, controllers);
                if (i < count - 1) yield return new WaitForSeconds(duration);
            }
        }

        static void Set(float frequency, float amplitude, OVRInput.Controller controllers)
        {
            if ((controllers & OVRInput.Controller.LTouch) != 0 && OVRInput.IsControllerConnected(OVRInput.Controller.LTouch))
                OVRInput.SetControllerVibration(frequency, amplitude, OVRInput.Controller.LTouch);
            if ((controllers & OVRInput.Controller.RTouch) != 0 && OVRInput.IsControllerConnected(OVRInput.Controller.RTouch))
                OVRInput.SetControllerVibration(frequency, amplitude, OVRInput.Controller.RTouch);
        }
    }
}
