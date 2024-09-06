using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace OnlineFPS
{
    public static class UIUtilities
    {
        public static void SetDisplay(this VisualElement element, bool enabled)
        {
            element.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}