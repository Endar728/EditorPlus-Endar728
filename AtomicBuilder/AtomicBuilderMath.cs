using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EditorPlus.AtomicBuilder
{
    internal static class AtomicBuilderMath
    {
        internal static float Dist3D(Vector3 a, Vector3 b) => Vector3.Distance(a, b);

        internal static Vector3 GetCoords(JObject obj, string key = null)
        {
            if (obj == null) return Vector3.zero;
            JToken pos = key != null ? obj[key] : (obj["globalPosition"] ?? obj["SelectionPosition"]);
            if (pos is JObject o)
                return new Vector3(o.Value<float>("x"), o.Value<float>("y"), o.Value<float>("z"));
            return Vector3.zero;
        }

        internal static JObject FormatCoords(Vector3 c) =>
            new JObject { ["x"] = c.x, ["y"] = c.y, ["z"] = c.z };

        internal static Vector3 Relative(Vector3 coords, Vector3 origin) => coords - origin;

        internal static void SetGlobalPosition(JObject obj, Vector3 world)
        {
            obj["globalPosition"] = FormatCoords(world);
        }

        internal static void SetAirbasePositions(JObject obj, Vector3 world)
        {
            var coords = FormatCoords(world);
            obj["SelectionPosition"] = coords;
            obj["Center"] = coords;
        }

        internal static Quaternion GetRotation(JObject obj)
        {
            if (obj?["rotation"] is not JObject r) return Quaternion.identity;
            return new Quaternion(
                r.Value<float>("x"), r.Value<float>("y"),
                r.Value<float>("z"), r.Value<float>("w"));
        }

        internal static int GetNextPasteCode(JObject missionRoot)
        {
            int code = 0;
            foreach (string cat in AtomicBuilderPaths.UnitCategories)
            {
                if (missionRoot[cat] is not JArray arr) continue;
                foreach (var token in arr)
                {
                    if (token is not JObject o) continue;
                    code = Math.Max(code, ParseTrailingNumber(o.Value<string>("UniqueName")));
                    if (o["Airbase"] is JValue ab)
                        code = Math.Max(code, ParseTrailingNumber(ab.ToString()));
                }
            }

            if (missionRoot["objectives"]?["Objectives"] is JArray objectives)
            {
                foreach (var token in objectives)
                {
                    if (token is not JObject o) continue;
                    code = Math.Max(code, ParseTrailingNumber(o.Value<string>("UniqueName")));
                    if (o["Outcomes"] is JArray outs)
                    {
                        foreach (var outName in outs)
                            code = Math.Max(code, ParseTrailingNumber(outName?.ToString()));
                    }
                }
            }

            if (missionRoot["objectives"]?["Outcomes"] is JArray outcomes)
            {
                foreach (var token in outcomes)
                {
                    if (token is JObject o)
                        code = Math.Max(code, ParseTrailingNumber(o.Value<string>("UniqueName")));
                }
            }

            return code + 1;
        }

        static int ParseTrailingNumber(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            string[] parts = name.Split('_');
            string last = parts[parts.Length - 1];
            return int.TryParse(last, out int n) ? n : 0;
        }
    }
}
