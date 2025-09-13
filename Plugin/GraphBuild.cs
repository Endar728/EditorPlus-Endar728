using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NuclearOption.SavedMission.ObjectiveV2.Objectives;
using NuclearOption.SavedMission.ObjectiveV2.Outcomes;
using NuclearOption.SavedMission.ObjectiveV2;
using NuclearOption.SavedMission;
using System.Linq;
using UnityEngine;

namespace EditorPlus
{
    public sealed partial class Plugin
    {
        private void RebuildGraph()
        {

            Logger.LogDebug("[Graph] Rebuild start");

            MissionObjectives mo = MissionManager.Objectives;
            if (mo == null) return;

            GraphView.ObjectiveDTO[] objectives = [.. mo.AllObjectives.Select(o => new GraphView.ObjectiveDTO
            {
                Id = o.SavedObjective.UniqueName,
                UniqueName = o.SavedObjective.UniqueName,
                DisplayName = o.SavedObjective.DisplayName,
                TypeName = o.SavedObjective.Type.ToString(),
                Hidden = o.SavedObjective.Hidden,
                OutcomeCount = 0,
                Layer = 0,
                Row = 0,
                FactionName = (o as IHasFaction)?.FactionName
            })];

            GraphView.OutcomeDTO[] outcomes = [.. mo.AllOutcomes.Select(uc => new GraphView.OutcomeDTO
            {
                Id = uc.SavedOutcome.UniqueName,
                UniqueName = uc.SavedOutcome.UniqueName,
                TypeName = uc.SavedOutcome.Type.ToString(),
                UsedByCount = 0,
                Layer = 0,
                Row = 0
            })];

            List<GraphView.LinkDTO> links = [];
            foreach (Objective o in mo.AllObjectives)
            {
                string oid = o.SavedObjective.UniqueName;
                foreach (Outcome oc in o.Outcomes)
                    links.Add(new GraphView.LinkDTO { FromId = oid, FromIsObjective = true, ToId = oc.SavedOutcome.UniqueName, ToIsObjective = false });
            }
            foreach (Outcome oc in mo.AllOutcomes)
            {
                string uid = oc.SavedOutcome.UniqueName;
                foreach (Objective nextObj in ReflectObjectives(oc))
                    links.Add(new GraphView.LinkDTO { FromId = uid, FromIsObjective = false, ToId = nextObj.SavedObjective.UniqueName, ToIsObjective = true });
            }

            _view.CanOutcomeHaveOutputs = (outcomeId) =>
            {
                Outcome oc = mo.AllOutcomes.FirstOrDefault(x => x.SavedOutcome.UniqueName == outcomeId);
                return OutcomeTypeSupportsOutputs(oc);
            };

            _view.BuildGraph(new GraphView.GraphData
            {
                Objectives = objectives,
                Outcomes = outcomes,
                Links = [.. links]
            }, computeLayout: true, snapshotAsFull: true);

            Logger.LogDebug($"[Graph] Rebuild done: objectives={objectives.Length}, outcomes={outcomes.Length}, links={links.Count}");
        }
        private static IEnumerable<Objective> ReflectObjectives(Outcome outc)
        {
            if (outc is StartObjectiveOutcome so)
                return so.objectivesToStart ?? (IEnumerable<Objective>)[];

            if (outc.SavedOutcome.Type == OutcomeType.StopOrCompleteObjective)
                return GetCompleteList(outc) ?? (IEnumerable<Objective>)[];

            return [];
        }
        private static bool OutcomeTypeSupportsOutputs(Outcome outc)
        {
            if (outc == null) return false;
            if (outc is StartObjectiveOutcome) return true;
            return outc.SavedOutcome.Type == OutcomeType.StopOrCompleteObjective;
        }
        private static IEnumerable<Func<Vector3>> EnumerateUnitWorldGetters(string uniqueId, bool isObjective)
        {
            MissionObjectives mo = MissionManager.Objectives;
            if (mo == null) yield break;

            if (isObjective)
            {
                Objective obj = mo.AllObjectives.FirstOrDefault(o => o.SavedObjective.UniqueName == uniqueId);
                if (obj == null) yield break;

                FieldInfo fi = ReflectionUtils.FindFieldRecursive(obj.GetType(), "allItems");
                if (fi != null && fi.GetValue(obj) is IEnumerable items)
                {
                    foreach (object it in items)
                    {
                        if (it is SavedUnit su && su != null)
                        {
                            SavedUnit suLocal = su;
                            yield return () => suLocal.globalPosition.AsVector3() + Datum.originPosition;
                            continue;
                        }

                        if (it is SavedAirbase ab && ab != null)
                        {
                            SavedAirbase abLocal = ab;
                            yield return () => abLocal.Center.AsVector3() + Datum.originPosition;
                            continue;
                        }

                        if (it is Waypoint wp && wp != null)
                        {
                            Waypoint wpLocal = wp;
                            yield return () => wpLocal.GlobalPosition.Value.AsVector3() + Datum.originPosition;
                            continue;
                        }
                    }
                }

                yield break;
            }

            Outcome ow = mo.AllOutcomes.FirstOrDefault(oc => oc.SavedOutcome.UniqueName == uniqueId);
            if (ow == null) yield break;

            foreach (SavedUnit su in ReflectionUtils.EnumerateFromObject<SavedUnit>(ow))
            {
                SavedUnit local = su;
                yield return () => local.globalPosition.AsVector3() + Datum.originPosition;
            }

            object saved = ReflectionUtils.GetPropOrFieldValue(ow, "SavedOutcome");
            foreach (SavedUnit su in ReflectionUtils.EnumerateFromObject<SavedUnit>(saved))
            {
                SavedUnit local = su;
                yield return () => local.globalPosition.AsVector3() + Datum.originPosition;
            }

        }
        private static List<Objective> GetCompleteList(Outcome outc, bool createIfMissing = false)
        {

            Type t = outc.GetType();
            if (!_completeObjListField.TryGetValue(t, value: out _))
            {
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                FieldInfo fi = t.GetField("objectivesToStart", BF);
                _completeObjListField[t] = fi;
            }

            List<Objective> list = (List<Objective>)_completeObjListField[t]?.GetValue(outc);
            if (list == null && createIfMissing && _completeObjListField[t] != null)
            {
                list = [];
                _completeObjListField[t].SetValue(outc, list);
            }
            return list;
        }
        private static bool TryAddObjectiveReferenceToOutcome(Outcome outc, Objective obj)
        {
            if (outc is StartObjectiveOutcome so)
            {
                so.objectivesToStart ??= [];
                if (so.objectivesToStart.Contains(obj)) return false;
                so.objectivesToStart.Add(obj);
                return true;
            }

            if (outc.SavedOutcome.Type == OutcomeType.StopOrCompleteObjective)
            {
                List<Objective> list = GetCompleteList(outc, createIfMissing: true);
                if (list.Contains(obj)) return false;
                list.Add(obj);
                return true;
            }

            return false;
        }
        private static bool RemoveObjectiveReferenceFromOutcome(Outcome outc, Objective obj)
        {
            if (outc is StartObjectiveOutcome so)
                return so.objectivesToStart != null && so.objectivesToStart.Remove(obj);

            if (outc.SavedOutcome.Type == OutcomeType.StopOrCompleteObjective)
            {
                List<Objective> list = GetCompleteList(outc);
                return list != null && list.Remove(obj);
            }

            return false;
        }

    }
}
