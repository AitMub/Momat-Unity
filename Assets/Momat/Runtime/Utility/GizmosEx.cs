using UnityEngine;

namespace Momat.Runtime
{
    public static class GizmosEx
    {
        public static void DrawArrow(Vector3 pos, Vector3 direction, float arrowLength = 0.3f, float arrowHeadLength = 0.1f, float arrowHeadAngle = 20.0f)
        {
            Gizmos.DrawRay(pos, direction * arrowLength);
       
            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180+arrowHeadAngle,0) * new Vector3(0,0,1);
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180-arrowHeadAngle,0) * new Vector3(0,0,1);
            Gizmos.DrawRay(pos + direction * arrowLength, right * arrowHeadLength);
            Gizmos.DrawRay(pos + direction * arrowLength, left * arrowHeadLength);
        }
    }
}
 