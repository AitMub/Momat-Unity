using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using Unity.Mathematics;


namespace Momat.Runtime
{
    internal struct UpdateAnimationPoseJob : IAnimationJob, System.IDisposable
    {
        private NativeArray<TransformStreamHandle> transforms;
        private NativeArray<bool> boundJoints;

        private PropertySceneHandle deltaTime;

        private bool transformBufferValid;

        private RuntimeAnimationData runtimeAnimationData;

        public int loopTime;
        
        public bool Setup(Animator animator, Transform[] transforms, RuntimeAnimationData runtimeAnimationData, PropertySceneHandle deltaTimePropertyHandle)
        {
            this.runtimeAnimationData = runtimeAnimationData;
            int numJoints = runtimeAnimationData.rig.NumJoints;
            this.transforms = new NativeArray<TransformStreamHandle>(numJoints, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            boundJoints = new NativeArray<bool>(numJoints, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            // Root joint is always first transform, and names don't need to match contrary to other joints
            this.transforms[0] = animator.BindStreamTransform(transforms[0]);
            boundJoints[0] = true;

            for (int i = 1; i < transforms.Length; i++)
            {
                int jointIndex = runtimeAnimationData.rig.GetJointIndexFromName(transforms[i].name);
                if (jointIndex >= 0)
                {
                    this.transforms[jointIndex] = animator.BindStreamTransform(transforms[i]);
                    boundJoints[jointIndex] = true;
                }
            }

            deltaTime = deltaTimePropertyHandle;

            loopTime = 1;

            transformBufferValid = true;

            return true;
        }

        public void Dispose()
        {
            transforms.Dispose();
            boundJoints.Dispose();
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
            /*ref MotionSynthesizer synthesizer = ref this.synthesizer.Ref;
            float dt = deltaTime.GetFloat(stream);

            var previousTransform = synthesizer.WorldRootTransform;
            transformBufferValid = synthesizer.Update(dt);
            
            
            //convert the delta transform to root motion in the stream
            if (transformBufferValid && dt > 0.0f)
            {
                WriteRootMotion(stream, dt, previousTransform, synthesizer.WorldRootTransform);
            }*/
        }
        
        public void ProcessAnimation(AnimationStream stream)
        {
            // ref MotionSynthesizer synthesizer = ref this.synthesizer.Ref;

            if (transformBufferValid)
            {
                // int numTransforms = synthesizer.LocalSpaceTransformBuffer.Length;
                // int numTransforms = runtimeAnimationData.transforms.Count / runtimeAnimationData.rig.NumJoints;
                int numTransforms = transforms.Length;
                for (int i = 1; i < numTransforms; ++i)
                {
                    if (!boundJoints[i])
                    {
                        continue;
                    }
                    
                    // transforms[i].SetGlobalTR(stream, runtimeAnimationData.transforms[i].t, runtimeAnimationData.transforms[i].q, true);
                    transforms[i].SetLocalPosition(stream, runtimeAnimationData.transforms[i + MomatAnimator.t * numTransforms].t);
                    transforms[i].SetLocalRotation(stream, runtimeAnimationData.transforms[i + MomatAnimator.t * numTransforms].q);
                    //transforms[i].SetPosition(stream, runtimeAnimationData.transforms[i + MomatAnimator.t * numTransforms].t);
                    //transforms[i].SetRotation(stream, runtimeAnimationData.transforms[i + MomatAnimator.t * numTransforms].q);
                }
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