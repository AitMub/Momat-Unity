using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Assertions;

using Unity.Mathematics;

using UnityEditor;
using Unity.Collections;

namespace Momat.Runtime
{
    [Serializable]
    public class AnimationRig
    {
        [Serializable]
        public struct Joint
        {
            public string name;
            public int parentIndex;
            public AffineTransform localTransform;
        }

        public Joint[] joints;
        public string[] jointPaths;

        public Avatar avatar;

        public int NumJoints => joints.Length;
        

        public int GetParentJointIndex(int index)
        {
            Assert.IsTrue(index < NumJoints);
            return joints[index].parentIndex;
        }

        public int GetJointIndex(EditorCurveBinding binding)
        {
            for (int i = 0; i < jointPaths.Length; ++i)
            {
                if (jointPaths[i] == binding.path)
                {
                    return i;
                }
            }

            return -1;
        }

        public int GetJointIndexFromName(string name)
        {
            for (int i = 0; i < joints.Length; ++i)
            {
                if (joints[i].name == name)
                {
                    return i;
                }
            }

            return -1;
        }

        public int GetJointIndexFromPath(string path)
        {
            for (int i = 0; i < joints.Length; ++i)
            {
                if (jointPaths[i] == path)
                {
                    return i;
                }
            }

            return -1;
        }

        public int GetJointIndexFromStdName(string stdName)
        {
            var humanBone = avatar.humanDescription.human;
            for (int i = 0; i < humanBone.Length; i++)
            {
                if (humanBone[i].humanName == stdName)
                {
                    return GetJointIndexFromName(humanBone[i].boneName);
                }
            }

            return -1;
        }

        public Transform[] MapRigOnTransforms(Transform root)
        {
            Transform[] transforms = new Transform[NumJoints];

            transforms[0] = root;

            Transform FindChildRecursive(Transform t, string name)
            {
                Transform child = t.Find(name);
                if (child != null)
                {
                    return child;
                }

                foreach (Transform c in t)
                {
                    child = FindChildRecursive(c, name);
                    if (child != null)
                    {
                        return child;
                    }
                }

                return null;
            }

            for (int i = 1; i < NumJoints; ++i)
            {
                transforms[i] = FindChildRecursive(root, joints[i].name);
            }

            return transforms;
        }

        public NativeArray<int> GenerateParentIndices()
        {
            NativeArray<int> parents = new NativeArray<int>(NumJoints, Allocator.Persistent);

            for (int jointIndex = 0; jointIndex < NumJoints; ++jointIndex)
            {
                parents[jointIndex] = joints[jointIndex].parentIndex;
            }

            return parents;
        }

        internal static void CollectJointsRecursive(List<Joint> jointsList, Transform transform, int parentIndex)
        {
            int jointIndex = jointsList.Count;

            jointsList.Add(new Joint()
            {
                name = transform.name,
                parentIndex = parentIndex,
                localTransform = new AffineTransform(
                    transform.localPosition,
                    transform.localRotation)
            });

            foreach (Transform child in transform)
            {
                CollectJointsRecursive(jointsList, child, jointIndex);
            }
        }

        void GenerateJointPaths(string rootName)
        {
            jointPaths = new string[NumJoints];

            jointPaths[0] = rootName;

            for (int i = 1; i < NumJoints; ++i)
            {
                int parentIndex = joints[i].parentIndex;
                if (parentIndex < 0 || jointPaths[parentIndex].Length == 0)
                {
                    jointPaths[i] = joints[i].name;
                }
                else
                {
                    jointPaths[i] = jointPaths[parentIndex] + "/" + joints[i].name;
                }
            }
        }
    }
}
