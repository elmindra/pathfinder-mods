using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace EldritchArcana {
    public class Localization{
        static Dictionary<string, string> dict;
        void Init() {
            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            string dll_dir = Path.GetDirectoryName(path);
            FileStream fs = new FileStream(Path.Combine(dll_dir, "localization", "zh_cn.txt"), FileMode.Open);
            StreamReader fin = new StreamReader(fs);
            string line;
            while((line = fin.ReadLine()) != null) {
                var phrs = line.Split('\t');
                if(phrs.GetLength(0) < 2) {
                    continue;
                }
                phrs[1] = phrs[1].Replace('杪', '\n');
                phrs[1] = phrs[1].Replace('厸', '\t');
                dict.Add(phrs[0], phrs[1]);
            }
            fin.Close();
            fs.Close();
            
        }
        public string GetTranslate(string s) {
            if (dict.ContainsKey(s)) {
                return dict[s];
            }
            else throw new Exception("String not translated");
        }
        public Localization() {
            dict = new Dictionary<string, string>();
            Init();
        }
    }

}

