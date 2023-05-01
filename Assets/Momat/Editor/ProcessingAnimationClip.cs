using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Momat.Editor
{
    public enum AnimationTypeEnum : byte
    {
        EMotion = 0,
        EIdle = 1,
        EEvent = 2
    };
    
    [Serializable]
    public class ProcessingAnimationClip
    {
        public AnimationClip sourceAnimClip;
        public AnimationTypeEnum animationType;
        public AvatarRetargetMap avatarRetargetMap;
        
        public string Name { get => sourceAnimClip.name; }
    }
}
