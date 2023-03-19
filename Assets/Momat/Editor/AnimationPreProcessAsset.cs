using System.Collections;
using System.Collections.Generic;
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