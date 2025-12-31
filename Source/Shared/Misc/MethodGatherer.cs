using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Shared
{
    public static class MethodGatherer
    {
        public static Dictionary<PacketHeader, MethodInfo> ClientMethodDictionary { get; private set; }

        public static Dictionary<PacketHeader, MethodInfo> ServerMethodDictionary { get; private set; }

        public static MethodInfo[] OnStartMethods { get; private set; }

        public static MethodInfo[] OnEndMethods { get; private set; }

        public static MethodInfo[] PerFrameMethods { get; private set; }

        public enum AssemblyType { Client, Server }

        public static void CacheAllMethods(AssemblyType type)
        {
            if (type == AssemblyType.Client)
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(fetch => fetch.GetName().Name == "GameClient");
                MethodInfo[] clientMethods = GetPacketHandlerAttributes(GetAllGameTypes()).ToArray();
                ClientMethodDictionary = new Dictionary<PacketHeader, MethodInfo>();
                for (int i = 0; i < clientMethods.Length; i++)
                {
                    ClientMethodDictionary.Add(clientMethods[i].GetCustomAttribute<HandlesPacket>().header,
                        clientMethods[i]);
                }

                OnStartMethods = GetSessionStartMethods(GetAllGameTypes());
                OnEndMethods = GetSessionEndMethods(GetAllGameTypes());
                PerFrameMethods = GetPerFrameMethods(GetAllGameTypes());
            }

            else
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(fetch => fetch.GetName().Name == "GameServer");
                MethodInfo[] serverMethods = GetPacketHandlerAttributes((Type[])assembly.GetTypes().ToArray());
                ServerMethodDictionary = new Dictionary<PacketHeader, MethodInfo>();
                for (int i = 0; i < serverMethods.Length; i++)
                {
                    ServerMethodDictionary.Add(serverMethods[i].GetCustomAttribute<HandlesPacket>().header,
                        serverMethods[i]);
                }
            }
        }

        private static MethodInfo[] GetPacketHandlerAttributes(Type[] types)
        {
            List<MethodInfo> toAdd = new List<MethodInfo>();
            for (int x = 0; x < types.Length; x++)
            {
                toAdd.AddRange(types[x].GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                    .Where(fetch => fetch.GetCustomAttribute<HandlesPacket>() != null).ToList());
            }
            return toAdd.ToArray();
        }

        private static Type[] GetAllGameTypes()
        {
            List<Type> allTypes = new List<Type>();

            Assembly toUse = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(fetch => fetch.GetName().Name == "GameClient");
            allTypes.AddRange(toUse.GetTypes().ToList());

            toUse = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(fetch => fetch.GetName().Name == "Synchronous");
            allTypes.AddRange(toUse.GetTypes().ToList());

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
    }
}