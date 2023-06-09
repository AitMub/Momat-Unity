using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Momat.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Serialization;
using AffineTransform = Unity.Mathematics.AffineTransform;

namespace Momat.Editor
{
    [CreateAssetMenu(menuName = "Momat/Animation PreProcess Asset")]
    internal class AnimationPreProcessAsset : ScriptableObject
    {
        public Avatar avatar;

        public List<ProcessingAnimationClip> animSet;
        
        public AnimationFeatureDefinition featureDefinition;

        public List<string> tags;

        public float sampleRate = 30f;
        
        public void BuildRuntimeData()
        {
            int totalFrame = 0;
            var targetRig = AnimationRig.Create(avatar);
            var animatedJointIndices = new HashSet<int>();
            var animationFrameOffset = new List<int>();
            var animationFrameNum = new List<int>();
            
            animSet.Sort((clip1, clip2) => (int)clip1.animationType - (int)clip2.animationType);
            
            var runtimeData = CreateInstance<RuntimeAnimationData>();

            for (int i = 0; i < animSet.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Building...", $"{animSet[i].Name}", i / (float)animSet.Count);
                
                var jointTransforms = GenerateClipRuntimeJointTransform
                    (animSet[i], targetRig, ref animatedJointIndices);
                var featureVectors = GenerateClipFeatureVector
                    (animSet[i], jointTransforms, targetRig);

                var frameNum = jointTransforms.Length / targetRig.NumJoints;
                animationFrameOffset.Add(totalFrame);
                animationFrameNum.Add(frameNum);
                totalFrame += frameNum;
                
                runtimeData.transforms.AddRange(jointTransforms);
                runtimeData.trajectoryPoints.AddRange(featureVectors.trajectories);
                runtimeData.comparedJointRootSpaceT.AddRange(featureVectors.jointRootSpaceT);
                
                jointTransforms.Dispose();
                featureVectors.Dispose();
            }
            
            runtimeData.transforms = RemoveUnanimatedTransform
                (runtimeData.transforms, animatedJointIndices, targetRig);
            runtimeData.animationFrameOffset = animationFrameOffset.ToArray();
            runtimeData.animationFrameNum = animationFrameNum.ToArray();
            runtimeData.animationTypeOffset = GenerateAnimationTypeOffset();
            runtimeData.animationTypeNum = GenerateAnimationTypeNum(runtimeData.animationTypeOffset);

            runtimeData.eventClipDatas = GenerateEventClipData(runtimeData.animationTypeOffset);

            runtimeData.animatedJointIndices = animatedJointIndices.ToArray();
            runtimeData.jointIndexOfTransformsGroup =
                GenerateJointIndexOfTransformsGroup(runtimeData.animatedJointIndices, targetRig);
            
            runtimeData.frameRate = sampleRate;
            runtimeData.rig = targetRig.GenerateRuntimeRig();
            runtimeData.trajectoryFeatureDefinition = featureDefinition.trajectoryFeatureDefinition;
            runtimeData.poseFeatureDefinition = featureDefinition.poseFeatureDefinition;
            
            SaveRuntimeDataToAsset(runtimeData);
            
            EditorUtility.ClearProgressBar();
        }

        private NativeArray<AffineTransform> GenerateClipRuntimeJointTransform
            (ProcessingAnimationClip animClip, AnimationRig targetRig, ref HashSet<int> animatedJointIndex)
        {
            var clip = animClip.sourceAnimClip;
            var avatarRetargetMap = animClip.avatarRetargetMap;
            
            int numJoints = targetRig.NumJoints;
            int numFrames = (int)math.ceil(clip.frameRate * clip.length);
            int numTransforms = numFrames * numJoints;

            var outJointTransforms = new NativeArray<AffineTransform>(numTransforms, Allocator.Persistent);

            if (avatarRetargetMap != null)
            {
                var sourceRig = AnimationRig.Create(avatarRetargetMap.sourceAvatar);

                using (var animSampler = new AnimationSampler(clip, sourceRig))
                {
                    animSampler.RetargetAnimation(targetRig, avatarRetargetMap);
                    animSampler.CollectAnimatedJointIndex(ref animatedJointIndex);
                    
                    float sourceSampleRate = clip.frameRate;
                    float targetSampleRate = sampleRate;
                    float sampleRateRatio = sourceSampleRate / targetSampleRate;
                    int numFrameResampledClip = (int)math.ceil(targetSampleRate * clip.length);

                    var sampleRange = new AnimationCurveBake.SampleRange()
                    {
                        startFrameIndex = 0,
                        numFrames = numFrames
                    };

                    using (var rangeSampler = animSampler.PrepareRangeSampler
                           (targetSampleRate, sampleRange, 0, outJointTransforms))
                    {
                        rangeSampler.Schedule();
                        rangeSampler.Complete();
                    }
                }
            }
            
            return outJointTransforms;
        }

        private ClipFeatureVectors GenerateClipFeatureVector
            (ProcessingAnimationClip animClip, NativeArray<AffineTransform> jointTransforms, AnimationRig targetRig)
        {
            var clip = animClip.sourceAnimClip;
            int numFrames = (int)math.ceil(clip.frameRate * clip.length);

            var outFeatureVectors = new ClipFeatureVectors
            {
                trajectories = new NativeArray<AffineTransform>
                (numFrames * featureDefinition.trajectoryFeatureDefinition.trajectoryTimeStamps.Count,
                    Allocator.Persistent),
                jointRootSpaceT = new NativeArray<AffineTransform>
                    (numFrames * featureDefinition.poseFeatureDefinition.comparedJoint.Count,
                        Allocator.Persistent)
            };

            var jointIndices = new int[featureDefinition.poseFeatureDefinition.comparedJoint.Count];
            for (int i = 0; i < jointIndices.Length; i++)
            {
                var index = targetRig.GetJointIndexFromName
                    (featureDefinition.poseFeatureDefinition.comparedJoint[i]);
                jointIndices[i] = index;
            }

            using (var generateFeatureVectorJob = new GenerateFeatureVectorJob
                   {
                       localPoses = jointTransforms,
                       numJoints = targetRig.NumJoints,
                       frameRate = sampleRate,
                       trajectoryTimeStamps = new NativeArray<float>
                           (featureDefinition.trajectoryFeatureDefinition.trajectoryTimeStamps.ToArray(), Allocator.Persistent),
                       jointIndices = new NativeArray<int>
                           (jointIndices, Allocator.Persistent),
                       parentIndices = targetRig.GenerateParentIndicesNA(),
                       featureVectors = outFeatureVectors
                   })
            {
                var handle = generateFeatureVectorJob.Schedule(numFrames, 1);
                handle.Complete();
            }

            return outFeatureVectors;
        }

        private EventClipData[] GenerateEventClipData(int[] animationTypeOffset)
        {
            var clipDatas = new List<EventClipData>();
            for (int i = animationTypeOffset[(int)EAnimationType.EEvent];
                 i < animationTypeOffset[(int)EAnimationType.EEvent + 1];
                 i++)
            {
                clipDatas.Add(animSet[i].clipData.eventClipData);
            }

            return clipDatas.ToArray();
        }

        private List<AffineTransform> RemoveUnanimatedTransform(List<AffineTransform> transforms, in HashSet<int> animatedJointIndices, AnimationRig rig)
        {
            if (animatedJointIndices.Count == rig.NumJoints)
            {
                return transforms;
            }

            int frameCnt = transforms.Count / rig.NumJoints;
            
            var cutTransform = new List<AffineTransform>(frameCnt * animatedJointIndices.Count);

            for (int i = 0; i < frameCnt; i++)
            {
                foreach (var jointIndex in animatedJointIndices)
                {
                    cutTransform.Add(transforms[i * rig.NumJoints + jointIndex]);
                }
            }

            return cutTransform;
        }

        private int[] GenerateAnimationTypeOffset()
        {
            var currType = animSet[0].animationType;

            var animationTypeOffset = new List<int>(3);
            animationTypeOffset.Add(0);

            for (int i = 0; i < animSet.Count; i++)
            {
                if (animSet[i].animationType != currType)
                {
                    currType = animSet[i].animationType;
                    animationTypeOffset.Add(i);
                }
            }
            
            for (int i = 2; i >= animationTypeOffset.Count; i--)
            {
                animationTypeOffset.Add(animationTypeOffset.Last());
            }
            
            animationTypeOffset.Add(animSet.Count);
            
            return animationTypeOffset.ToArray();
        }

        private int[] GenerateAnimationTypeNum(int[] animationTypeOffset)
        {
            var animationTypeNum = new int[animationTypeOffset.Length - 1];

            for (int i = 1; i < animationTypeOffset.Length; i++)
            {
                animationTypeNum[i - 1] = animationTypeOffset[i] - animationTypeOffset[i - 1];
            }

            return animationTypeNum;
        }

        private int[] GenerateJointIndexOfTransformsGroup(int[] animatedJointIndices, AnimationRig rig)
        {
            var jointIndexOfTransformsGroup = Enumerable.Repeat(-1, rig.NumJoints).ToArray();
            for (int i = 0; i < animatedJointIndices.Length; i++)
            {
                jointIndexOfTransformsGroup[animatedJointIndices[i]] = i;
            }

            return jointIndexOfTransformsGroup;
        }

        private void SaveRuntimeDataToAsset(RuntimeAnimationData runtimeAnimationData)
        {
            string assetName = name.Substring(name.IndexOf('t') + 1);
            AssetDatabase.DeleteAsset($"Assets/Momat/Assets/AnimationRuntimeAsset{assetName}.asset");
            AssetDatabase.CreateAsset(runtimeAnimationData,
                $"Assets/Momat/Assets/AnimationRuntimeAsset{assetName}.asset");
            AssetDatabase.SaveAssets();
        }
 
        public void AddClipsToAnimSet(List<AnimationClip> clips)
        {
            foreach (var c in clips)
            {
                var processingAnimClip = new ProcessingAnimationClip();
                processingAnimClip.sourceAnimClip = c;
                animSet.Add(processingAnimClip);
            }   
        }

        public void RemoveClipInAnimSet(ProcessingAnimationClip clip)
        {
            animSet.Remove(clip);
        }
    }
}