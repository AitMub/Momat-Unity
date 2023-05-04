using System;
using System.Collections;
using System.Collections.Generic;
using Momat.Runtime;
using UnityEngine;
using UnityEngine.Serialization;

namespace Momat.Editor
{
    [Serializable]
    public class ProcessingAnimationClip
    {
        public AnimationClip sourceAnimClip;
        public EAnimationType animationType;
        public AvatarRetargetMap avatarRetargetMap;
        
        public string Name { get => sourceAnimClip.name; }
    }
}
