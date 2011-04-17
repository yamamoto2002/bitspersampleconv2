using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.IsolatedStorage;
using System.Xml.Serialization;

namespace PlayPcmWin {
    public class PlaylistItemSave {
        public string Title { get; set; }
        public string AlbumName { get; set; }
        public string ArtistName { get; set; }
        public string PathName { get; set; }
        public int CueSheetIndex { get; set; }
        public int StartTick { get; set; }
        public int EndTick { get; set; }
        public bool ReadSeparaterAfter { get; set; }

        public PlaylistItemSave() {
            Reset();
        }

        public void Reset() {
            Title = string.Empty;
            AlbumName = string.Empty;
            ArtistName = string.Empty;
            PathName = string.Empty;
            CueSheetIndex = 1;
            StartTick = 0;
            EndTick = -1;
            ReadSeparaterAfter = false;
        }

        public PlaylistItemSave Set(
                string title,
                string albumName,
                string artistName,
                string pathName,
                int cueSheetIndex,
                int startTick,
                int endTick,
                bool readSeparatorAfter) {
            Title = title;
            AlbumName = albumName;
            ArtistName = artistName;
            PathName = pathName;
            CueSheetIndex = cueSheetIndex;
            StartTick = startTick;
            EndTick = endTick;
            ReadSeparaterAfter = readSeparatorAfter;
            return this;
        }
    }

    public class PlaylistSave {
        public static readonly int CurrentVersion = 1;
        public int Version { get; set; }
        public int ItemNum { get { return Items.Count(); } }
        public List<PlaylistItemSave> Items;

        public void Reset() {
            Version = CurrentVersion;
            Items = new List<PlaylistItemSave>();
        }

        public PlaylistSave() {
            Reset();
        }

        public void Add(PlaylistItemSave item) {
            Items.Add(item);
        }
    }

    /// <summary>
    ///  @todo PreferenceStoreクラスと同じなので、1個にまとめる。
    /// </summary>
    class PlaylistRW {
        private static readonly string m_fileName = "PlayPcmWinPlayList.xml";

        public static PlaylistSave Load() {
            PlaylistSave p = new PlaylistSave();

            try {
                using (IsolatedStorageFileStream isfs = new IsolatedStorageFileStream(
                        m_fileName, System.IO.FileMode.Open,
                        IsolatedStorageFile.GetUserStoreForDomain())) {
                    byte[] buffer = new byte[isfs.Length];
                    isfs.Read(buffer, 0, (int)isfs.Length);
                    System.IO.MemoryStream stream = new System.IO.MemoryStream(buffer);
                    XmlSerializer formatter = new XmlSerializer(typeof(PlaylistSave));
                    p = formatter.Deserialize(stream) as PlaylistSave;
                    isfs.Close();
                }
            } catch (System.Exception ex) {
                Console.WriteLine(ex);
                p = new PlaylistSave();
            }

            if (PlaylistSave.CurrentVersion != p.Version) {
                Console.WriteLine("PlayList Version mismatch {0} != {1}", PlaylistSave.CurrentVersion, p.Version);
                p = new PlaylistSave();
            }

            return p;
        }

        public static bool Save(PlaylistSave p) {
            bool result = false;

            try {
                using (IsolatedStorageFileStream isfs = new IsolatedStorageFileStream(
                        m_fileName, System.IO.FileMode.Create,
                        IsolatedStorageFile.GetUserStoreForDomain())) {
                    XmlSerializer s = new XmlSerializer(typeof(PlaylistSave));
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
