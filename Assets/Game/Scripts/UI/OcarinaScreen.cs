using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// The Blue Ocarina, played (from the bag's KEY ITEMS: Play). The game stands still; eight holes, one octave of
    /// the major scale from C: held, a note sounds for as long as it is held (a breath at its start, a soft end when
    /// it is let go); a new one pressed while another is held slurs into it, and letting go of it goes back to the one
    /// still held - one voice, as an ocarina has. Shift / R1 held raises a note by a half step, Ctrl / L1 drops it an
    /// octave. What is played is written on a staff that scrolls by.
    /// Keyboard: A S D F G H J K (or 1 - 8). Gamepad: the d-pad down, left, up, right, then the four face buttons
    /// bottom, left, top, right. Pause (Esc, the pad's menu button) or the bag's button puts it away (GameDirector).
    /// </summary>
    public class OcarinaScreen : MonoBehaviour
    {
        public GameObject root;
        [Tooltip("The sound of one held note (C5), made to loop without a seam (Audio/Sfx/ocarina_note.wav).")] public AudioClip noteClip;
        [Tooltip("The eight holes, low to high: the tile, its letter, the key or button under it.")] public Image[] holes;
        public TMPro.TMP_Text[] holeNames, holeKeys;
        [Tooltip("The four face buttons drawn as a diamond under each of the top four holes (bottom, left, top, right): the one to press is lit.")] public Image[] faceDots;
        [Tooltip("The staff the notes are written on (its middle line is B4); notes scroll from right to left.")] public RectTransform staff;
        public Sprite noteSprite;
        public TMPro.TMP_Text sharpTemplate, modeText;
        public PlayerInputReader input;
        public Color holeIdle = new Color(0.16f, 0.36f, 0.62f), holeLit = new Color(0.5f, 0.85f, 1f), noteColor = new Color(1f, 0.86f, 0.5f);

        public float lineGap = 16f, scrollSpeed = 160f;
        [Tooltip("How loud a note is played, before the volume sliders. Kept low: the whole mix is raised afterwards (LoudnessBoost), and a note pushed into its limiter turns harsh.")]
        public float loudness = 0.55f;

        static readonly int[] Scale = { 0, 2, 4, 5, 7, 9, 11, 12 };
        static readonly string[] Names = { "C", "D", "E", "F", "G", "A", "B", "C" };
        static readonly Key[] KeysA = { Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K };
        static readonly Key[] KeysB = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8 };

        bool open;
        int shownKind = -1;
        float savedScale = 1f, env, target;
        AudioSource voice;
        readonly List<int> held = new List<int>();          // the holes held, in the order they were pressed
        readonly bool[] blocked = new bool[8];              // held already when it was opened (the press that chose Play): not a note until let go
        int sounding = -1;
        float soundingPitch = 1f;

        class Written { public RectTransform rt; public TMPro.TMP_Text sharp; public float x; }
        readonly List<Written> written = new List<Written>();
        Written current;

        public bool IsOpen => open;
        /// <summary>The hole sounding now (-1 = none), and its pitch against C5 (tests).</summary>
        public int Sounding => sounding;
        public float SoundingPitch => soundingPitch;
        public int NotesWritten => written.Count;
        /// <summary>Tests: holes held, as if their keys were down (0 - 7), with the half-step and octave buttons.</summary>
        [System.NonSerialized] public bool[] testHeld;
        [System.NonSerialized] public bool testSharp, testLow;

        void Awake()
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.loop = true; voice.clip = noteClip; voice.volume = 0f;
            // a little room round the note, the way an ocarina rings: a soft, darkish tail that lets each note hang
            var room = gameObject.AddComponent<AudioReverbFilter>();
            room.reverbPreset = AudioReverbPreset.User;
            room.dryLevel = 0f; room.room = -900f; room.roomHF = -1400f; room.roomLF = 0f;
            room.decayTime = 1.7f; room.decayHFRatio = 0.55f;
            room.reflectionsLevel = -1400f; room.reflectionsDelay = 0.012f;
            room.reverbLevel = -500f; room.reverbDelay = 0.03f;
            room.diffusion = 100f; room.density = 100f;
        }

        public void Open()
        {
            if (open) return;
            open = true;
            savedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            root.SetActive(true);
            held.Clear(); sounding = -1; env = 0f; target = 0f;
            foreach (var w in written) if (w.rt != null) Destroy(w.rt.gameObject);
            written.Clear(); current = null;
            for (int i = 0; i < 8; i++) blocked[i] = Down(i);
            if (voice != null) { voice.clip = noteClip; voice.volume = 0f; voice.Play(); }
            if (AudioManager.Instance != null) AudioManager.Instance.HushMusic(true);         // (only the ocarina is heard)
            ShowKeys();
            Refresh();
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            if (voice != null) { voice.volume = 0f; voice.Stop(); }
            if (AudioManager.Instance != null) AudioManager.Instance.HushMusic(false);
            testHeld = null; testSharp = testLow = false;
            root.SetActive(false);
            Time.timeScale = savedScale;
            input?.ClearLatches();
        }

        bool Down(int i)
        {
            if (testHeld != null) return i < testHeld.Length && testHeld[i];
            var k = Keyboard.current;
            if (k != null && (k[KeysA[i]].isPressed || k[KeysB[i]].isPressed)) return true;
            var g = Gamepad.current;
            if (g == null) return false;
            switch (i)
            {
                case 0: return g.dpad.down.isPressed;
                case 1: return g.dpad.left.isPressed;
                case 2: return g.dpad.up.isPressed;
                case 3: return g.dpad.right.isPressed;
                case 4: return g.buttonSouth.isPressed;
                case 5: return g.buttonWest.isPressed;
                case 6: return g.buttonNorth.isPressed;
                default: return g.buttonEast.isPressed;
            }
        }

        bool Sharp()
        {
            if (testHeld != null) return testSharp;
            var k = Keyboard.current; var g = Gamepad.current;
            return (k != null && k.shiftKey.isPressed) || (g != null && g.rightShoulder.isPressed);
        }

        bool Low()
        {
            if (testHeld != null) return testLow;
            var k = Keyboard.current; var g = Gamepad.current;
            return (k != null && k.ctrlKey.isPressed) || (g != null && g.leftShoulder.isPressed);
        }

        int Semis(int hole) => Scale[hole] + (Sharp() ? 1 : 0) - (Low() ? 12 : 0);

        void Update()
        {
            if (!open) return;
            float dt = Time.unscaledDeltaTime;
            if (InputDeviceTracker.Kind != shownKind) ShowKeys();
            // which holes are held: a new one goes to the end of the list; the last of the list is the one that sounds
            for (int i = 0; i < 8; i++)
            {
                bool d = Down(i);
                if (blocked[i]) { if (d) continue; blocked[i] = false; }
                bool was = held.Contains(i);
                if (d && !was) { held.Add(i); Breath(); }
                else if (!d && was) held.Remove(i);
            }
            int want = held.Count > 0 ? held[held.Count - 1] : -1;
            if (want >= 0)
            {
                int semis = Semis(want);
                float p = Mathf.Pow(2f, semis / 12f);
                bool changed = want != sounding || !Mathf.Approximately(p, soundingPitch);
                sounding = want; soundingPitch = p;
                if (voice != null) voice.pitch = p;
                target = 1f;
                if (changed) Write(semis);
            }
            else { sounding = -1; target = 0f; current = null; }
            // the breath: quick to start, a little slower to die away
            env = Mathf.MoveTowards(env, target, dt * (target > env ? 22f : 7f));
            var s = SaveSystem.Settings;
            if (voice != null) voice.volume = Mathf.Clamp01(env * loudness * s.masterVolume * s.sfxVolume);
            Scroll(dt);
            Refresh();
        }

        void Breath()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play("ocarina_chiff", null, 0.5f, Random.Range(0.95f, 1.1f));
        }

        // ------------------------------------------------------------------ the staff

        /// <summary>Steps of the staff above the middle line (B4) for a note this many half steps over C5.</summary>
        static int StaffStep(int semis, out bool sharp)
        {
            int oct = Mathf.FloorToInt(semis / 12f), within = semis - oct * 12;
            int[] stepOf = { 0, 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 6 };
            bool[] isSharp = { false, true, false, true, false, false, true, false, true, false, true, false };
            sharp = isSharp[within];
            return 1 + stepOf[within] + oct * 7;        // (C5 is one step above B4)
        }

        void Write(int semis)
        {
            if (staff == null || noteSprite == null) return;
            int step = StaffStep(semis, out bool sharp);
            var go = new GameObject("Note", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(staff, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(lineGap * 1.5f, lineGap * 1.1f);
            var img = go.GetComponent<Image>();
            img.sprite = noteSprite; img.color = noteColor; img.raycastTarget = false;
            var w = new Written { rt = rt, x = -30f };
            rt.anchoredPosition = new Vector2(w.x, step * lineGap * 0.5f);
            if (sharp && sharpTemplate != null)
            {
                var sh = Instantiate(sharpTemplate, rt);
                sh.gameObject.SetActive(true);
                sh.rectTransform.anchoredPosition = new Vector2(-lineGap * 1.3f, 0f);
                w.sharp = sh;
            }
            written.Add(w);
            current = w;
        }

        void Scroll(float dt)
        {
            float width = staff != null ? staff.rect.width : 1000f;
            for (int i = written.Count - 1; i >= 0; i--)
            {
                var w = written[i];
                if (w.rt == null) { written.RemoveAt(i); continue; }
                if (w == current) continue;                     // (a note held stays at the pen)
                w.x -= scrollSpeed * dt;
                w.rt.anchoredPosition = new Vector2(w.x, w.rt.anchoredPosition.y);
                if (w.x < -width) { Destroy(w.rt.gameObject); written.RemoveAt(i); }
            }
            // the notes before the one held move up out of its way
            if (current != null)
                foreach (var w in written)
                    if (w != current && w.x > current.x - lineGap * 2.2f) { w.x = current.x - lineGap * 2.2f; w.rt.anchoredPosition = new Vector2(w.x, w.rt.anchoredPosition.y); }
        }

        // ------------------------------------------------------------------ the holes

        void ShowKeys()
        {
            shownKind = InputDeviceTracker.Kind;
            bool pad = InputDeviceTracker.UsingGamepad;
            string[] dpad = { "▼", "◄", "▲", "►" };
            for (int i = 0; i < 8; i++)
            {
                if (holeNames != null && i < holeNames.Length && holeNames[i] != null) holeNames[i].text = Names[i];
                if (holeKeys != null && i < holeKeys.Length && holeKeys[i] != null)
                    holeKeys[i].text = !pad ? KeysA[i].ToString() : i < 4 ? dpad[i] : "";
            }
            if (faceDots != null) foreach (var d in faceDots) if (d != null) d.gameObject.SetActive(pad);
            if (modeText != null)
                modeText.text = "Hold [ocsharp]  Half step up        Hold [oclow]  Octave down        [pause]  Put it away";
        }

        void Refresh()
        {
            if (holes == null) return;
            for (int i = 0; i < holes.Length && i < 8; i++)
                if (holes[i] != null) holes[i].color = i == sounding ? holeLit : held.Contains(i) ? Color.Lerp(holeIdle, holeLit, 0.4f) : holeIdle;
        }
    }
}
