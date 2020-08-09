using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
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
                        // TODO: AnimationControllerの自動作成
                        /*
                        string notSettingMessage, createMessage;
                        if (_tab == Tab.Standing)
                        {
                            notSettingMessage = LocalizeText.instance.langPair.noCustomStandingAnimsMessageText;
                            createMessage = LocalizeText.instance.langPair.createCustomStandingAnimsButtonText;
                        }
                        else
                        {
                            notSettingMessage = LocalizeText.instance.langPair.noCustomSittingAnimsMessageText;
                            createMessage = LocalizeText.instance.langPair.createCustomSittingAnimsButtonText;
                        }
                        EditorGUILayout.HelpBox(notSettingMessage, MessageType.Warning);

                        if (GUILayout.Button(createMessage))
                        {
                            var fileName = "CO_" + originalAvatar.animator.gameObject.name + ".overrideController";
                            saveFolderPath = "Assets/" + originalAvatar.animator.gameObject.name + "/";
                            var fullFolderPath = Path.GetFullPath(saveFolderPath);
                            if (!Directory.Exists(fullFolderPath))
                            {
                                Directory.CreateDirectory(fullFolderPath);
                                AssetDatabase.Refresh();
                            }
                            var createdCustomOverrideController = InstantiateVrcCustomOverideController(saveFolderPath + fileName);

                            if (_tab == Tab.Standing)
                            {
                                // TODO: Avatars3.0へ対応させる
                                //originalAvatar.descriptor.CustomStandingAnims = createdCustomOverrideController;
                                //editAvatar.descriptor.CustomStandingAnims = createdCustomOverrideController;
                            }
                            else
                            {
                                // TODO: Avatars3.0へ対応させる
                                //originalAvatar.descriptor.CustomSittingAnims = createdCustomOverrideController;
                                //editAvatar.descriptor.CustomSittingAnims = createdCustomOverrideController;
                            }

                            originalAvatar.LoadAvatarInfo();
                            editAvatar.LoadAvatarInfo();
                        }
                        */

                        // TODO: SittingをStandingと同じにする
                        /*
                        if (_tab == Tab.Sitting)
                        {
                            using (new EditorGUI.DisabledGroupScope(editAvatar.fxController == null))
                            {
                                if (GUILayout.Button(LocalizeText.instance.langPair.setToSameAsCustomStandingAnimsButtonText))
                                {
                                    // TODO: Avatars3.0へ対応させる
                                    //var customStandingAnimsController = originalAvatar.descriptor.CustomStandingAnims;
                                    //originalAvatar.descriptor.CustomSittingAnims = customStandingAnimsController;
                                    //editAvatar.descriptor.CustomSittingAnims = customStandingAnimsController;
                                    originalAvatar.LoadAvatarInfo();
                                    editAvatar.LoadAvatarInfo();
                                }
                            }
                        }
                        */
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
            var overrideController = AssetDatabase.LoadAssetAtPath(newFilePath, typeof(AnimatorOverrideController)) as AnimatorOverrideController;

            return overrideController;
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
    }
}
