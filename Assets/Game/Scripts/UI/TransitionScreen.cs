using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gloomdelve
{
    /// <summary>Short "level complete" card between levels.</summary>
    public class TransitionScreen : MonoBehaviour
    {
        public GameObject root;
        public UnityEngine.UI.Text title;
        public UnityEngine.UI.Text body;
        public UnityEngine.UI.Text hint;
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
            if (gp != null && t > 0.5f && gp.buttonSouth.wasPressedThisFrame) key = true;
            if (key || t >= autoDelay)
            {
                done = true;
                onContinue?.Invoke();
            }
        }
    }
}
