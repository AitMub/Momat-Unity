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
using Vector2 = System.Numerics.Vector2;

namespace Momat.Editor
{
    enum AnimationSetEnum : byte
    {
        EMotion = 0,
        EIdle = 1
    };
    
    [CreateAssetMenu(menuName = "Momat/Animation PreProcess Asset")]
    internal class AnimationPreProcessAsset : ScriptableObject
    {
        public Avatar avatar;

        public List<ProcessingAnimationClip> motionAnimSet;
        public List<ProcessingAnimationClip> idleAnimSet;

        public AnimationFeatureDefinition featureDefinition;
        
        public float sampleRate = 30f;
        
        public void BuildRuntimeData()
        {
            var targetRig = AnimationRig.Create(avatar);
            
            var runtimeAsset = CreateInstance<RuntimeAnimationData>();
            runtimeAsset.frameRate = sampleRate;

            var animatedJointIndices = new HashSet<int>();
            
            int totalFrame = 0;
            for (int i = 0; i < motionAnimSet.Count; i++)
            {
                var jointTransforms = GenerateClipRuntimeJointTransform
                    (motionAnimSet[i], targetRig, ref animatedJointIndices);
                var featureVectors = GenerateClipFeatureVector
                    (motionAnimSet[i], jointTransforms, targetRig);

                var frameNum = jointTransforms.Length / targetRig.NumJoints;
                runtimeAsset.animationFrameOffset.Add(totalFrame);
                runtimeAsset.animationFrameNum.Add(frameNum);
                totalFrame += frameNum;
                
                runtimeAsset.transforms.AddRange(jointTransforms);
                runtimeAsset.trajectoryPoints.AddRange(featureVectors.trajectories);
                runtimeAsset.comparedJointRootSpaceT.AddRange(featureVectors.jointRootSpaceT);

                jointTransforms.Dispose();
                featureVectors.Dispose();
            }
            
            runtimeAsset.transforms = RemoveUnanimatedTransform
                (runtimeAsset.transforms, animatedJointIndices, targetRig);

            runtimeAsset.animatedJointIndices = animatedJointIndices.ToArray();
            runtimeAsset.jointIndexInTransforms = Enumerable.Repeat(-1, targetRig.NumJoints).ToArray();
            for (int i = 0; i < runtimeAsset.animatedJointIndices.Length; i++)
            {
                runtimeAsset.jointIndexInTransforms[runtimeAsset.animatedJointIndices[i]] = i;
            }
            
            runtimeAsset.rig = targetRig.GenerateRuntimeRig();

            runtimeAsset.trajectoryFeatureDefinition = featureDefinition.trajectoryFeatureDefinition;
            runtimeAsset.poseFeatureDefinition = featureDefinition.poseFeatureDefinition;
            
            string assetName = name.Substring(name.IndexOf('t') + 1);
            AssetDatabase.DeleteAsset($"Assets/Momat/Assets/AnimationRuntimeAsset{assetName}.asset");
            AssetDatabase.CreateAsset(runtimeAsset,
                $"Assets/Momat/Assets/AnimationRuntimeAsset{assetName}.asset");
            AssetDatabase.SaveAssets();
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
                trajectories = new NativeArray<float3>
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
 
        public void AddClipsToAnimSet(List<AnimationClip> clips, AnimationSetEnum animationSetEnum)
        {
            if (animationSetEnum == AnimationSetEnum.EMotion)
            {
                foreach (var c in clips)
                {
                    var processingAnimClip = new ProcessingAnimationClip();
                    processingAnimClip.sourceAnimClip = c;
                    motionAnimSet.Add(processingAnimClip);
                }   
            }
            else if (animationSetEnum == AnimationSetEnum.EIdle)
            {
                foreach (var c in clips)
                {
                    var processingAnimClip = new ProcessingAnimationClip();
                    processingAnimClip.sourceAnimClip = c;
                    idleAnimSet.Add(processingAnimClip);
                }   
            }
        }

        public void RemoveClipInAnimSet(ProcessingAnimationClip clip, AnimationSetEnum animationSetEnum)
        {
            if (animationSetEnum == AnimationSetEnum.EMotion)
            {
                motionAnimSet.Remove(clip);
            }
            else if (animationSetEnum == AnimationSetEnum.EIdle)
            {
                idleAnimSet.Remove(clip);
            }
        }
    }
}