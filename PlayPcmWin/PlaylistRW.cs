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

    public class PlaylistSave : WWXmlRW.SaveLoadContents {
        // SaveLoadContents IF
        public int GetCurrentVersion() { return CurrentVersion; }
        public int GetVersion() { return Version; }

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
            var xmlRW = new WWXmlRW.XmlRW<PlaylistSave>(m_fileName);
            PlaylistSave p = xmlRW.Load();

            // TODO: ロード後に、強制的に上書きしたいパラメータがある場合はここで上書きする。

            return p;
        }

        public static bool Save(PlaylistSave p) {
            var xmlRW = new WWXmlRW.XmlRW<PlaylistSave>(m_fileName);
            return xmlRW.Save(p);
        }
    }
}
