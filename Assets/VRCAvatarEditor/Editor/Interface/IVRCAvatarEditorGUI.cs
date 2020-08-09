using UnityEngine;

namespace VRCAvatarEditor.Avatars3
{
    public interface IVRCAvatarEditorGUI
    {
        bool DrawGUI(GUILayoutOption[] layoutOptions);
        void DrawSettingsGUI();
        void LoadSettingData(SettingData settingAsset);
        void SaveSettingData(ref SettingData settingAsset);
        void Dispose();
    }
}
