using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Momat.Editor
{
    [Serializable]
    class ProcessingAnimationClip
    {
        public string Name { get => sourceAnimClip.name; }
        
        public AnimationClip sourceAnimClip;
    }
}
