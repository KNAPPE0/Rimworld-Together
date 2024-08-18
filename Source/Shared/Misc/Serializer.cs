using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.IO;

namespace Shared
{
    // Class that handles all of the mod's serialization functions
    public static class Serializer
    {
        private static JsonSerializerSettings DefaultSettings => new JsonSerializerSettings 
        { 
            TypeNameHandling = TypeNameHandling.None 
        };

        private static JsonSerializerSettings IndentedSettings => new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            Formatting = Formatting.Indented
        };

        // Serialize an object to a compressed byte array
        [Obsolete("Consider using a more modern serialization method or format.")]
        public static byte[] ConvertObjectToBytes(object toConvert)
        {
            if (toConvert == null) throw new ArgumentNullException(nameof(toConvert));

            var serializer = JsonSerializer.Create(DefaultSettings);
            using var memoryStream = new MemoryStream();
            using var writer = new BsonWriter(memoryStream);
            serializer.Serialize(writer, toConvert);

            return GZip.Compress(memoryStream.ToArray());
        }

        // Deserialize an object from a compressed byte array
        [Obsolete("Consider using a more modern deserialization method or format.")]
        public static T? ConvertBytesToObject<T>(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) throw new ArgumentNullException(nameof(bytes));

            bytes = GZip.Decompress(bytes);

            var serializer = JsonSerializer.Create(DefaultSettings);
            using var memoryStream = new MemoryStream(bytes);
            using var reader = new BsonReader(memoryStream);

            return serializer.Deserialize<T>(reader);
        }

        // Serialize an object to a JSON string
        public static string SerializeToString(object serializable)
        {
            if (serializable == null) throw new ArgumentNullException(nameof(serializable));

            return JsonConvert.SerializeObject(serializable, DefaultSettings);
        }

        // Deserialize an object from a JSON string
        public static T? SerializeFromString<T>(string serializable)
        {
            if (string.IsNullOrWhiteSpace(serializable)) throw new ArgumentNullException(nameof(serializable));

            return JsonConvert.DeserializeObject<T>(serializable, DefaultSettings);
        }

        // Serialize an object to a file
        public static void SerializeToFile(string path, object serializable)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            if (serializable == null) throw new ArgumentNullException(nameof(serializable));

            var json = JsonConvert.SerializeObject(serializable, IndentedSettings);
            File.WriteAllText(path, json);
        }

        // Deserialize an object from a file
        public static T? SerializeFromFile<T>(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json, DefaultSettings);
        }
    }
}