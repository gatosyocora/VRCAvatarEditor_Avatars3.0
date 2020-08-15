using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Avatar = VRCAvatarEditor.Avatars3.Avatar;

namespace VRCAvatarEditor.Avatars3
{
    public class FaceEmotionGUI : Editor, IVRCAvatarEditorGUI
    {
        private Avatar editAvatar;
        private Avatar originalAvatar;
        private VRCAvatarEditorGUI parentWindow;
        private AnimationsGUI animationsGUI;

        private static readonly string DEFAULT_ANIM_NAME = "faceAnim";
        private HandPose.HandPoseType selectedHandAnim = HandPose.HandPoseType.NoSelection;

        private Vector2 scrollPos = Vector2.zero;

        public enum SortType
        {
            UnSort,
            AToZ,
        }

        public SortType selectedSortType = SortType.UnSort;
        public List<string> blendshapeExclusions = new List<string> { "vrc.v_", "vrc.blink_", "vrc.lowerlid_", "vrc.owerlid_", "mmd" };

        private bool isOpeningBlendShapeExclusionList = false;

        private string animName;

        private AnimationClip handPoseAnim;

        private bool usePreviousAnimationOnHandAnimation;

        private int selectedStateIndex = 0;

        private bool setLeftAndRight = true;

        public void Initialize(ref Avatar editAvatar, Avatar originalAvatar, string saveFolderPath, EditorWindow window, AnimationsGUI animationsGUI)
        {
            this.editAvatar = editAvatar;
            this.originalAvatar = originalAvatar;
            animName = DEFAULT_ANIM_NAME;
            this.parentWindow = window as VRCAvatarEditorGUI;
            this.animationsGUI = animationsGUI;
        }

        public bool DrawGUI(GUILayoutOption[] layoutOptions)
        {
            EditorGUILayout.LabelField(LocalizeText.instance.langPair.faceEmotionTitle, EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                using (new EditorGUI.DisabledScope(editAvatar.descriptor == null))
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(LocalizeText.instance.langPair.loadAnimationButtonText))
                    {
                        FaceEmotion.LoadAnimationProperties(this, parentWindow);
                    }

                    if (GUILayout.Button(LocalizeText.instance.langPair.setToDefaultButtonText))
                    {
                        if (EditorUtility.DisplayDialog(
                                LocalizeText.instance.langPair.setToDefaultDialogTitleText,
                                LocalizeText.instance.langPair.setToDefaultDialogMessageText,
                                LocalizeText.instance.langPair.ok, LocalizeText.instance.langPair.cancel))
                        {
                            FaceEmotion.SetToDefaultFaceEmotion(ref editAvatar, originalAvatar);
                        }
                    }

                    if (GUILayout.Button(LocalizeText.instance.langPair.resetToDefaultButtonText))
                    {
                        FaceEmotion.ResetToDefaultFaceEmotion(ref editAvatar);
                        ChangeSaveAnimationState();
                    }
                }

                if (editAvatar.skinnedMeshList != null)
                {
                    BlendShapeListGUI();
                }

                animName = EditorGUILayout.TextField(LocalizeText.instance.langPair.animClipFileNameLabel, animName);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(LocalizeText.instance.langPair.animClipSaveFolderLabel, originalAvatar.animSavedFolderPath);

                    if (GUILayout.Button(LocalizeText.instance.langPair.selectFolder, GUILayout.Width(100)))
                    {
                        originalAvatar.animSavedFolderPath = EditorUtility.OpenFolderPanel(LocalizeText.instance.langPair.selectFolderDialogMessageText, originalAvatar.animSavedFolderPath, string.Empty);
                        originalAvatar.animSavedFolderPath = $"{FileUtil.GetProjectRelativePath(originalAvatar.animSavedFolderPath)}{Path.DirectorySeparatorChar}";
                        if (originalAvatar.animSavedFolderPath == $"{Path.DirectorySeparatorChar}") originalAvatar.animSavedFolderPath = $"Assets{Path.DirectorySeparatorChar}";
                        parentWindow.animationsGUI.UpdateSaveFolderPath(originalAvatar.animSavedFolderPath);
                    }

                }

                EditorGUILayout.Space();

                string[] stateNames;
                ChildAnimatorState[] states = null;

                if (editAvatar.fxController != null)
                {
                    var stateMachine = editAvatar.fxController.layers[editAvatar.targetFxLayerIndex].stateMachine;
                    states = stateMachine.states
                                .Where(s => !(s.state.motion is BlendTree))
                                .OrderBy(s => s.state.name)
                                .ToArray();
                    stateNames = states.Select((s, i) => $"{i + 1}:{s.state.name}").ToArray();
                    
                    EditorGUILayout.LabelField("Layer", editAvatar.fxController.layers[editAvatar.targetFxLayerIndex].name);

                    // Stateがないとき, 自動設定できない
                    if (states.Any())
                    {
                        using (var check = new EditorGUI.ChangeCheckScope())
                        {
                            selectedStateIndex = EditorGUILayout.Popup(
                                "State",
                                selectedStateIndex,
                                stateNames);

                            //if (check.changed)
                            //{
                            //    ChangeSelectionHandAnimation();
                            //}
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Create Only (not set to AniamatorController) because Exist no states in this layer.", MessageType.Info);
                    }

                    if (editAvatar.gestureController != null)
                    {
                        ChildAnimatorState handState = default;
                        if (editAvatar.gestureController.layers.Length > editAvatar.targetFxLayerIndex &&
                            states.Any())
                        {
                            handState = editAvatar.gestureController.layers[editAvatar.targetFxLayerIndex]
                                            .stateMachine.states
                                            .Where(s => !(s.state.motion is BlendTree))
                                            .Where(s => s.state.name == states[selectedStateIndex].state.name)
                                            .SingleOrDefault();
                        }

                        // LayerまたはStateが見つからない時はGestureまわりは利用できない
                        if (handState.state != null)
                        {
                            handPoseAnim = handState.state.motion as AnimationClip;
                            using (var check = new EditorGUI.ChangeCheckScope())
                            {
                                handPoseAnim = EditorGUILayout.ObjectField(LocalizeText.instance.langPair.handPoseAnimClipLabel, handPoseAnim, typeof(AnimationClip), true) as AnimationClip;
                                if (check.changed)
                                {
                                    handState.state.motion = handPoseAnim;
                                    EditorUtility.SetDirty(editAvatar.gestureController);
                                }
                            }
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("HandPose Animation can't be changed because not found target layer or state.", MessageType.Info);
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No Gesture Layer Controller", MessageType.Warning);

                        if (GUILayout.Button("Create Gesture Layer Controller"))
                        {
                            AnimationsGUI.CreateGestureController(originalAvatar, editAvatar);

                            parentWindow.TabChanged();
                        }
                    }

                    setLeftAndRight = EditorGUILayout.ToggleLeft("Set to Left Hand & Right Hand Layer", setLeftAndRight);
                }
                else
                {
                    EditorGUILayout.HelpBox("No Fx Layer Controller", MessageType.Error);

                    if (GUILayout.Button("Create Fx Layer Controller"))
                    {
                        AnimationsGUI.CreatePlayableLayerController(originalAvatar, editAvatar);

                        parentWindow.TabChanged();
                    }
                }

                GUILayout.Space(20);

                using (new EditorGUI.DisabledGroupScope(originalAvatar.fxController == null))
                {
                    if (GUILayout.Button(LocalizeText.instance.langPair.createAnimFileButtonText))
                    {
                        var controller = originalAvatar.fxController;

                        var createdAnimClip = FaceEmotion.CreateBlendShapeAnimationClip(animName, originalAvatar.animSavedFolderPath, ref editAvatar, ref blendshapeExclusions, editAvatar.descriptor.gameObject);
                        //if (selectedHandAnim != HandPose.HandPoseType.NoSelection)
                        //{
                        //HandPose.AddHandPoseAnimationKeysFromOriginClip(createdAnimClip, handPoseAnim);
                        
                        // Stateがない場合は作成のみ
                        if (states.Any())
                        {
                            states[selectedStateIndex].state.motion = createdAnimClip;
                            EditorUtility.SetDirty(controller);

                            // 可能であればもう一方の手も同じAnimationClipを設定する
                            if (setLeftAndRight)
                            {
                                var layerName = editAvatar.fxController.layers[editAvatar.targetFxLayerIndex].name;
                                string targetLayerName = string.Empty;
                                if (layerName == "Left Hand")
                                {
                                    targetLayerName = "Right Hand";
                                }
                                else if (layerName == "Right Hand")
                                {
                                    targetLayerName = "Left Hand";
                                }

                                if (!string.IsNullOrEmpty(targetLayerName))
                                {
                                    var targetLayer = editAvatar.fxController.layers
                                                            .Where(l => l.name == targetLayerName)
                                                            .SingleOrDefault();

                                    if (targetLayer != null)
                                    {
                                        var targetStateName = states[selectedStateIndex].state.name;
                                        var targetState = targetLayer.stateMachine.states
                                                                .Where(s => s.state.name == targetStateName)
                                                                .SingleOrDefault();

                                        if (targetState.state != null)
                                        {
                                            targetState.state.motion = createdAnimClip;
                                            EditorUtility.SetDirty(controller);
                                        }
                                    }
                                }
                            }
                        }

                        FaceEmotion.ResetToDefaultFaceEmotion(ref editAvatar);
                        //}

                        originalAvatar.fxController = controller;
                        editAvatar.fxController = controller;

                        //animationsGUI.ResetPathMissing(AnimationsGUI.HANDANIMS[(int)selectedHandAnim - 1]);
                    }
                }

            }

            return false;
        }

        public void DrawSettingsGUI()
        {
            EditorGUILayout.LabelField("FaceEmotion Creator", EditorStyles.boldLabel);

            selectedSortType = (SortType)EditorGUILayout.EnumPopup(LocalizeText.instance.langPair.sortTypeLabel, selectedSortType);

            isOpeningBlendShapeExclusionList = EditorGUILayout.Foldout(isOpeningBlendShapeExclusionList, LocalizeText.instance.langPair.blendShapeExclusionsLabel);
            if (isOpeningBlendShapeExclusionList)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    for (int i = 0; i < blendshapeExclusions.Count; i++)
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            blendshapeExclusions[i] = EditorGUILayout.TextField(blendshapeExclusions[i]);
                            if (GUILayout.Button(LocalizeText.instance.langPair.remove))
                                blendshapeExclusions.RemoveAt(i);
                        }
                    }
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(LocalizeText.instance.langPair.add))
                        blendshapeExclusions.Add(string.Empty);
                }
            }

            usePreviousAnimationOnHandAnimation = EditorGUILayout.ToggleLeft(LocalizeText.instance.langPair.usePreviousAnimationOnHandAnimationLabel, usePreviousAnimationOnHandAnimation);
        }

        public void LoadSettingData(SettingData settingAsset)
        {
            selectedSortType = settingAsset.selectedSortType;
            blendshapeExclusions = new List<string>(settingAsset.blendshapeExclusions);
            usePreviousAnimationOnHandAnimation = settingAsset.usePreviousAnimationOnHandAnimation;
        }

        public void SaveSettingData(ref SettingData settingAsset)
        {
            settingAsset.selectedSortType = selectedSortType;
            settingAsset.blendshapeExclusions = new List<string>(blendshapeExclusions);
            settingAsset.usePreviousAnimationOnHandAnimation = usePreviousAnimationOnHandAnimation;
        }

        public void Dispose()
        {
            FaceEmotion.ResetToDefaultFaceEmotion(ref editAvatar);
        }

        private void BlendShapeListGUI()
        {
            // BlendShapeのリスト
            using (var scrollView = new EditorGUILayout.ScrollViewScope(scrollPos))
            {
                scrollPos = scrollView.scrollPosition;
                foreach (var skinnedMesh in editAvatar.skinnedMeshList)
                {
                    skinnedMesh.isOpenBlendShapes = EditorGUILayout.Foldout(skinnedMesh.isOpenBlendShapes, skinnedMesh.obj.name);
                    if (skinnedMesh.isOpenBlendShapes)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            using (new GUILayout.HorizontalScope())
                            {
                                using (var check = new EditorGUI.ChangeCheckScope())
                                {
                                    skinnedMesh.isContainsAll = EditorGUILayout.ToggleLeft(string.Empty, skinnedMesh.isContainsAll, GUILayout.Width(45));
                                    if (check.changed)
                                    {
                                        FaceEmotion.SetContainsAll(skinnedMesh.isContainsAll, ref skinnedMesh.blendshapes);
                                    }
                                }
                                EditorGUILayout.LabelField(LocalizeText.instance.langPair.toggleAllLabel, GUILayout.Height(20));
                            }

                            foreach (var blendshape in skinnedMesh.blendshapes)
                            {
                                if (!blendshape.isExclusion)
                                {
                                    using (new EditorGUILayout.HorizontalScope())
                                    {
                                        blendshape.isContains = EditorGUILayout.ToggleLeft(string.Empty, blendshape.isContains, GUILayout.Width(45));

                                        EditorGUILayout.SelectableLabel(blendshape.name, GUILayout.Height(20));
                                        using (var check = new EditorGUI.ChangeCheckScope())
                                        {
                                            var value = skinnedMesh.renderer.GetBlendShapeWeight(blendshape.id);
                                            value = EditorGUILayout.Slider(value, 0, 100);
                                            if (check.changed)
                                                skinnedMesh.renderer.SetBlendShapeWeight(blendshape.id, value);
                                        }

                                        if (GUILayout.Button(LocalizeText.instance.langPair.minButtonText, GUILayout.MaxWidth(50)))
                                        {
                                            FaceEmotion.SetBlendShapeMinValue(ref skinnedMesh.renderer, blendshape.id);
                                        }
                                        if (GUILayout.Button(LocalizeText.instance.langPair.maxButtonText, GUILayout.MaxWidth(50)))
                                        {
                                            FaceEmotion.SetBlendShapeMaxValue(ref skinnedMesh.renderer, blendshape.id);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void OnLoadedAnimationProperties()
        {
            FaceEmotion.ApplyAnimationProperties(ScriptableSingleton<SendData>.instance.loadingProperties, ref editAvatar);
            ChangeSaveAnimationState();
        }

        public void ChangeSaveAnimationState(
                string animName = "",
                int selectedStateIndex = 0,
                AnimationClip handPoseAnim = null)
        {
            this.animName = animName;
            this.selectedStateIndex = selectedStateIndex;
            if (handPoseAnim is null)
                handPoseAnim = HandPose.GetHandAnimationClip(selectedHandAnim);
            this.handPoseAnim = handPoseAnim;
        }

        private void ChangeSelectionHandAnimation()
        {
            if (usePreviousAnimationOnHandAnimation)
            {
                var animController = originalAvatar.fxController;
                // TODO: 以前のアニメーションの取得
                //var previousAnimation = animController[AnimationsGUI.HANDANIMS[(int)selectedHandAnim - 1]];

                // 未設定でなければ以前設定されていたものをHandPoseAnimationとして使う
                //if (previousAnimation != null && previousAnimation.name != AnimationsGUI.HANDANIMS[(int)selectedHandAnim - 1])
                //{
                //    handPoseAnim = previousAnimation;
                //}
                //else
                //{
                //    handPoseAnim = HandPose.GetHandAnimationClip(selectedHandAnim);
                //}
            }
            else
            {
                handPoseAnim = HandPose.GetHandAnimationClip(selectedHandAnim);
            }
        }
    }
}
