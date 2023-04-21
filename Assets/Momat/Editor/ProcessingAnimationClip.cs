using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Momat.Editor
{
    [Serializable]
    public class ProcessingAnimationClip
    {
        public AnimationClip sourceAnimClip;
        public AvatarRetargetMap avatarRetargetMap;
        
        public string Name { get => sourceAnimClip.name; }

    }
}
