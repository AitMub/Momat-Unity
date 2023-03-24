using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


namespace Momat.Editor
{
    internal static class Utility
    {
        // Unity AnimationClip can have inaccurate length when the clip is pretty long (noticeable for clip > 5 mins)
        internal static float ComputeAccurateClipDuration(AnimationClip clip)
        {
            float decimalNumFrames = clip.length * clip.frameRate;
            int numFrames = Unity.Mathematics.Missing.truncToInt(decimalNumFrames);
            return numFrames / clip.frameRate;
        }

        internal static ModelImporterClipAnimation GetImporterFromClip(AnimationClip clip)
        {
            string assetPath = AssetDatabase.GetAssetPath(clip);

            var modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;

            if (modelImporter == null)
            {
                return null;
            }

            foreach (ModelImporterClipAnimation clipImporter in modelImporter.clipAnimations)
            {
                if (clipImporter.name == clip.name)
                {
                    return clipImporter;
                }
            }

            return null;
        }

        internal static string GetImporterRootJointName(string assetPath)
        {
            ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (modelImporter == null)
            {
                return null;
            }

            SerializedObject serializedImporter = new SerializedObject(modelImporter);
            if (serializedImporter == null)
            {
                return null;
            }

            SerializedProperty property = serializedImporter.FindProperty("m_HumanDescription.m_RootMotionBoneName");
            if (property == null)
            {
                return null;
            }

            return string.IsNullOrEmpty(property.stringValue) ? null : property.stringValue;
        }

        internal static List<T> InstantiateAllTypesDerivingFrom<T>()
        {
            List<T> instances = new List<T>();

            TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<T>();
            foreach (Type type in types)
            {
                T instance = (T)Activator.CreateInstance(type);
                instances.Add(instance);
            }

            return instances;
        }

        internal static bool CheckMultiSelectModifier(IMouseEvent evt)
        {
            return Application.platform == RuntimePlatform.OSXEditor ? evt.commandKey : evt.ctrlKey;
        }

        internal static EventModifiers GetMultiSelectModifier()
        {
            return Application.platform == RuntimePlatform.OSXEditor ? EventModifiers.Command : EventModifiers.Control;
        }
    }
}
