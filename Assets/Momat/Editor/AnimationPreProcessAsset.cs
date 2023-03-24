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
        private float sampleRate = 15f;
        
        public void BuildRuntimeData()
        {
            var clip = motionAnimSet[0].sourceAnimClip;
            
            rig = AnimationRig.Create(avatar);
            Debug.Log("Rig Created");

            int numJoints = rig.NumJoints;
            int numFrames = (int)math.ceil(clip.frameRate * clip.length);
            int numTransforms = numFrames * numJoints;

            using(NativeArray<AffineTransform> transforms = new NativeArray<AffineTransform>(numTransforms, Allocator.Persistent))
            {
                using (AnimationSampler animSampler = new AnimationSampler(rig, clip))
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

                        while (!rangeSampler.IsComplete)
                        {
                            Debug.Log("Sampling");
                        }
                        
                        rangeSampler.Complete();
                    }
                }
                
                var runtimeAsset = ScriptableObject.CreateInstance<Momat.Runtime.MomatRuntimeAnimationData>();
                runtimeAsset.transforms = new List<AffineTransform>(transforms.Length);
                for (int i = 0; i < transforms.Length; i++)
                {
                    runtimeAsset.transforms.Add(transforms[i]);
                    if (transforms[i].t.x != 0)
                    {
                        Debug.Log(i);
                    }
                }
                AssetDatabase.CreateAsset(runtimeAsset, "Assets/Momat/Assets/AnimationRuntimeAsset.asset");
                AssetDatabase.SaveAssets();
            }

            Debug.Log("Transforms Generated");
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

        public void DeleteClipInAnimSet(ProcessingAnimationClip clip, AnimationSetEnum animationSetEnum)
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