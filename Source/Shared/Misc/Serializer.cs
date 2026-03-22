using System;
using MessagePack;
using MessagePack.Resolvers;
using Newtonsoft.Json;
using System.IO;
using Shared.Misc;

namespace Shared
{
    public static class Serializer
    {
        private static JsonSerializerSettings DefaultSettings => new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.None
        };

        private static JsonSerializerSettings IndentedSettings => new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.None,
            Formatting = Formatting.Indented
        };

        public static byte[] ConvertObjectToBytes<T>(T toConvert, bool compression = true)
        {
            try
            {
                if (compression)
                    return MessagePackSerializer.Serialize(
                        toConvert,
                        ContractlessStandardResolver.Options.WithCompression(MessagePackCompression.Lz4Block));

                return MessagePackSerializer.Serialize(
                    toConvert,
                    ContractlessStandardResolver.Options);
            }
            catch (Exception e)
            {
                Printer.Error($"[Serializer] Failed to serialize object of type {typeof(T).FullName}\n{e}");
                throw;
            }
        }

        public static T ConvertBytesToObject<T>(byte[] bytes, bool compression = true)
        {
            if (bytes == null || bytes.Length == 0)
                return default;

            try
            {
                if (compression)
                {
                    return MessagePackSerializer.Deserialize<T>(
                        bytes,
                        ContractlessStandardResolver.Options.WithCompression(MessagePackCompression.Lz4Block));
                }

                return MessagePackSerializer.Deserialize<T>(
                    bytes,
                    ContractlessStandardResolver.Options);
            }
            catch (Exception e)
            {
                Printer.Error($"[Serializer] Failed to deserialize bytes into {typeof(T).FullName}\n{e}");
                throw;
            }
        }

        public static T ConvertBytesToObject<T>(ReadOnlyMemory<byte> bytes)
        {
            if (bytes.Length == 0)
                return default;

            try
            {
                return MessagePackSerializer.Deserialize<T>(
                    bytes,
                    ContractlessStandardResolver.Options.WithCompression(MessagePackCompression.Lz4Block));
            }
            catch (Exception e)
            {
                Printer.Error($"[Serializer] Failed to deserialize memory into {typeof(T).FullName}\n{e}");
                throw;
            }
        }

        public static string SerializeToString(object serializable)
        {
            try
            {
                return JsonConvert.SerializeObject(serializable, DefaultSettings);
            }
            catch (Exception e)
            {
                Printer.Error($"[Serializer] Failed to serialize object to string\n{e}");
                throw;
            }
        }

        public static T SerializeFromString<T>(string serializable)
        {
            if (string.IsNullOrWhiteSpace(serializable))
                return default;

            try
            {
                return JsonConvert.DeserializeObject<T>(serializable, DefaultSettings);
            }
            catch (Exception e)
            {
                Printer.Error($"[Serializer] Failed to deserialize string into {typeof(T).FullName}\n{e}");
                throw;
            }
        }

        public static void SerializeToFile(string path, object serializable)
        {
            try
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(serializable, IndentedSettings));
            }
            catch (Exception e)
            {
                Printer.Error($"[Serializer] Failed to write JSON file at {path}\n{e}");
                throw;
            }
        }

        public static T SerializeFromFile<T>(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return default;

                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), DefaultSettings);
            }
            catch (Exception e)
            {
                Printer.Error($"[Serializer] Failed to read JSON file at {path} into {typeof(T).FullName}\n{e}");
                throw;
            }
        }

        public static void ObjectBytesToFile(string path, object serializable)
        {
            try
            {
                File.WriteAllBytes(path, ConvertObjectToBytes(serializable));
            }
            catch (Exception e)
            {
                Printer.Error($"[Serializer] Failed to write binary file at {path}\n{e}");
                throw;
            }
        }

        public static T FileBytesToObject<T>(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return default;

                return ConvertBytesToObject<T>(File.ReadAllBytes(path));
            }
            catch (Exception e)
            {
                Printer.Error($"[Serializer] Failed to read binary file at {path} into {typeof(T).FullName}\n{e}");
                throw;
            }
        }
    }
}