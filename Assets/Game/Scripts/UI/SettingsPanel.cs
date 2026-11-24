using System;
using UnityEngine;
using UnityEngine.UI;

namespace Gloomdelve
{
    public class SettingsPanel : MonoBehaviour
    {
        public Slider master;
        public Slider music;
        public Slider sfx;
        public Toggle fullscreen;
        public Toggle shake;
        public Button back;
        [Tooltip("Opens the controls list, where keyboard keys can be changed.")] public Button keys;
        [Tooltip("Steps through the fixed window sizes (made from the Keys button when the scene has none).")] public Button windowSize;
        TMPro.TMP_Text windowSizeText;
        public TMPro.TMP_Text masterValue, musicValue, sfxValue;

        public Action onBack;
        public Action onKeys;

        void Awake()
        {
            MakeWindowSize();
            master.onValueChanged.AddListener(v => { SaveSystem.Settings.masterVolume = v; Changed(); });
            music.onValueChanged.AddListener(v => { SaveSystem.Settings.musicVolume = v; Changed(); });
            sfx.onValueChanged.AddListener(v => { SaveSystem.Settings.sfxVolume = v; Changed(); Sfx.PlayUI("coin", 0.6f); });
            fullscreen.onValueChanged.AddListener(v => { SaveSystem.Settings.fullscreen = v; Changed(); GameServices.ApplyDisplaySettings(); });
            UIUtil.Wire(windowSize, () => { GameServices.NextWindowSize(); Labels(); });
            shake.onValueChanged.AddListener(v => { SaveSystem.Settings.screenShake = v; Changed(); });
            UIUtil.Wire(back, () => onBack?.Invoke());
            UIUtil.Wire(keys, () => onKeys?.Invoke());
        }

        void OnEnable()
        {
            SaveSystem.EnsureLoaded();
            var s = SaveSystem.Settings;
            master.SetValueWithoutNotify(s.masterVolume);
            music.SetValueWithoutNotify(s.musicVolume);
            sfx.SetValueWithoutNotify(s.sfxVolume);
            fullscreen.SetIsOnWithoutNotify(s.fullscreen);
            shake.SetIsOnWithoutNotify(s.screenShake);
            Labels();
        }

        void Changed()
        {
            SaveSystem.SaveSettings();
            Labels();
        }

        void Labels()
        {
            var s = SaveSystem.Settings;
            if (masterValue) masterValue.text = Mathf.RoundToInt(s.masterVolume * 100) + "%";
            if (musicValue) musicValue.text = Mathf.RoundToInt(s.musicVolume * 100) + "%";
            if (sfxValue) sfxValue.text = Mathf.RoundToInt(s.sfxVolume * 100) + "%";
            if (windowSizeText != null)
            {
                // full screen always runs at the screen's own size; the sizes to pick from are for the window
                var size = s.fullscreen ? GameServices.NativeSize : GameServices.WindowSizes[GameServices.WindowSizeIndex];
                windowSizeText.text = (s.fullscreen ? "SCREEN SIZE  " : "WINDOW SIZE  ") + size.x + " x " + size.y;
                windowSize.interactable = !s.fullscreen && GameServices.MaxWindowSizeIndex > 0;
            }
        }

        void Update()
        {
            // F11 changes full screen while this is open: the toggle and the size follow it
            if (fullscreen != null && fullscreen.isOn != SaveSystem.Settings.fullscreen)
            {
                fullscreen.SetIsOnWithoutNotify(SaveSystem.Settings.fullscreen);
                Labels();
            }
        }

        /// <summary>
        /// The window size row, under the screen shake toggle: a copy of the Keys button, which moves one row down
        /// (the panel grows by that row, so BACK stays clear at the bottom).
        /// </summary>
        void MakeWindowSize()
        {
            if (windowSize == null && keys != null)
            {
                const float row = 74f;
                windowSize = Instantiate(keys, keys.transform.parent);
                windowSize.name = "WindowSize";
                windowSize.onClick = new Button.ButtonClickedEvent();
                windowSize.transform.SetSiblingIndex(keys.transform.GetSiblingIndex());
                var panel = (RectTransform)transform;
                panel.sizeDelta += new Vector2(0f, row);
                var kr = (RectTransform)keys.transform;
                kr.anchoredPosition -= new Vector2(0f, row);
                // everything placed from the top above the Keys button stays where it was
                ((RectTransform)windowSize.transform).anchoredPosition = kr.anchoredPosition + new Vector2(0f, row);
            }
            if (windowSize != null) windowSizeText = windowSize.GetComponentInChildren<TMPro.TMP_Text>(true);
        }

        public Selectable First => master;
    }
}
