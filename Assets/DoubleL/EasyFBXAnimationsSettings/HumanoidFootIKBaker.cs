using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DoubleL
{
    /// <summary>
    /// Bakes the final Humanoid-retargeted pose, including optional Animator Foot IK,
    /// into a target-specific Transform-curve AnimationClip.
    /// </summary>
    public sealed class HumanoidFootIKBaker : EditorWindow
    {
        private const int MaximumBatchClipCount = 50;
        private const string TemporaryControllerBasePath =
            "Assets/__DoubleL_HumanoidFootIKBaker_Temp.controller";

        private enum MotionMode
        {
            AutoDetect,
            InPlace,
            RootMotion
        }

        private enum MotionRootType
        {
            DedicatedRootBone,
            SkeletonRootOrHips
        }

        private enum HelpLanguage
        {
            Korean,
            English
        }

        [Serializable]
        private sealed class CustomBoneMapping
        {
            public string sourceBoneName = "weapon_r";
            public string targetBoneName = "weapon_r_socket";
        }

        [SerializeField] private Animator targetAnimator;
        [SerializeField] private Transform motionRoot;
        [SerializeField] private List<AnimationClip> sourceClips = new List<AnimationClip>();
        [SerializeField] private Vector2 windowScrollPosition;
        [SerializeField] private Vector2 clipListScrollPosition;
        [SerializeField] private string outputFolder = "Assets/Retargeted";
        [SerializeField] private bool useSourceFrameRate = true;
        [SerializeField] private float sampleRate = 30f;
        [SerializeField] private bool enableFootIk = true;
        [SerializeField] private bool retargetCustomBones;
        [SerializeField] private GameObject sourceCharacterModel;
        [SerializeField] private List<CustomBoneMapping> customBoneMappings = new List<CustomBoneMapping>();
        [SerializeField] private HelpLanguage helpLanguage = HelpLanguage.Korean;
        [SerializeField] private MotionMode motionMode = MotionMode.AutoDetect;
        [SerializeField] private MotionRootType motionRootType = MotionRootType.DedicatedRootBone;
        [SerializeField] private bool includeRootRotation = true;
        [SerializeField] private bool includeRootPositionY;
        [SerializeField] private bool includeRootPositionXZ = true;

        [MenuItem("Tools/DoubleL/Humanoid Foot IK Baker")]
        private static void OpenWindow()
        {
            GetWindow<HumanoidFootIKBaker>("Humanoid Retarget Baker");
        }

        private void OnGUI()
        {
            windowScrollPosition = EditorGUILayout.BeginScrollView(windowScrollPosition);

            EditorGUILayout.LabelField("Humanoid to Generic Retarget Baker", EditorStyles.boldLabel);
            helpLanguage = (HelpLanguage)EditorGUILayout.EnumPopup("Help Language", helpLanguage);
            EditorGUILayout.HelpBox(
                Help("Humanoid 리타게팅과 Foot IK가 적용된 최종 타깃 포즈를 타깃 리그 전용 Transform-curve AnimationClip으로 저장합니다.",
                    "Saves the final target pose, evaluated with Humanoid retargeting and Foot IK, as a target-rig-specific Transform-curve AnimationClip."),
                MessageType.Info);

            targetAnimator = (Animator)EditorGUILayout.ObjectField(
                "Target Animator", targetAnimator, typeof(Animator), true);
            motionRoot = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Motion Root", "Root Motion을 사용할 실제 이동용 본입니다. Hips가 아닌 root 본을 지정하세요."),
                motionRoot, typeof(Transform), true);

            DrawClipList();

            EditorGUILayout.Space();
            motionMode = (MotionMode)EditorGUILayout.EnumPopup("Motion Mode", motionMode);
            if (motionMode != MotionMode.InPlace)
            {
                motionRootType = (MotionRootType)EditorGUILayout.EnumPopup("Motion Root Type", motionRootType);
                DrawRootMotionComponents();
            }

            DrawMotionModeHelp();

            enableFootIk = EditorGUILayout.Toggle("Enable Foot IK", enableFootIk);
            DrawCustomBoneRetargetOptions();
            useSourceFrameRate = EditorGUILayout.Toggle("Use Source Frame Rate", useSourceFrameRate);
            using (new EditorGUI.DisabledScope(useSourceFrameRate))
            {
                sampleRate = EditorGUILayout.FloatField("Sample Rate", sampleRate);
            }

            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!CanBake()))
            {
                if (GUILayout.Button("Bake Selected Clips", GUILayout.Height(36f)))
                {
                    BakeAll();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCustomBoneRetargetOptions()
        {
            EditorGUILayout.Space();
            retargetCustomBones = EditorGUILayout.Toggle("Retarget Weapon Bone", retargetCustomBones);
            if (!retargetCustomBones)
            {
                return;
            }

            sourceCharacterModel = (GameObject)EditorGUILayout.ObjectField(
                "Source Character FBX",
                sourceCharacterModel,
                typeof(GameObject),
                false);
            EditorGUILayout.HelpBox(
                Help("먼저 Add Weapon Mapping을 눌러 매핑을 추가한 뒤, Source Weapon Bone Name과 Target Weapon Bone Name에 실제 본 이름을 입력하세요.",
                    "First click Add Weapon Mapping, then enter the actual bone names in Source Weapon Bone Name and Target Weapon Bone Name."),
                MessageType.Info);

            for (int index = 0; index < customBoneMappings.Count; index++)
            {
                CustomBoneMapping mapping = customBoneMappings[index];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Mapping {index + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(62f)))
                {
                    customBoneMappings.RemoveAt(index);
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
                mapping.sourceBoneName = EditorGUILayout.TextField(
                    "Source Weapon Bone Name",
                    mapping.sourceBoneName);
                mapping.targetBoneName = EditorGUILayout.TextField(
                    "Target Weapon Bone Name",
                    mapping.targetBoneName);
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Weapon Mapping"))
            {
                customBoneMappings.Add(new CustomBoneMapping());
            }
        }

        private void DrawClipList()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Source Humanoid Clips", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                Help("한 작업에서 최대 50개의 Clip을 선택할 수 있습니다. 진행 중에는 취소할 수 있으며, 이미 생성된 Clip은 유지됩니다.",
                    "You can select up to 50 clips per batch. You can cancel while baking; clips already created are kept."),
                MessageType.None);

            float listHeight = EditorGUIUtility.singleLineHeight * 5f + 8f;
            clipListScrollPosition = EditorGUILayout.BeginScrollView(
                clipListScrollPosition,
                GUILayout.Height(listHeight));

            for (int index = 0; index < sourceClips.Count; index++)
            {
                EditorGUILayout.BeginHorizontal();
                sourceClips[index] = (AnimationClip)EditorGUILayout.ObjectField(
                    sourceClips[index], typeof(AnimationClip), false);

                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    sourceClips.RemoveAt(index);
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(sourceClips.Count >= MaximumBatchClipCount))
            {
                if (GUILayout.Button("Add Clip"))
                {
                    sourceClips.Add(null);
                }
            }

            using (new EditorGUI.DisabledScope(sourceClips.Count >= MaximumBatchClipCount))
            {
                if (GUILayout.Button("Add Clips From Project Selection"))
                {
                    AddClipsFromProjectSelection();
                }
            }

        }

        private void AddClipsFromProjectSelection()
        {
            var existingClipIds = new HashSet<int>();
            foreach (AnimationClip existingClip in sourceClips)
            {
                if (existingClip != null)
                {
                    existingClipIds.Add(existingClip.GetInstanceID());
                }
            }

            int addedCount = 0;
            foreach (UnityEngine.Object selectedAsset in Selection.objects)
            {
                if (sourceClips.Count >= MaximumBatchClipCount)
                {
                    break;
                }

                string assetPath = AssetDatabase.GetAssetPath(selectedAsset);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                {
                    if (sourceClips.Count >= MaximumBatchClipCount)
                    {
                        break;
                    }

                    if (!(asset is AnimationClip clip) ||
                        clip.name.StartsWith("__preview__", StringComparison.Ordinal) ||
                        !existingClipIds.Add(clip.GetInstanceID()))
                    {
                        continue;
                    }

                    sourceClips.Add(clip);
                    addedCount++;
                }
            }

            if (addedCount == 0)
            {
                Debug.LogWarning("[DoubleL.HumanoidFootIKBaker] Project 창에서 AnimationClip 또는 AnimationClip이 포함된 FBX를 선택해 주세요.");
            }
            else if (sourceClips.Count >= MaximumBatchClipCount)
            {
                Debug.LogWarning($"[DoubleL.HumanoidFootIKBaker] 한 번에 선택할 수 있는 최대 Clip 수는 {MaximumBatchClipCount}개입니다.");
            }
        }

        private void DrawMotionModeHelp()
        {
            string message;
            MessageType messageType = MessageType.Info;

            switch (motionMode)
            {
                case MotionMode.InPlace:
                    message = Help("캐릭터 전체 이동·회전을 만들지 않습니다. Idle 또는 코드로 이동시키는 동작에 사용합니다.",
                        "Does not create character-level movement or rotation. Use for Idle or motions moved by code.");
                    break;
                case MotionMode.RootMotion:
                    message = motionRootType == MotionRootType.DedicatedRootBone
                        ? Help("별도 root 본의 이동·회전을 기록합니다. root → Hips 구조의 Walk, Run, Roll에 사용합니다.",
                            "Records movement and rotation from a dedicated root bone. Use for root → Hips rigs, such as Walk, Run, and Roll.")
                        : Help("최상위 Hips의 포즈를 유지하면서 이동·회전을 기록합니다. Hips가 최상위인 리그에 사용합니다.",
                            "Records movement and rotation while preserving the top-level Hips pose. Use for rigs where Hips is the hierarchy root.");
                    break;
                default:
                    message = Help("입력 Clip의 Root Motion 데이터 유무를 기준으로 모드를 자동 선택합니다. 필요하면 수동으로 바꿔 주세요.",
                        "Automatically selects a mode based on Root Motion data in the input clip. Override it manually when needed.");
                    break;
            }

            if (motionMode == MotionMode.RootMotion && !IsMotionRootValid())
            {
                message += Help(" Motion Root는 Target Animator 하위의 Transform이어야 합니다.",
                    " Motion Root must be a child Transform of the Target Animator.");
                messageType = MessageType.Warning;
            }

            EditorGUILayout.HelpBox(message, messageType);
        }

        private string Help(string korean, string english)
        {
            return helpLanguage == HelpLanguage.Korean ? korean : english;
        }

        private void DrawRootMotionComponents()
        {
            EditorGUILayout.LabelField("Root Motion Components", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            includeRootRotation = EditorGUILayout.Toggle("Rotation", includeRootRotation);
            includeRootPositionY = EditorGUILayout.Toggle("Position Y", includeRootPositionY);
            includeRootPositionXZ = EditorGUILayout.Toggle("Position XZ", includeRootPositionXZ);
            EditorGUI.indentLevel--;
        }

        private bool CanBake()
        {
            return targetAnimator != null && sourceClips.Exists(clip => clip != null);
        }

        private void BakeAll()
        {
            if (!ValidateSharedInput())
            {
                return;
            }

            List<AnimationClip> clipsToBake = sourceClips.FindAll(clip => clip != null);
            if (clipsToBake.Count > MaximumBatchClipCount)
            {
                Debug.LogError($"[DoubleL.HumanoidFootIKBaker] 한 번에 최대 {MaximumBatchClipCount}개 Clip만 변환할 수 있습니다.");
                return;
            }

            int bakedCount = 0;
            int failedCount = 0;

            try
            {
                for (int index = 0; index < clipsToBake.Count; index++)
                {
                    AnimationClip clip = clipsToBake[index];
                    float progress = (float)index / clipsToBake.Count;
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Humanoid Retarget Bake",
                            $"{index + 1}/{clipsToBake.Count}: {clip.name}",
                            progress))
                    {
                        Debug.Log("[DoubleL.HumanoidFootIKBaker] 사용자가 배치를 취소했습니다.");
                        break;
                    }

                    if (BakeClip(clip))
                    {
                        bakedCount++;
                    }
                    else
                    {
                        failedCount++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                $"[DoubleL.HumanoidFootIKBaker] 배치 완료 - 성공: {bakedCount}, 실패: {failedCount}, 출력: {outputFolder}");
        }

        private bool BakeClip(AnimationClip sourceClip)
        {
            if (!ValidateClip(sourceClip))
            {
                return false;
            }

            RuntimeAnimatorController originalController = targetAnimator.runtimeAnimatorController;
            AnimatorCullingMode originalCullingMode = targetAnimator.cullingMode;
            AnimatorUpdateMode originalUpdateMode = targetAnimator.updateMode;
            bool originalApplyRootMotion = targetAnimator.applyRootMotion;
            Vector3 originalPosition = targetAnimator.transform.localPosition;
            Quaternion originalRotation = targetAnimator.transform.localRotation;
            string temporaryControllerPath = null;
            string sourceTemporaryControllerPath = null;
            GameObject sourceInstance = null;

            bool bakeRootMotion = ResolveMotionMode(sourceClip) == MotionMode.RootMotion;

            try
            {
                EnsureFolder(outputFolder);

                Animator sourceAnimator = null;
                CustomBoneRetargeter customBoneRetargeter = null;
                if (retargetCustomBones)
                {
                    sourceInstance = PrefabUtility.InstantiatePrefab(sourceCharacterModel) as GameObject;
                    if (sourceInstance == null)
                    {
                        throw new InvalidOperationException("Source Character FBX를 임시 인스턴스로 만들 수 없습니다.");
                    }

                    sourceInstance.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                    sourceInstance.transform.SetPositionAndRotation(
                        targetAnimator.transform.position,
                        targetAnimator.transform.rotation);
                    sourceAnimator = sourceInstance.GetComponentInChildren<Animator>();
                    if (sourceAnimator == null || sourceAnimator.avatar == null || !sourceAnimator.avatar.isHuman)
                    {
                        throw new InvalidOperationException("Source Character FBX에는 유효한 Humanoid Animator와 Avatar가 필요합니다.");
                    }

                    sourceTemporaryControllerPath = AssetDatabase.GenerateUniqueAssetPath(TemporaryControllerBasePath);
                    sourceAnimator.runtimeAnimatorController = CreateTemporaryController(
                        sourceTemporaryControllerPath,
                        sourceClip,
                        false);
                    sourceAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    sourceAnimator.applyRootMotion = false;
                    sourceAnimator.Rebind();
                    sourceAnimator.Play("BakeState", 0, 0f);
                    sourceAnimator.Update(0f);
                    customBoneRetargeter = new CustomBoneRetargeter(
                        sourceInstance.transform,
                        targetAnimator.transform,
                        customBoneMappings);
                }

                temporaryControllerPath = AssetDatabase.GenerateUniqueAssetPath(TemporaryControllerBasePath);
                AnimatorController temporaryController = CreateTemporaryController(
                    temporaryControllerPath,
                    sourceClip,
                    enableFootIk);

                targetAnimator.runtimeAnimatorController = temporaryController;
                targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                targetAnimator.updateMode = AnimatorUpdateMode.Normal;
                targetAnimator.applyRootMotion = bakeRootMotion;
                targetAnimator.Rebind();
                targetAnimator.Play("BakeState", 0, 0f);
                targetAnimator.Update(0f);

                float effectiveSampleRate = useSourceFrameRate && sourceClip.frameRate > 0f
                    ? sourceClip.frameRate
                    : sampleRate;

                if (effectiveSampleRate <= 0f)
                {
                    throw new InvalidOperationException("Sample Rate는 0보다 커야 합니다.");
                }

                var recorder = new TransformRecorder(
                    targetAnimator.transform,
                    motionRoot,
                    bakeRootMotion,
                    motionRootType);
                int frameCount = Mathf.CeilToInt(sourceClip.length * effectiveSampleRate);
                float previousTime = 0f;
                Vector3 initialAnimatorPosition = targetAnimator.transform.position;
                Quaternion initialAnimatorRotation = targetAnimator.transform.rotation;

                customBoneRetargeter?.Apply();
                RecordCurrentFrame(recorder, 0f, bakeRootMotion, initialAnimatorPosition, initialAnimatorRotation);

                for (int frame = 1; frame <= frameCount; frame++)
                {
                    float time = Mathf.Min(frame / effectiveSampleRate, sourceClip.length);
                    targetAnimator.Update(time - previousTime);
                    if (sourceInstance != null)
                    {
                        sourceInstance.transform.SetPositionAndRotation(
                            targetAnimator.transform.position,
                            targetAnimator.transform.rotation);
                    }

                    sourceAnimator?.Update(time - previousTime);
                    customBoneRetargeter?.Apply();
                    RecordCurrentFrame(recorder, time, bakeRootMotion, initialAnimatorPosition, initialAnimatorRotation);
                    previousTime = time;
                }

                AnimationClip bakedClip = recorder.CreateClip(
                    sourceClip.name,
                    effectiveSampleRate);
                ApplyClipSettings(bakedClip, bakeRootMotion);

                string outputPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{outputFolder}/{bakedClip.name}.anim");
                AssetDatabase.CreateAsset(bakedClip, outputPath);
                Debug.Log($"[DoubleL.HumanoidFootIKBaker] Bake 완료: {outputPath}");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
            finally
            {
                targetAnimator.runtimeAnimatorController = originalController;
                targetAnimator.cullingMode = originalCullingMode;
                targetAnimator.updateMode = originalUpdateMode;
                targetAnimator.applyRootMotion = originalApplyRootMotion;
                targetAnimator.transform.localPosition = originalPosition;
                targetAnimator.transform.localRotation = originalRotation;
                targetAnimator.Rebind();

                if (!string.IsNullOrEmpty(temporaryControllerPath))
                {
                    AssetDatabase.DeleteAsset(temporaryControllerPath);
                }

                if (!string.IsNullOrEmpty(sourceTemporaryControllerPath))
                {
                    AssetDatabase.DeleteAsset(sourceTemporaryControllerPath);
                }

                if (sourceInstance != null)
                {
                    DestroyImmediate(sourceInstance);
                }

            }
        }

        private void RecordCurrentFrame(
            TransformRecorder recorder,
            float time,
            bool bakeRootMotion,
            Vector3 initialAnimatorPosition,
            Quaternion initialAnimatorRotation)
        {
            if (!bakeRootMotion)
            {
                recorder.Record(time);
                return;
            }

            Quaternion rootDeltaRotation = includeRootRotation
                ? Quaternion.Inverse(initialAnimatorRotation) * targetAnimator.transform.rotation
                : Quaternion.identity;
            Vector3 rootDeltaPosition = Quaternion.Inverse(initialAnimatorRotation) *
                (targetAnimator.transform.position - initialAnimatorPosition);

            if (!includeRootPositionXZ)
            {
                rootDeltaPosition.x = 0f;
                rootDeltaPosition.z = 0f;
            }

            if (!includeRootPositionY)
            {
                rootDeltaPosition.y = 0f;
            }

            recorder.Record(time, rootDeltaPosition, rootDeltaRotation);
        }

        private bool ValidateSharedInput()
        {
            if (targetAnimator == null)
            {
                Debug.LogError("[DoubleL.HumanoidFootIKBaker] Target Animator가 없습니다.");
                return false;
            }

            if (targetAnimator.avatar == null || !targetAnimator.avatar.isHuman)
            {
                Debug.LogError("[DoubleL.HumanoidFootIKBaker] Target Animator에는 유효한 Humanoid Avatar가 필요합니다.");
                return false;
            }

            if (motionMode == MotionMode.RootMotion && !IsMotionRootValid())
            {
                Debug.LogError("[DoubleL.HumanoidFootIKBaker] Motion Root는 Target Animator 하위의 Transform이어야 합니다.");
                return false;
            }

            if (retargetCustomBones && (sourceCharacterModel == null || customBoneMappings.Count == 0))
            {
                Debug.LogError("[DoubleL.HumanoidFootIKBaker] Custom Bone Retarget에는 Source Character FBX와 하나 이상의 본 매핑이 필요합니다.");
                return false;
            }

            return true;
        }

        private bool ValidateClip(AnimationClip sourceClip)
        {
            if (!sourceClip.isHumanMotion)
            {
                Debug.LogWarning($"[DoubleL.HumanoidFootIKBaker] '{sourceClip.name}'은 Humanoid Motion이 아닙니다. 건너뜁니다.");
                return false;
            }

            if (ResolveMotionMode(sourceClip) == MotionMode.RootMotion && !IsMotionRootValid())
            {
                Debug.LogError($"[DoubleL.HumanoidFootIKBaker] '{sourceClip.name}'은 Root Motion 변환에 Motion Root가 필요합니다.");
                return false;
            }

            return true;
        }

        private MotionMode ResolveMotionMode(AnimationClip sourceClip)
        {
            if (motionMode != MotionMode.AutoDetect)
            {
                return motionMode;
            }

            return sourceClip.hasRootCurves
                ? MotionMode.RootMotion
                : MotionMode.InPlace;
        }

        private bool IsMotionRootValid()
        {
            return motionRoot != null && targetAnimator != null &&
                motionRoot != targetAnimator.transform &&
                motionRoot.IsChildOf(targetAnimator.transform);
        }

        private static AnimatorController CreateTemporaryController(
            string path,
            AnimationClip clip,
            bool useFootIk)
        {
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState state = stateMachine.AddState("BakeState");
            state.motion = clip;
            state.iKOnFeet = useFootIk;
            state.writeDefaultValues = true;
            stateMachine.defaultState = state;
            return controller;
        }

        private void ApplyClipSettings(AnimationClip clip, bool hasRootMotion)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.keepOriginalOrientation = !hasRootMotion || !includeRootRotation;
            settings.keepOriginalPositionY = !hasRootMotion || !includeRootPositionY;
            settings.keepOriginalPositionXZ = !hasRootMotion || !includeRootPositionXZ;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] parts = folder.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new ArgumentException("Output Folder는 Assets 아래여야 합니다.");
            }

            string current = "Assets";
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private sealed class CustomBoneRetargeter
        {
            private readonly List<BonePair> bonePairs = new List<BonePair>();

            public CustomBoneRetargeter(
                Transform sourceRoot,
                Transform targetRoot,
                List<CustomBoneMapping> mappings)
            {
                foreach (CustomBoneMapping mapping in mappings)
                {
                    Transform source = FindRecursive(sourceRoot, mapping.sourceBoneName);
                    Transform target = FindRecursive(targetRoot, mapping.targetBoneName);
                    if (source == null || target == null || target.parent == null)
                    {
                        Debug.LogWarning(
                            $"[DoubleL.HumanoidFootIKBaker] Custom Bone Mapping을 찾지 못했습니다: " +
                            $"{mapping.sourceBoneName} → {mapping.targetBoneName}");
                        continue;
                    }

                    bonePairs.Add(new BonePair(source, target));
                }

                if (bonePairs.Count == 0)
                {
                    throw new InvalidOperationException("유효한 Custom Bone Mapping이 없습니다.");
                }
            }

            public void Apply()
            {
                foreach (BonePair pair in bonePairs)
                {
                    pair.target.localPosition = pair.target.parent.InverseTransformPoint(pair.source.position);
                    pair.target.localRotation = Quaternion.Inverse(pair.target.parent.rotation) * pair.source.rotation;
                }
            }

            private static Transform FindRecursive(Transform parent, string name)
            {
                if (parent.name == name)
                {
                    return parent;
                }

                foreach (Transform child in parent)
                {
                    Transform found = FindRecursive(child, name);
                    if (found != null)
                    {
                        return found;
                    }
                }

                return null;
            }

            private sealed class BonePair
            {
                public readonly Transform source;
                public readonly Transform target;

                public BonePair(Transform source, Transform target)
                {
                    this.source = source;
                    this.target = target;
                }
            }
        }

        private sealed class TransformRecorder
        {
            private readonly List<BoneRecord> bones = new List<BoneRecord>();
            private readonly BoneRecord motionRootRecord;
            private readonly bool bakeRootMotion;
            private readonly MotionRootType motionRootType;

            public TransformRecorder(
                Transform animatorRoot,
                Transform motionRoot,
                bool bakeRootMotion,
                MotionRootType motionRootType)
            {
                this.bakeRootMotion = bakeRootMotion;
                this.motionRootType = motionRootType;
                foreach (Transform transform in animatorRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (transform == animatorRoot)
                    {
                        continue;
                    }

                    var record = new BoneRecord(transform, GetRelativePath(animatorRoot, transform));
                    bones.Add(record);
                    if (transform == motionRoot)
                    {
                        motionRootRecord = record;
                    }
                }
            }

            public void Record(float time)
            {
                foreach (BoneRecord bone in bones)
                {
                    bone.Record(time);
                }
            }

            public void Record(float time, Vector3 rootDeltaPosition, Quaternion rootDeltaRotation)
            {
                foreach (BoneRecord bone in bones)
                {
                    if (bone == motionRootRecord)
                    {
                        bone.RecordRootMotion(
                            time,
                            rootDeltaPosition,
                            rootDeltaRotation,
                            motionRootType == MotionRootType.SkeletonRootOrHips);
                    }
                    else
                    {
                        bone.Record(time);
                    }
                }
            }

            public AnimationClip CreateClip(string clipName, float frameRate)
            {
                var clip = new AnimationClip { name = clipName, frameRate = frameRate };
                foreach (BoneRecord bone in bones)
                {
                    bone.WriteTo(clip, bakeRootMotion && bone == motionRootRecord);
                }

                clip.EnsureQuaternionContinuity();
                return clip;
            }

            private static string GetRelativePath(Transform root, Transform target)
            {
                var names = new Stack<string>();
                for (Transform current = target; current != null && current != root; current = current.parent)
                {
                    names.Push(current.name);
                }

                return string.Join("/", names);
            }
        }

        private sealed class BoneRecord
        {
            private readonly Transform transform;
            private readonly string path;
            private readonly Vector3 initialPosition;
            private readonly Quaternion initialRotation;
            private readonly AnimationCurve positionX = new AnimationCurve();
            private readonly AnimationCurve positionY = new AnimationCurve();
            private readonly AnimationCurve positionZ = new AnimationCurve();
            private readonly AnimationCurve rotationX = new AnimationCurve();
            private readonly AnimationCurve rotationY = new AnimationCurve();
            private readonly AnimationCurve rotationZ = new AnimationCurve();
            private readonly AnimationCurve rotationW = new AnimationCurve();

            public BoneRecord(Transform transform, string path)
            {
                this.transform = transform;
                this.path = path;
                initialPosition = transform.localPosition;
                initialRotation = transform.localRotation;
            }

            public void Record(float time)
            {
                Record(time, transform.localPosition, transform.localRotation);
            }

            public void RecordRootMotion(
                float time,
                Vector3 rootDeltaPosition,
                Quaternion rootDeltaRotation,
                bool preserveCurrentPose)
            {
                Vector3 position = preserveCurrentPose
                    ? transform.localPosition + rootDeltaPosition
                    : initialPosition + rootDeltaPosition;

                Quaternion rotation = preserveCurrentPose
                    ? transform.localRotation * rootDeltaRotation
                    : initialRotation * rootDeltaRotation;

                Record(time, position, rotation);
            }

            public void WriteTo(AnimationClip clip, bool forcePosition)
            {
                if (forcePosition || HasMeaningfulPositionChange())
                {
                    SetCurve(clip, "m_LocalPosition.x", positionX);
                    SetCurve(clip, "m_LocalPosition.y", positionY);
                    SetCurve(clip, "m_LocalPosition.z", positionZ);
                }

                SetCurve(clip, "m_LocalRotation.x", rotationX);
                SetCurve(clip, "m_LocalRotation.y", rotationY);
                SetCurve(clip, "m_LocalRotation.z", rotationZ);
                SetCurve(clip, "m_LocalRotation.w", rotationW);
            }

            private void Record(float time, Vector3 position, Quaternion rotation)
            {
                AddLinearKey(positionX, time, position.x);
                AddLinearKey(positionY, time, position.y);
                AddLinearKey(positionZ, time, position.z);
                AddLinearKey(rotationX, time, rotation.x);
                AddLinearKey(rotationY, time, rotation.y);
                AddLinearKey(rotationZ, time, rotation.z);
                AddLinearKey(rotationW, time, rotation.w);
            }

            private bool HasMeaningfulPositionChange()
            {
                const float epsilon = 0.00001f;
                return CurveVaries(positionX, epsilon) ||
                    CurveVaries(positionY, epsilon) ||
                    CurveVaries(positionZ, epsilon);
            }

            private static bool CurveVaries(AnimationCurve curve, float epsilon)
            {
                if (curve.length < 2)
                {
                    return false;
                }

                float firstValue = curve.keys[0].value;
                for (int index = 1; index < curve.length; index++)
                {
                    if (Mathf.Abs(curve.keys[index].value - firstValue) > epsilon)
                    {
                        return true;
                    }
                }

                return false;
            }

            private void SetCurve(AnimationClip clip, string property, AnimationCurve curve)
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), property),
                    curve);
            }

            private static void AddLinearKey(AnimationCurve curve, float time, float value)
            {
                int index = curve.AddKey(new Keyframe(time, value));
                AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
            }
        }
    }
}
