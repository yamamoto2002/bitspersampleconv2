using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.IsolatedStorage;
using System.Xml.Serialization;

namespace WWXmlRW {
    public interface SaveLoadContents {
        int GetVersion();
        int GetCurrentVersion();

    }

    /// <summary>
    /// TをXML形式でセーブロードする
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class XmlRW<T> where T : class, SaveLoadContents, new() {

        private readonly string m_fileName;

        public XmlRW(string fileName) {
            m_fileName = fileName;
        }

        public T Load() {
            T p = new T();

            try {
                using (IsolatedStorageFileStream isfs = new IsolatedStorageFileStream(
                        m_fileName, System.IO.FileMode.Open,
                        IsolatedStorageFile.GetUserStoreForDomain())) {
                    byte[] buffer = new byte[isfs.Length];
                    isfs.Read(buffer, 0, (int)isfs.Length);
                    System.IO.MemoryStream stream = new System.IO.MemoryStream(buffer);
                    XmlSerializer formatter = new XmlSerializer(typeof(T));
                    p = formatter.Deserialize(stream) as T;
                    isfs.Close();
                }
            } catch (System.Exception ex) {
                Console.WriteLine(ex);
                p = new T();
            }

            if (p.GetCurrentVersion() != p.GetVersion()) {
                Console.WriteLine("Version mismatch {0} != {1}", p.GetCurrentVersion(), p.GetVersion());
                p = new T();
            }

            return p;
        }

        public bool Save(T p) {
            bool result = false;

            try {
                using (IsolatedStorageFileStream isfs = new IsolatedStorageFileStream(
                        m_fileName, System.IO.FileMode.Create,
                        IsolatedStorageFile.GetUserStoreForDomain())) {
                    XmlSerializer s = new XmlSerializer(typeof(T));
                    s.Serialize(isfs, p);
                    result = true;
                }
            } catch (System.Exception ex) {
                Console.WriteLine(ex.ToString());
            }

            return result;
        }
    }
}
