using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gloomdelve
{
    /// <summary>Short "level complete" card between levels.</summary>
    public class TransitionScreen : MonoBehaviour
    {
        public GameObject root;
        public TMPro.TMP_Text title;
        public TMPro.TMP_Text body;
        public TMPro.TMP_Text hint;
        public float autoContinue = 4f;

        Action onContinue;
        float t, autoDelay;
        bool done;

        void Awake() { root.SetActive(false); }

        public void Show(StageDef stage, RunState run, int goldGained, float autoDelayOverride, Action cont)
        {
            root.SetActive(true);
            onContinue = cont;
            t = 0f; done = false;
            autoDelay = autoDelayOverride > 0f ? autoDelayOverride : autoContinue;
            title.text = $"{stage.displayName} COMPLETE";
            body.text =
                $"Level time:  {UIUtil.FormatTime(run.levelTime)}\n" +
                $"Total time:  {UIUtil.FormatTime(run.totalTime)}\n" +
                $"Gold found:  ${goldGained}     (total ${run.gold})\n" +
                $"Health {run.health}    Bombs {run.bombs}    Ropes {run.ropes}";
            hint.text = "Press [jump] to continue";
        }

        void Update()
        {
            if (!root.activeSelf || done) return;
            t += Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            bool key = kb != null && t > 0.5f && (KeyBindings.Pressed("jump") || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
            var gp = Gamepad.current;
            if (t > 0.5f && InputDeviceTracker.PadConfirmPressed) key = true;
            // The hint shows the jump button: whatever jump is on this device has to work (on a pad that is not
            // the bottom face button any more), and so does the interact button.
            var gd = GameDirector.Instance;
            if (gd != null && gd.input != null && t > 0.5f && gd.input.ConfirmPressed) key = true;
            if (key || t >= autoDelay)
            {
                done = true;
                onContinue?.Invoke();
            }
        }
    }
}
