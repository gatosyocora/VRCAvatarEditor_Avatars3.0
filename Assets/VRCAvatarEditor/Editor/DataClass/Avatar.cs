using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

namespace VRCAvatarEditor.Avatars3
{
    public class Avatar
    {
        public enum EyelidBlendShapes
        {
            Blink = 0,
            LookingUp = 1,
            LookingDown = 2
        }

        public Animator animator { get; set; }
        public VRCAvatarDescriptor descriptor { get; set; }
        public Vector3 eyePos { get; set; }
        public AnimatorController fxController { get; set; }

        public CustomAnimLayer fxLayer { get; set; }
        public int targetFxLayerIndex { get; set; }
        public VRCAvatarDescriptor.AnimationSet sex { get; set; }
        public string avatarId { get; set; }
        public int overridesNum { get; set; }
        public SkinnedMeshRenderer faceMesh { get; set; }
        public List<string> lipSyncShapeKeyNames;
        public Material[] materials { get; set; }
        public int triangleCount { get; set; }
        public int triangleCountInactive { get; set; }
        public VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle lipSyncStyle { get; set; }
        public Enum faceShapeKeyEnum { get; set; }
        public List<SkinnedMesh> skinnedMeshList { get; set; }

        public List<SkinnedMeshRenderer> skinnedMeshRendererList { get; set; }
        public List<MeshRenderer> meshRendererList { get; set; }

        public string animSavedFolderPath { get; set; }

        public List<FaceEmotion.AnimParam> defaultFaceEmotion { get; set; }

        public string[] eyelidBlendShapeNames;
        public SkinnedMeshRenderer eyelidsSkinnedMesh;

        public Avatar()
        {
            animator = null;
            descriptor = null;
            eyePos = Vector3.zero;
            fxController = null;
            targetFxLayerIndex = 0;
            sex = VRCAvatarDescriptor.AnimationSet.None;
            avatarId = string.Empty;
            overridesNum = 0;
            faceMesh = null;
            lipSyncShapeKeyNames = null;
            triangleCount = 0;
            triangleCountInactive = 0;
            lipSyncStyle = VRCAvatarDescriptor.LipSyncStyle.Default;
            faceShapeKeyEnum = null;
            skinnedMeshList = null;
            animSavedFolderPath = $"Assets{Path.DirectorySeparatorChar}";
            eyelidBlendShapeNames = new string[Enum.GetNames(typeof(EyelidBlendShapes)).Length];
            eyelidsSkinnedMesh = null;
        }

        public Avatar(VRCAvatarDescriptor descriptor) : this()
        {
            LoadAvatarInfo(descriptor);
        }

        public void LoadAvatarInfo(VRCAvatarDescriptor descriptor)
        {
            this.descriptor = descriptor;
            LoadAvatarInfo();
        }

        /// <summary>
        /// アバターの情報を取得する
        /// </summary>
        public void LoadAvatarInfo()
        {
            if (descriptor == null) return;

            var avatarObj = descriptor.gameObject;

            animator = avatarObj.GetComponent<Animator>();

            eyePos = descriptor.ViewPosition;
            sex = descriptor.Animations;

            fxLayer = descriptor.baseAnimationLayers
                            .Where(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX)
                            .FirstOrDefault();

            fxController = fxLayer.animatorController as AnimatorController;

            if (fxController != null)
            {
                var layerNames = fxController.layers.Select(l => l.name).ToArray();
                targetFxLayerIndex = Array.IndexOf(layerNames, "Left Hand");

                if (targetFxLayerIndex == -1)
                {
                    targetFxLayerIndex = Array.IndexOf(layerNames, "Right Hand");

                    if (targetFxLayerIndex == -1)
                    {
                        targetFxLayerIndex = 0;
                    }
                }
            }

            eyelidsSkinnedMesh = descriptor.customEyeLookSettings.eyelidsSkinnedMesh;
            if (eyelidsSkinnedMesh != null)
            {
                var eyelidsFaceMesh = eyelidsSkinnedMesh.sharedMesh;

                if (descriptor.customEyeLookSettings.eyelidType == EyelidType.Blendshapes)
                {
                    var blinkBlendShapeIndex = descriptor.customEyeLookSettings
                                                    .eyelidsBlendshapes[(int)EyelidBlendShapes.Blink];
                    if (blinkBlendShapeIndex != -1)
                    {
                        eyelidBlendShapeNames[(int)EyelidBlendShapes.Blink] = eyelidsFaceMesh.GetBlendShapeName(blinkBlendShapeIndex);
                    }

                    var lookingUpBlendShapeIndex = descriptor.customEyeLookSettings
                                                        .eyelidsBlendshapes[(int)EyelidBlendShapes.LookingUp];
                    if (lookingUpBlendShapeIndex != -1)
                    {
                        eyelidBlendShapeNames[(int)EyelidBlendShapes.LookingUp] = eyelidsFaceMesh.GetBlendShapeName(lookingUpBlendShapeIndex);
                    }

                    var lookingDownBlendShapeIndex = descriptor.customEyeLookSettings
                                                            .eyelidsBlendshapes[(int)EyelidBlendShapes.LookingDown];
                    if (lookingDownBlendShapeIndex != -1)
                    {
                        eyelidBlendShapeNames[(int)EyelidBlendShapes.LookingDown] = eyelidsFaceMesh.GetBlendShapeName(lookingUpBlendShapeIndex);
                    }
                }
            }

            SetAnimSavedFolderPath();

            avatarId = descriptor.gameObject.GetComponent<PipelineManager>().blueprintId;

            faceMesh = descriptor.VisemeSkinnedMesh;

            if (faceMesh != null && descriptor.lipSync == VRCAvatarDescriptor.LipSyncStyle.VisemeBlendShape)
            {
                lipSyncShapeKeyNames = new List<string>();
                lipSyncShapeKeyNames.AddRange(descriptor.VisemeBlendShapes);
            }

            materials = GatoUtility.GetMaterials(avatarObj);

            int triangleCountInactive = 0;
            triangleCount = GatoUtility.GetAllTrianglesCount(avatarObj, ref triangleCountInactive);
            this.triangleCountInactive = triangleCountInactive;

            lipSyncStyle = descriptor.lipSync;

            skinnedMeshList = FaceEmotion.GetSkinnedMeshListOfBlendShape(avatarObj, faceMesh.gameObject);

            skinnedMeshRendererList = GatoUtility.GetSkinnedMeshList(avatarObj);
            meshRendererList = GatoUtility.GetMeshList(avatarObj);

            defaultFaceEmotion = FaceEmotion.GetAvatarFaceParamaters(skinnedMeshList);
        }

        /// <summary>
        /// Avatarにシェイプキー基準のLipSyncの設定をおこなう
        /// </summary>
        public void SetLipSyncToViseme()
        {
            if (descriptor == null) return;

            lipSyncStyle = VRCAvatarDescriptor.LipSyncStyle.VisemeBlendShape;
            descriptor.lipSync = VRCAvatarDescriptor.LipSyncStyle.VisemeBlendShape;

            if (faceMesh == null)
            {
                var rootObj = animator.gameObject;
                faceMesh = rootObj.GetComponentInChildren<SkinnedMeshRenderer>();
                descriptor.VisemeSkinnedMesh = faceMesh;
            }

            if (faceMesh == null) return;
            var mesh = faceMesh.sharedMesh;

            var visemeBlendShapeNames = Enum.GetNames(typeof(VRCAvatarDescriptor.Viseme));

            for (int visemeIndex = 0; visemeIndex < visemeBlendShapeNames.Length; visemeIndex++)
            {
                // VRC用アバターとしてよくあるシェイプキーの名前を元に自動設定
                var visemeShapeKeyName = "vrc.v_" + visemeBlendShapeNames[visemeIndex];
                if (mesh.GetBlendShapeIndex(visemeShapeKeyName) != -1)
                {
                    descriptor.VisemeBlendShapes[visemeIndex] = visemeShapeKeyName;
                    continue;
                }

                visemeShapeKeyName = "VRC.v_" + visemeBlendShapeNames[visemeIndex];
                if (mesh.GetBlendShapeIndex(visemeShapeKeyName) != -1)
                {
                    descriptor.VisemeBlendShapes[visemeIndex] = visemeShapeKeyName;
                }
            }
        }

        public void SetAnimSavedFolderPath()
        {
            if (fxController != null)
            {
                var assetPath = AssetDatabase.GetAssetPath(fxController);
                animSavedFolderPath = $"{Path.GetDirectoryName(assetPath)}{Path.DirectorySeparatorChar}";
            }
        }
    }
}

