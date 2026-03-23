using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NuclearOption.MissionEditorScripts;

namespace EditorPlus
{
    /// <summary>
    /// Resolves which units are "selected" for copy/delete when both vanilla <see cref="UnitSelection"/>
    /// and <see cref="GroupFollowers"/> may disagree (e.g. stale GroupFollowers after multi-select).
    /// </summary>
    internal static class EditorSelectionHelper
    {
        /// <summary>
        /// Collect units from vanilla selection (typed paths + reflection fallback for renamed multi-select types).
        /// </summary>
        public static void CollectVanillaUnits(SelectionDetails details, List<Unit> addTo, HashSet<Unit> seen)
        {
            if (details == null || addTo == null) return;
            if (seen == null) seen = new HashSet<Unit>();

            if (details is MultiSelectSelectionDetails multi)
            {
                foreach (var item in multi.Items)
                    TryAddUnitFromSelectable(item, addTo, seen);
                if (addTo.Count > 0) return;
            }

            if (details is UnitSelectionDetails single && single.Unit != null && seen.Add(single.Unit))
            {
                addTo.Add(single.Unit);
                return;
            }

            // Runtime type may differ from compiled MultiSelectSelectionDetails (or Items failed empty)
            TryReflectionCollectUnitsFromSelectionDetails(details, addTo, seen);
        }

        private static void TryAddUnitFromSelectable(object item, List<Unit> addTo, HashSet<Unit> seen)
        {
            if (item == null) return;
            if (item is UnitSelectionDetails usd && usd.Unit != null && seen.Add(usd.Unit))
                addTo.Add(usd.Unit);
        }

        private static void TryReflectionCollectUnitsFromSelectionDetails(SelectionDetails details, List<Unit> addTo, HashSet<Unit> seen)
        {
            Type t = details.GetType();
            string[] propNames =
            {
                "Items", "items", "SelectedItems", "selectedItems",
                "Selections", "selections", "Details", "details", "SelectedDetails"
            };

            foreach (string name in propNames)
            {
                PropertyInfo prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (prop == null) continue;
                object val;
                try { val = prop.GetValue(details); }
                catch { continue; }
                if (val is string || val is not IEnumerable en) continue;

                int before = addTo.Count;
                foreach (object item in en)
                    TryAddUnitFromSelectable(item, addTo, seen);

                if (addTo.Count > before) return;
            }
        }

        /// <summary>
        /// Picks vanilla vs GroupFollowers by <b>total</b> selected unit count so vanilla multi-select
        /// wins over a stale single-unit group. Tie goes to vanilla.
        /// </summary>
        public static List<Unit> ResolveSelectedUnits(UnitSelection selection, GroupFollowers groupFollowers, out string sourceLabel)
        {
            sourceLabel = "none";
            var vanilla = new List<Unit>();
            var seen = new HashSet<Unit>();
            if (selection != null)
                CollectVanillaUnits(selection.SelectionDetails, vanilla, seen);

            var gfList = new List<Unit>();
            if (groupFollowers != null && groupFollowers.CurrentUnits != null)
            {
                for (int i = 0; i < groupFollowers.CurrentUnits.Count; i++)
                {
                    Unit u = groupFollowers.CurrentUnits[i];
                    if (u) gfList.Add(u);
                }
            }

            int v = vanilla.Count;
            int g = gfList.Count;

            if (v > g)
            {
                sourceLabel = "vanilla";
                return vanilla;
            }
            if (g > v)
            {
                sourceLabel = "GroupFollowers";
                return gfList;
            }
            if (v > 0)
            {
                sourceLabel = "vanilla (tie)";
                return vanilla;
            }
            if (g > 0)
            {
                sourceLabel = "GroupFollowers (tie)";
                return gfList;
            }

            return vanilla;
        }
    }
}
