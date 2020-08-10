using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Avatar = VRCAvatarEditor.Avatars3.Avatar;

namespace VRCAvatarEditor.Avatars3
{
    public class AnimationsGUI : Editor, IVRCAvatarEditorGUI
    {
        private Avatar editAvatar;
        private Avatar originalAvatar;
        private VRCAvatarEditorGUI vrcAvatarEditorGUI;
        private FaceEmotionGUI faceEmotionGUI;

        public static readonly string[] HANDANIMS = { "FIST", "FINGERPOINT", "ROCKNROLL", "HANDOPEN", "THUMBSUP", "VICTORY", "HANDGUN" };
        public static readonly string[] EMOTEANIMS = { "EMOTE1", "EMOTE2", "EMOTE3", "EMOTE4", "EMOTE5", "EMOTE6", "EMOTE7", "EMOTE8" };
        public static readonly string[] EMOTIONSTATES = { "Idle", "Fist", "Open", "Point", "Peace", "RockNRoll", "Gun", "Thumbs up" };

        private bool[] pathMissing;

        private GUIStyle normalStyle = new GUIStyle();
        private GUIStyle errorStyle = new GUIStyle();

        private bool failedAutoFixMissingPath = false;

        string titleText;
        AnimatorController controller;
        private bool showEmoteAnimations = false;

        private Tab _tab = Tab.Standing;

        private enum Tab
        {
            Standing,
            Sitting,
        }

        private string saveFolderPath;

        private int layerIndex = 0;

        public void Initialize(ref Avatar editAvatar,
                               Avatar originalAvatar,
                               string saveFolderPath,
                               VRCAvatarEditorGUI vrcAvatarEditorGUI,
                               FaceEmotionGUI faceEmotionGUI)
        {
            this.editAvatar = editAvatar;
            this.originalAvatar = originalAvatar;
            this.vrcAvatarEditorGUI = vrcAvatarEditorGUI;
            this.faceEmotionGUI = faceEmotionGUI;
            UpdateSaveFolderPath(saveFolderPath);

            errorStyle.normal.textColor = Color.red;

            if (editAvatar != null && editAvatar.fxController != null)
            {
                // TODO: AnimationClipのバリエーション機能
                //ValidateAnimatorOverrideController(editAvatar.animator, editAvatar.fxController);
            }
        }

        public bool DrawGUI(GUILayoutOption[] layoutOptions)
        {
            // 設定済みアニメーション一覧
            using (new EditorGUILayout.VerticalScope(GUI.skin.box, layoutOptions))
            {
                if (originalAvatar != null)
                    controller = originalAvatar.fxController;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("FX Layer", EditorStyles.boldLabel);

                    // TODO: Emote一覧
                    //string btnText;
                    //if (!showEmoteAnimations)
                    //{
                    //    btnText = LocalizeText.instance.langPair.emoteButtonText;
                    //}
                    //else
                    //{
                    //    btnText = LocalizeText.instance.langPair.faceAndHandButtonText;
                    //}

                    //if (GUILayout.Button(btnText))
                    //{
                    //    showEmoteAnimations = !showEmoteAnimations;
                    //}
                }

                EditorGUILayout.Space();

                if (controller != null)
                {
                    //if (!showEmoteAnimations)
                    //{
                    var layerNames = controller.layers.Select(l => l.name).ToArray();
                    editAvatar.targetFxLayerIndex = EditorGUILayout.Popup("Layer", editAvatar.targetFxLayerIndex, layerNames);
                    var states = controller.layers[editAvatar.targetFxLayerIndex].stateMachine.states.OrderBy(s => s.state.name).ToArray();
                    pathMissing = new bool[states.Length];

                    if (!states.Any())
                    {
                        EditorGUILayout.HelpBox("Have No AnimationClip", MessageType.Info);
                    }

                    AnimationClip anim;
                    for (int i = 0; i < states.Length; i++)
                    {
                        var stateName = states[i].state.name;
                        anim = states[i].state.motion as AnimationClip;

                        using (new EditorGUILayout.HorizontalScope(GUILayout.Width(350)))
                        {
                            GUILayout.Label((i + 1) + ":" + stateName, (pathMissing[i]) ? errorStyle : normalStyle, GUILayout.Width(100));

                            states[i].state.motion = EditorGUILayout.ObjectField(
                                string.Empty,
                                anim,
                                typeof(AnimationClip),
                                true,
                                GUILayout.Width(200)
                            ) as AnimationClip;

                            using (new EditorGUI.DisabledGroupScope(anim == null || anim.name.StartsWith("proxy_")))
                            {
                                if (GUILayout.Button(LocalizeText.instance.langPair.edit, GUILayout.Width(50)))
                                {
                                    if (vrcAvatarEditorGUI.currentTool != VRCAvatarEditorGUI.ToolFunc.FaceEmotion)
                                    {
                                        vrcAvatarEditorGUI.currentTool = VRCAvatarEditorGUI.ToolFunc.FaceEmotion;
                                        vrcAvatarEditorGUI.TabChanged();
                                    }
                                    FaceEmotion.ApplyAnimationProperties(anim, ref editAvatar);
                                    faceEmotionGUI.ChangeSaveAnimationState(anim.name,
                                        i,
                                        anim);
                                }
                            }
                        }
                    }
                    //}
                    // TODO: Emote一覧
                    //else
                    //{
                    //    AnimationClip anim;
                    //    foreach (var emoteAnim in EMOTEANIMS)
                    //    {
                    //        if (emoteAnim == controller[emoteAnim].name)
                    //            anim = null;
                    //        else
                    //            anim = controller[emoteAnim];

                    //        using (new EditorGUILayout.HorizontalScope(GUILayout.Width(350)))
                    //        {
                    //            GUILayout.Label(emoteAnim, GUILayout.Width(90));

                    //            controller[emoteAnim] = EditorGUILayout.ObjectField(
                    //                string.Empty,
                    //                anim,
                    //                typeof(AnimationClip),
                    //                true,
                    //                GUILayout.Width(250)
                    //            ) as AnimationClip;
                    //        }
                    //    }
                    //}
                }
                else
                {
                    if (editAvatar.descriptor == null)
                    {
                        EditorGUILayout.HelpBox(LocalizeText.instance.langPair.noAvatarMessageText, MessageType.Warning);
                    }
                    else
                    {
                        string notSettingMessage = "No Setting Fx Layer Controller";
                        string createMessage = "Create Fx Layer Controller";
                        EditorGUILayout.HelpBox(notSettingMessage, MessageType.Warning);

                        if (GUILayout.Button(createMessage))
                        {
                            CreatePlayableLayerController(originalAvatar, editAvatar);
                        }
                    }
                }

                if (pathMissing != null && pathMissing.Any(x => x))
                {
                    var warningMessage = (failedAutoFixMissingPath) ? LocalizeText.instance.langPair.failAutoFixMissingPathMessageText : LocalizeText.instance.langPair.existMissingPathMessageText;
                    EditorGUILayout.HelpBox(warningMessage, MessageType.Warning);
                    // TODO: Missingの自動修正
                    //using (new EditorGUI.DisabledGroupScope(failedAutoFixMissingPath))
                    //{
                    //    if (GUILayout.Button(LocalizeText.instance.langPair.autoFix))
                    //    {
                    //        failedAutoFixMissingPath = false;
                    //        for (int i = 0; i < pathMissing.Length; i++)
                    //        {
                    //            if (!pathMissing[i]) continue;
                    //            var result = GatoUtility.TryFixMissingPathInAnimationClip(
                    //                                editAvatar.animator,
                    //                                editAvatar.fxController[HANDANIMS[i]]);
                    //            pathMissing[i] = !result;
                    //            failedAutoFixMissingPath = !result;
                    //        }
                    //    }
                    //}
                }
            }
            return false;
        }

        public void DrawSettingsGUI() { }
        public void LoadSettingData(SettingData settingAsset) { }
        public void SaveSettingData(ref SettingData settingAsset) { }
        public void Dispose() { }

        /// <summary>
        /// VRChat用のCustomOverrideControllerの複製を取得する
        /// </summary>
        /// <param name="animFolder"></param>
        /// <returns></returns>
        private AnimatorOverrideController InstantiateVrcCustomOverideController(string newFilePath)
        {
            string path = VRCAvatarEditorGUI.GetVRCSDKFilePath("CustomOverrideEmpty");

            newFilePath = AssetDatabase.GenerateUniqueAssetPath(newFilePath);
            AssetDatabase.CopyAsset(path, newFilePath);
            var overrideController = AssetDatabase.LoadAssetAtPath(newFilePath, typeof(AnimatorController)) as AnimatorOverrideController;

            return overrideController;
        }

        private static AnimatorController InstantiateFxController(string newFilePath)
        {
            string path = VRCAvatarEditorGUI.GetVRCSDKFilePath("vrc_AvatarV3HandsLayer");

            newFilePath = AssetDatabase.GenerateUniqueAssetPath(newFilePath);
            AssetDatabase.CopyAsset(path, newFilePath);
            var controller = AssetDatabase.LoadAssetAtPath(newFilePath, typeof(AnimatorController)) as AnimatorController;

            return controller;
        }

        public void UpdateSaveFolderPath(string saveFolderPath)
        {
            this.saveFolderPath = saveFolderPath;
        }

        private void ValidateAnimatorOverrideController(Animator animator, AnimatorOverrideController controller)
        {
            for (int i = 0; i < HANDANIMS.Length; i++)
            {
                var clip = controller[HANDANIMS[i]];
                if (clip.name == HANDANIMS[i])
                {
                    pathMissing[i] = false;
                }
                else
                {
                    pathMissing[i] = !GatoUtility.ValidateMissingPathInAnimationClip(animator, clip);
                }
            }
        }

        public void ResetPathMissing(string HandAnimName)
        {
            var index = Array.IndexOf(HANDANIMS, HandAnimName);
            if (index == -1) return;
            pathMissing[index] = false;
            failedAutoFixMissingPath = false;
        }

        private static void EnableCustomPlayableLayers(VRCAvatarEditor.Avatars3.Avatar avatar)
        {
            avatar.descriptor.customizeAnimationLayers = true;
        }

        public static void CreateGestureController(Avatar originalAvatar, Avatar editAvatar)
        {
            if (!originalAvatar.descriptor.customizeAnimationLayers)
            {
                EnableCustomPlayableLayers(originalAvatar);
                EnableCustomPlayableLayers(editAvatar);
            }

            if (originalAvatar.gestureController is null)
            {
                string saveFolderPath;
                if (originalAvatar.fxController != null)
                {
                    saveFolderPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(originalAvatar.fxController));
                }
                else
                {
                    saveFolderPath = "Assets/" + originalAvatar.animator.gameObject.name + "/";
                }

                var fileName = $"Gesture_HandsLayer_{ originalAvatar.animator.gameObject.name}.controller";
                var createdGestureController = InstantiateFxController(Path.Combine(saveFolderPath, fileName));

                originalAvatar.descriptor.baseAnimationLayers[2].isDefault = false;
                editAvatar.descriptor.baseAnimationLayers[2].isDefault = false;
                originalAvatar.descriptor.baseAnimationLayers[2].animatorController = createdGestureController;
                editAvatar.descriptor.baseAnimationLayers[2].animatorController = createdGestureController;
            }

            originalAvatar.LoadAvatarInfo();
            editAvatar.LoadAvatarInfo();
        }

        public static void CreatePlayableLayerController(Avatar originalAvatar, Avatar editAvatar)
        {
            var fileName = $"Fx_HandsLayer_{ originalAvatar.animator.gameObject.name}.controller";
            var saveFolderPath = "Assets/" + originalAvatar.animator.gameObject.name + "/";
            var fullFolderPath = Path.GetFullPath(saveFolderPath);
            if (!Directory.Exists(fullFolderPath))
            {
                Directory.CreateDirectory(fullFolderPath);
                AssetDatabase.Refresh();
            }
            var createdFxController = InstantiateFxController(Path.Combine(saveFolderPath,fileName));

            // まばたき防止機構をつける
            SetNoBlink(createdFxController);

            if (!originalAvatar.descriptor.customizeAnimationLayers)
            {
                EnableCustomPlayableLayers(originalAvatar);
                EnableCustomPlayableLayers(editAvatar);
            }
            originalAvatar.descriptor.baseAnimationLayers[4].isDefault = false;
            originalAvatar.descriptor.baseAnimationLayers[4].animatorController = createdFxController;
            editAvatar.descriptor.baseAnimationLayers[4].isDefault = false;
            editAvatar.descriptor.baseAnimationLayers[4].animatorController = createdFxController;

            if (originalAvatar.gestureController is null)
            {
                fileName = $"Gesture_HandsLayer_{ originalAvatar.animator.gameObject.name}.controller";
                var createdGestureController = InstantiateFxController(Path.Combine(saveFolderPath, fileName));

                originalAvatar.descriptor.baseAnimationLayers[2].isDefault = false;
                originalAvatar.descriptor.baseAnimationLayers[2].animatorController = createdGestureController;
                editAvatar.descriptor.baseAnimationLayers[2].isDefault = false;
                editAvatar.descriptor.baseAnimationLayers[2].animatorController = createdGestureController;
            }

            originalAvatar.LoadAvatarInfo();
            editAvatar.LoadAvatarInfo();
        }

        private static void SetNoBlink(AnimatorController fxController)
        {
            var layers = fxController.layers.Where(l => l.name == "Left Hand" || l.name == "Right Hand");
            foreach (var layer in layers)
            {
                var states = layer.stateMachine.states;

                foreach (var state in states)
                {
                    var stateName = state.state.name;
                    if (!EMOTIONSTATES.Contains(stateName)) continue;

                    var control = state.state.AddStateMachineBehaviour(typeof(VRCAnimatorTrackingControl)) as VRCAnimatorTrackingControl;

                    if (stateName == "Idle")
                    {
                        control.trackingEyes = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Tracking;
                    }
                    else
                    {
                        control.trackingEyes = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Animation;
                    }
                }
            }
        }
    }
}
