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
            
            var runtimeAsset = CreateInstance<Runtime.RuntimeAnimationData>();
            runtimeAsset.transforms = new List<AffineTransform>();
            runtimeAsset.animationTransformOffset = new List<int>();
                
            for (int i = 0; i < 2; i++)
            {
                var jointTransforms = GenerateClipRuntimeData(motionAnimSet[i], targetRig);
                
                runtimeAsset.animationTransformOffset.Add(runtimeAsset.transforms.Count);
                for (int j = 0; j < jointTransforms.Length; j++)
                {
                    runtimeAsset.transforms.Add(jointTransforms[j]);
                }

                runtimeAsset.rig = targetRig.GenerateRuntimeRig();

                jointTransforms.Dispose();
            }
            
            string assetName = name.Substring(name.IndexOf('t'));
            AssetDatabase.CreateAsset(runtimeAsset,
                $"Assets/Momat/Assets/AnimationRuntimeAsset{assetName}.asset");
            AssetDatabase.SaveAssets();
        }

        internal NativeArray<AffineTransform> GenerateClipRuntimeData(ProcessingAnimationClip animClip, AnimationRig targetRig)
        {
            var clip = animClip.sourceAnimClip;
            var avatarRetargetMap = animClip.avatarRetargetMap;
            
            int numJoints = targetRig.NumJoints;
            int numFrames = (int)math.ceil(clip.frameRate * clip.length);
            int numTransforms = numFrames * numJoints;

            var jointTransforms = new NativeArray<AffineTransform>(numTransforms, Allocator.Persistent);

            if (avatarRetargetMap != null)
            {
                var sourceRig = AnimationRig.Create(avatarRetargetMap.sourceAvatar);

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
            }

            return jointTransforms;
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