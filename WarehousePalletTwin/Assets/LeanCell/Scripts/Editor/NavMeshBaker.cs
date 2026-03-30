using UnityEditor;
using UnityEngine;
using Unity.AI.Navigation;

namespace LeanCell.Editor
{
    public static class NavMeshBaker
    {
        [MenuItem("LeanCell/Bake NavMesh")]
        public static void BakeNavMesh()
        {
            var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
            foreach (var surface in surfaces)
            {
                surface.BuildNavMesh();
                Debug.Log($"[LeanCell] NavMesh baked on {surface.gameObject.name}");
            }
        }
    }
}
