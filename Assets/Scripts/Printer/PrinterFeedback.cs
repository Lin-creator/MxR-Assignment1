using MxR.Common;
using MxR.Sign;
using TMPro;
using UnityEngine;
using State = MxR.Printer.PrinterController.PrinterState;

namespace MxR.Printer
{
    /// <summary>
    /// Visual, audio and haptic feedback for the printer state:
    /// - status light on top of the frame (blue = ready, pulsing green = printing, blinking amber = paused, white = done)
    /// - text on the printer's glowing display (state, progress, remaining time)
    /// - sounds: motor hum while the nozzle moves, beeps on start/pause, chime + vibration when finished
    /// - status line on the wall sign
    /// </summary>
    [RequireComponent(typeof(PrinterController))]
    public class PrinterFeedback : MonoBehaviour
    {
        [Header("Status light")]
        [SerializeField] Material statusLightMaterial;
        [Tooltip("On top of the frame, user's right corner (printer local space).")]
        [SerializeField] Vector3 statusLightPosition = new Vector3(-0.165f, 0.465f, 0f);
        [SerializeField] float statusLightSize = 0.018f;
        [SerializeField] Color idleColor = new Color(0.2f, 0.5f, 1f);
        [SerializeField] Color printingColor = new Color(0.1f, 1f, 0.3f);
        [SerializeField] Color pausedColor = new Color(1f, 0.6f, 0.05f);
        [SerializeField] Color finishedColor = new Color(0.9f, 1f, 0.9f);

        [Header("Display")]
        [Tooltip("Screen normal in printer space, measured from the screen faces of Printer3D.fbx: straight forward, tilted up 25 deg.")]
        [SerializeField] Vector3 screenNormal = new Vector3(0f, 0.422f, 0.907f);
        [Tooltip("Text area on the screen (m); the screen itself is 0.079 x 0.049 m.")]
        [SerializeField] Vector2 screenSize = new Vector2(0.07f, 0.042f);
        [SerializeField] Color displayTextColor = new Color(0.85f, 0.95f, 1f);
        [SerializeField] TMP_FontAsset font;

        [Header("Sound")]
        [SerializeField] AudioClip motorLoop;
        [SerializeField] AudioClip startClip;
        [SerializeField] AudioClip pauseClip;
        [SerializeField] AudioClip doneClip;
        [SerializeField, Range(0f, 1f)] float motorVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] float effectsVolume = 0.8f;

        [Header("Wall sign (found automatically)")]
        [SerializeField] WallSign wallSign;

        PrinterController printer;
        Material lightMaterial;
        TextMeshPro display;
        AudioSource motorSource, effectsSource;
        int shownPercent = -1;
        State shownState;
        bool shownEngaged;
        string shownModel;
        bool shownPartOnBed;

#if UNITY_EDITOR
        void Reset()
        {
            T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            statusLightMaterial = Load<Material>("Assets/Materials/StatusLight.mat");
            motorLoop = Load<AudioClip>("Assets/Audio/printer_motor_loop.wav");
            startClip = Load<AudioClip>("Assets/Audio/beep_start.wav");
            pauseClip = Load<AudioClip>("Assets/Audio/beep_pause.wav");
            doneClip = Load<AudioClip>("Assets/Audio/chime_done.wav");
        }
#endif

        void Awake()
        {
            printer = GetComponent<PrinterController>();
            CreateStatusLight();
            CreateDisplayText();
            motorSource = CreateAudioSource("MotorAudio", motorLoop, true);
            effectsSource = CreateAudioSource("EffectsAudio", null, false);
        }

        void OnEnable() => printer.onStateChanged.AddListener(OnStateChanged);
        void OnDisable() => printer.onStateChanged.RemoveListener(OnStateChanged);

        void Start()
        {
            if (!wallSign) wallSign = FindFirstObjectByType<WallSign>();
            if (motorSource.clip) motorSource.Play();   // runs silently, volume follows the motion
            Refresh(force: true);
        }

        void Update()
        {
            UpdateLight();

            // motor hum only while the axes move (also during travel to/from the bed)
            bool moving = printer.State == State.Printing ||
                          (printer.Engage > 0f && printer.Engage < 1f);
            motorSource.volume = Mathf.MoveTowards(motorSource.volume, moving ? motorVolume : 0f, Time.deltaTime);

            Refresh(force: false);
        }

        void OnStateChanged(State state)
        {
            switch (state)
            {
                case State.Printing: Play(startClip); break;
                case State.Paused: Play(pauseClip); break;
                case State.Finished:
                    Play(doneClip);
                    ControllerHaptics.Pulse(this, 0.5f, 0.12f, count: 2);
                    break;
            }
            Refresh(force: true);
        }

        // ---------- status light ----------

        void CreateStatusLight()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "StatusLight";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = statusLightPosition;
            go.transform.localScale = Vector3.one * statusLightSize;
            var r = go.GetComponent<Renderer>();
            if (statusLightMaterial) r.sharedMaterial = statusLightMaterial;
            lightMaterial = r.material;   // own instance, so the colour can change
        }

        void UpdateLight()
        {
            Color c;
            float intensity;
            float t = Time.time;
            switch (printer.State)
            {
                case State.Printing: c = printingColor; intensity = 0.6f + 0.4f * Mathf.Sin(t * 4f); break;  // pulse
                case State.Paused: c = pausedColor; intensity = Mathf.Repeat(t, 1f) < 0.5f ? 1f : 0.15f; break; // blink
                case State.Finished: c = finishedColor; intensity = 1f; break;
                default: c = idleColor; intensity = 0.8f; break;
            }
            lightMaterial.SetColor("_BaseColor", c * 0.2f);
            lightMaterial.SetColor("_EmissionColor", c * intensity);
        }

        // ---------- display + wall sign ----------

        void CreateDisplayText()
        {
            Transform anchor = transform.Find("DisplayAnchor");
            if (!anchor) { Debug.LogWarning("[PrinterFeedback] DisplayAnchor not found.", this); return; }

            Vector3 n = screenNormal.normalized;
            // with a model selector the screen edges show the < > arrows, so keep the text in the middle
            Vector2 size = GetComponent<PrintModelSelector>() ? new Vector2(screenSize.x - 0.024f, screenSize.y) : screenSize;
            display = TextFactory.CreateAutoSized("DisplayText", transform, anchor.localPosition + n * 0.001f,
                size, 0.04f, 0.14f, TextAlignmentOptions.Center, displayTextColor, FontStyles.Normal, font);
            // text faces -Z, so its forward must point into the screen
            display.transform.localRotation = Quaternion.LookRotation(-n, Vector3.up);
        }

        void Refresh(bool force)
        {
            int percent = Mathf.FloorToInt(printer.Progress * 100f);
            bool engaged = printer.Engage >= 1f;
            string model = printer.JobName;
            if (!force && percent == shownPercent && printer.State == shownState && engaged == shownEngaged
                && model == shownModel && printer.HasPartOnBed == shownPartOnBed) return;
            shownModel = model;
            shownPartOnBed = printer.HasPartOnBed;
            shownPercent = percent;
            shownState = printer.State;
            shownEngaged = engaged;

            string time = FormatTime(printer.RemainingSeconds);
            string screen, sign;
            switch (printer.State)
            {
                case State.Printing when !engaged:
                    screen = "<b>STARTING</b>\n<size=60%>moving to bed</size>";
                    sign = "Status: starting print";
                    break;
                case State.Printing:
                    screen = $"<b>PRINTING</b>\n{percent}%  <size=60%>{time} left</size>\n<size=45%>tap to pause</size>";
                    sign = $"Status: printing {Named(model)}{percent}% ({time} left)";
                    break;
                case State.Paused:
                    screen = $"<b>PAUSED</b>\n{percent}%\n<size=45%>tap to resume</size>";
                    sign = $"Status: paused at {percent}%";
                    break;
                case State.Finished when printer.PrintsGrabbableParts && printer.HasPartOnBed:
                    screen = $"<b>DONE</b>\n<size=60%>grab your part</size>\n<size=45%>tap: print {Short(model)}</size>";
                    sign = $"Status: {Named(model)}finished - grab it from the bed!";
                    break;
                case State.Finished when !string.IsNullOrEmpty(model):
                    screen = $"<b>{model.ToUpperInvariant()}</b>\n<size=45%>tap to print</size>";
                    sign = "Status: choose a model with < > on the printer display, tap the middle to print";
                    break;
                case State.Finished:
                    screen = "<b>DONE</b>\n<size=60%>print finished</size>\n<size=45%>tap to print again</size>";
                    sign = "Status: print finished";
                    break;
                default:
                    screen = string.IsNullOrEmpty(model)
                        ? "<b>READY</b>\n<size=60%>tap to print</size>"
                        : $"<b>{model.ToUpperInvariant()}</b>\n<size=45%>tap to print</size>";
                    sign = string.IsNullOrEmpty(model)
                        ? "Status: ready - tap the printer's display to print"
                        : "Status: choose a model with < > on the printer display, tap the middle to print";
                    break;
            }
            if (display) display.text = screen;
            if (wallSign) wallSign.SetStatus(sign);
        }

        static string Named(string model) => string.IsNullOrEmpty(model) ? "" : model + " ";
        static string Short(string model) => string.IsNullOrEmpty(model) ? "again" : model.ToLowerInvariant();

        static string FormatTime(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }

        // ---------- audio ----------

        AudioSource CreateAudioSource(string name, AudioClip clip, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = loop;
            src.playOnAwake = false;
            src.volume = loop ? 0f : effectsVolume;
            src.spatialBlend = 1f;                       // 3D sound from the printer's position
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 0.5f;
            src.maxDistance = 8f;
            return src;
        }

        void Play(AudioClip clip)
        {
            if (clip) effectsSource.PlayOneShot(clip, effectsVolume);
        }
    }
}
