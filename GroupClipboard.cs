using System.Collections.Generic;
using NuclearOption.SavedMission;
using UnityEngine;

namespace EditorPlus
{
    public class CopiedUnitData
    {
        public string Type;
        public string Faction;
        /// <summary>Delta from group centroid in Unity world space (<c>transform.position</c>), not mission <see cref="SavedUnit.globalPosition"/>.</summary>
        public Vector3 RelativeOffset;
        public Quaternion Rotation;
        public SavedUnit SourceSaved;
    }

    public static class GroupClipboard
    {
        public static readonly List<CopiedUnitData> Items = new List<CopiedUnitData>();
        public static bool HasData => Items.Count > 0;

        public static void Clear()
        {
            Items.Clear();
        }
    }
}
