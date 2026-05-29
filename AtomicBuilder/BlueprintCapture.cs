using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission;
using UnityEngine;

namespace EditorPlus.AtomicBuilder
{
    internal static class BlueprintCapture
    {
        internal static bool TrySaveBlueprint(string blueprintName, float radiusMeters, Vector3 centerWorld, out string message)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(blueprintName))
            {
                message = "Enter a blueprint name.";
                return false;
            }

            try
            {
                string missionPath = AtomicBuilderPaths.TryGetCurrentMissionJsonPath();
                JObject root = BuildMinimalMissionShell();

                int liveCount = MergeLiveUnitsIntoMission(root, centerWorld, radiusMeters);
                Plugin.Logger?.LogInfo(
                    $"[AtomicBuilder] Live editor capture: {liveCount} unit(s) within {radiusMeters:0}m of center.");

                if (liveCount == 0 && !string.IsNullOrEmpty(missionPath))
                {
                    root = JObject.Parse(File.ReadAllText(missionPath));
                    Plugin.Logger?.LogInfo($"[AtomicBuilder] No live units in radius — falling back to mission file: {missionPath}");
                }
                else if (liveCount == 0)
                {
                    Plugin.Logger?.LogInfo("[AtomicBuilder] No mission file on disk — building blueprint from live editor units.");
                }

                Vector3 origin = ResolveOrigin(root, centerWorld, radiusMeters);
                JObject blueprint = FilterToZone(root, origin, centerWorld, radiusMeters);
                int count = CountUnits(blueprint);
                if (count == 0)
                {
                    message =
                        "No units in capture radius. Select units, move the capture center near your build, or increase radius.";
                    Plugin.Logger?.LogWarning($"[AtomicBuilder] {message}");
                    return false;
                }

                blueprint["AtomicBuilderInfo"] = new JObject
                {
                    ["origin"] = JArray.FromObject(new[] { origin.x, origin.y, origin.z })
                };

                string path = AtomicBuilderPaths.BlueprintFile(blueprintName);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AtomicBuilderPaths.BlueprintsRoot);
                File.WriteAllText(path, blueprint.ToString(Newtonsoft.Json.Formatting.Indented));
                message = $"Saved \"{Path.GetFileNameWithoutExtension(path)}\" ({count} objects, R={radiusMeters:0}m).";
                Plugin.Logger?.LogInfo($"[AtomicBuilder] {message} → {path}");
                return true;
            }
            catch (Exception ex)
            {
                message = "Save failed: " + ex.Message;
                Plugin.Logger?.LogError($"[AtomicBuilder] {message}\n{ex}");
                return false;
            }
        }

        static JObject BuildMinimalMissionShell() => new JObject
        {
            ["JsonVersion"] = 4,
            ["objectives"] = new JObject
            {
                ["Objectives"] = new JArray(),
                ["Outcomes"] = new JArray()
            }
        };

        static int MergeLiveUnitsIntoMission(JObject root, Vector3 center, float radius)
        {
            foreach (string cat in AtomicBuilderPaths.UnitCategories)
                root[cat] = new JArray();

            int added = 0;
            int scanned = 0;
            int withSaved = 0;

            foreach (var unit in UnityEngine.Object.FindObjectsOfType<Unit>(true))
            {
                scanned++;
                if (unit?.SavedUnit == null) continue;
                withSaved++;

                Vector3 pos = GetUnitWorldPosition(unit);
                if (AtomicBuilderMath.Dist3D(pos, center) > radius) continue;

                string cat = CategoryForSavedUnit(unit.SavedUnit);
                JObject entry = SerializeUnitFromEditor(unit, pos);
                if (entry == null) continue;

                ((JArray)root[cat]).Add(entry);
                added++;
            }

            Plugin.Logger?.LogInfo(
                $"[AtomicBuilder] Scan: {scanned} Unit(s), {withSaved} with SavedUnit, {added} in radius.");
            return added;
        }

        static Vector3 GetUnitWorldPosition(Unit unit)
        {
            if (unit?.SavedUnit != null)
            {
                try { return unit.SavedUnit.globalPosition.AsVector3(); }
                catch { /* fall through */ }
            }
            return unit != null ? unit.transform.position : Vector3.zero;
        }

        static string CategoryForSavedUnit(SavedUnit su)
        {
            string typeName = su.GetType().Name;
            return typeName switch
            {
                "SavedAircraft" => "aircraft",
                "SavedVehicle" => "vehicles",
                "SavedShip" => "ships",
                "SavedBuilding" => "buildings",
                "SavedScenery" => "scenery",
                "SavedMissile" => "missiles",
                "SavedContainer" => "containers",
                "SavedPilot" => "pilots",
                "SavedAirbase" => "airbases",
                _ => "vehicles"
            };
        }

        static JObject SerializeUnitFromEditor(Unit unit, Vector3 worldPos)
        {
            var su = unit.SavedUnit;
            if (su == null) return null;

            Quaternion rot;
            try { rot = su.rotation; }
            catch { rot = unit.transform.rotation; }

            return new JObject
            {
                ["type"] = su.type,
                ["faction"] = su.faction ?? "",
                ["UniqueName"] = su.UniqueName ?? unit.name,
                ["globalPosition"] = AtomicBuilderMath.FormatCoords(worldPos),
                ["rotation"] = new JObject
                {
                    ["x"] = rot.x,
                    ["y"] = rot.y,
                    ["z"] = rot.z,
                    ["w"] = rot.w
                }
            };
        }

        static Vector3 ResolveOrigin(JObject data, Vector3 fallbackCenter, float radius)
        {
            var positions = new List<Vector3>();
            foreach (string cat in AtomicBuilderPaths.UnitCategories)
            {
                if (data[cat] is not JArray arr) continue;
                foreach (var token in arr)
                {
                    if (token is not JObject o) continue;
                    Vector3 p = AtomicBuilderMath.GetCoords(o);
                    if (AtomicBuilderMath.Dist3D(p, fallbackCenter) <= radius)
                        positions.Add(p);
                }
            }

            if (positions.Count == 0) return fallbackCenter;
            Vector3 sum = Vector3.zero;
            foreach (var p in positions) sum += p;
            return sum / positions.Count;
        }

        static JObject FilterToZone(JObject data, Vector3 origin, Vector3 filterCenter, float radius)
        {
            var blueprint = (JObject)data.DeepClone();
            foreach (string cat in AtomicBuilderPaths.UnitCategories)
            {
                if (blueprint[cat] is not JArray arr)
                {
                    blueprint[cat] = new JArray();
                    continue;
                }

                var kept = new JArray();
                foreach (var token in arr)
                {
                    if (token is not JObject o) continue;
                    Vector3 p = AtomicBuilderMath.GetCoords(o, o["SelectionPosition"] != null ? "SelectionPosition" : null);
                    if (AtomicBuilderMath.Dist3D(p, filterCenter) > radius) continue;

                    if (o["globalPosition"] != null)
                        AtomicBuilderMath.SetGlobalPosition(o, AtomicBuilderMath.Relative(p, origin));
                    else if (o["SelectionPosition"] != null)
                    {
                        AtomicBuilderMath.SetAirbasePositions(o, AtomicBuilderMath.Relative(p, origin));
                        if (o["Center"] != null)
                        {
                            Vector3 center = AtomicBuilderMath.GetCoords(o, "Center");
                            o["Center"] = AtomicBuilderMath.FormatCoords(AtomicBuilderMath.Relative(center, origin));
                        }
                    }
                    kept.Add(o);
                }
                blueprint[cat] = kept;
            }

            if (blueprint["objectives"] == null)
            {
                blueprint["objectives"] = new JObject
                {
                    ["Objectives"] = new JArray(),
                    ["Outcomes"] = new JArray()
                };
            }

            return blueprint;
        }

        static int CountUnits(JObject blueprint)
        {
            int n = 0;
            foreach (string cat in AtomicBuilderPaths.UnitCategories)
                if (blueprint[cat] is JArray arr) n += arr.Count;
            return n;
        }

        internal static Vector3 GetCaptureCenter()
        {
            var gf = UnityEngine.Object.FindObjectOfType<GroupFollowers>();
            if (gf?.CurrentUnits != null && gf.CurrentUnits.Count > 0)
            {
                Vector3 c = Vector3.zero;
                int n = 0;
                foreach (var u in gf.CurrentUnits)
                {
                    if (u == null) continue;
                    c += GetUnitWorldPosition(u);
                    n++;
                }
                if (n > 0) return c / n;
            }

            var editorUnits = UnityEngine.Object.FindObjectsOfType<Unit>(true)
                .Where(u => u?.SavedUnit != null)
                .ToList();
            if (editorUnits.Count > 0)
            {
                Vector3 c = Vector3.zero;
                foreach (var u in editorUnits)
                    c += GetUnitWorldPosition(u);
                return c / editorUnits.Count;
            }

            return PasteAtCursor.GetLocalPastePointOrFallback();
        }
    }
}
