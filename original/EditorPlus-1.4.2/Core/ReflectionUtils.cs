using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace EditorPlus
{
    internal static class ReflectionUtils
    {
        private static readonly Dictionary<(Type srcType, Type elementType), MemberInfo[]> s_EnumerableMembersCache = new();
        private static readonly Dictionary<Type, FieldInfo> s_AllItemsFieldCache = new();
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
                fi = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                     ?? FindFieldRecursive(t.BaseType, name);
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
