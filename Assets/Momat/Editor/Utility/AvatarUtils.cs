using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Momat.Editor
{
    internal static class AvatarExtension
    {
        public static List<AnimationRig.Joint> GetAvatarJoints(this Avatar avatar)
        {
            if (avatar == null)
            {
                return null;
            }

            string assetPath = AssetDatabase.GetAssetPath(avatar);
            GameObject avatarRootObject = AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject)) as GameObject;
            if (avatarRootObject == null)
            {
                return null;
            }
            
            List<AnimationRig.Joint> jointsList = new List<AnimationRig.Joint>();
            jointsList.Add(new AnimationRig.Joint()
            {
                name = avatarRootObject.transform.name,
                parentIndex = -1,
                localTransform = AffineTransform.identity,
            });

            foreach (Transform child in avatarRootObject.transform)
            {
                AnimationRig.CollectJointsRecursive(jointsList, child, 0);
            }

            var skeleton = avatar.humanDescription.skeleton;
            for (int i = 0; i < jointsList.Count; i++)
            {
                foreach (var bone in skeleton)
                {
                    if (bone.name == jointsList[i].name)
                    {
                        var newJoint = new AnimationRig.Joint();
                        newJoint.name = jointsList[i].name;
                        newJoint.parentIndex = jointsList[i].parentIndex;
                        newJoint.localTransform = AffineTransform.Create(bone.position, bone.rotation);
                        jointsList[i] = newJoint;
                    }
                }
            }

            return jointsList;
        }

        public static List<string> GetAvatarJointNames(this Avatar avatar)
        {
            var jointsList = avatar.GetAvatarJoints();
            if (jointsList == null)
            {
                return null;
            }

            List<string> names = jointsList.Select(j => j.name).ToList();
            return names;
        }
    }
}
