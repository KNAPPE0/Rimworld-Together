using Shared.Files.Configs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Files
{
    public abstract class BaseFile
    {
        private static Semaphore SavingSemaphore { get; set; } = new Semaphore(1, 1);

        public static void Save(string savePath, object obj, bool inBytes = false)
        {
            SavingSemaphore.WaitOne();

            try 
            { 
                if (inBytes) Serializer.ObjectBytesToFile(savePath, obj);
                else Serializer.SerializeToFile(savePath, obj); 
            }
            catch (Exception e) { throw new Exception(e.ToString()); }

            SavingSemaphore.Release();
        }

        public static object Load<T>(string savePath, bool inBytes = false, bool generateIfNull = true)
        {
            if (File.Exists(savePath))
            {
                if (inBytes) return Serializer.FileBytesToObject<T>(savePath);
                else return Serializer.SerializeFromFile<T>(savePath);
            }

            else
            {
                if (!generateIfNull) return null;
                else
                {
                    object file = (T)Activator.CreateInstance(typeof(T));
                    Serializer.SerializeToFile(savePath, file);
                    return file;
                }
            }
        }
    }
}
