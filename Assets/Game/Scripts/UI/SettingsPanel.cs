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
        public TMPro.TMP_Text masterValue, musicValue, sfxValue;

        public Action onBack;
        public Action onKeys;

        void Awake()
        {
            master.onValueChanged.AddListener(v => { SaveSystem.Settings.masterVolume = v; Changed(); });
            music.onValueChanged.AddListener(v => { SaveSystem.Settings.musicVolume = v; Changed(); });
            sfx.onValueChanged.AddListener(v => { SaveSystem.Settings.sfxVolume = v; Changed(); Sfx.PlayUI("coin", 0.6f); });
            fullscreen.onValueChanged.AddListener(v => { SaveSystem.Settings.fullscreen = v; Changed(); GameServices.ApplyDisplaySettings(); });
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
        }

        public Selectable First => master;
    }
}
