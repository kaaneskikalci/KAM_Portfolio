using System.Diagnostics;
using UnityEngine;

namespace KAM.Core
{
    public static class EditorSelectionGuard
    {
        [Conditional("UNITY_EDITOR")]
        public static void ClearSelection()
        {
#if UNITY_EDITOR
            UnityEditor.Selection.activeObject = null;
#endif
        }

        [Conditional("UNITY_EDITOR")]
        public static void ClearSelectionIfTarget(Object target)
        {
#if UNITY_EDITOR
            if (target == null)
                return;

            Object active = UnityEditor.Selection.activeObject;
            if (active == null)
                return;

            if (active == target || IsSelectionPartOfTarget(active, target))
                UnityEditor.Selection.activeObject = null;
#endif
        }

#if UNITY_EDITOR
        private static bool IsSelectionPartOfTarget(Object active, Object target)
        {
            GameObject targetGameObject = target as GameObject;
            if (targetGameObject == null && target is Component targetComponent)
                targetGameObject = targetComponent.gameObject;

            if (targetGameObject == null)
                return false;

            if (active is GameObject activeGameObject)
                return activeGameObject == targetGameObject || activeGameObject.transform.IsChildOf(targetGameObject.transform);

            if (active is Component activeComponent)
                return activeComponent.gameObject == targetGameObject || activeComponent.transform.IsChildOf(targetGameObject.transform);

            return false;
        }
#endif
    }
}
