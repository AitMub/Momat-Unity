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

        internal static string TryParseClipPropertyNameToStdAvatarJointName(string clipPropertyName)
        {
            // Remove prefix. e.g. 'mixamorig:'
            int index = clipPropertyName.IndexOfAny(new char[]{':'});
            if (index != -1)
            {
                clipPropertyName = clipPropertyName.Substring(index + 1);
            }
            
            Debug.Log(clipPropertyName);
            
            return null;
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

        internal static int LongestCommonSubstrLength(string s1, string s2)
        {
            int[,] lengths = new int[s1.Length + 1, s2.Length + 1];
            
            int maxLength = 0;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    if (s1[i - 1] == s2[j - 1])
                    {
                        lengths[i, j] = lengths[i - 1, j - 1] + 1;
                        if (lengths[i, j] > maxLength)
                        {
                            maxLength = lengths[i, j];
                        }
                    }
                    else
                    {
                        lengths[i, j] = Math.Max(lengths[i - 1, j], lengths[i, j - 1]);
                    }
                }
            }

            return maxLength;
        }
    }
}
