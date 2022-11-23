using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities.Serialization;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
#endif

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(BakedSubSceneReference))]
public class BakedSubSceneReferencePropertyDrawer : PropertyDrawer
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
public struct BakedSubSceneReference
{
#if UNITY_EDITOR
    public SceneAsset SceneAsset;
#endif

    public EntitySceneReference GetEntitySceneReference()
    {
#if UNITY_EDITOR
        return new EntitySceneReference(SceneAsset);
#else
        return default;
#endif
    }
}
