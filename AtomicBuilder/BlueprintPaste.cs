using System;

using System.Collections;

using System.IO;

using Newtonsoft.Json.Linq;

using NuclearOption.MissionEditorScripts;

using UnityEngine;



namespace EditorPlus.AtomicBuilder

{

    internal static class BlueprintPaste

    {

        internal static bool TryPasteBlueprint(string blueprintName, Vector3 pasteCenterWorld, out string message)

        {

            message = null;

            string path = AtomicBuilderPaths.BlueprintFile(blueprintName);

            if (!File.Exists(path))

            {

                message = $"Blueprint not found: {blueprintName}";

                return false;

            }



            if (Plugin.Instance == null)

            {

                message = "Plugin not ready.";

                return false;

            }



            Plugin.Instance.StartCoroutine(PasteCoroutine(path, pasteCenterWorld, blueprintName));

            message = $"Pasting \"{blueprintName}\"…";

            return true;

        }



        static IEnumerator PasteCoroutine(string path, Vector3 pasteCenterWorld, string displayName)

        {

            CopyPasteInputHandler.SetPasting(true);

            int spawned = 0;

            int skipped = 0;

            int totalEntries = 0;

            string status = "";



            try

            {

                JObject blueprint = null;

                MissionEditor editor = null;

                Spawner spawner = null;

                GlobalPosition pasteAnchorGlobal = default;

                int pasteCode = 0;

                string setupError = null;



                try

                {

                    blueprint = JObject.Parse(File.ReadAllText(path));

                    editor = SceneSingleton<MissionEditor>.i;

                    spawner = NetworkSceneSingleton<Spawner>.i;

                    if (editor == null || spawner == null)

                        setupError = "Paste failed: editor not ready.";

                    else

                    {

                        pasteCode = 100 + UnityEngine.Random.Range(1, 9000);

                        string missionPath = AtomicBuilderPaths.TryGetCurrentMissionJsonPath();

                        if (!string.IsNullOrEmpty(missionPath))

                        {

                            try

                            {

                                var missionRoot = JObject.Parse(File.ReadAllText(missionPath));

                                pasteCode = AtomicBuilderMath.GetNextPasteCode(missionRoot);

                            }

                            catch { /* keep random fallback */ }

                        }



                        PasteCodeAssigner.Apply(blueprint, pasteCode);



                        try { pasteAnchorGlobal = pasteCenterWorld.ToGlobalPosition(); }

                        catch { pasteAnchorGlobal = new GlobalPosition(pasteCenterWorld); }

                    }

                }

                catch (Exception ex)

                {

                    setupError = "Paste failed: " + ex.Message;

                    Plugin.Logger?.LogError($"[AtomicBuilder] {setupError}\n{ex}");

                }



                if (setupError != null)

                {

                    status = setupError;

                    yield break;

                }



                totalEntries = CountBlueprintEntries(blueprint);

                if (totalEntries == 0)

                {

                    status = $"Blueprint \"{displayName}\" has no spawnable units.";

                    Plugin.Logger?.LogWarning($"[AtomicBuilder] {status}");

                    yield break;

                }



                const int unitsPerFrame = 24;

                int frameBudget = 0;

                int sincePhysicsSync = 0;



                foreach (string cat in AtomicBuilderPaths.UnitCategories)

                {

                    if (blueprint[cat] is not JArray arr) continue;

                    foreach (var token in arr)

                    {

                        if (token is not JObject entry) continue;

                        if (BlueprintSpawner.TrySpawnEntry(editor, spawner, entry, pasteAnchorGlobal, out _, syncPhysics: false))

                        {

                            spawned++;

                            sincePhysicsSync++;

                        }

                        else

                            skipped++;



                        frameBudget++;

                        if (frameBudget >= unitsPerFrame)

                        {

                            frameBudget = 0;

                            yield return null;

                        }



                        if (sincePhysicsSync >= 48)

                        {

                            Physics.SyncTransforms();

                            sincePhysicsSync = 0;

                        }

                    }

                }



                if (sincePhysicsSync > 0)

                    Physics.SyncTransforms();



                TryMergeObjectives(blueprint);

#pragma warning disable CS0618

                SceneSingleton<MissionEditor>.i?.CheckAutoSave();

#pragma warning restore CS0618



                status = BuildPasteStatus(displayName, spawned, skipped, totalEntries, pasteCode);

                Plugin.Logger?.LogInfo($"[AtomicBuilder] {status}");

            }

            finally

            {

                CopyPasteInputHandler.SetPasting(false);

                if (!string.IsNullOrEmpty(status))

                    AtomicBuilderUI.SetStatus(status);

            }

        }



        static int CountBlueprintEntries(JObject blueprint)

        {

            int count = 0;

            foreach (string cat in AtomicBuilderPaths.UnitCategories)

            {

                if (blueprint[cat] is JArray arr)

                    count += arr.Count;

            }

            return count;

        }



        static string BuildPasteStatus(string displayName, int spawned, int skipped, int total, int pasteCode)

        {

            if (spawned == 0)

            {

                return skipped > 0

                    ? $"Paste \"{displayName}\": 0/{total} units (code {pasteCode}). Check logs for unknown types."

                    : $"Paste \"{displayName}\": no units spawned (code {pasteCode}).";

            }



            if (skipped > 0)

                return $"Pasted \"{displayName}\": {spawned}/{total} units, {skipped} skipped (code {pasteCode}).";



            return $"Pasted \"{displayName}\": {spawned} units (code {pasteCode}).";

        }



        static void TryMergeObjectives(JObject blueprint)

        {

            var mo = ReflectionUtils.GetMissionObjectives();

            if (mo == null || blueprint["objectives"] == null) return;



            Plugin.Logger?.LogInfo("[AtomicBuilder] Objective merge skipped in v1.6 — spawn units only. Use graph editor for objective links.");

        }



        internal static Vector3 GetPasteCenter() => PasteAtCursor.GetLocalPastePointOrFallback();

    }

}


