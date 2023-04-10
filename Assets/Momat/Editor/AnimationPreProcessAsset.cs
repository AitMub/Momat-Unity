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
        
        public void BuildRuntimeData()
        {
            var clip = motionAnimSet[0].sourceAnimClip;
            var avatarRetargetMap = motionAnimSet[0].avatarRetargetMap;

            if (avatarRetargetMap != null)
            {
                var targetRig = AnimationRig.Create(avatar);
                var sourceRig = AnimationRig.Create(avatarRetargetMap.sourceAvatar);

                int numJoints = targetRig.NumJoints;
                int numFrames = (int)math.ceil(clip.frameRate * clip.length);
                int numTransforms = numFrames * numJoints;

                using (var jointTransforms = new NativeArray<AffineTransform>(numTransforms, Allocator.Persistent))
                {
                    using (var animSampler = new AnimationSampler(clip, sourceRig))
                    {
                        animSampler.RetargetAnimation(targetRig, avatarRetargetMap);
                        
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
                               (targetSampleRate, sampleRange, 0, jointTransforms))
                        {
                            rangeSampler.Schedule();

                            rangeSampler.Complete();
                        }
                    }

                    var runtimeAsset = CreateInstance<Runtime.RuntimeAnimationData>();
                    runtimeAsset.transforms = new List<AffineTransform>(jointTransforms.Length);
                    for (int i = 0; i < jointTransforms.Length; i++)
                    {
                        runtimeAsset.transforms.Add(jointTransforms[i]);
                    }

                    runtimeAsset.rig = targetRig.GenerateRuntimeRig();

                    string assetName = name.Substring(name.IndexOf('t'));
                    AssetDatabase.CreateAsset(runtimeAsset,
                        $"Assets/Momat/Assets/AnimationRuntimeAsset{assetName}.asset");
                    AssetDatabase.SaveAssets();
                }
            }
        }

        public void BuildRuntimeDataForMotionClip(ProcessingAnimationClip motionAnimClip)
        {
            
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