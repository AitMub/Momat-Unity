using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;

namespace Momat.Runtime
{
    public class MomatRuntimeAnimationData : ScriptableObject
    {
        public List<AffineTransform> transforms;
    }
}