using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using NuclearOption.SavedMission.ObjectiveV2;
using NuclearOption.MissionEditorScripts;

namespace EditorPlus
{
    internal static class ReflectionUtils
    {
        private static readonly Dictionary<(Type srcType, Type elementType), MemberInfo[]> s_EnumerableMembersCache = new();
        private static readonly Dictionary<Type, FieldInfo> s_AllItemsFieldCache = new();
        
        // Compatibility: Get MissionObjectives via reflection since MissionManager.Objectives may not exist
        private static MissionObjectives _cachedMissionObjectives;
        private static Type _missionManagerType;
        private static PropertyInfo _missionManagerObjectivesProp;
        private static bool _hasLoggedNotFound = false;
        
        public static MissionObjectives GetMissionObjectives()
        {
            if (_cachedMissionObjectives != null) return _cachedMissionObjectives;
            
            // Try MissionManager first (original mod's approach)
            if (_missionManagerType == null)
            {
                // Try with full namespace first
                _missionManagerType = Type.GetType("NuclearOption.MissionManager, Assembly-CSharp");
                if (_missionManagerType == null)
                {
                    // Try without namespace (just "MissionManager")
                    _missionManagerType = Type.GetType("MissionManager, Assembly-CSharp");
                }
                if (_missionManagerType == null)
                {
                    // Search all assemblies for MissionManager
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        // Try with namespace
                        _missionManagerType = asm.GetType("NuclearOption.MissionManager");
                        if (_missionManagerType != null)
                        {
                            Plugin.Logger?.LogInfo($"[ReflectionUtils] Found MissionManager type (with namespace) in assembly: {asm.FullName}");
                            break;
                        }
                        // Try without namespace
                        _missionManagerType = asm.GetType("MissionManager");
                        if (_missionManagerType != null)
                        {
                            Plugin.Logger?.LogInfo($"[ReflectionUtils] Found MissionManager type (without namespace) in assembly: {asm.FullName}");
                            break;
                        }
                    }
                }
                else
                {
                    Plugin.Logger?.LogInfo($"[ReflectionUtils] Found MissionManager type via Type.GetType: {_missionManagerType.FullName}");
                }
            }
            
            if (_missionManagerType != null)
            {
                Plugin.Logger?.LogInfo($"[ReflectionUtils] MissionManager type found: {_missionManagerType.FullName}");
                
                // Log all static properties and fields for debugging
                if (!_hasLoggedNotFound)
                {
                    var staticProps = _missionManagerType.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    var staticFields = _missionManagerType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    Plugin.Logger?.LogInfo($"[ReflectionUtils] MissionManager has {staticProps.Length} static properties, {staticFields.Length} static fields");
                    foreach (var prop in staticProps)
                    {
                        Plugin.Logger?.LogInfo($"  - Static property: {prop.Name} ({prop.PropertyType.Name})");
                    }
                    foreach (var field in staticFields)
                    {
                        Plugin.Logger?.LogInfo($"  - Static field: {field.Name} ({field.FieldType.Name})");
                    }
                }
                
                // Try static property
                if (_missionManagerObjectivesProp == null)
                {
                    _missionManagerObjectivesProp = _missionManagerType.GetProperty("Objectives", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (_missionManagerObjectivesProp != null)
                {
                    try
                    {
                        _cachedMissionObjectives = _missionManagerObjectivesProp.GetValue(null) as MissionObjectives;
                        if (_cachedMissionObjectives != null)
                        {
                            _hasLoggedNotFound = false;
                            Plugin.Logger?.LogInfo("[ReflectionUtils] Found MissionObjectives via MissionManager.Objectives (static property)");
                            return _cachedMissionObjectives;
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger?.LogWarning($"[ReflectionUtils] Error accessing MissionManager.Objectives property: {ex.Message}");
                    }
                }
                
                // Try static field
                var staticField = _missionManagerType.GetField("Objectives", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (staticField != null)
                {
                    try
                    {
                        _cachedMissionObjectives = staticField.GetValue(null) as MissionObjectives;
                        if (_cachedMissionObjectives != null)
                        {
                            _hasLoggedNotFound = false;
                            Plugin.Logger?.LogInfo("[ReflectionUtils] Found MissionObjectives via MissionManager.Objectives (static field)");
                            return _cachedMissionObjectives;
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger?.LogWarning($"[ReflectionUtils] Error accessing MissionManager.Objectives field: {ex.Message}");
                    }
                }
                
                // Try instance property/field (if MissionManager has an Instance)
                var instanceProp = _missionManagerType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var instanceField = _missionManagerType.GetField("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object instance = null;
                try
                {
                    instance = instanceProp?.GetValue(null) ?? instanceField?.GetValue(null);
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogWarning($"[ReflectionUtils] Error accessing MissionManager.Instance: {ex.Message}");
                }
                
                if (instance != null)
                {
                    var instObjectivesProp = _missionManagerType.GetProperty("Objectives", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (instObjectivesProp != null)
                    {
                        try
                        {
                            _cachedMissionObjectives = instObjectivesProp.GetValue(instance) as MissionObjectives;
                            if (_cachedMissionObjectives != null)
                            {
                                _hasLoggedNotFound = false;
                                Plugin.Logger?.LogInfo("[ReflectionUtils] Found MissionObjectives via MissionManager.Instance.Objectives");
                                return _cachedMissionObjectives;
                            }
                        }
                        catch (Exception ex)
                        {
                            Plugin.Logger?.LogWarning($"[ReflectionUtils] Error accessing MissionManager.Instance.Objectives property: {ex.Message}");
                        }
                    }
                    var instObjectivesField = _missionManagerType.GetField("Objectives", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (instObjectivesField != null)
                    {
                        try
                        {
                            _cachedMissionObjectives = instObjectivesField.GetValue(instance) as MissionObjectives;
                            if (_cachedMissionObjectives != null)
                            {
                                _hasLoggedNotFound = false;
                                Plugin.Logger?.LogInfo("[ReflectionUtils] Found MissionObjectives via MissionManager.Instance.Objectives (field)");
                                return _cachedMissionObjectives;
                            }
                        }
                        catch (Exception ex)
                        {
                            Plugin.Logger?.LogWarning($"[ReflectionUtils] Error accessing MissionManager.Instance.Objectives field: {ex.Message}");
                        }
                    }
                }
            }
            
            // Fallback: try to get from MissionEditor
            var missionEditor = SceneSingleton<MissionEditor>.i;
            if (missionEditor != null)
            {
                var editorType = missionEditor.GetType();
                
                // Try MissionEditor.Objectives property
                var objectivesProp = editorType.GetProperty("Objectives", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (objectivesProp != null)
                {
                    _cachedMissionObjectives = objectivesProp.GetValue(missionEditor) as MissionObjectives;
                    if (_cachedMissionObjectives != null)
                    {
                        _hasLoggedNotFound = false;
                        Plugin.Logger?.LogInfo("[ReflectionUtils] Found MissionObjectives via MissionEditor.Objectives");
                        return _cachedMissionObjectives;
                    }
                }
                
                // Try MissionEditor.Objectives field
                var objectivesField = editorType.GetField("Objectives", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (objectivesField != null)
                {
                    _cachedMissionObjectives = objectivesField.GetValue(missionEditor) as MissionObjectives;
                    if (_cachedMissionObjectives != null)
                    {
                        _hasLoggedNotFound = false;
                        Plugin.Logger?.LogInfo("[ReflectionUtils] Found MissionObjectives via MissionEditor.Objectives (field)");
                        return _cachedMissionObjectives;
                    }
                }
                
                // Try to find Objectives in tabs (property)
                var tabsField = editorType.GetField("tabs", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (tabsField != null)
                {
                    var tabs = tabsField.GetValue(missionEditor);
                    if (tabs != null)
                    {
                        var tabsType = tabs.GetType();
                        var allProps = tabsType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var allFields = tabsType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var allMethods = tabsType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        
                        // Debug: Log ALL members of tabs type (once, when first searching)
                        if (!_hasLoggedNotFound)
                        {
                            Plugin.Logger?.LogInfo($"[ReflectionUtils] EditorTabs type: {tabsType.FullName}");
                            Plugin.Logger?.LogInfo($"[ReflectionUtils] EditorTabs has {allProps.Length} properties, {allFields.Length} fields, {allMethods.Length} methods");
                            
                            // Log ALL properties
                            Plugin.Logger?.LogInfo("[ReflectionUtils] All EditorTabs properties:");
                            foreach (var prop in allProps)
                            {
                                Plugin.Logger?.LogInfo($"  - {prop.Name}: {prop.PropertyType.FullName ?? prop.PropertyType.Name}");
                            }
                            
                            // Log ALL fields
                            Plugin.Logger?.LogInfo("[ReflectionUtils] All EditorTabs fields:");
                            foreach (var field in allFields)
                            {
                                Plugin.Logger?.LogInfo($"  - {field.Name}: {field.FieldType.FullName ?? field.FieldType.Name}");
                            }
                            
                            // Log ALL methods (first 50)
                            Plugin.Logger?.LogInfo("[ReflectionUtils] EditorTabs methods (first 50):");
                            foreach (var method in allMethods.Take(50))
                            {
                                Plugin.Logger?.LogInfo($"  - {method.Name}() -> {method.ReturnType.FullName ?? method.ReturnType.Name}");
                            }
                        }
                        
                        // Try property first
                        var tabsObjectivesProp = tabsType.GetProperty("Objectives", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (tabsObjectivesProp != null)
                        {
                            try
                            {
                                _cachedMissionObjectives = tabsObjectivesProp.GetValue(tabs) as MissionObjectives;
                                if (_cachedMissionObjectives != null)
                                {
                                    _hasLoggedNotFound = false;
                                    Plugin.Logger?.LogInfo("[ReflectionUtils] Found MissionObjectives via MissionEditor.tabs.Objectives");
                                    return _cachedMissionObjectives;
                                }
                            }
                            catch (Exception ex)
                            {
                                Plugin.Logger?.LogWarning($"[ReflectionUtils] Error accessing tabs.Objectives property: {ex.Message}");
                            }
                        }
                        
                        // Try field
                        var tabsObjectivesField = tabsType.GetField("Objectives", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (tabsObjectivesField != null)
                        {
                            try
                            {
                                _cachedMissionObjectives = tabsObjectivesField.GetValue(tabs) as MissionObjectives;
                                if (_cachedMissionObjectives != null)
                                {
                                    _hasLoggedNotFound = false;
                                    Plugin.Logger?.LogInfo("[ReflectionUtils] Found MissionObjectives via MissionEditor.tabs.Objectives (field)");
                                    return _cachedMissionObjectives;
                                }
                            }
                            catch (Exception ex)
                            {
                                Plugin.Logger?.LogWarning($"[ReflectionUtils] Error accessing tabs.Objectives field: {ex.Message}");
                            }
                        }
                        
                        // Try methods that return MissionObjectives
                        foreach (var method in allMethods)
                        {
                            if (method.ReturnType == typeof(MissionObjectives) && method.GetParameters().Length == 0)
                            {
                                try
                                {
                                    _cachedMissionObjectives = method.Invoke(tabs, null) as MissionObjectives;
                                    if (_cachedMissionObjectives != null)
                                    {
                                        _hasLoggedNotFound = false;
                                        Plugin.Logger?.LogInfo($"[ReflectionUtils] Found MissionObjectives via MissionEditor.tabs.{method.Name}()");
                                        return _cachedMissionObjectives;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Plugin.Logger?.LogWarning($"[ReflectionUtils] Error invoking tabs.{method.Name}(): {ex.Message}");
                                }
                            }
                        }
                        
                        // Try to find any property/field/method that returns MissionObjectives type
                        foreach (var prop in allProps)
                        {
                            if (prop.PropertyType == typeof(MissionObjectives) || 
                                (prop.PropertyType.IsClass && prop.PropertyType.Name.Contains("Objective")))
                            {
                                try
                                {
                                    var value = prop.GetValue(tabs);
                                    _cachedMissionObjectives = value as MissionObjectives;
                                    if (_cachedMissionObjectives != null)
                                    {
                                        _hasLoggedNotFound = false;
                                        Plugin.Logger?.LogInfo($"[ReflectionUtils] Found MissionObjectives via MissionEditor.tabs.{prop.Name} (type: {prop.PropertyType.Name})");
                                        return _cachedMissionObjectives;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Plugin.Logger?.LogWarning($"[ReflectionUtils] Error accessing tabs.{prop.Name}: {ex.Message}");
                                }
                            }
                        }
                        
                        foreach (var field in allFields)
                        {
                            if (field.FieldType == typeof(MissionObjectives) || 
                                (field.FieldType.IsClass && field.FieldType.Name.Contains("Objective")))
                            {
                                try
                                {
                                    var value = field.GetValue(tabs);
                                    _cachedMissionObjectives = value as MissionObjectives;
                                    if (_cachedMissionObjectives != null)
                                    {
                                        _hasLoggedNotFound = false;
                                        Plugin.Logger?.LogInfo($"[ReflectionUtils] Found MissionObjectives via MissionEditor.tabs.{field.Name} (field, type: {field.FieldType.Name})");
                                        return _cachedMissionObjectives;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Plugin.Logger?.LogWarning($"[ReflectionUtils] Error accessing tabs.{field.Name}: {ex.Message}");
                                }
                            }
                        }
                        
                        // Try to find ObjectiveEditorV2 or similar tab objects
                        foreach (var field in allFields)
                        {
                            if (field.FieldType.Name.Contains("Objective") && field.FieldType.Name.Contains("Editor"))
                            {
                                try
                                {
                                    var tabObj = field.GetValue(tabs);
                                    if (tabObj != null)
                                    {
                                        var tabObjType = tabObj.GetType();
                                        var objProp = tabObjType.GetProperty("Objectives", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                        var objField = tabObjType.GetField("Objectives", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                        
                                        if (objProp != null)
                                        {
                                            _cachedMissionObjectives = objProp.GetValue(tabObj) as MissionObjectives;
                                            if (_cachedMissionObjectives != null)
                                            {
                                                _hasLoggedNotFound = false;
                                                Plugin.Logger?.LogInfo($"[ReflectionUtils] Found MissionObjectives via MissionEditor.tabs.{field.Name}.Objectives");
                                                return _cachedMissionObjectives;
                                            }
                                        }
                                        
                                        if (objField != null)
                                        {
                                            _cachedMissionObjectives = objField.GetValue(tabObj) as MissionObjectives;
                                            if (_cachedMissionObjectives != null)
                                            {
                                                _hasLoggedNotFound = false;
                                                Plugin.Logger?.LogInfo($"[ReflectionUtils] Found MissionObjectives via MissionEditor.tabs.{field.Name}.Objectives (field)");
                                                return _cachedMissionObjectives;
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Plugin.Logger?.LogWarning($"[ReflectionUtils] Error accessing tabs.{field.Name}: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            
            if (!_hasLoggedNotFound)
            {
                Plugin.Logger?.LogWarning("[ReflectionUtils] Could not find MissionObjectives (will retry)");
                _hasLoggedNotFound = true;
            }
            return null;
        }

        /// <summary>Clear cached MissionObjectives so next access re-resolves (call when leaving mission editor scene).</summary>
        public static void ClearMissionObjectivesCache()
        {
            _cachedMissionObjectives = null;
        }
        
        public static List<Objective> GetAllObjectives()
        {
            var mo = GetMissionObjectives();
            return mo?.AllObjectives ?? new List<Objective>();
        }
        
        public static List<Outcome> GetAllOutcomes()
        {
            var mo = GetMissionObjectives();
            return mo?.AllOutcomes ?? new List<Outcome>();
        }
        
        public static IEnumerable<T> EnumerateFromObject<T>(object src)
        {
            if (src == null) yield break;

            Type t = src.GetType();
            if (!s_EnumerableMembersCache.TryGetValue((t, typeof(T)), out MemberInfo[] members))
            {
                const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                List<MemberInfo> list = [];

                foreach (FieldInfo f in t.GetFields(F))
                    if (IsEnumerableOf<T>(f.FieldType)) list.Add(f);

                foreach (PropertyInfo p in t.GetProperties(F))
                    if (p.CanRead && p.GetIndexParameters().Length == 0 && IsEnumerableOf<T>(p.PropertyType)) list.Add(p);

                members = [.. list];
                s_EnumerableMembersCache[(t, typeof(T))] = members;
            }

            foreach (MemberInfo m in members)
            {
                object v = m is FieldInfo f ? f.GetValue(src)
                           : m is PropertyInfo p ? p.GetValue(src, null)
                           : null;

                if (v == null) continue;

                if (v is IEnumerable<T> typed) { foreach (T e in typed) if (e != null) yield return e; }
                else if (v is IEnumerable any) { foreach (object e in any) if (e is T te) yield return te; }
            }

            static bool IsEnumerableOf<TElem>(Type ft)
            {
                if (typeof(IEnumerable<TElem>).IsAssignableFrom(ft)) return true;
                if (!typeof(IEnumerable).IsAssignableFrom(ft)) return false;
                if (ft.IsGenericType)
                {
                    Type[] ga = ft.GetGenericArguments();
                    if (ga.Length == 1 && typeof(TElem).IsAssignableFrom(ga[0])) return true;
                }
                return false;
            }
        }

        public static FieldInfo FindFieldRecursive(Type t, string name)
        {
            if (t == null) return null;
            if (!s_AllItemsFieldCache.TryGetValue(t, out FieldInfo fi))
            {
                const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                fi = t.GetField(name, F) ?? FindFieldRecursive(t.BaseType, name);
                s_AllItemsFieldCache[t] = fi;
            }
            return fi;
        }

        public static object GetPropOrFieldValue(object obj, string name)
        {
            if (obj == null) return null;
            Type t = obj.GetType();
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            return t.GetProperty(name, F)?.GetValue(obj, null) ?? t.GetField(name, F)?.GetValue(obj);
        }
    }
}
