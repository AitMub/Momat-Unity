using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using Unity.Mathematics;
using Unity.VisualScripting;


namespace Momat.Runtime
{
    internal struct UpdateAnimationPoseJob : IAnimationJob, IDisposable
    {
        private NativeArray<TransformStreamHandle> transforms;
        private NativeArray<bool> boundJoints;
        
        private AnimationGenerator animationGenerator;
        
        public bool Setup(Animator animator, Transform[] transforms, RuntimeAnimationData runtimeAnimationData, AnimationGenerator animationGenerator)
        {
            int numJoints = runtimeAnimationData.rig.NumJoints;
            this.transforms = new NativeArray<TransformStreamHandle>(numJoints, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            boundJoints = new NativeArray<bool>(numJoints, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            // Root joint is always first transform, and names don't need to match contrary to other joints
            this.transforms[0] = animator.BindStreamTransform(transforms[0]);
            boundJoints[0] = true;

            for (int i = 0; i < transforms.Length; i++)
            {
                int jointIndex = runtimeAnimationData.rig.GetJointIndexFromName(transforms[i].name);
                if (jointIndex >= 0)
                {
                    this.transforms[jointIndex] = animator.BindStreamTransform(transforms[i]);
                    boundJoints[jointIndex] = true;
                }
            }

            this.animationGenerator = animationGenerator;
            
            return true;
        }

        public void Dispose()
        {
            transforms.Dispose();
            boundJoints.Dispose();
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
            stream.velocity = animationGenerator.velocity;
            stream.angularVelocity = animationGenerator.angularVelocity;
        }
        
        public void ProcessAnimation(AnimationStream stream)
        {
            int numJoints = transforms.Length;
            for (int i = 1; i < numJoints; ++i)
            {
                if (!boundJoints[i])
                {
                    continue;
                }

                var transform = animationGenerator.GetPoseJointTransformAtTime(i);
                if (transform == null)
                {
                    continue;
                }
                transforms[i].SetLocalPosition(stream, transform.Value.t);
                if(i != 1)
                transforms[i].SetLocalRotation(stream, transform.Value.q);
            }
        }

        void WriteRootMotion(AnimationStream stream, float deltaTime, AffineTransform previousTransform, AffineTransform updatedTransform)
        {
            float invDeltaTime = 1.0f / deltaTime;
            AffineTransform rootMotion = previousTransform.inverse() * updatedTransform;

            Vector3 rootMotionAngles = ((Quaternion)rootMotion.q).eulerAngles;
            rootMotionAngles = math.radians(rootMotionAngles);

            stream.velocity = invDeltaTime * rootMotion.t;
            stream.angularVelocity = invDeltaTime * rootMotionAngles;
        }
    }
}