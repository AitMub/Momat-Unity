using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Momat.Editor
{
    enum AnimationSetEnum : byte
    {
        EMotion = 0,
        EIdle = 1
    };

    struct JointIndexToQ
    {
        public int refJointIndex;
        public Quaternion refAvatarQ;
        public Quaternion avatarQ;
    }
    
    [CreateAssetMenu(menuName = "Momat/Create Asset")]
    class AnimationPreProcessAsset : ScriptableObject
    {
        public Avatar avatar;

        public List<ProcessingAnimationClip> motionAnimSet;
        public List<ProcessingAnimationClip> idleAnimSet;
        
        public float sampleRate = 30f;

        private AnimationRig rig;
        
        public void BuildRuntimeData()
        {
            var clip = motionAnimSet[0].sourceAnimClip;
            var avatarRetargetMap = motionAnimSet[0].avatarRetargetMap;

            rig = AnimationRig.Create(avatar);
            AnimationRig sourceRig = AnimationRig.Create(avatarRetargetMap.sourceAvatar);

            int numJoints = rig.NumJoints;
            int numFrames = (int)math.ceil(clip.frameRate * clip.length);
            int numTransforms = numFrames * numJoints;

            using(NativeArray<AffineTransform> transforms = new NativeArray<AffineTransform>(numTransforms, Allocator.Persistent))
            {
                using (AnimationSampler animSampler = new AnimationSampler(rig, sourceRig, clip, avatarRetargetMap))
                {
                    float sourceSampleRate = clip.frameRate;
                    float targetSampleRate = sampleRate;
                    float sampleRateRatio = sourceSampleRate / targetSampleRate;
                    int numFrameResampledClip = (int)math.ceil(targetSampleRate * clip.length);
                    
                    var sampleRange = new AnimationCurveBake.SampleRange()
                    {
                        startFrameIndex = 0,
                        numFrames = numFrames
                    };

                    NativeArray<JointIndexToQ> jointIndexToQs =
                        new NativeArray<JointIndexToQ>(numJoints, Allocator.Persistent);
                    for (int i = 0; i < jointIndexToQs.Length; i++)
                    {
                        var jointIndexToQ = new JointIndexToQ();
                        jointIndexToQ.refJointIndex = -1;
                        jointIndexToQs[i] = jointIndexToQ;
                    }
                    for (int i = 0; i < avatarRetargetMap.sourceToTargetIndices.Length; i++)
                    {
                        var targetJointName = avatarRetargetMap.FindTargetNameBySourceIndex(i);
                        int targetIndex = rig.GetJointIndexFromName(targetJointName);

                        if (targetIndex >= 0)
                        {
                            var jointIndexToQ = new JointIndexToQ();
                            jointIndexToQ.refJointIndex = i;
                            jointIndexToQs[targetIndex] = jointIndexToQ;
                        }
                    }
                    
                    using (AnimationSampler.RangeSampler rangeSampler =
                           animSampler.PrepareRangeSampler(targetSampleRate, sampleRange, 0, 
                               transforms, jointIndexToQs))
                    {
                        rangeSampler.Schedule();

                        rangeSampler.Complete();
                    }

                    jointIndexToQs.Dispose();
                }
                
                var runtimeAsset = ScriptableObject.CreateInstance<Momat.Runtime.RuntimeAnimationData>();
                runtimeAsset.transforms = new List<AffineTransform>(transforms.Length);
                for (int i = 0; i < transforms.Length; i++)
                {
                    runtimeAsset.transforms.Add(transforms[i]);
                }

                runtimeAsset.rig = rig.GenerateRuntimeRig();

                string assetName = name.Substring(name.IndexOf('t'));
                AssetDatabase.CreateAsset(runtimeAsset, $"Assets/Momat/Assets/AnimationRuntimeAsset{assetName}.asset");
                AssetDatabase.SaveAssets();
            }
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