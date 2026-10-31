#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Motion = SubwayCarry.Gameplay3D.AnimationLab.AnimationLabAuthoredMotion;

namespace SubwayCarry.Gameplay3D.AnimationLab
{
    public static class AnimationLabSeatedDozeSetBuilder
    {
        private const string Author = "SubwayCarry";
        private const string EnterName = "Seated_Doze_Enter";
        private const string LoopName = "Seated_Doze_Loop";
        private const string ExitName = "Seated_Doze_Exit";
        private const string PhoneEnterName = "Seated_Phone_Enter";
        private const string PhoneLoopName = "Seated_Phone_Loop";
        private const string PhoneExitName = "Seated_Phone_Exit";
        private const string BookEnterName = "Seated_Book_Enter";
        private const string BookLoopName = "Seated_Book_Loop";
        private const string BookExitName = "Seated_Book_Exit";
        private const string ArmsCrossedEnterName = "Seated_ArmsCrossed_Enter";
        private const string ArmsCrossedLoopName = "Seated_ArmsCrossed_Loop";
        private const string ArmsCrossedExitName = "Seated_ArmsCrossed_Exit";
        private const int ArmsCrossedTransitionFrames = 24;
        private const string LegCrossedEnterName = "Seated_LegCrossed_Enter";
        private const string LegCrossedLoopName = "Seated_LegCrossed_Loop";
        private const string LegCrossedExitName = "Seated_LegCrossed_Exit";
        private const int LegCrossedTransitionFrames = 30;
        private const string ScenePath =
            AnimationLabMotionAuthoring.Root + "/SeatedDoze_Preview.unity";
        private const string PreviewPath = "Logs/SeatedDoze_Set_Preview.png";
        private const string PhoneScenePath =
            AnimationLabMotionAuthoring.Root + "/SeatedPhone_Preview.unity";
        private const string PhonePreviewPath = "Logs/SeatedPhone_Set_Preview.png";
        private const string BookScenePath =
            AnimationLabMotionAuthoring.Root + "/SeatedBook_Preview.unity";
        private const string BookPreviewPath = "Logs/SeatedBook_Set_Preview.png";
        private const string ArmsCrossedScenePath =
            AnimationLabMotionAuthoring.Root + "/SeatedArmsCrossed_Preview.unity";
        private const string ArmsCrossedPreviewPath = "Logs/SeatedArmsCrossed_Set_Preview.png";
        private const string LegCrossedScenePath =
            AnimationLabMotionAuthoring.Root + "/SeatedLegCrossed_Preview.unity";
        private const string LegCrossedPreviewPath = "Logs/SeatedLegCrossed_Set_Preview.png";

        private readonly struct PoseSpec
        {
            public readonly int frame;
            public readonly float bodySlump;
            public readonly float headSlump;
            public readonly float handBlend;
            public readonly float breath;
            public readonly float sway;
            public readonly float nod;
            public readonly float ease;

            public PoseSpec(
                int frame,
                float bodySlump,
                float headSlump,
                float handBlend,
                float breath = 0f,
                float sway = 0f,
                float nod = 0f,
                float ease = 0.6f)
            {
                this.frame = frame;
                this.bodySlump = bodySlump;
                this.headSlump = headSlump;
                this.handBlend = handBlend;
                this.breath = breath;
                this.sway = sway;
                this.nod = nod;
                this.ease = ease;
            }
        }

        private readonly struct PhonePoseSpec
        {
            public readonly int frame;
            public readonly float poseBlend;
            public readonly float breath;
            public readonly float headYaw;
            public readonly float phoneSide;
            public readonly float phoneLift;
            public readonly float rightTap;
            public readonly float ease;

            public PhonePoseSpec(
                int frame,
                float poseBlend,
                float breath = 0f,
                float headYaw = 0f,
                float phoneSide = 0f,
                float phoneLift = 0f,
                float rightTap = 0f,
                float ease = 0.6f)
            {
                this.frame = frame;
                this.poseBlend = poseBlend;
                this.breath = breath;
                this.headYaw = headYaw;
                this.phoneSide = phoneSide;
                this.phoneLift = phoneLift;
                this.rightTap = rightTap;
                this.ease = ease;
            }
        }

        private readonly struct BookPoseSpec
        {
            public readonly int frame;
            public readonly float poseBlend;
            public readonly float breath;
            public readonly float gazeYaw;
            public readonly float bookSide;
            public readonly float bookLift;
            public readonly float pageTouch;
            public readonly float ease;

            public BookPoseSpec(
                int frame,
                float poseBlend,
                float breath = 0f,
                float gazeYaw = 0f,
                float bookSide = 0f,
                float bookLift = 0f,
                float pageTouch = 0f,
                float ease = 0.6f)
            {
                this.frame = frame;
                this.poseBlend = poseBlend;
                this.breath = breath;
                this.gazeYaw = gazeYaw;
                this.bookSide = bookSide;
                this.bookLift = bookLift;
                this.pageTouch = pageTouch;
                this.ease = ease;
            }
        }

        private readonly struct ArmsCrossedPoseSpec
        {
            public readonly int frame;
            public readonly float poseBlend;
            public readonly float breath;
            public readonly float sway;
            public readonly float headYaw;
            public readonly float ease;

            public ArmsCrossedPoseSpec(
                int frame,
                float poseBlend,
                float breath = 0f,
                float sway = 0f,
                float headYaw = 0f,
                float ease = 0.6f)
            {
                this.frame = frame;
                this.poseBlend = poseBlend;
                this.breath = breath;
                this.sway = sway;
                this.headYaw = headYaw;
                this.ease = ease;
            }
        }

        private readonly struct LegCrossedPoseSpec
        {
            public readonly int frame;
            public readonly float poseBlend;
            public readonly float breath;
            public readonly float sway;
            public readonly float footLift;
            public readonly float footRoll;
            public readonly float ease;

            public LegCrossedPoseSpec(
                int frame,
                float poseBlend,
                float breath = 0f,
                float sway = 0f,
                float footLift = 0f,
                float footRoll = 0f,
                float ease = 0.6f)
            {
                this.frame = frame;
                this.poseBlend = poseBlend;
                this.breath = breath;
                this.sway = sway;
                this.footLift = footLift;
                this.footRoll = footRoll;
                this.ease = ease;
            }
        }

        private sealed class PoseContext
        {
            public Motion template;
            public GameObject rig;
            public Transform[] bones;
            public Motion.Pose basePose;
            public Motion.Pose dozePose;
            public Motion.Pose phonePose;
            public Motion.Pose bookPose;
            public Motion.Pose armsCrossedPose;
            public Motion.Pose legCrossedPose;
            public Transform root;
            public Transform hips;
            public Transform spine1;
            public Transform spine2;
            public Transform spine3;
            public Transform neck;
            public Transform head;
            public Transform leftThigh;
            public Transform leftShin;
            public Transform leftFoot;
            public Transform leftToe;
            public Transform rightThigh;
            public Transform rightShin;
            public Transform rightFoot;
            public Transform rightToe;
            public Transform leftUpperArm;
            public Transform leftForearm;
            public Transform leftHand;
            public Transform rightUpperArm;
            public Transform rightForearm;
            public Transform rightHand;
            public Transform leftThumb1;
            public Transform leftThumb2;
            public Transform rightThumb1;
            public Transform rightThumb2;
            public Transform[] leftGripFingers;
            public Transform[] rightGripFingers;
            public Vector3 forward;
            public Vector3 bodyRight;
            public Vector3 leftLapTarget;
            public Vector3 rightLapTarget;
            public Vector3 leftPhoneTarget;
            public Vector3 rightPhoneTarget;
            public Vector3 rightPocketTarget;
            public Vector3 leftBookTarget;
            public Vector3 rightBookTarget;
            public Vector3 rightBookStorageTarget;
            public Vector3 leftArmsCrossedTarget;
            public Vector3 rightArmsCrossedTarget;
            public Vector3 rightCrossedFootTarget;
            public Vector3 leftLegCrossedHandTarget;
            public Vector3 rightLegCrossedHandTarget;
            public Vector3 leftFootTarget;
            public Vector3 rightFootTarget;
            public Quaternion baseLeftHandRotation;
            public Quaternion baseRightHandRotation;
            public Quaternion dozeLeftHandRotation;
            public Quaternion dozeRightHandRotation;
            public bool hasDozeHandRotations;
            public Quaternion phoneLeftHandRotation;
            public Quaternion phoneRightHandRotation;
            public bool hasPhoneHandRotations;
            public Quaternion bookLeftHandRotation;
            public Quaternion bookRightHandRotation;
            public bool hasBookHandRotations;
            public Quaternion armsCrossedLeftHandRotation;
            public Quaternion armsCrossedRightHandRotation;
            public bool hasArmsCrossedHandRotations;
            public Quaternion baseRightFootRotation;
            public Quaternion legCrossedRightFootRotation;
            public Quaternion legCrossedLeftHandRotation;
            public Quaternion legCrossedRightHandRotation;
            public bool hasLegCrossedRotations;
        }

        private readonly struct ReviewSample
        {
            public readonly Motion motion;
            public readonly int frame;

            public ReviewSample(Motion motion, int frame)
            {
                this.motion = motion;
                this.frame = frame;
            }
        }

        [MenuItem("SubwayCarry/Animation/제작 · Seated Doze 세트")]
        public static void Generate()
        {
            GameObject rig = null;
            Motion template = null;

            try
            {
                AnimationClip seatedIdle = FindSharedClip("Sitting_Idle_Loop");
                rig = AnimationLabMotionAuthoring.CreateRig();
                template = AnimationLabMotionAuthoring.CreateSource(rig, seatedIdle, 0f);
                template.hideFlags = HideFlags.HideAndDontSave;
                template.author = Author;

                PoseContext context = CreateContext(template, rig);
                BuildBoundaryPoses(context);

                List<Motion.Pose> enterKeys = BuildEnterKeys(context);
                List<Motion.Pose> loopKeys = BuildLoopKeys(context);
                List<Motion.Pose> exitKeys = BuildExitKeys(context);
                ValidateBoundaries(context.basePose, context.dozePose, enterKeys, loopKeys, exitKeys);

                Motion enter = SaveMotion(
                    template,
                    EnterName,
                    "subwaycarry-seated-doze-enter-v1",
                    30,
                    enterKeys,
                    false,
                    context);
                Motion loop = SaveMotion(
                    template,
                    LoopName,
                    "subwaycarry-seated-doze-loop-v1",
                    120,
                    loopKeys,
                    true,
                    context);
                Motion exit = SaveMotion(
                    template,
                    ExitName,
                    "subwaycarry-seated-doze-exit-v1",
                    30,
                    exitKeys,
                    false,
                    context);

                BuildPreviewScene(enter, loop, exit);
                RenderReviewSheet(enter, loop, exit);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    "[MotionAuthoring] Seated Doze set generated and validated.\n" +
                    SourcePath(EnterName) + "\n" +
                    SourcePath(LoopName) + "\n" +
                    SourcePath(ExitName) + "\n" +
                    ScenePath);
            }
            finally
            {
                if (rig != null)
                {
                    UnityEngine.Object.DestroyImmediate(rig);
                }

                if (template != null)
                {
                    UnityEngine.Object.DestroyImmediate(template);
                }
            }
        }

        [MenuItem("SubwayCarry/Animation/제작 · Seated Phone 세트")]
        public static void GeneratePhone()
        {
            GameObject rig = null;
            Motion template = null;

            try
            {
                AnimationClip seatedIdle = FindSharedClip("Sitting_Idle_Loop");
                rig = AnimationLabMotionAuthoring.CreateRig();
                template = AnimationLabMotionAuthoring.CreateSource(rig, seatedIdle, 0f);
                template.hideFlags = HideFlags.HideAndDontSave;
                template.author = Author;

                PoseContext context = CreateContext(template, rig);
                BuildPhoneBoundaryPose(context);

                List<Motion.Pose> enterKeys = BuildPhoneEnterKeys(context);
                List<Motion.Pose> loopKeys = BuildPhoneLoopKeys(context);
                List<Motion.Pose> exitKeys = BuildPhoneExitKeys(context);
                ValidateBoundaries(
                    context.basePose,
                    context.phonePose,
                    enterKeys,
                    loopKeys,
                    exitKeys);

                Motion enter = SaveMotion(
                    template,
                    PhoneEnterName,
                    "subwaycarry-seated-phone-enter-v1",
                    52,
                    enterKeys,
                    false,
                    context,
                    "Phone",
                    true);
                Motion loop = SaveMotion(
                    template,
                    PhoneLoopName,
                    "subwaycarry-seated-phone-loop-v1",
                    120,
                    loopKeys,
                    true,
                    context,
                    "Phone",
                    true);
                Motion exit = SaveMotion(
                    template,
                    PhoneExitName,
                    "subwaycarry-seated-phone-exit-v1",
                    52,
                    exitKeys,
                    false,
                    context,
                    "Phone",
                    true);

                BuildPreviewScene(
                    enter,
                    loop,
                    exit,
                    PhoneScenePath,
                    "SeatedPhone_PreviewRig",
                    true,
                    false,
                    "Seated Phone");
                RenderReviewSheet(
                    enter,
                    loop,
                    exit,
                    PhoneScenePath,
                    "SeatedPhone_PreviewRig",
                    PhonePreviewPath,
                    "Seated Phone");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    "[MotionAuthoring] Seated Phone set generated and validated.\n" +
                    SourcePath(PhoneEnterName) + "\n" +
                    SourcePath(PhoneLoopName) + "\n" +
                    SourcePath(PhoneExitName) + "\n" +
                    PhoneScenePath);
            }
            finally
            {
                if (rig != null)
                {
                    UnityEngine.Object.DestroyImmediate(rig);
                }

                if (template != null)
                {
                    UnityEngine.Object.DestroyImmediate(template);
                }
            }
        }

        [MenuItem("SubwayCarry/Animation/제작 · Seated Book 세트")]
        public static void GenerateBook()
        {
            GameObject rig = null;
            Motion template = null;

            try
            {
                AnimationClip seatedIdle = FindSharedClip("Sitting_Idle_Loop");
                rig = AnimationLabMotionAuthoring.CreateRig();
                template = AnimationLabMotionAuthoring.CreateSource(rig, seatedIdle, 0f);
                template.hideFlags = HideFlags.HideAndDontSave;
                template.author = Author;

                PoseContext context = CreateContext(template, rig);
                BuildBookBoundaryPose(context);

                List<Motion.Pose> enterKeys = BuildBookEnterKeys(context);
                List<Motion.Pose> loopKeys = BuildBookLoopKeys(context);
                List<Motion.Pose> exitKeys = BuildBookExitKeys(context);
                ValidateBoundaries(
                    context.basePose,
                    context.bookPose,
                    enterKeys,
                    loopKeys,
                    exitKeys);

                Motion enter = SaveMotion(
                    template,
                    BookEnterName,
                    "subwaycarry-seated-book-enter-v1",
                    64,
                    enterKeys,
                    false,
                    context,
                    "Book",
                    false,
                    true);
                Motion loop = SaveMotion(
                    template,
                    BookLoopName,
                    "subwaycarry-seated-book-loop-v1",
                    150,
                    loopKeys,
                    true,
                    context,
                    "Book",
                    false,
                    true);
                Motion exit = SaveMotion(
                    template,
                    BookExitName,
                    "subwaycarry-seated-book-exit-v1",
                    64,
                    exitKeys,
                    false,
                    context,
                    "Book",
                    false,
                    true);

                BuildPreviewScene(
                    enter,
                    loop,
                    exit,
                    BookScenePath,
                    "SeatedBook_PreviewRig",
                    false,
                    true,
                    "Seated Book");
                RenderReviewSheet(
                    enter,
                    loop,
                    exit,
                    BookScenePath,
                    "SeatedBook_PreviewRig",
                    BookPreviewPath,
                    "Seated Book");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    "[MotionAuthoring] Seated Book set generated and validated.\n" +
                    SourcePath(BookEnterName) + "\n" +
                    SourcePath(BookLoopName) + "\n" +
                    SourcePath(BookExitName) + "\n" +
                    BookScenePath);
            }
            finally
            {
                if (rig != null)
                {
                    UnityEngine.Object.DestroyImmediate(rig);
                }

                if (template != null)
                {
                    UnityEngine.Object.DestroyImmediate(template);
                }
            }
        }

        [MenuItem("SubwayCarry/Animation/Create · Seated Arms Crossed Set")]
        public static void GenerateArmsCrossed()
        {
            GameObject rig = null;
            Motion template = null;

            try
            {
                AnimationClip seatedIdle = FindSharedClip("Sitting_Idle_Loop");
                rig = AnimationLabMotionAuthoring.CreateRig();
                template = AnimationLabMotionAuthoring.CreateSource(rig, seatedIdle, 0f);
                template.hideFlags = HideFlags.HideAndDontSave;
                template.author = Author;

                PoseContext context = CreateContext(template, rig);
                BuildArmsCrossedBoundaryPose(context);

                List<Motion.Pose> enterKeys = BuildArmsCrossedEnterKeys(context);
                List<Motion.Pose> loopKeys = BuildArmsCrossedLoopKeys(context);
                List<Motion.Pose> exitKeys = BuildArmsCrossedExitKeys(context);
                ValidateBoundaries(
                    context.basePose,
                    context.armsCrossedPose,
                    enterKeys,
                    loopKeys,
                    exitKeys);

                Motion enter = SaveMotion(
                    template,
                    ArmsCrossedEnterName,
                    "subwaycarry-seated-arms-crossed-enter-v1",
                    ArmsCrossedTransitionFrames,
                    enterKeys,
                    false,
                    context,
                    "ArmsCrossed",
                    false,
                    false,
                    true);
                Motion loop = SaveMotion(
                    template,
                    ArmsCrossedLoopName,
                    "subwaycarry-seated-arms-crossed-loop-v1",
                    120,
                    loopKeys,
                    true,
                    context,
                    "ArmsCrossed",
                    false,
                    false,
                    true);
                Motion exit = SaveMotion(
                    template,
                    ArmsCrossedExitName,
                    "subwaycarry-seated-arms-crossed-exit-v1",
                    ArmsCrossedTransitionFrames,
                    exitKeys,
                    false,
                    context,
                    "ArmsCrossed",
                    false,
                    false,
                    true);

                BuildPreviewScene(
                    enter,
                    loop,
                    exit,
                    ArmsCrossedScenePath,
                    "SeatedArmsCrossed_PreviewRig",
                    false,
                    false,
                    "Seated Arms Crossed");
                RenderReviewSheet(
                    enter,
                    loop,
                    exit,
                    ArmsCrossedScenePath,
                    "SeatedArmsCrossed_PreviewRig",
                    ArmsCrossedPreviewPath,
                    "Seated Arms Crossed");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    "[MotionAuthoring] Seated Arms Crossed set generated and validated.\n" +
                    SourcePath(ArmsCrossedEnterName) + "\n" +
                    SourcePath(ArmsCrossedLoopName) + "\n" +
                    SourcePath(ArmsCrossedExitName) + "\n" +
                    ArmsCrossedScenePath);
            }
            finally
            {
                if (rig != null)
                {
                    UnityEngine.Object.DestroyImmediate(rig);
                }

                if (template != null)
                {
                    UnityEngine.Object.DestroyImmediate(template);
                }
            }
        }

        [MenuItem("SubwayCarry/Animation/Sync · Seated Arms Crossed Boundaries from Loop 0")]
        public static void SyncArmsCrossedBoundariesFromLoopStart()
        {
            Motion storedLoop = LoadRequiredMotion(ArmsCrossedLoopName);
            Motion.Pose authoredStart = storedLoop.Exact(storedLoop.keys, 0)?.Copy(0);
            if (authoredStart == null)
            {
                throw new InvalidOperationException(
                    "Seated Arms Crossed Loop does not contain a saved key at frame 0.");
            }

            GameObject rig = null;
            Motion template = null;
            try
            {
                AnimationClip seatedIdle = FindSharedClip("Sitting_Idle_Loop");
                rig = AnimationLabMotionAuthoring.CreateRig();
                template = AnimationLabMotionAuthoring.CreateSource(rig, seatedIdle, 0f);
                template.hideFlags = HideFlags.HideAndDontSave;
                template.author = Author;
                RequireMatchingRig(storedLoop, template);

                PoseContext context = CreateContext(template, rig);
                BuildArmsCrossedBoundaryPose(context);
                Motion.Pose generatedBoundary = context.armsCrossedPose.Copy(0);
                List<Motion.Pose> enterKeys = ApplyAuthoredBoundaryAdjustment(
                    BuildArmsCrossedEnterKeys(context),
                    generatedBoundary,
                    authoredStart,
                    frame => Mathf.SmoothStep(
                        0f,
                        1f,
                        frame / (float)ArmsCrossedTransitionFrames));
                List<Motion.Pose> loopKeys = ApplyAuthoredBoundaryAdjustment(
                    BuildArmsCrossedLoopKeys(context),
                    generatedBoundary,
                    authoredStart,
                    frame => 1f);
                List<Motion.Pose> exitKeys = ApplyAuthoredBoundaryAdjustment(
                    BuildArmsCrossedExitKeys(context),
                    generatedBoundary,
                    authoredStart,
                    frame => 1f - Mathf.SmoothStep(
                        0f,
                        1f,
                        frame / (float)ArmsCrossedTransitionFrames));

                AnimationLabMotionAuthoring.Apply(template, context.bones, authoredStart);
                context.leftArmsCrossedTarget = context.leftHand.position;
                context.rightArmsCrossedTarget = context.rightHand.position;
                context.armsCrossedLeftHandRotation = context.leftHand.rotation;
                context.armsCrossedRightHandRotation = context.rightHand.rotation;
                context.hasArmsCrossedHandRotations = true;
                context.armsCrossedPose = authoredStart.Copy(0);

                ValidateBoundaries(
                    context.basePose,
                    context.armsCrossedPose,
                    enterKeys,
                    loopKeys,
                    exitKeys);
                Motion enter = SaveMotion(
                    template,
                    ArmsCrossedEnterName,
                    "subwaycarry-seated-arms-crossed-enter-v1",
                    ArmsCrossedTransitionFrames,
                    enterKeys,
                    false,
                    context,
                    "ArmsCrossed",
                    false,
                    false,
                    true);
                Motion loop = SaveMotion(
                    template,
                    ArmsCrossedLoopName,
                    "subwaycarry-seated-arms-crossed-loop-v1",
                    120,
                    loopKeys,
                    true,
                    context,
                    "ArmsCrossed",
                    false,
                    false,
                    true);
                Motion exit = SaveMotion(
                    template,
                    ArmsCrossedExitName,
                    "subwaycarry-seated-arms-crossed-exit-v1",
                    ArmsCrossedTransitionFrames,
                    exitKeys,
                    false,
                    context,
                    "ArmsCrossed",
                    false,
                    false,
                    true);

                ValidateMirroredTransitions(enter, exit);
                RequireSamePose(
                    loop.Exact(loop.keys, 0),
                    enter.Exact(enter.keys, enter.lastFrame),
                    "authored enter -> loop");
                RequireSamePose(
                    loop.Exact(loop.keys, 0),
                    loop.Exact(loop.keys, loop.lastFrame),
                    "authored loop seam");
                RequireSamePose(
                    loop.Exact(loop.keys, 0),
                    exit.Exact(exit.keys, 0),
                    "authored loop -> exit");
                RenderReviewSheet(
                    enter,
                    loop,
                    exit,
                    ArmsCrossedScenePath,
                    "SeatedArmsCrossed_PreviewRig",
                    ArmsCrossedPreviewPath,
                    "Seated Arms Crossed");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    "[MotionAuthoring] Seated Arms Crossed full set rebuilt from the authored Loop 0 pose.\n" +
                    "Enter keys blend into the authored pose; every Loop key carries the adjustment; " +
                    "Exit keys blend back to seated idle.");
            }
            finally
            {
                if (rig != null)
                {
                    UnityEngine.Object.DestroyImmediate(rig);
                }

                if (template != null)
                {
                    UnityEngine.Object.DestroyImmediate(template);
                }
            }
        }

        private static Motion LoadRequiredMotion(string clipName)
        {
            string path = SourcePath(clipName);
            Motion motion = AssetDatabase.LoadAssetAtPath<Motion>(path);
            if (motion == null)
            {
                throw new InvalidOperationException("Missing authored motion: " + path);
            }

            return motion;
        }

        private static void RequireMatchingRig(Motion target, Motion reference)
        {
            if (target.rigSignature != reference.rigSignature ||
                target.paths.Length != reference.paths.Length ||
                !target.paths.SequenceEqual(reference.paths))
            {
                throw new InvalidOperationException(
                    target.clipName + " does not use the same rig layout as " + reference.clipName + ".");
            }
        }

        private static List<Motion.Pose> ApplyAuthoredBoundaryAdjustment(
            IReadOnlyList<Motion.Pose> generatedKeys,
            Motion.Pose generatedBoundary,
            Motion.Pose authoredBoundary,
            Func<int, float> frameWeight)
        {
            if (generatedBoundary.rotations.Length != authoredBoundary.rotations.Length)
            {
                throw new InvalidOperationException(
                    "The generated and authored Arms Crossed poses use different bone counts.");
            }

            Vector3 rootOffset = authoredBoundary.root - generatedBoundary.root;
            Vector3 hipsOffset = authoredBoundary.hips - generatedBoundary.hips;
            Quaternion[] rotationOffsets = new Quaternion[generatedBoundary.rotations.Length];
            bool[] changed = new bool[generatedBoundary.rotations.Length];
            for (int index = 0; index < rotationOffsets.Length; index++)
            {
                Quaternion generated = generatedBoundary.rotations[index];
                Quaternion authored = Motion.SameHemisphere(
                    generated,
                    authoredBoundary.rotations[index]);
                changed[index] = Quaternion.Angle(generated, authored) > 0.001f;
                rotationOffsets[index] = changed[index]
                    ? Quaternion.Inverse(generated) * authored
                    : Quaternion.identity;
            }

            List<Motion.Pose> adjusted = new List<Motion.Pose>(generatedKeys.Count);
            foreach (Motion.Pose generatedKey in generatedKeys)
            {
                float weight = Mathf.Clamp01(frameWeight(generatedKey.frame));
                Motion.Pose pose = generatedKey.Copy(generatedKey.frame);
                pose.root += rootOffset * weight;
                pose.hips += hipsOffset * weight;
                for (int index = 0; index < pose.rotations.Length; index++)
                {
                    if (!changed[index])
                    {
                        continue;
                    }

                    Quaternion weightedOffset = Quaternion.Slerp(
                        Quaternion.identity,
                        rotationOffsets[index],
                        weight);
                    pose.rotations[index] = (pose.rotations[index] * weightedOffset).normalized;
                }

                adjusted.Add(pose);
            }

            return adjusted;
        }

        private static void ValidateMirroredTransitions(
            Motion enter,
            Motion exit,
            string activityName = "Seated Arms Crossed")
        {
            if (enter.lastFrame != exit.lastFrame ||
                enter.sampleRate != exit.sampleRate)
            {
                throw new InvalidOperationException(
                    "Seated Arms Crossed Enter and Exit timing does not match.");
            }

            for (int frame = 0; frame <= enter.lastFrame; frame++)
            {
                RequireSamePose(
                    enter.Evaluate(frame, false),
                    exit.Evaluate(exit.lastFrame - frame, false),
                    "mirrored transition frame " + frame);
            }

            Debug.Log(
                "[MotionAuthoring] " + activityName + " Enter/Exit are mirrored over " +
                enter.lastFrame + " frames (" + enter.Duration.ToString("F2") + "s).");
        }

        [MenuItem("SubwayCarry/Animation/Create · Seated Leg Crossed Set")]
        public static void GenerateLegCrossed()
        {
            GameObject rig = null;
            Motion template = null;

            try
            {
                AnimationClip seatedIdle = FindSharedClip("Sitting_Idle_Loop");
                rig = AnimationLabMotionAuthoring.CreateRig();
                template = AnimationLabMotionAuthoring.CreateSource(rig, seatedIdle, 0f);
                template.hideFlags = HideFlags.HideAndDontSave;
                template.author = Author;

                PoseContext context = CreateContext(template, rig);
                BuildLegCrossedBoundaryPose(context);

                List<Motion.Pose> enterKeys = BuildLegCrossedEnterKeys(context);
                List<Motion.Pose> loopKeys = BuildLegCrossedLoopKeys(context);
                List<Motion.Pose> exitKeys = BuildLegCrossedExitKeys(context);
                ValidateBoundaries(
                    context.basePose,
                    context.legCrossedPose,
                    enterKeys,
                    loopKeys,
                    exitKeys);

                Motion enter = SaveMotion(
                    template,
                    LegCrossedEnterName,
                    "subwaycarry-seated-leg-crossed-enter-v1",
                    LegCrossedTransitionFrames,
                    enterKeys,
                    false,
                    context,
                    "LegCrossed",
                    false,
                    false,
                    false,
                    true,
                    true);
                Motion loop = SaveMotion(
                    template,
                    LegCrossedLoopName,
                    "subwaycarry-seated-leg-crossed-loop-v1",
                    120,
                    loopKeys,
                    true,
                    context,
                    "LegCrossed",
                    false,
                    false,
                    false,
                    true,
                    true);
                Motion exit = SaveMotion(
                    template,
                    LegCrossedExitName,
                    "subwaycarry-seated-leg-crossed-exit-v1",
                    LegCrossedTransitionFrames,
                    exitKeys,
                    false,
                    context,
                    "LegCrossed",
                    false,
                    false,
                    false,
                    true,
                    true);

                ValidateMirroredTransitions(enter, exit, "Seated Leg Crossed");

                BuildPreviewScene(
                    enter,
                    loop,
                    exit,
                    LegCrossedScenePath,
                    "SeatedLegCrossed_PreviewRig",
                    false,
                    false,
                    "Seated Leg Crossed");
                RenderReviewSheet(
                    enter,
                    loop,
                    exit,
                    LegCrossedScenePath,
                    "SeatedLegCrossed_PreviewRig",
                    LegCrossedPreviewPath,
                    "Seated Leg Crossed");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    "[MotionAuthoring] Seated Leg Crossed set generated and validated.\n" +
                    SourcePath(LegCrossedEnterName) + "\n" +
                    SourcePath(LegCrossedLoopName) + "\n" +
                    SourcePath(LegCrossedExitName) + "\n" +
                    LegCrossedScenePath);
            }
            finally
            {
                if (rig != null)
                {
                    UnityEngine.Object.DestroyImmediate(rig);
                }

                if (template != null)
                {
                    UnityEngine.Object.DestroyImmediate(template);
                }
            }
        }

        private static PoseContext CreateContext(Motion template, GameObject rig)
        {
            PoseContext context = new PoseContext
            {
                template = template,
                rig = rig,
                bones = AnimationLabMotionAuthoring.Bind(template, rig),
                basePose = template.keys[0].Copy(0),
                root = rig.transform,
                hips = Bone(rig, "DEF-hips"),
                spine1 = Bone(rig, "DEF-spine.001"),
                spine2 = Bone(rig, "DEF-spine.002"),
                spine3 = Bone(rig, "DEF-spine.003"),
                neck = Bone(rig, "DEF-neck"),
                head = Bone(rig, "DEF-head"),
                leftThigh = Bone(rig, "DEF-thigh.L"),
                leftShin = Bone(rig, "DEF-shin.L"),
                leftFoot = Bone(rig, "DEF-foot.L"),
                leftToe = Bone(rig, "DEF-toe.L"),
                rightThigh = Bone(rig, "DEF-thigh.R"),
                rightShin = Bone(rig, "DEF-shin.R"),
                rightFoot = Bone(rig, "DEF-foot.R"),
                rightToe = Bone(rig, "DEF-toe.R"),
                leftUpperArm = Bone(rig, "DEF-upper_arm.L"),
                leftForearm = Bone(rig, "DEF-forearm.L"),
                leftHand = Bone(rig, "DEF-hand.L"),
                rightUpperArm = Bone(rig, "DEF-upper_arm.R"),
                rightForearm = Bone(rig, "DEF-forearm.R"),
                rightHand = Bone(rig, "DEF-hand.R"),
                leftThumb1 = Bone(rig, "DEF-thumb.01.L"),
                leftThumb2 = Bone(rig, "DEF-thumb.02.L"),
                rightThumb1 = Bone(rig, "DEF-thumb.01.R"),
                rightThumb2 = Bone(rig, "DEF-thumb.02.R"),
                leftGripFingers = GripFingers(rig, "L"),
                rightGripFingers = GripFingers(rig, "R"),
            };

            AnimationLabMotionAuthoring.Apply(template, context.bones, context.basePose);
            GetBodyAxes(context, out context.forward, out context.bodyRight);
            context.leftFootTarget = context.leftFoot.position;
            context.rightFootTarget = context.rightFoot.position;
            context.baseLeftHandRotation = context.leftHand.rotation;
            context.baseRightHandRotation = context.rightHand.rotation;
            context.baseRightFootRotation = context.rightFoot.rotation;
            context.leftLapTarget = Vector3.Lerp(
                                        context.leftThigh.position,
                                        context.leftShin.position,
                                        0.58f)
                                    + Vector3.up * 0.045f
                                    + context.forward * 0.025f;
            context.rightLapTarget = Vector3.Lerp(
                                         context.rightThigh.position,
                                         context.rightShin.position,
                                         0.58f)
                                     + Vector3.up * 0.045f
                                     + context.forward * 0.025f;
            Vector3 phoneCenter = context.spine3.position
                                  + context.forward * 0.22f
                                  - Vector3.up * 0.20f;
            Vector3 gripCenter = phoneCenter - Vector3.up * 0.035f;
            context.leftPhoneTarget = gripCenter - context.bodyRight * 0.108f;
            context.rightPhoneTarget = gripCenter + context.bodyRight * 0.108f;
            context.rightPocketTarget = Vector3.Lerp(
                                                context.rightThigh.position,
                                                context.rightShin.position,
                                                0.20f)
                                            + context.bodyRight * 0.10f
                                            + Vector3.up * 0.035f
                                            - context.forward * 0.025f;
            Vector3 bookCenter = context.spine3.position
                                 + context.forward * 0.245f
                                 - Vector3.up * 0.17f;
            context.leftBookTarget = bookCenter
                                     - context.bodyRight * 0.19f
                                     - Vector3.up * 0.015f;
            context.rightBookTarget = bookCenter
                                      + context.bodyRight * 0.19f
                                      - Vector3.up * 0.015f;
            context.rightBookStorageTarget = context.rightThigh.position
                                                   + context.bodyRight * 0.22f
                                                   + Vector3.up * 0.035f
                                                   - context.forward * 0.035f;
            Vector3 armsCrossedCenter = context.spine3.position
                                        + context.forward * 0.095f
                                        - Vector3.up * 0.035f;
            context.leftArmsCrossedTarget = armsCrossedCenter
                                            + context.bodyRight * 0.118f
                                            + Vector3.up * 0.020f
                                            + context.forward * 0.025f;
            context.rightArmsCrossedTarget = armsCrossedCenter
                                             - context.bodyRight * 0.115f
                                             - Vector3.up * 0.020f
                                             + context.forward * 0.005f;
            context.rightCrossedFootTarget = Vector3.Lerp(
                                                 context.leftThigh.position,
                                                 context.leftShin.position,
                                                 0.58f)
                                             - context.bodyRight * 0.035f
                                             + Vector3.up * 0.155f
                                             + context.forward * 0.025f;
            Vector3 closeLapCenter = Vector3.Lerp(
                                         (context.leftThigh.position + context.rightThigh.position) * 0.5f,
                                         (context.leftShin.position + context.rightShin.position) * 0.5f,
                                         0.28f)
                                     + Vector3.up * 0.065f
                                     + context.forward * 0.015f;
            context.leftLegCrossedHandTarget = closeLapCenter - context.bodyRight * 0.065f;
            context.rightLegCrossedHandTarget = closeLapCenter + context.bodyRight * 0.065f;
            return context;
        }

        private static void BuildBoundaryPoses(PoseContext context)
        {
            PoseSpec fullDoze = new PoseSpec(0, 1f, 1f, 1f, ease: 0.55f);
            CreatePose(context, fullDoze);
            context.dozeLeftHandRotation = context.leftHand.rotation;
            context.dozeRightHandRotation = context.rightHand.rotation;
            context.hasDozeHandRotations = true;
            context.dozePose = CreatePose(context, fullDoze);
        }

        private static List<Motion.Pose> BuildEnterKeys(PoseContext context)
        {
            return new List<Motion.Pose>
            {
                At(context.basePose, 0, 0.70f),
                CreatePose(context, new PoseSpec(5, 0.02f, 0.10f, 0.02f, ease: 0.70f)),
                CreatePose(context, new PoseSpec(10, 0.08f, 0.28f, 0.12f, ease: 0.68f)),
                CreatePose(context, new PoseSpec(15, 0.22f, 0.50f, 0.30f, ease: 0.64f)),
                CreatePose(context, new PoseSpec(20, 0.45f, 0.72f, 0.55f, ease: 0.60f)),
                CreatePose(context, new PoseSpec(25, 0.72f, 0.90f, 0.82f, ease: 0.56f)),
                At(context.dozePose, 30, 0.50f),
            };
        }

        private static List<Motion.Pose> BuildLoopKeys(PoseContext context)
        {
            return new List<Motion.Pose>
            {
                At(context.dozePose, 0, 0.64f),
                CreatePose(context, new PoseSpec(15, 1f, 1f, 1f, -0.8f, 0.5f, 1.0f, 0.64f)),
                CreatePose(context, new PoseSpec(30, 1f, 1f, 1f, -1.2f, 1.1f, 2.0f, 0.62f)),
                CreatePose(context, new PoseSpec(45, 1f, 1f, 1f, -0.4f, 1.6f, 5.0f, 0.58f)),
                CreatePose(context, new PoseSpec(60, 1f, 1f, 1f, 0.6f, 1.2f, 11.0f, 0.54f)),
                CreatePose(context, new PoseSpec(75, 1f, 1f, 1f, 0.8f, 0.3f, 7.0f, 0.58f)),
                CreatePose(context, new PoseSpec(90, 1f, 1f, 1f, 0f, -1.0f, 3.0f, 0.62f)),
                CreatePose(context, new PoseSpec(105, 1f, 1f, 1f, -0.9f, -0.6f, 1.0f, 0.64f)),
                At(context.dozePose, 120, 0.64f),
            };
        }

        private static List<Motion.Pose> BuildExitKeys(PoseContext context)
        {
            return new List<Motion.Pose>
            {
                At(context.dozePose, 0, 0.62f),
                CreatePose(context, new PoseSpec(5, 0.96f, 0.70f, 1f, ease: 0.58f)),
                CreatePose(context, new PoseSpec(10, 0.82f, 0.40f, 0.88f, ease: 0.56f)),
                CreatePose(context, new PoseSpec(16, 0.58f, 0.20f, 0.65f, ease: 0.58f)),
                CreatePose(context, new PoseSpec(23, 0.28f, 0.07f, 0.30f, ease: 0.64f)),
                At(context.basePose, 30, 0.50f),
            };
        }

        private static Motion.Pose CreatePose(PoseContext context, PoseSpec spec)
        {
            AnimationLabMotionAuthoring.Apply(context.template, context.bones, context.basePose);

            context.spine1.rotation = Quaternion.AngleAxis(
                                          6f * spec.bodySlump,
                                          context.bodyRight)
                                      * context.spine1.rotation;
            context.spine2.rotation = Quaternion.AngleAxis(
                                          7f * spec.bodySlump + spec.breath,
                                          context.bodyRight)
                                      * context.spine2.rotation;
            context.spine2.rotation = Quaternion.AngleAxis(
                                          spec.sway * 0.45f,
                                          context.forward)
                                      * context.spine2.rotation;
            context.spine3.rotation = Quaternion.AngleAxis(
                                          5f * spec.bodySlump,
                                          context.bodyRight)
                                      * context.spine3.rotation;
            context.neck.rotation = Quaternion.AngleAxis(
                                        12f * spec.headSlump + spec.nod * 0.25f,
                                        context.bodyRight)
                                    * context.neck.rotation;
            context.neck.rotation = Quaternion.AngleAxis(
                                        spec.headSlump + spec.sway * 0.55f,
                                        context.forward)
                                    * context.neck.rotation;
            context.head.rotation = Quaternion.AngleAxis(
                                        20f * spec.headSlump + spec.nod,
                                        context.bodyRight)
                                    * context.head.rotation;
            context.head.rotation = Quaternion.AngleAxis(
                                        2.5f * spec.headSlump + spec.sway * 1.15f,
                                        context.forward)
                                    * context.head.rotation;

            PlaceHand(
                context,
                context.leftUpperArm,
                context.leftForearm,
                context.leftHand,
                context.leftLapTarget,
                -context.bodyRight,
                context.baseLeftHandRotation,
                context.dozeLeftHandRotation,
                spec.handBlend);
            PlaceHand(
                context,
                context.rightUpperArm,
                context.rightForearm,
                context.rightHand,
                context.rightLapTarget,
                context.bodyRight,
                context.baseRightHandRotation,
                context.dozeRightHandRotation,
                spec.handBlend);

            Motion.Pose pose = AnimationLabMotionAuthoring.Capture(
                context.template,
                context.rig,
                spec.frame);
            pose.ease = spec.ease;
            return pose;
        }

        private static void PlaceHand(
            PoseContext context,
            Transform upperArm,
            Transform forearm,
            Transform hand,
            Vector3 lapTarget,
            Vector3 outward,
            Quaternion baseRotation,
            Quaternion dozeRotation,
            float blend)
        {
            if (blend <= 0f)
            {
                return;
            }

            Vector3 naturalPosition = hand.position;
            Quaternion naturalRotation = hand.rotation;
            Vector3 target = Vector3.Lerp(naturalPosition, lapTarget, blend);
            Vector3 elbowPole = upperArm.position
                                + outward * 0.42f
                                - Vector3.up * 0.26f
                                - context.forward * 0.04f;
            Quaternion targetRotation = context.hasDozeHandRotations
                ? Quaternion.Slerp(baseRotation, dozeRotation, blend)
                : naturalRotation;

            SolveTwoBone(upperArm, forearm, hand, target, elbowPole, targetRotation);
        }

        private static void BuildPhoneBoundaryPose(PoseContext context)
        {
            PhonePoseSpec fullPhone = new PhonePoseSpec(0, 1f, ease: 0.58f);
            CreatePhonePose(context, fullPhone);
            context.phoneLeftHandRotation = context.leftHand.rotation;
            context.phoneRightHandRotation = context.rightHand.rotation;
            context.hasPhoneHandRotations = true;
            context.phonePose = CreatePhonePose(context, fullPhone);
        }

        private static List<Motion.Pose> BuildPhoneEnterKeys(PoseContext context)
        {
            Vector3 pocketLift = context.rightPocketTarget + Vector3.up * 0.055f;
            Vector3 lowerCenter = Vector3.Lerp(
                pocketLift,
                context.rightPhoneTarget,
                0.48f);
            return new List<Motion.Pose>
            {
                At(context.basePose, 0, 0.72f),
                CreatePhoneRetrievalPose(
                    context, 8, 0.02f, 0.35f, 0f, 0f,
                    context.rightPocketTarget, 0.38f, 0f, 0.72f),
                CreatePhoneRetrievalPose(
                    context, 16, 0.03f, 1f, 0f, 0f,
                    context.rightPocketTarget, 1f, 0.28f, 0.68f),
                CreatePhoneRetrievalPose(
                    context, 24, 0.10f, 0.88f, 0f, 0f,
                    pocketLift, 1f, 0.90f, 0.64f),
                CreatePhoneRetrievalPose(
                    context, 34, 0.38f, 0.52f, 0.28f, 0.30f,
                    lowerCenter, 1f, 1f, 0.60f),
                CreatePhoneRetrievalPose(
                    context, 44, 0.78f, 0.16f, 0.78f, 0.82f,
                    Vector3.Lerp(lowerCenter, context.rightPhoneTarget, 0.82f),
                    1f, 1f, 0.57f),
                At(context.phonePose, 52, 0.54f),
            };
        }

        private static List<Motion.Pose> BuildPhoneLoopKeys(PoseContext context)
        {
            return new List<Motion.Pose>
            {
                At(context.phonePose, 0, 0.64f),
                CreatePhonePose(context, new PhonePoseSpec(15, 1f, 0.35f, ease: 0.64f)),
                CreatePhonePose(context, new PhonePoseSpec(30, 1f, 0.70f, -0.6f, 0.002f, 0.002f, 2f, 0.62f)),
                CreatePhonePose(context, new PhonePoseSpec(45, 1f, 0.30f, -1.1f, 0.003f, 0.004f, 6f, 0.58f)),
                CreatePhonePose(context, new PhonePoseSpec(60, 1f, -0.20f, -0.4f, 0f, 0.006f, -2f, 0.58f)),
                CreatePhonePose(context, new PhonePoseSpec(75, 1f, -0.55f, 0.8f, -0.003f, 0.003f, 5f, 0.60f)),
                CreatePhonePose(context, new PhonePoseSpec(90, 1f, -0.15f, 1.1f, -0.004f, 0f, 1f, 0.62f)),
                CreatePhonePose(context, new PhonePoseSpec(105, 1f, 0.25f, 0.4f, -0.001f, 0f, -1f, 0.64f)),
                At(context.phonePose, 120, 0.64f),
            };
        }

        private static List<Motion.Pose> BuildPhoneExitKeys(PoseContext context)
        {
            Vector3 pocketLift = context.rightPocketTarget + Vector3.up * 0.055f;
            Vector3 lowerCenter = Vector3.Lerp(
                pocketLift,
                context.rightPhoneTarget,
                0.48f);
            return new List<Motion.Pose>
            {
                At(context.phonePose, 0, 0.62f),
                CreatePhoneRetrievalPose(
                    context, 8, 0.82f, 0.10f, 0.84f, 0.85f,
                    context.rightPhoneTarget, 1f, 1f, 0.60f),
                CreatePhoneRetrievalPose(
                    context, 18, 0.48f, 0.38f, 0.46f, 0.50f,
                    lowerCenter, 1f, 1f, 0.58f),
                CreatePhoneRetrievalPose(
                    context, 28, 0.14f, 0.82f, 0.12f, 0.12f,
                    pocketLift, 1f, 1f, 0.60f),
                CreatePhoneRetrievalPose(
                    context, 36, 0.03f, 1f, 0f, 0f,
                    context.rightPocketTarget, 1f, 0.72f, 0.64f),
                CreatePhoneRetrievalPose(
                    context, 44, 0f, 0.48f, 0f, 0f,
                    context.rightPocketTarget, 0.45f, 0.10f, 0.68f),
                At(context.basePose, 52, 0.52f),
            };
        }

        private static Motion.Pose CreatePhoneRetrievalPose(
            PoseContext context,
            int frame,
            float activityBlend,
            float pocketFocus,
            float leftReach,
            float leftGrip,
            Vector3 rightTarget,
            float rightReach,
            float rightGrip,
            float ease)
        {
            AnimationLabMotionAuthoring.Apply(context.template, context.bones, context.basePose);

            activityBlend = Mathf.Clamp01(activityBlend);
            pocketFocus = Mathf.Clamp01(pocketFocus);
            context.spine1.rotation = Quaternion.AngleAxis(
                                          2.5f * activityBlend + 2.5f * pocketFocus,
                                          context.bodyRight)
                                      * context.spine1.rotation;
            context.spine2.rotation = Quaternion.AngleAxis(
                                          3.5f * activityBlend + 3.5f * pocketFocus,
                                          context.bodyRight)
                                      * context.spine2.rotation;
            context.spine3.rotation = Quaternion.AngleAxis(
                                          -5f * pocketFocus,
                                          Vector3.up)
                                      * Quaternion.AngleAxis(
                                          2f * activityBlend,
                                          context.bodyRight)
                                      * context.spine3.rotation;
            context.neck.rotation = Quaternion.AngleAxis(
                                        7f * activityBlend + 3f * pocketFocus,
                                        context.bodyRight)
                                    * context.neck.rotation;
            context.head.rotation = Quaternion.AngleAxis(
                                        11f * activityBlend + 6f * pocketFocus,
                                        context.bodyRight)
                                    * Quaternion.AngleAxis(-7f * pocketFocus, Vector3.up)
                                    * context.head.rotation;

            Quaternion pocketRotation = Quaternion.AngleAxis(-38f, context.forward)
                                        * Quaternion.AngleAxis(18f, context.bodyRight)
                                        * context.baseRightHandRotation;
            Quaternion rightRotation = Quaternion.Slerp(
                pocketRotation,
                context.phoneRightHandRotation,
                activityBlend);
            PlaceTransitionHand(
                context,
                context.rightUpperArm,
                context.rightForearm,
                context.rightHand,
                rightTarget,
                context.bodyRight,
                context.baseRightHandRotation,
                rightRotation,
                rightReach);
            PlaceTransitionHand(
                context,
                context.leftUpperArm,
                context.leftForearm,
                context.leftHand,
                context.leftPhoneTarget,
                -context.bodyRight,
                context.baseLeftHandRotation,
                context.phoneLeftHandRotation,
                leftReach);

            CurlGripFingers(context.leftGripFingers, leftGrip);
            CurlGripFingers(context.rightGripFingers, rightGrip);
            RotateLocal(context.leftThumb1, 16f * leftGrip, Vector3.right);
            RotateLocal(context.leftThumb2, 11f * leftGrip, Vector3.right);
            RotateLocal(context.rightThumb1, 17f * rightGrip, Vector3.right);
            RotateLocal(context.rightThumb2, 11f * rightGrip, Vector3.right);

            Motion.Pose pose = AnimationLabMotionAuthoring.Capture(
                context.template,
                context.rig,
                frame);
            pose.ease = ease;
            return pose;
        }

        private static Motion.Pose CreatePhonePose(PoseContext context, PhonePoseSpec spec)
        {
            AnimationLabMotionAuthoring.Apply(context.template, context.bones, context.basePose);

            float blend = Mathf.Clamp01(spec.poseBlend);
            context.spine1.rotation = Quaternion.AngleAxis(2.5f * blend, context.bodyRight)
                                      * context.spine1.rotation;
            context.spine2.rotation = Quaternion.AngleAxis(
                                          3.5f * blend + spec.breath,
                                          context.bodyRight)
                                      * context.spine2.rotation;
            context.spine3.rotation = Quaternion.AngleAxis(2f * blend, context.bodyRight)
                                      * context.spine3.rotation;
            context.neck.rotation = Quaternion.AngleAxis(7f * blend, context.bodyRight)
                                    * context.neck.rotation;
            context.head.rotation = Quaternion.AngleAxis(11f * blend, context.bodyRight)
                                    * context.head.rotation;
            context.head.rotation = Quaternion.AngleAxis(spec.headYaw, Vector3.up)
                                    * context.head.rotation;

            Vector3 phoneOffset = context.bodyRight * spec.phoneSide
                                  + Vector3.up * spec.phoneLift;
            PlacePhoneHand(
                context,
                context.leftUpperArm,
                context.leftForearm,
                context.leftHand,
                context.leftPhoneTarget + phoneOffset,
                -context.bodyRight,
                context.baseLeftHandRotation,
                context.phoneLeftHandRotation,
                blend);
            PlacePhoneHand(
                context,
                context.rightUpperArm,
                context.rightForearm,
                context.rightHand,
                context.rightPhoneTarget + phoneOffset + context.forward * (spec.rightTap * 0.00045f),
                context.bodyRight,
                context.baseRightHandRotation,
                context.phoneRightHandRotation,
                blend);

            CurlGripFingers(context.leftGripFingers, blend);
            CurlGripFingers(context.rightGripFingers, blend);
            RotateLocal(context.leftThumb1, 16f * blend, Vector3.right);
            RotateLocal(context.leftThumb2, 11f * blend, Vector3.right);
            RotateLocal(context.rightThumb1, 17f * blend + spec.rightTap, Vector3.right);
            RotateLocal(context.rightThumb2, 11f * blend + spec.rightTap * 0.45f, Vector3.right);

            Motion.Pose pose = AnimationLabMotionAuthoring.Capture(
                context.template,
                context.rig,
                spec.frame);
            pose.ease = spec.ease;
            return pose;
        }

        private static void PlacePhoneHand(
            PoseContext context,
            Transform upperArm,
            Transform forearm,
            Transform hand,
            Vector3 phoneTarget,
            Vector3 outward,
            Quaternion baseRotation,
            Quaternion phoneRotation,
            float blend)
        {
            if (blend <= 0f)
            {
                return;
            }

            Vector3 target = Vector3.Lerp(hand.position, phoneTarget, blend);
            Vector3 elbowPole = upperArm.position
                                + outward * 0.34f
                                - Vector3.up * 0.28f
                                - context.forward * 0.07f;
            float side = Mathf.Sign(Vector3.Dot(outward, context.bodyRight));
            Quaternion initialPhoneRotation = Quaternion.AngleAxis(
                                                  -side * 24f,
                                                  context.forward)
                                              * baseRotation;
            Quaternion finalRotation = context.hasPhoneHandRotations
                ? phoneRotation
                : initialPhoneRotation;
            Quaternion targetRotation = Quaternion.Slerp(baseRotation, finalRotation, blend);
            SolveTwoBone(upperArm, forearm, hand, target, elbowPole, targetRotation);
        }

        private static void RotateLocal(Transform bone, float degrees, Vector3 axis)
        {
            bone.localRotation *= Quaternion.AngleAxis(degrees, axis);
        }

        private static void CurlGripFingers(
            Transform[] fingers,
            float blend,
            float strength = 1f)
        {
            float[] curlAngles =
            {
                12f, 18f, 8f,
                22f, 28f, 12f,
                26f, 32f, 14f,
                30f, 36f, 16f,
            };
            for (int index = 0; index < fingers.Length; index++)
            {
                RotateLocal(
                    fingers[index],
                    curlAngles[index] * blend * strength,
                    Vector3.right);
            }
        }

        private static void CurlBookFingers(Transform[] fingers, float blend)
        {
            float[] curlAngles =
            {
                4f, 8f, 3f,
                18f, 24f, 10f,
                22f, 28f, 12f,
                26f, 32f, 14f,
            };
            for (int index = 0; index < fingers.Length; index++)
            {
                RotateLocal(fingers[index], curlAngles[index] * blend, Vector3.right);
            }
        }

        private static void BuildBookBoundaryPose(PoseContext context)
        {
            BookPoseSpec fullBook = new BookPoseSpec(0, 1f, ease: 0.58f);
            CreateBookPose(context, fullBook);
            context.bookLeftHandRotation = context.leftHand.rotation;
            context.bookRightHandRotation = context.rightHand.rotation;
            context.hasBookHandRotations = true;
            context.bookPose = CreateBookPose(context, fullBook);
        }

        private static List<Motion.Pose> BuildBookEnterKeys(PoseContext context)
        {
            Vector3 storageLift = context.rightBookStorageTarget + Vector3.up * 0.07f;
            Vector3 lowerCenter = Vector3.Lerp(
                storageLift,
                context.rightBookTarget,
                0.52f);
            return new List<Motion.Pose>
            {
                At(context.basePose, 0, 0.72f),
                CreateBookRetrievalPose(
                    context, 10, 0.02f, 0.38f, 0f, 0f,
                    context.rightBookStorageTarget, 0.40f, 0f, 0.72f),
                CreateBookRetrievalPose(
                    context, 20, 0.03f, 1f, 0f, 0f,
                    context.rightBookStorageTarget, 1f, 0.32f, 0.68f),
                CreateBookRetrievalPose(
                    context, 30, 0.12f, 0.86f, 0f, 0f,
                    storageLift, 1f, 1f, 0.64f),
                CreateBookRetrievalPose(
                    context, 42, 0.42f, 0.48f, 0.36f, 0.40f,
                    lowerCenter, 1f, 1f, 0.60f),
                CreateBookRetrievalPose(
                    context, 54, 0.82f, 0.12f, 0.82f, 0.86f,
                    Vector3.Lerp(lowerCenter, context.rightBookTarget, 0.86f),
                    1f, 1f, 0.57f),
                At(context.bookPose, 64, 0.54f),
            };
        }

        private static List<Motion.Pose> BuildBookLoopKeys(PoseContext context)
        {
            return new List<Motion.Pose>
            {
                At(context.bookPose, 0, 0.64f),
                CreateBookPose(context, new BookPoseSpec(25, 1f, 0.35f, -1.2f, ease: 0.64f)),
                CreateBookPose(context, new BookPoseSpec(50, 1f, 0.65f, -0.5f, 0.003f, 0.003f, 2f, 0.62f)),
                CreateBookPose(context, new BookPoseSpec(75, 1f, 0f, 0.6f, 0f, 0.006f, 0f, 0.60f)),
                CreateBookPose(context, new BookPoseSpec(100, 1f, -0.55f, 1.2f, -0.003f, 0.003f, 1f, 0.60f)),
                CreateBookPose(context, new BookPoseSpec(125, 1f, -0.15f, 0.3f, -0.001f, 0f, 5f, 0.62f)),
                At(context.bookPose, 150, 0.64f),
            };
        }

        private static List<Motion.Pose> BuildBookExitKeys(PoseContext context)
        {
            Vector3 storageLift = context.rightBookStorageTarget + Vector3.up * 0.07f;
            Vector3 lowerCenter = Vector3.Lerp(
                storageLift,
                context.rightBookTarget,
                0.52f);
            return new List<Motion.Pose>
            {
                At(context.bookPose, 0, 0.62f),
                CreateBookRetrievalPose(
                    context, 10, 0.84f, 0.10f, 0.86f, 0.88f,
                    context.rightBookTarget, 1f, 1f, 0.60f),
                CreateBookRetrievalPose(
                    context, 22, 0.50f, 0.34f, 0.48f, 0.52f,
                    lowerCenter, 1f, 1f, 0.58f),
                CreateBookRetrievalPose(
                    context, 34, 0.16f, 0.78f, 0.12f, 0.12f,
                    storageLift, 1f, 1f, 0.60f),
                CreateBookRetrievalPose(
                    context, 44, 0.03f, 1f, 0f, 0f,
                    context.rightBookStorageTarget, 1f, 0.74f, 0.64f),
                CreateBookRetrievalPose(
                    context, 54, 0f, 0.46f, 0f, 0f,
                    context.rightBookStorageTarget, 0.44f, 0.10f, 0.68f),
                At(context.basePose, 64, 0.52f),
            };
        }

        private static Motion.Pose CreateBookRetrievalPose(
            PoseContext context,
            int frame,
            float activityBlend,
            float storageFocus,
            float leftReach,
            float leftGrip,
            Vector3 rightTarget,
            float rightReach,
            float rightGrip,
            float ease)
        {
            AnimationLabMotionAuthoring.Apply(context.template, context.bones, context.basePose);

            activityBlend = Mathf.Clamp01(activityBlend);
            storageFocus = Mathf.Clamp01(storageFocus);
            context.spine1.rotation = Quaternion.AngleAxis(
                                          3f * activityBlend + 2.5f * storageFocus,
                                          context.bodyRight)
                                      * context.spine1.rotation;
            context.spine2.rotation = Quaternion.AngleAxis(
                                          4f * activityBlend + 3.5f * storageFocus,
                                          context.bodyRight)
                                      * context.spine2.rotation;
            context.spine3.rotation = Quaternion.AngleAxis(
                                          -6f * storageFocus,
                                          Vector3.up)
                                      * Quaternion.AngleAxis(
                                          2.5f * activityBlend,
                                          context.bodyRight)
                                      * context.spine3.rotation;
            context.neck.rotation = Quaternion.AngleAxis(
                                        8f * activityBlend + 3f * storageFocus,
                                        context.bodyRight)
                                    * context.neck.rotation;
            context.head.rotation = Quaternion.AngleAxis(
                                        12f * activityBlend + 6f * storageFocus,
                                        context.bodyRight)
                                    * Quaternion.AngleAxis(-8f * storageFocus, Vector3.up)
                                    * context.head.rotation;

            Quaternion storageRotation = Quaternion.AngleAxis(-32f, context.forward)
                                         * Quaternion.AngleAxis(15f, context.bodyRight)
                                         * context.baseRightHandRotation;
            Quaternion rightRotation = Quaternion.Slerp(
                storageRotation,
                context.bookRightHandRotation,
                activityBlend);
            PlaceTransitionHand(
                context,
                context.rightUpperArm,
                context.rightForearm,
                context.rightHand,
                rightTarget,
                context.bodyRight,
                context.baseRightHandRotation,
                rightRotation,
                rightReach);
            PlaceTransitionHand(
                context,
                context.leftUpperArm,
                context.leftForearm,
                context.leftHand,
                context.leftBookTarget,
                -context.bodyRight,
                context.baseLeftHandRotation,
                context.bookLeftHandRotation,
                leftReach);

            CurlBookFingers(context.leftGripFingers, leftGrip);
            CurlBookFingers(context.rightGripFingers, rightGrip);
            RotateLocal(context.leftThumb1, 2f * leftGrip, Vector3.right);
            RotateLocal(context.leftThumb2, 1f * leftGrip, Vector3.right);
            RotateLocal(context.rightThumb1, 3f * rightGrip, Vector3.right);
            RotateLocal(context.rightThumb2, 1f * rightGrip, Vector3.right);

            Motion.Pose pose = AnimationLabMotionAuthoring.Capture(
                context.template,
                context.rig,
                frame);
            pose.ease = ease;
            return pose;
        }

        private static void PlaceTransitionHand(
            PoseContext context,
            Transform upperArm,
            Transform forearm,
            Transform hand,
            Vector3 target,
            Vector3 outward,
            Quaternion baseRotation,
            Quaternion targetRotation,
            float reach)
        {
            reach = Mathf.Clamp01(reach);
            if (reach <= 0f)
            {
                return;
            }

            Vector3 handTarget = Vector3.Lerp(hand.position, target, reach);
            Vector3 elbowPole = upperArm.position
                                + outward * 0.38f
                                - Vector3.up * 0.24f
                                - context.forward * 0.04f;
            SolveTwoBone(
                upperArm,
                forearm,
                hand,
                handTarget,
                elbowPole,
                Quaternion.Slerp(baseRotation, targetRotation, reach));
        }

        private static Motion.Pose CreateBookPose(PoseContext context, BookPoseSpec spec)
        {
            AnimationLabMotionAuthoring.Apply(context.template, context.bones, context.basePose);

            float blend = Mathf.Clamp01(spec.poseBlend);
            context.spine1.rotation = Quaternion.AngleAxis(3f * blend, context.bodyRight)
                                      * context.spine1.rotation;
            context.spine2.rotation = Quaternion.AngleAxis(
                                          4f * blend + spec.breath,
                                          context.bodyRight)
                                      * context.spine2.rotation;
            context.spine3.rotation = Quaternion.AngleAxis(2.5f * blend, context.bodyRight)
                                      * context.spine3.rotation;
            context.neck.rotation = Quaternion.AngleAxis(8f * blend, context.bodyRight)
                                    * context.neck.rotation;
            context.head.rotation = Quaternion.AngleAxis(12f * blend, context.bodyRight)
                                    * context.head.rotation;
            context.head.rotation = Quaternion.AngleAxis(spec.gazeYaw, Vector3.up)
                                    * context.head.rotation;

            Vector3 bookOffset = context.bodyRight * spec.bookSide
                                 + Vector3.up * spec.bookLift;
            PlaceBookHand(
                context,
                context.leftUpperArm,
                context.leftForearm,
                context.leftHand,
                context.leftBookTarget + bookOffset,
                -context.bodyRight,
                context.baseLeftHandRotation,
                context.bookLeftHandRotation,
                blend);
            PlaceBookHand(
                context,
                context.rightUpperArm,
                context.rightForearm,
                context.rightHand,
                context.rightBookTarget + bookOffset + context.forward * (spec.pageTouch * 0.00035f),
                context.bodyRight,
                context.baseRightHandRotation,
                context.bookRightHandRotation,
                blend);

            CurlBookFingers(context.leftGripFingers, blend);
            CurlBookFingers(context.rightGripFingers, blend);
            RotateLocal(context.leftThumb1, 2f * blend, Vector3.right);
            RotateLocal(context.leftThumb2, 1f * blend, Vector3.right);
            RotateLocal(context.rightThumb1, 3f * blend + spec.pageTouch, Vector3.right);
            RotateLocal(context.rightThumb2, 1f * blend + spec.pageTouch * 0.35f, Vector3.right);

            Motion.Pose pose = AnimationLabMotionAuthoring.Capture(
                context.template,
                context.rig,
                spec.frame);
            pose.ease = spec.ease;
            return pose;
        }

        private static void PlaceBookHand(
            PoseContext context,
            Transform upperArm,
            Transform forearm,
            Transform hand,
            Vector3 bookTarget,
            Vector3 outward,
            Quaternion baseRotation,
            Quaternion bookRotation,
            float blend)
        {
            if (blend <= 0f)
            {
                return;
            }

            Vector3 target = Vector3.Lerp(hand.position, bookTarget, blend);
            Vector3 elbowPole = upperArm.position
                                + outward * 0.40f
                                - Vector3.up * 0.24f
                                - context.forward * 0.04f;
            float side = Mathf.Sign(Vector3.Dot(outward, context.bodyRight));
            Quaternion initialBookRotation = Quaternion.AngleAxis(
                                                 -side * 16f,
                                                 context.forward)
                                             * Quaternion.AngleAxis(10f, context.bodyRight)
                                             * baseRotation;
            Quaternion finalRotation = context.hasBookHandRotations
                ? bookRotation
                : initialBookRotation;
            Quaternion targetRotation = Quaternion.Slerp(baseRotation, finalRotation, blend);
            SolveTwoBone(upperArm, forearm, hand, target, elbowPole, targetRotation);
        }

        private static void BuildArmsCrossedBoundaryPose(PoseContext context)
        {
            ArmsCrossedPoseSpec fullPose = new ArmsCrossedPoseSpec(0, 1f, ease: 0.58f);
            CreateArmsCrossedPose(context, fullPose);
            float leftHandBeyondRightElbow = Vector3.Dot(
                context.leftHand.position - context.rightForearm.position,
                context.bodyRight);
            float rightHandBeyondLeftElbow = Vector3.Dot(
                context.leftForearm.position - context.rightHand.position,
                context.bodyRight);
            if (leftHandBeyondRightElbow > 0.01f || rightHandBeyondLeftElbow > 0.01f)
            {
                throw new InvalidOperationException(
                    $"Arms crossed hands exceed opposite elbows: " +
                    $"left {leftHandBeyondRightElbow:F4}m, right {rightHandBeyondLeftElbow:F4}m");
            }

            Debug.Log(
                $"[MotionAuthoring] Arms crossed elbow limits: " +
                $"left {leftHandBeyondRightElbow:F4}m, right {rightHandBeyondLeftElbow:F4}m");
            context.armsCrossedLeftHandRotation = context.leftHand.rotation;
            context.armsCrossedRightHandRotation = context.rightHand.rotation;
            context.hasArmsCrossedHandRotations = true;
            context.armsCrossedPose = CreateArmsCrossedPose(context, fullPose);
        }

        private static List<Motion.Pose> BuildArmsCrossedEnterKeys(PoseContext context)
        {
            return new List<Motion.Pose>
            {
                At(context.basePose, 0, 0.45f),
                CreateArmsCrossedPose(context, new ArmsCrossedPoseSpec(4, 0.17f, ease: 0.45f)),
                CreateArmsCrossedPose(context, new ArmsCrossedPoseSpec(8, 0.34f, ease: 0.45f)),
                CreateArmsCrossedPose(context, new ArmsCrossedPoseSpec(12, 0.50f, ease: 0.45f)),
                CreateArmsCrossedPose(context, new ArmsCrossedPoseSpec(16, 0.66f, ease: 0.45f)),
                CreateArmsCrossedPose(context, new ArmsCrossedPoseSpec(20, 0.83f, ease: 0.45f)),
                At(context.armsCrossedPose, ArmsCrossedTransitionFrames, 0.45f),
            };
        }

        private static List<Motion.Pose> BuildArmsCrossedLoopKeys(PoseContext context)
        {
            return new List<Motion.Pose>
            {
                At(context.armsCrossedPose, 0, 0.64f),
                CreateArmsCrossedPose(context, new ArmsCrossedPoseSpec(20, 1f, 0.25f, 0.20f, -0.3f, 0.64f)),
                CreateArmsCrossedPose(context, new ArmsCrossedPoseSpec(40, 1f, 0.55f, 0.45f, -0.7f, 0.62f)),
                CreateArmsCrossedPose(context, new ArmsCrossedPoseSpec(60, 1f, 0.20f, 0f, 0.2f, 0.60f)),
                CreateArmsCrossedPose(context, new ArmsCrossedPoseSpec(80, 1f, -0.35f, -0.35f, 0.7f, 0.60f)),
                CreateArmsCrossedPose(context, new ArmsCrossedPoseSpec(100, 1f, -0.15f, -0.20f, 0.3f, 0.62f)),
                At(context.armsCrossedPose, 120, 0.64f),
            };
        }

        private static List<Motion.Pose> BuildArmsCrossedExitKeys(PoseContext context)
        {
            return new List<Motion.Pose>
            {
                At(context.armsCrossedPose, 0, 0.45f),
                CreateArmsCrossedPose(context, new ArmsCrossedPoseSpec(4, 0.83f, ease: 0.45f)),
                CreateArmsCrossedPose(context, new ArmsCrossedPoseSpec(8, 0.66f, ease: 0.45f)),
                CreateArmsCrossedPose(context, new ArmsCrossedPoseSpec(12, 0.50f, ease: 0.45f)),
                CreateArmsCrossedPose(context, new ArmsCrossedPoseSpec(16, 0.34f, ease: 0.45f)),
                CreateArmsCrossedPose(context, new ArmsCrossedPoseSpec(20, 0.17f, ease: 0.45f)),
                At(context.basePose, ArmsCrossedTransitionFrames, 0.45f),
            };
        }

        private static Motion.Pose CreateArmsCrossedPose(
            PoseContext context,
            ArmsCrossedPoseSpec spec)
        {
            AnimationLabMotionAuthoring.Apply(context.template, context.bones, context.basePose);

            float blend = Mathf.Clamp01(spec.poseBlend);
            context.spine1.rotation = Quaternion.AngleAxis(-1.2f * blend, context.bodyRight)
                                      * context.spine1.rotation;
            context.spine2.rotation = Quaternion.AngleAxis(
                                          -1.5f * blend + spec.breath,
                                          context.bodyRight)
                                      * context.spine2.rotation;
            context.spine3.rotation = Quaternion.AngleAxis(spec.sway, context.forward)
                                      * context.spine3.rotation;
            context.neck.rotation = Quaternion.AngleAxis(spec.sway * -0.25f, context.forward)
                                    * context.neck.rotation;
            context.head.rotation = Quaternion.AngleAxis(spec.headYaw, Vector3.up)
                                    * Quaternion.AngleAxis(spec.sway * -0.35f, context.forward)
                                    * context.head.rotation;

            PlaceArmsCrossedHand(
                context,
                context.leftUpperArm,
                context.leftForearm,
                context.leftHand,
                context.leftArmsCrossedTarget,
                -context.bodyRight,
                context.baseLeftHandRotation,
                context.armsCrossedLeftHandRotation,
                blend);
            PlaceArmsCrossedHand(
                context,
                context.rightUpperArm,
                context.rightForearm,
                context.rightHand,
                context.rightArmsCrossedTarget,
                context.bodyRight,
                context.baseRightHandRotation,
                context.armsCrossedRightHandRotation,
                blend);

            CurlGripFingers(context.leftGripFingers, blend, 0.82f);
            CurlGripFingers(context.rightGripFingers, blend, 0.82f);
            RotateLocal(context.leftThumb1, 12f * blend, Vector3.right);
            RotateLocal(context.leftThumb2, 7f * blend, Vector3.right);
            RotateLocal(context.rightThumb1, 12f * blend, Vector3.right);
            RotateLocal(context.rightThumb2, 7f * blend, Vector3.right);

            Motion.Pose pose = AnimationLabMotionAuthoring.Capture(
                context.template,
                context.rig,
                spec.frame);
            pose.ease = spec.ease;
            return pose;
        }

        private static void PlaceArmsCrossedHand(
            PoseContext context,
            Transform upperArm,
            Transform forearm,
            Transform hand,
            Vector3 crossedTarget,
            Vector3 outward,
            Quaternion baseRotation,
            Quaternion crossedRotation,
            float blend)
        {
            if (blend <= 0f)
            {
                return;
            }

            Vector3 target = Vector3.Lerp(hand.position, crossedTarget, blend);
            Vector3 elbowPole = upperArm.position
                                + outward * 0.40f
                                - Vector3.up * 0.06f
                                + context.forward * 0.035f;
            float side = Mathf.Sign(Vector3.Dot(outward, context.bodyRight));
            Quaternion initialRotation = Quaternion.AngleAxis(
                                             -side * 75f,
                                             Vector3.up)
                                         * Quaternion.AngleAxis(
                                             -side * 38f,
                                             context.forward)
                                         * Quaternion.AngleAxis(6f, context.bodyRight)
                                         * baseRotation;
            Quaternion finalRotation = context.hasArmsCrossedHandRotations
                ? crossedRotation
                : initialRotation;
            SolveTwoBone(
                upperArm,
                forearm,
                hand,
                target,
                elbowPole,
                Quaternion.Slerp(baseRotation, finalRotation, blend));
        }

        private static void BuildLegCrossedBoundaryPose(PoseContext context)
        {
            LegCrossedPoseSpec fullPose = new LegCrossedPoseSpec(0, 1f, ease: 0.58f);
            CreateLegCrossedPose(context, fullPose);
            context.legCrossedRightFootRotation = context.rightFoot.rotation;
            context.legCrossedLeftHandRotation = context.leftHand.rotation;
            context.legCrossedRightHandRotation = context.rightHand.rotation;
            context.hasLegCrossedRotations = true;
            context.legCrossedPose = CreateLegCrossedPose(context, fullPose);
        }

        private static List<Motion.Pose> BuildLegCrossedEnterKeys(PoseContext context)
        {
            return new List<Motion.Pose>
            {
                At(context.basePose, 0, 0.45f),
                CreateLegCrossedPose(context, new LegCrossedPoseSpec(5, 0.17f, ease: 0.45f)),
                CreateLegCrossedPose(context, new LegCrossedPoseSpec(10, 0.34f, ease: 0.45f)),
                CreateLegCrossedPose(context, new LegCrossedPoseSpec(15, 0.50f, ease: 0.45f)),
                CreateLegCrossedPose(context, new LegCrossedPoseSpec(20, 0.66f, ease: 0.45f)),
                CreateLegCrossedPose(context, new LegCrossedPoseSpec(25, 0.83f, ease: 0.45f)),
                At(context.legCrossedPose, LegCrossedTransitionFrames, 0.45f),
            };
        }

        private static List<Motion.Pose> BuildLegCrossedLoopKeys(PoseContext context)
        {
            return new List<Motion.Pose>
            {
                At(context.legCrossedPose, 0, 0.64f),
                CreateLegCrossedPose(context, new LegCrossedPoseSpec(20, 1f, 0.20f, 0.18f, 0.002f, 1.5f, 0.64f)),
                CreateLegCrossedPose(context, new LegCrossedPoseSpec(40, 1f, 0.45f, 0.35f, 0.005f, 3f, 0.62f)),
                CreateLegCrossedPose(context, new LegCrossedPoseSpec(60, 1f, 0.10f, 0f, 0.002f, 1f, 0.60f)),
                CreateLegCrossedPose(context, new LegCrossedPoseSpec(80, 1f, -0.30f, -0.30f, -0.002f, -2f, 0.60f)),
                CreateLegCrossedPose(context, new LegCrossedPoseSpec(100, 1f, -0.10f, -0.16f, -0.001f, -1f, 0.62f)),
                At(context.legCrossedPose, 120, 0.64f),
            };
        }

        private static List<Motion.Pose> BuildLegCrossedExitKeys(PoseContext context)
        {
            return new List<Motion.Pose>
            {
                At(context.legCrossedPose, 0, 0.45f),
                CreateLegCrossedPose(context, new LegCrossedPoseSpec(5, 0.83f, ease: 0.45f)),
                CreateLegCrossedPose(context, new LegCrossedPoseSpec(10, 0.66f, ease: 0.45f)),
                CreateLegCrossedPose(context, new LegCrossedPoseSpec(15, 0.50f, ease: 0.45f)),
                CreateLegCrossedPose(context, new LegCrossedPoseSpec(20, 0.34f, ease: 0.45f)),
                CreateLegCrossedPose(context, new LegCrossedPoseSpec(25, 0.17f, ease: 0.45f)),
                At(context.basePose, LegCrossedTransitionFrames, 0.45f),
            };
        }

        private static Motion.Pose CreateLegCrossedPose(
            PoseContext context,
            LegCrossedPoseSpec spec)
        {
            AnimationLabMotionAuthoring.Apply(context.template, context.bones, context.basePose);

            float blend = Mathf.Clamp01(spec.poseBlend);
            context.spine1.rotation = Quaternion.AngleAxis(-0.8f * blend, context.bodyRight)
                                      * context.spine1.rotation;
            context.spine2.rotation = Quaternion.AngleAxis(
                                          -1.1f * blend + spec.breath,
                                          context.bodyRight)
                                      * context.spine2.rotation;
            context.spine3.rotation = Quaternion.AngleAxis(spec.sway, context.forward)
                                      * context.spine3.rotation;
            context.head.rotation = Quaternion.AngleAxis(spec.sway * -0.3f, context.forward)
                                    * context.head.rotation;

            float crossingArc = Mathf.Sin(blend * Mathf.PI);
            Vector3 crossedFootTarget = context.rightCrossedFootTarget
                                        + Vector3.up * spec.footLift;
            Vector3 footTarget = Vector3.Lerp(
                                     context.rightFootTarget,
                                     crossedFootTarget,
                                     blend)
                                 + Vector3.up * (0.075f * crossingArc)
                                 + context.bodyRight * (0.055f * crossingArc);
            Vector3 kneePole = context.rightThigh.position
                               + context.bodyRight * 0.62f
                               + context.forward * 0.28f
                               - Vector3.up * 0.18f;
            Quaternion initialFootRotation = Quaternion.AngleAxis(-24f, Vector3.up)
                                             * Quaternion.AngleAxis(12f, context.forward)
                                             * context.baseRightFootRotation;
            Quaternion finalFootRotation = context.hasLegCrossedRotations
                ? context.legCrossedRightFootRotation
                : initialFootRotation;
            Quaternion footRotation = Quaternion.AngleAxis(
                                          spec.footRoll,
                                          context.bodyRight)
                                      * Quaternion.Slerp(
                                          context.baseRightFootRotation,
                                          finalFootRotation,
                                          blend);
            SolveTwoBone(
                context.rightThigh,
                context.rightShin,
                context.rightFoot,
                footTarget,
                kneePole,
                footRotation);

            Vector3 leftHandTarget = context.leftLegCrossedHandTarget;
            Vector3 rightHandTarget = context.rightLegCrossedHandTarget;
            Quaternion leftHandRotation = context.hasLegCrossedRotations
                ? context.legCrossedLeftHandRotation
                : Quaternion.AngleAxis(72f, context.bodyRight)
                  * context.baseLeftHandRotation;
            Quaternion rightHandRotation = context.hasLegCrossedRotations
                ? context.legCrossedRightHandRotation
                : Quaternion.AngleAxis(72f, context.bodyRight)
                  * context.baseRightHandRotation;
            PlaceTransitionHand(
                context,
                context.leftUpperArm,
                context.leftForearm,
                context.leftHand,
                leftHandTarget,
                -context.bodyRight,
                context.baseLeftHandRotation,
                leftHandRotation,
                blend);
            PlaceTransitionHand(
                context,
                context.rightUpperArm,
                context.rightForearm,
                context.rightHand,
                rightHandTarget,
                context.bodyRight,
                context.baseRightHandRotation,
                rightHandRotation,
                blend);

            CurlGripFingers(context.leftGripFingers, blend, 0.20f);
            CurlGripFingers(context.rightGripFingers, blend, 0.20f);

            Motion.Pose pose = AnimationLabMotionAuthoring.Capture(
                context.template,
                context.rig,
                spec.frame);
            pose.ease = spec.ease;
            return pose;
        }

        private static Motion SaveMotion(
            Motion template,
            string clipName,
            string id,
            int lastFrame,
            List<Motion.Pose> keys,
            bool loop,
            PoseContext validationContext,
            string activityLabel = "Doze",
            bool phoneContacts = false,
            bool bookContacts = false,
            bool armsCrossedContacts = false,
            bool legCrossedContacts = false,
            bool rightFootFree = false)
        {
            Motion draft = UnityEngine.Object.Instantiate(template);
            draft.hideFlags = HideFlags.HideAndDontSave;
            draft.id = id;
            draft.author = Author;
            draft.clipName = clipName;
            draft.sampleRate = 30;
            draft.lastFrame = lastFrame;
            draft.loop = loop;
            draft.generatedClip = null;
            draft.generatedFbx = null;
            draft.keys.Clear();
            draft.drafts.Clear();
            foreach (Motion.Pose key in keys)
            {
                draft.StoreKey(key);
            }

            ValidateMotionContacts(
                draft,
                validationContext,
                loop,
                phoneContacts,
                bookContacts,
                armsCrossedContacts,
                legCrossedContacts,
                rightFootFree);

            string path = SourcePath(clipName);
            Motion source = AssetDatabase.LoadAssetAtPath<Motion>(path);
            if (source != null && source.id != id)
            {
                UnityEngine.Object.DestroyImmediate(draft);
                throw new InvalidOperationException(
                    "다른 제작 원본이 Seated Doze 출력 경로를 사용 중입니다: " + path);
            }

            string baseline = source != null
                ? AnimationLabMotionAuthoring.Revision(source)
                : string.Empty;

            try
            {
                AnimationLabMotionAuthoring.SaveBundle(draft, ref source, baseline);
                ConfigureLoopSettings(source, loop);
                AssetDatabase.SetLabels(source, new[]
                {
                    "AnimationLab",
                    "Seated",
                    activityLabel,
                    loop ? "Loop" : "Transition",
                });
                return source;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        private static void ConfigureLoopSettings(Motion source, bool loop)
        {
            AnimationClip clip = source.generatedClip;
            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlend = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);

            string fbxPath = AssetDatabase.GetAssetPath(source.generatedFbx);
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation importedClip in clips)
            {
                importedClip.loopTime = loop;
                importedClip.loopPose = loop;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static void ValidateBoundaries(
            Motion.Pose basePose,
            Motion.Pose activityPose,
            IReadOnlyList<Motion.Pose> enter,
            IReadOnlyList<Motion.Pose> loop,
            IReadOnlyList<Motion.Pose> exit)
        {
            RequireSamePose(basePose, enter[0], "base -> enter");
            RequireSamePose(activityPose, enter[enter.Count - 1], "enter -> activity");
            RequireSamePose(activityPose, loop[0], "activity -> loop start");
            RequireSamePose(activityPose, loop[loop.Count - 1], "loop seam");
            RequireSamePose(activityPose, exit[0], "loop -> exit");
            RequireSamePose(basePose, exit[exit.Count - 1], "exit -> base");
        }

        private static void ValidateMotionContacts(
            Motion motion,
            PoseContext context,
            bool loop,
            bool phoneContacts,
            bool bookContacts,
            bool armsCrossedContacts,
            bool legCrossedContacts,
            bool rightFootFree)
        {
            float rootDrift = 0f;
            float footDrift = 0f;
            float handDrift = 0f;
            Vector3 baseRoot = motion.keys[0].root;

            for (int frame = 0; frame <= motion.lastFrame; frame++)
            {
                Motion.Pose pose = motion.Evaluate(frame, false);
                AnimationLabMotionAuthoring.Apply(motion, context.bones, pose);
                rootDrift = Mathf.Max(rootDrift, Vector3.Distance(baseRoot, pose.root));
                footDrift = rightFootFree
                    ? Mathf.Max(
                        footDrift,
                        Vector3.Distance(context.leftFoot.position, context.leftFootTarget))
                    : Mathf.Max(
                        footDrift,
                        Vector3.Distance(context.leftFoot.position, context.leftFootTarget),
                        Vector3.Distance(context.rightFoot.position, context.rightFootTarget));
                if (loop)
                {
                    Vector3 leftTarget = armsCrossedContacts
                        ? context.leftArmsCrossedTarget
                        : legCrossedContacts
                            ? context.leftLegCrossedHandTarget
                            : bookContacts
                                ? context.leftBookTarget
                                : phoneContacts
                                    ? context.leftPhoneTarget
                                    : context.leftLapTarget;
                    Vector3 rightTarget = armsCrossedContacts
                        ? context.rightArmsCrossedTarget
                        : legCrossedContacts
                            ? context.rightLegCrossedHandTarget
                            : bookContacts
                                ? context.rightBookTarget
                                : phoneContacts
                                    ? context.rightPhoneTarget
                                    : context.rightLapTarget;
                    handDrift = Mathf.Max(
                        handDrift,
                        Vector3.Distance(context.leftHand.position, leftTarget),
                        Vector3.Distance(context.rightHand.position, rightTarget));
                }
            }

            float handLimit = phoneContacts || bookContacts || armsCrossedContacts || legCrossedContacts
                ? 0.035f
                : 0.018f;
            if (rootDrift > 0.0001f || footDrift > 0.006f || loop && handDrift > handLimit)
            {
                throw new InvalidOperationException(
                    $"Seated activity contact validation failed: root {rootDrift:F4}m, " +
                    $"feet {footDrift:F4}m, hands {handDrift:F4}m");
            }

            Debug.Log(
                $"[MotionAuthoring] {motion.clipName} contacts: root {rootDrift:F4}m, " +
                $"feet {footDrift:F4}m, hands {handDrift:F4}m");
        }

        private static void RequireSamePose(Motion.Pose a, Motion.Pose b, string boundary)
        {
            float position = Mathf.Max(
                Vector3.Distance(a.root, b.root),
                Vector3.Distance(a.hips, b.hips));
            float angle = 0f;
            for (int index = 0; index < a.rotations.Length; index++)
            {
                angle = Mathf.Max(angle, Quaternion.Angle(a.rotations[index], b.rotations[index]));
            }

            if (position > 0.000001f || angle > 0.001f)
            {
                throw new InvalidOperationException(
                    $"Seated Doze boundary mismatch ({boundary}): {position:F6}m / {angle:F4}deg");
            }
        }

        private static void BuildPreviewScene(
            Motion enter,
            Motion loop,
            Motion exit,
            string scenePath = ScenePath,
            string rigName = "SeatedDoze_PreviewRig",
            bool phoneView = false,
            bool bookView = false,
            string previewLabel = "Seated Doze")
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject stageOffset = new GameObject("PreviewStageOffset");
            GameObject rig = AnimationLabMotionAuthoring.CreateRig(enter.sourceRig);
            rig.name = rigName;
            ClearHideFlags(rig);
            AnimationLabMotionAuthoring.StripAnimators(rig);
            rig.transform.SetParent(stageOffset.transform, false);

            Transform[] bones = AnimationLabMotionAuthoring.Bind(enter, rig);
            AnimationLabMotionAuthoring.Apply(enter, bones, enter.Evaluate(0, false));
            float lowest = LowestVertex(rig);
            if (float.IsFinite(lowest))
            {
                stageOffset.transform.position = Vector3.up * -lowest;
            }

            Animation player = rig.AddComponent<Animation>();
            player.playAutomatically = false;
            player.AddClip(enter.generatedClip, enter.generatedClip.name);
            player.AddClip(loop.generatedClip, loop.generatedClip.name);
            player.AddClip(exit.generatedClip, exit.generatedClip.name);
            player.clip = enter.generatedClip;
            AnimationLabSequencePreview preview = rig.AddComponent<AnimationLabSequencePreview>();
            preview.Configure(
                enter.generatedClip,
                loop.generatedClip,
                exit.generatedClip,
                5f,
                previewLabel);

            Transform hips = Bone(rig, "DEF-hips");
            Transform leftThigh = Bone(rig, "DEF-thigh.L");
            Transform rightThigh = Bone(rig, "DEF-thigh.R");
            Transform leftFoot = Bone(rig, "DEF-foot.L");
            Transform rightFoot = Bone(rig, "DEF-foot.R");
            Transform leftToe = Bone(rig, "DEF-toe.L");
            Transform rightToe = Bone(rig, "DEF-toe.R");
            GetBodyAxes(
                leftThigh,
                rightThigh,
                leftFoot,
                rightFoot,
                leftToe,
                rightToe,
                rig.transform,
                out Vector3 forward,
                out Vector3 bodyRight);

            Material floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                AnimationLabMotionAuthoring.Root + "/Materials/Floor.mat");
            Quaternion seatRotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 cushionCenter = new Vector3(hips.position.x, hips.position.y - 0.09f, hips.position.z)
                                    - forward * 0.015f;
            StageBox(
                "PreviewFloor",
                new Vector3(0f, -0.035f, 0f),
                new Vector3(4f, 0.07f, 4f),
                Quaternion.identity,
                floorMaterial);
            StageBox(
                "SubwaySeat_Cushion",
                cushionCenter,
                new Vector3(0.70f, 0.08f, 0.52f),
                seatRotation,
                floorMaterial);
            StageBox(
                "SubwaySeat_Back",
                cushionCenter - forward * 0.23f + Vector3.up * 0.35f,
                new Vector3(0.70f, 0.70f, 0.08f),
                seatRotation,
                floorMaterial);

            GameObject cameraObject = new GameObject("Preview Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.085f, 1f);
            camera.fieldOfView = 32f;
            Vector3 lookAt = hips.position + Vector3.up * 0.24f + forward * 0.04f;
            camera.transform.position = phoneView || bookView
                ? lookAt + bodyRight * 2.30f + forward * 2.35f + Vector3.up * 0.18f
                : lookAt + bodyRight * 3.25f + forward * 1.10f + Vector3.up * 0.18f;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - camera.transform.position, Vector3.up);

            GameObject lightObject = new GameObject("Preview Key Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.color = new Color(1f, 0.95f, 0.88f);
            lightObject.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            GameObject fillObject = new GameObject("Preview Fill Light");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.45f;
            fill.color = new Color(0.55f, 0.68f, 1f);
            fillObject.transform.rotation = Quaternion.Euler(25f, 145f, 0f);

            EditorUtility.SetDirty(preview);
            EditorSceneManager.MarkSceneDirty(scene);
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void RenderReviewSheet(
            Motion enter,
            Motion loop,
            Motion exit,
            string scenePath = ScenePath,
            string rigName = "SeatedDoze_PreviewRig",
            string previewPath = PreviewPath,
            string activityName = "Seated Doze")
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject rig = GameObject.Find(rigName);
            Camera camera = GameObject.Find("Preview Camera").GetComponent<Camera>();
            if (rig == null || camera == null)
            {
                throw new InvalidOperationException(activityName + " preview scene is incomplete.");
            }

            if (activityName == "Seated Phone")
            {
                enter.generatedClip.SampleAnimation(rig, enter.Duration);
                Transform leftHand = Bone(rig, "DEF-hand.L");
                Transform rightHand = Bone(rig, "DEF-hand.R");
                Vector3 focus = (leftHand.position + rightHand.position) * 0.5f
                                + Vector3.up * 0.08f;
                Vector3 viewDirection = (camera.transform.position - focus).normalized;
                camera.transform.position = focus + viewDirection * 1.05f + Vector3.up * 0.025f;
                camera.transform.rotation = Quaternion.LookRotation(
                    focus - camera.transform.position,
                    Vector3.up);
                camera.fieldOfView = 30f;
            }

            else if (activityName == "Seated Book")
            {
                enter.generatedClip.SampleAnimation(rig, enter.Duration);
                Transform leftHand = Bone(rig, "DEF-hand.L");
                Transform rightHand = Bone(rig, "DEF-hand.R");
                Vector3 focus = (leftHand.position + rightHand.position) * 0.5f
                                + Vector3.up * 0.18f;
                Vector3 horizontalView = Vector3.ProjectOnPlane(
                    camera.transform.position - focus,
                    Vector3.up).normalized;
                camera.transform.position = focus
                                            + Vector3.up * 1.45f
                                            + horizontalView * 0.75f;
                camera.transform.rotation = Quaternion.LookRotation(
                    focus - camera.transform.position,
                    Vector3.up);
                camera.fieldOfView = 31f;
            }

            ReviewSample[] samples =
            {
                new ReviewSample(enter, 0),
                new ReviewSample(enter, enter.lastFrame / 2),
                new ReviewSample(enter, enter.lastFrame),
                new ReviewSample(loop, 0),
                new ReviewSample(loop, loop.lastFrame / 2),
                new ReviewSample(loop, loop.lastFrame * 3 / 4),
                new ReviewSample(exit, 0),
                new ReviewSample(exit, exit.lastFrame / 3),
                new ReviewSample(exit, exit.lastFrame),
            };

            const int cellWidth = 420;
            const int cellHeight = 420;
            const int columns = 3;
            const int rows = 3;
            RenderTexture renderTexture = new RenderTexture(
                cellWidth,
                cellHeight,
                24,
                RenderTextureFormat.ARGB32);
            Texture2D frameTexture = new Texture2D(
                cellWidth,
                cellHeight,
                TextureFormat.RGB24,
                false);
            Texture2D contactSheet = new Texture2D(
                cellWidth * columns,
                cellHeight * rows,
                TextureFormat.RGB24,
                false);

            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture previous = RenderTexture.active;
                Transform[] reviewBones = AnimationLabMotionAuthoring.Bind(enter, rig);
                for (int index = 0; index < samples.Length; index++)
                {
                    ReviewSample sample = samples[index];
                    AnimationLabMotionAuthoring.Apply(
                        sample.motion,
                        reviewBones,
                        sample.motion.Evaluate(sample.frame, false));
                    camera.Render();
                    RenderTexture.active = renderTexture;
                    frameTexture.ReadPixels(
                        new Rect(0f, 0f, cellWidth, cellHeight),
                        0,
                        0,
                        false);
                    frameTexture.Apply(false, false);
                    int column = index % columns;
                    int row = rows - 1 - index / columns;
                    contactSheet.SetPixels(
                        column * cellWidth,
                        row * cellHeight,
                        cellWidth,
                        cellHeight,
                        frameTexture.GetPixels());
                }

                RenderTexture.active = previous;
                camera.targetTexture = null;
                contactSheet.Apply(false, false);
                string outputPath = Path.GetFullPath(previewPath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, contactSheet.EncodeToPNG());
                Debug.Log("[MotionAuthoring] " + activityName + " review sheet: " + outputPath);
            }
            finally
            {
                camera.targetTexture = null;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(frameTexture);
                UnityEngine.Object.DestroyImmediate(contactSheet);
            }
        }

        private static void SolveTwoBone(
            Transform upper,
            Transform middle,
            Transform tip,
            Vector3 requestedTarget,
            Vector3 pole,
            Quaternion tipRotation)
        {
            Vector3 origin = upper.position;
            float upperLength = Vector3.Distance(origin, middle.position);
            float lowerLength = Vector3.Distance(middle.position, tip.position);
            Vector3 toTarget = requestedTarget - origin;
            float requestedDistance = Mathf.Max(0.0001f, toTarget.magnitude);
            Vector3 axis = toTarget / requestedDistance;
            float distance = Mathf.Clamp(
                requestedDistance,
                Mathf.Abs(upperLength - lowerLength) + 0.0001f,
                upperLength + lowerLength - 0.0001f);
            Vector3 target = origin + axis * distance;
            Vector3 bendDirection = Vector3.ProjectOnPlane(pole - origin, axis);
            if (bendDirection.sqrMagnitude < 0.000001f)
            {
                bendDirection = Vector3.ProjectOnPlane(Vector3.up, axis);
            }

            if (bendDirection.sqrMagnitude < 0.000001f)
            {
                bendDirection = Vector3.ProjectOnPlane(Vector3.forward, axis);
            }

            bendDirection.Normalize();
            float along =
                (upperLength * upperLength - lowerLength * lowerLength + distance * distance) /
                (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 middleTarget = origin + axis * along + bendDirection * height;

            upper.rotation =
                Quaternion.FromToRotation(middle.position - origin, middleTarget - origin)
                * upper.rotation;
            middle.rotation =
                Quaternion.FromToRotation(tip.position - middle.position, target - middle.position)
                * middle.rotation;
            tip.rotation = tipRotation;
        }

        private static Motion.Pose At(Motion.Pose source, int frame, float ease)
        {
            Motion.Pose copy = source.Copy(frame);
            copy.ease = ease;
            return copy;
        }

        private static AnimationClip FindSharedClip(string name)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(AnimationLabMotionAuthoring.SharedMotion)
                .OfType<AnimationClip>()
                .FirstOrDefault(value =>
                    value.name == name || value.name.EndsWith("|" + name, StringComparison.Ordinal));
            if (clip == null)
            {
                throw new InvalidOperationException("공유 애니메이션 클립이 없습니다: " + name);
            }

            return clip;
        }

        private static string SourcePath(string clipName)
        {
            return AnimationLabMotionAuthoring.Authored + "/" + Author + "_" + clipName + ".asset";
        }

        private static void GetBodyAxes(PoseContext context, out Vector3 forward, out Vector3 bodyRight)
        {
            GetBodyAxes(
                context.leftThigh,
                context.rightThigh,
                context.leftFoot,
                context.rightFoot,
                context.leftToe,
                context.rightToe,
                context.rig.transform,
                out forward,
                out bodyRight);
        }

        private static void GetBodyAxes(
            Transform leftThigh,
            Transform rightThigh,
            Transform leftFoot,
            Transform rightFoot,
            Transform leftToe,
            Transform rightToe,
            Transform fallback,
            out Vector3 forward,
            out Vector3 bodyRight)
        {
            bodyRight = Vector3.ProjectOnPlane(
                rightThigh.position - leftThigh.position,
                Vector3.up).normalized;
            if (bodyRight.sqrMagnitude < 0.5f)
            {
                bodyRight = Vector3.ProjectOnPlane(fallback.right, Vector3.up).normalized;
            }

            forward = Vector3.Cross(bodyRight, Vector3.up).normalized;
            Vector3 toeDirection = Vector3.ProjectOnPlane(
                (leftToe.position + rightToe.position - leftFoot.position - rightFoot.position) * 0.5f,
                Vector3.up);
            if (Vector3.Dot(forward, toeDirection) < 0f)
            {
                forward = -forward;
            }
        }

        private static float LowestVertex(GameObject rig)
        {
            float lowest = float.PositiveInfinity;
            Mesh mesh = new Mesh();
            try
            {
                foreach (SkinnedMeshRenderer skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.BakeMesh(mesh);
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        lowest = Mathf.Min(lowest, skin.transform.TransformPoint(vertex).y);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }

            return lowest;
        }

        private static GameObject StageBox(
            string name,
            Vector3 position,
            Vector3 scale,
            Quaternion rotation,
            Material material)
        {
            GameObject value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.SetPositionAndRotation(position, rotation);
            value.transform.localScale = scale;
            if (material != null)
            {
                value.GetComponent<Renderer>().sharedMaterial = material;
            }

            return value;
        }

        private static void ClearHideFlags(GameObject value)
        {
            foreach (Transform child in value.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags = HideFlags.None;
                child.hideFlags = HideFlags.None;
            }

            foreach (Component component in value.GetComponentsInChildren<Component>(true))
            {
                if (component != null)
                {
                    component.hideFlags = HideFlags.None;
                }
            }
        }

        private static Transform Bone(GameObject rig, string name)
        {
            Transform[] matches = rig.GetComponentsInChildren<Transform>(true)
                .Where(value => value.name == name)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one bone named {name}, found {matches.Length}.");
            }

            return matches[0];
        }

        private static Transform[] GripFingers(GameObject rig, string side)
        {
            string[] names =
            {
                "f_index.01", "f_index.02", "f_index.03",
                "f_middle.01", "f_middle.02", "f_middle.03",
                "f_ring.01", "f_ring.02", "f_ring.03",
                "f_pinky.01", "f_pinky.02", "f_pinky.03",
            };
            return names
                .Select(name => Bone(rig, "DEF-" + name + "." + side))
                .ToArray();
        }
    }
}
#endif
