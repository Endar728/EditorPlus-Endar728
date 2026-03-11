using System.Collections.Generic;
using NuclearOption.SavedMission;
using UnityEngine;

namespace EditorPlus
{
    /// <summary>
    /// Stores desired positions for units before RegisterNewUnit is called,
    /// so we can restore them after registration to prevent terrain clamping.
    /// </summary>
    internal static class NoClipPositionStore
    {
        private static readonly Dictionary<Unit, GlobalPosition> _storedPositions = new Dictionary<Unit, GlobalPosition>();

        public static void StorePosition(Unit unit, GlobalPosition position)
        {
            if (unit != null)
            {
                _storedPositions[unit] = position;
                Plugin.Logger?.LogInfo($"[EditorPlus] No clip: Stored position for unit {unit.name}: {position.AsVector3()}");
            }
        }

        public static bool TryGetPosition(Unit unit, out GlobalPosition position)
        {
            if (unit != null && _storedPositions.TryGetValue(unit, out position))
            {
                return true;
            }
            position = default;
            return false;
        }

        public static void ClearPosition(Unit unit)
        {
            if (unit != null)
            {
                _storedPositions.Remove(unit);
            }
        }

        public static void ClearAll()
        {
            _storedPositions.Clear();
        }
    }
}
