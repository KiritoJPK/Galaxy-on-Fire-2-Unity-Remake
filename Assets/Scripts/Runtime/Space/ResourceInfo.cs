// ResourceInfo.cs
// Links a prefab back to the original game's resource IDs (as used throughout the decompiled code).
// Lives in its own file because Unity needs a MonoBehaviour's file name to match its class name.

using UnityEngine;

namespace GoF2Remake.Visuals
{
    public class ResourceInfo : MonoBehaviour
    {
        public int meshId;
        public int materialId;
        public string originalPath;
    }
}
