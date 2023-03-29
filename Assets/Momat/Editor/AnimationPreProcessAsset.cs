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

    [CreateAssetMenu(menuName = "Momat/Create Asset")]
    class AnimationPreProcessAsset : ScriptableObject
    {
        public List<ProcessingAnimationClip> motionAnimSet;
        public List<ProcessingAnimationClip> idleAnimSet;
        public Avatar avatar;

        private AnimationRig rig;
        public float sampleRate = 30f;
        
        public void BuildRuntimeData()
        {
            var clip = motionAnimSet[0].sourceAnimClip;
            var clipJointMapToStdAvatar = motionAnimSet[0].clipJointMapToStdAvatar;
            
            rig = AnimationRig.Create(avatar);

            int numJoints = rig.NumJoints;
            int numFrames = (int)math.ceil(clip.frameRate * clip.length);
            int numTransforms = numFrames * numJoints;

            using(NativeArray<AffineTransform> transforms = new NativeArray<AffineTransform>(numTransforms, Allocator.Persistent))
            {
                using (AnimationSampler animSampler = new AnimationSampler(rig, clip, clipJointMapToStdAvatar))
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

                    using (AnimationSampler.RangeSampler rangeSampler =
                           animSampler.PrepareRangeSampler(targetSampleRate, sampleRange, 0, transforms))
                    {
                        rangeSampler.Schedule();

                        rangeSampler.Complete();
                    }
                }
                
                var runtimeAsset = ScriptableObject.CreateInstance<Momat.Runtime.RuntimeAnimationData>();
                runtimeAsset.transforms = new List<AffineTransform>(transforms.Length);
                for (int i = 0; i < transforms.Length; i++)
                {
                    runtimeAsset.transforms.Add(transforms[i]);
                }

                runtimeAsset.rig = rig.GenerateRuntimeRig();
                
                AssetDatabase.CreateAsset(runtimeAsset, "Assets/Momat/Assets/AnimationRuntimeAsset.asset");
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