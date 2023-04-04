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
        public List<ProcessingAnimationClip> motionAnimSet;
        public List<ProcessingAnimationClip> idleAnimSet;
        public Avatar avatar;
        public Avatar refAvatar;

        private AnimationRig rig;
        public float sampleRate = 30f;
        
        public void BuildRuntimeData()
        {
            var clip = motionAnimSet[0].sourceAnimClip;
            var clipJointMapToStdAvatar = motionAnimSet[0].clipJointMapToStdAvatar;
            
            rig = AnimationRig.Create(avatar);
            AnimationRig refClipRig = AnimationRig.Create(refAvatar);

            int numJoints = rig.NumJoints;
            int numFrames = (int)math.ceil(clip.frameRate * clip.length);
            int numTransforms = numFrames * numJoints;

            using(NativeArray<AffineTransform> transforms = new NativeArray<AffineTransform>(numTransforms, Allocator.Persistent))
            {
                using (AnimationSampler animSampler = new AnimationSampler(rig, refClipRig, clip, clipJointMapToStdAvatar))
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
                    for (int i = 0; i < numJoints; i++)
                    {
                        var jointIndexToQ = new JointIndexToQ();
                        jointIndexToQ.avatarQ = rig.Joints[i].localTransform.q;

                        string jointStdName = rig.GetJointStdNameFromIndex(i);
                        int refJointIndex = refClipRig.GetJointIndexFromStdName(jointStdName);
                        if (jointStdName == null || refJointIndex == -1)
                        {
                            jointIndexToQ.refJointIndex = -1;
                            jointIndexToQ.refAvatarQ = Quaternion.identity;
                        }
                        else
                        {
                            jointIndexToQ.refJointIndex = refJointIndex;
                            // jointIndexToQ.refJointIndex = -1;
                            jointIndexToQ.refAvatarQ = refClipRig.Joints[refJointIndex].localTransform.q;
                        }

                        jointIndexToQs[i] = jointIndexToQ;
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