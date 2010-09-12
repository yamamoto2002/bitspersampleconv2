using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace PlayPcmWin {
    struct CueSheetTrackInfo {
        public string path;
    };

    class CueSheetReader {
        private List<CueSheetTrackInfo> m_trackInfoList = new List<CueSheetTrackInfo>();
        private string m_dirPath;

        public int GetTrackInfoCount() {
            return m_trackInfoList.Count;
        }

        public CueSheetTrackInfo GetTrackInfo(int nth) {
            return m_trackInfoList[nth];
        }

        public bool ReadFromFile(string path) {
            m_trackInfoList = new List<CueSheetTrackInfo>();
            m_dirPath = System.IO.Path.GetDirectoryName(path) + "\\";

            bool result = true;
            try {
                using (StreamReader sr = new StreamReader(path)) {
                    string line;
                    while ((line = sr.ReadLine()) != null) {
                        result = ParseOneLine(line);
                        if (!result) {
                            break;
                        }
                    }
                }
            } catch (Exception e) {
                // Let the user know what went wrong.
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
                result = false;
            }

            Console.WriteLine("trackInfoList.Count={0}", m_trackInfoList.Count);
            for (int i = 0; i < m_trackInfoList.Count; ++i) {
                Console.WriteLine("{0} \"{1}\"", i, m_trackInfoList[i].path);
            }

            return result;
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

        private bool ParseOneLine(string line) {
            line = line.Trim();

            List<string> tokenList = Tokenize(line);
            if (tokenList.Count == 0) {
                return true;
            }

            switch (tokenList[0].ToLower()) {
            case "rem":
                Console.WriteLine("rem skipped");
                break;
            case "file":
                if (tokenList.Count < 2) {
                    Console.WriteLine("file not specified");
                    return true;
                }
                CueSheetTrackInfo csti = new CueSheetTrackInfo();
                csti.path = m_dirPath + tokenList[1];
                m_trackInfoList.Add(csti);
                Console.WriteLine("added \"{0}\"", csti.path);
                break;
            default:
                Console.WriteLine("skipped {0}", tokenList[0]);
                break;
            }

            return true;
        }
    }
}
