using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static Shared.CommonEnumerators;

namespace Shared
{
    public static class MethodGatherer
    {
        public static MethodInfo[] OnStartMethods { get; private set; } = null;

        public static MethodInfo[] OnEndMethods { get; private set; } = null;

        public static MethodInfo[] PerFrameMethods { get; private set; } = null;

        public static MethodInfo[] OnSynchronousStartMethods { get; private set; } = null;

        public static MethodInfo[] OnSynchronousEndMethods { get; private set; } = null;

        public static Dictionary<PacketHeader, object[]> ClientMethodDictionary { get; set; } = new();

        public static Dictionary<PacketHeader, object[]> ServerMethodDictionary { get; set; } = new();

        public static void CacheAllMethods(CommonEnumerators.AssemblyType assembly)
        {
            if (assembly == CommonEnumerators.AssemblyType.Client)
            {
                OnStartMethods = GetSessionStartMethods(GetAllGameTypes());
                OnEndMethods = GetSessionEndMethods(GetAllGameTypes());
                PerFrameMethods = GetPerFrameMethods(GetAllGameTypes());
                OnSynchronousStartMethods = GetSynchronousStartMethods(GetAllGameTypes());
                OnSynchronousEndMethods = GetSynchronousEndMethods(GetAllGameTypes());
            }
        }

        public static void CacheAllPackets(CommonEnumerators.AssemblyType assembly)
        {
            foreach (Type type in Assembly.GetCallingAssembly().GetTypes().Where(fetch => fetch.GetCustomAttribute<ManagesPacket>() != null))
            {
                MethodInfo method = type.GetMethod("Receive", BindingFlags.Instance | BindingFlags.Public);
                HandlesPacket attribute = method.GetCustomAttribute<HandlesPacket>();
                if (attribute != null) AddMethod(attribute.header, type, method, assembly);
            }

            if (assembly == CommonEnumerators.AssemblyType.Server) Printer.Title($"----------------------------------------", LogImportanceMode.Extreme);
        }

        private static Type[] GetAllGameTypes()
        {
            List<Type> allTypes = new List<Type>();

            Assembly toUse = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(fetch => fetch.GetName().Name == "GameClient");
            if (toUse != null) allTypes.AddRange(toUse.GetTypes().ToList());
            try
            {
                toUse = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(fetch => fetch.GetName().Name == "Synchronous");
                allTypes.AddRange(toUse.GetTypes().ToList());
            }
            catch { }
            return allTypes.ToArray();
        }

        private static MethodInfo[] GetSessionStartMethods(Type[] types)
        {
            List<MethodInfo> toAdd = new List<MethodInfo>();
            for (int x = 0; x < types.Length; x++)
            {
                toAdd.AddRange(types[x].GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                    .Where(fetch => fetch.GetCustomAttribute<OnSessionStart>() != null).ToList());
            }

            return toAdd.ToArray();
        }

        private static MethodInfo[] GetSessionEndMethods(Type[] types)
        {
            List<MethodInfo> toAdd = new List<MethodInfo>();
            for (int x = 0; x < types.Length; x++)
            {
                toAdd.AddRange(types[x].GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                    .Where(fetch => fetch.GetCustomAttribute<OnSessionEnd>() != null).ToList());
            }

            return toAdd.ToArray();
        }

        private static MethodInfo[] GetPerFrameMethods(Type[] types)
        {
            List<MethodInfo> toAdd = new List<MethodInfo>();
            for (int x = 0; x < types.Length; x++)
            {
                toAdd.AddRange(types[x].GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                    .Where(fetch => fetch.GetCustomAttribute<OnUpdate>() != null).ToList());
            }

            return toAdd.ToArray();
        }

        private static MethodInfo[] GetSynchronousStartMethods(Type[] types)
        {
            List<MethodInfo> toAdd = new List<MethodInfo>();
            for (int x = 0; x < types.Length; x++)
            {
                toAdd.AddRange(types[x].GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                    .Where(fetch => fetch.GetCustomAttribute<OnSynchronousStart>() != null).ToList());
            }

            return toAdd.ToArray();
        }

        private static MethodInfo[] GetSynchronousEndMethods(Type[] types)
        {
            List<MethodInfo> toAdd = new List<MethodInfo>();
            for (int x = 0; x < types.Length; x++)
            {
                toAdd.AddRange(types[x].GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                    .Where(fetch => fetch.GetCustomAttribute<OnSynchronousEnd>() != null).ToList());
            }

            return toAdd.ToArray();
        }

        private static void AddMethod(PacketHeader header, Type type, MethodInfo method, CommonEnumerators.AssemblyType assembly)
        {
            if (assembly == CommonEnumerators.AssemblyType.Server)
            {
                Printer.Warning($"Adding {method.DeclaringType.FullName}.{method.Name} to the packet cache", CommonEnumerators.LogImportanceMode.Extreme);
            }

            switch (assembly)
            {
                case CommonEnumerators.AssemblyType.Client:
                    try { MethodGatherer.ClientMethodDictionary.Add(header, new object[] { Activator.CreateInstance(type), method }); }
                    catch (Exception ex) { Printer.Error(ex); }
                    break;

                case CommonEnumerators.AssemblyType.Server:
                    try { MethodGatherer.ServerMethodDictionary.Add(header, new object[] { Activator.CreateInstance(type), method }); }
                    catch (Exception ex) { Printer.Error(ex); }
                    break;
            }
        }
    }
}