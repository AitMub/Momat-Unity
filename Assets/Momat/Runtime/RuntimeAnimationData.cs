using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;

namespace Momat.Runtime
{
    public class RuntimeAnimationData : ScriptableObject
    {
        public List<AffineTransform> transforms;

        public AnimationRig rig;
    }
}