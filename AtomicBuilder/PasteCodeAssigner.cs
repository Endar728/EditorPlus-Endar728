using Newtonsoft.Json.Linq;

namespace EditorPlus.AtomicBuilder
{
    /// <summary>Ports Atomic Builder paste-code suffix logic so pasted blueprints do not collide with existing objectives/units.</summary>
    internal static class PasteCodeAssigner
    {
        internal static void Apply(JObject blueprint, int pasteCode)
        {
            string suffix = $"_ATOM_PASTE_{pasteCode}";

            foreach (string cat in AtomicBuilderPaths.UnitCategories)
            {
                if (blueprint[cat] is not JArray arr) continue;
                foreach (var token in arr)
                {
                    if (token is not JObject o) continue;
                    if (o["UniqueName"] is JValue un)
                        o["UniqueName"] = un.ToString() + suffix;
                    if (o["Airbase"] is JValue ab && !string.IsNullOrEmpty(ab.ToString()))
                        o["Airbase"] = ab.ToString() + suffix;
                }
            }

            if (blueprint["objectives"]?["Objectives"] is JArray objectives)
            {
                foreach (var token in objectives)
                {
                    if (token is not JObject obj) continue;
                    RenameObjectiveData(obj, suffix);
                    if (obj["UniqueName"]?.ToString() == "Mission Start") continue;
                    if (obj["UniqueName"] is JValue oun)
                        obj["UniqueName"] = oun.ToString() + suffix;
                }
            }

            if (blueprint["objectives"]?["Outcomes"] is JArray outcomes)
            {
                foreach (var token in outcomes)
                {
                    if (token is not JObject o) continue;
                    if (o["UniqueName"] is JValue un)
                        o["UniqueName"] = un.ToString() + suffix;
                    RenameObjectiveData(o, suffix);
                }
            }
        }

        static void RenameObjectiveData(JObject obj, string suffix)
        {
            if (obj["Data"] is not JArray data) return;
            foreach (var d in data)
            {
                if (d is not JObject row || row["StringValue"] is not JValue sv) continue;
                string s = sv.ToString();
                if (s.StartsWith("Boscali") || s.StartsWith("Primeva") || string.IsNullOrEmpty(s)) continue;
                row["StringValue"] = s + suffix;
            }

            if (obj["Outcomes"] is JArray outs)
            {
                var renamed = new JArray();
                foreach (var o in outs)
                    renamed.Add(o?.ToString() + suffix);
                obj["Outcomes"] = renamed;
            }
        }
    }
}
