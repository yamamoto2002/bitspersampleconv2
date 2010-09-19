// 日本語 UTF-8
// CUEシートを読む。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace PlayPcmWin {
    class CueSheetTrackInfo {
        public string path;
        public string title;
        public int    trackId;   // TRACK 10 ==> 10
        public int    startTick; // *75 seconds
        public int    endTick;   // -1: till the end of file
        public int    indexId;   // INDEX 00 ==> 0, INDEX 01 ==> 1
        public bool   preKokomade;

        public void Clear() {
            path = "";
            title = "NO TITLE";
            trackId = 0;
            startTick = 0;
            endTick = -1;
            indexId = -1;
            preKokomade = false;
        }

        public void CopyFrom(CueSheetTrackInfo rhs) {
            path      = rhs.path;
            title     = rhs.title;
            trackId   = rhs.trackId;
            startTick = rhs.startTick;
            endTick   = rhs.endTick;
            indexId   = rhs.indexId;
            preKokomade = rhs.preKokomade;
        }

        private static bool TimeStrToInt(string timeStr, out int timeInt) {
            string s = String.Copy(timeStr);
            if (2 <= s.Length && s[0] == '0') {
                s = s.Substring(1);
            }
            return int.TryParse(s, out timeInt);
        }

        public static int TickStrToInt(string tickStr) {
            string[] msf = tickStr.Split(':');
            if (msf.Length != 3) {
                return -1;
            }

            int m;
            if (!TimeStrToInt(msf[0], out m)) {
                return -1;
            }
            int s;
            if (!TimeStrToInt(msf[1], out s)) {
                return -1;
            }
            int f;
            if (!TimeStrToInt(msf[2], out f)) {
                return -1;
            }

            return f + s * 75 + m * 75 * 60;
        }

        public static string TickIntToStr(int tick) {
            if (tick < 0) {
                return "--:--:--";
            }

            int f = tick % 75;
            int s =((tick - f) / 75) % 60;
            int m = (tick - s * 75 - f)/75/60;

            return string.Format("{0:00}:{1:00}:{2:00}", m, s, f);
        }

        public void Debug() {
            Console.WriteLine("    path={0}\n    Track={1} Index={2} start={3}({4}) end={5}({6}) preK={7}\n    title={8}",
                path, trackId, indexId, startTick, TickIntToStr(startTick),
                endTick, TickIntToStr(endTick), preKokomade, title);
        }
    };

    class CueSheetReader {
        private List<CueSheetTrackInfo> m_trackInfoList;
        private CueSheetTrackInfo m_currentTrackInfo;
        private string m_dirPath;

        public int GetTrackInfoCount() {
            return m_trackInfoList.Count;
        }

        public CueSheetTrackInfo GetTrackInfo(int nth) {
            return m_trackInfoList[nth];
        }

        public bool ReadFromFile(string path) {
            // 2パス処理する。
            // パス1…ファイルから読み込んでm_trackInfoListに骨格を作る。
            // パス2…m_trackInfoListを走査して、前後関係によって判明する情報を埋める。
            //          現在のtrackInfoと1個後のトラックが同一ファイル名で
            //          cur.endTickが埋まっていない場合
            //          cur.endTick←next.startTickする。

            m_trackInfoList = new List<CueSheetTrackInfo>();
            m_dirPath = System.IO.Path.GetDirectoryName(path) + "\\";

            m_currentTrackInfo = new CueSheetTrackInfo();
            m_currentTrackInfo.Clear();

            bool result = true;
            try {
                using (StreamReader sr = new StreamReader(path)) {
                    string line;
                    int lineno = 0;
                    while ((line = sr.ReadLine()) != null) {
                        ++lineno;
                        result = ParseOneLine(line, lineno);
                        if (!result) {
                            break;
                        }
                    }
                }
            } catch (Exception e) {
                // Let the user know what went wrong.
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
                return false;
            }
            if (!result) {
                return false;
            }

            Console.WriteLine("Pass1 =================================");
            Console.WriteLine("trackInfoList.Count={0}", m_trackInfoList.Count);
            for (int i = 0; i < m_trackInfoList.Count; ++i) {
                Console.WriteLine("trackInfo {0}", i);
                m_trackInfoList[i].Debug();
            }

            for (int i = 0; i < m_trackInfoList.Count-1; ++i) {
                CueSheetTrackInfo cur = m_trackInfoList[i];
                CueSheetTrackInfo next = m_trackInfoList[i+1];

                if (cur.path.CompareTo(next.path) == 0 &&
                    cur.endTick < 0) {
                    cur.endTick = next.startTick;
                }

                if (0 <= cur.endTick &&
                    0 <= cur.startTick &&
                    cur.endTick < cur.startTick) {
                    Console.WriteLine("track {0}: startTick{1} points newer time than endTick{2}",
                        cur.trackId, cur.startTick, cur.endTick);
                    return false;
                }
            }

            Console.WriteLine("Pass2 =================================");
            Console.WriteLine("trackInfoList.Count={0}", m_trackInfoList.Count);
            for (int i = 0; i < m_trackInfoList.Count; ++i) {
                Console.WriteLine("trackInfo {0}", i);
                m_trackInfoList[i].Debug();
            }

            return true;
        }

        private List<string> Tokenize(string line) {
            int quoteStartPos = -1;
            int lastWhiteSpacePos = -1;
            List<string> tokenList = new List<string>();

            // 最後の文字がホワイトスペースではない場合の処理が面倒なので、
            // 最後にホワイトスペースをつける。
            line = line + " ";
            Console.WriteLine("line=\"{0}\"", line);

            for (int i = 0; i < line.Length; ++i) {
                if (0 <= quoteStartPos) {
                    // ダブルクォートの中の場合。
                    // 次にダブルクォートが出てきたらトークンが完成。クォーティングモードを終わる。
                    if (line[i] == '\"') {
                        string token = line.Substring(quoteStartPos, i - quoteStartPos);
                        tokenList.Add(token);
                        quoteStartPos = -1;
                        lastWhiteSpacePos = i;
                    }
                } else {
                    // ダブルクォートの中ではない場合。
                    if (line[i] == '\"') {
                        // トークン開始。
                        quoteStartPos = i + 1;
                    } else {
                        // ダブルクォートの中でなく、ダブルクォートでない場合。

                        // トークンは、ホワイトスペース的な物で区切られる。
                        if (line[i] == ' ' || line[i] == '\t' || line[i] == '\r' || line[i] == '\n') {
                            if (lastWhiteSpacePos + 1 == i) {
                                // 左隣の文字がホワイトスペース。
                                lastWhiteSpacePos = i;
                            } else {
                                // 左隣の文字がホワイトスペースではなかった。
                                // トークンが完成。
                                string token = line.Substring(lastWhiteSpacePos + 1, i - (lastWhiteSpacePos + 1));
                                tokenList.Add(token);
                                lastWhiteSpacePos = i;
                            }
                        }
                    }
                }
            }

            Console.WriteLine("tokenList.Count={0}", tokenList.Count);
            for (int i = 0; i < tokenList.Count; ++i) {
                Console.WriteLine("{0} : \"{1}\"", i, tokenList[i]);
            }

            return tokenList;
        }

        private bool ParseOneLine(string line, int lineno) {
            line = line.Trim();

            List<string> tokenList = Tokenize(line);
            if (tokenList.Count == 0) {
                return true;
            }

            switch (tokenList[0].ToLower()) {
            case "title":
                m_currentTrackInfo.title = "NO TITLE";
                if (2 <= tokenList.Count && 0 < tokenList[1].Trim().Length) {
                    m_currentTrackInfo.title = tokenList[1];
                }
                Console.WriteLine("title {0}", m_currentTrackInfo.title);
                break;
            case "rem":
                if (2 <= tokenList.Count) {
                    if (0 == tokenList[1].CompareTo("kokomade")) {
                        m_currentTrackInfo.preKokomade = true;
                        Console.WriteLine("rem KOKOMADE processed");
                    }
                }
                Console.WriteLine("rem tag has come");
                break;
            case "file":
                if (tokenList.Count < 2) {
                    Console.WriteLine("Error on line {0}: FILE directive error: filename is not specified", lineno);
                    return true;
                }
                m_currentTrackInfo.path = m_dirPath + tokenList[1];
                Console.WriteLine("file tag has come");
                m_currentTrackInfo.Debug();
                break;
            case "track":
                if (tokenList.Count < 2) {
                    Console.WriteLine("Error on line {0}: track number is not specified", lineno);
                    return true;
                }
                int.TryParse(tokenList[1], out m_currentTrackInfo.trackId);
                Console.WriteLine("track tag has come");
                m_currentTrackInfo.Debug();
                break;
            case "index":
                if (tokenList.Count < 3) {
                    Console.WriteLine("Error on line {0}: index number tick format err", lineno);
                    return true;
                }
                int.TryParse(tokenList[1], out m_currentTrackInfo.indexId);

                m_currentTrackInfo.startTick = CueSheetTrackInfo.TickStrToInt(tokenList[2]);
                if (m_currentTrackInfo.startTick < 0) {
                    Console.WriteLine("Error on line {0}: index {1} time format error ({2})",
                        lineno, m_currentTrackInfo.indexId, tokenList[2]);
                    return true;
                }

                if (m_currentTrackInfo.indexId == 0 ||
                    m_currentTrackInfo.indexId == 1) {
                    CueSheetTrackInfo newTrackInfo = new CueSheetTrackInfo();
                    newTrackInfo.CopyFrom(m_currentTrackInfo);
                    m_trackInfoList.Add(newTrackInfo);
                    Console.WriteLine("index tag has come. add trackInfoList");
                    m_currentTrackInfo.Debug();

                    // 揮発要素はここでリセットする。
                    m_currentTrackInfo.startTick = -1;
                    m_currentTrackInfo.preKokomade = false;
                }
                break;

            default:
                Console.WriteLine("skipped {0}", tokenList[0]);
                break;
            }

            return true;
        }
    }
}
