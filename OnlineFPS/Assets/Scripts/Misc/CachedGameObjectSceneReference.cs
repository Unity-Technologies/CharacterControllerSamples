using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.UIElements;
#endif

namespace OnlineFPS
{
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(CachedGameObjectSceneReference))]
    public class CachedGameObjectSceneReferencePropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement myInspector = new VisualElement();
            PropertyField sceneField = new PropertyField(property.FindPropertyRelative("SceneAsset"), property.name);
            myInspector.Add(sceneField);
            return myInspector;
        }
    }
#endif

    [Serializable]
    public struct CachedGameObjectSceneReference
    {
#if UNITY_EDITOR
        public SceneAsset SceneAsset;
#endif

        public int CachedBuildIndex;

        public void CacheData()
        {
            CachedBuildIndex = GetIndexInBuildScenes();
        }

        public int GetIndexInBuildScenes()
        {
#if UNITY_EDITOR
            if (SceneAsset != null)
            {
                string scenePath = AssetDatabase.GetAssetPath(SceneAsset);
                for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
                {
                    EditorBuildSettingsScene buildScene = EditorBuildSettings.scenes[i];
                    if (buildScene.path == scenePath)
                    {
                        return i;
                    }
                }

                Debug.LogError(
                    $"Error: Scene \"{SceneAsset.name}\" assigned in {typeof(CachedGameObjectSceneReference)} has invalid build index. Make sure this scene is added to build settings");
            }
#endif

            return -1;
        }
    }
}