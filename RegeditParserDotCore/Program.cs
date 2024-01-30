namespace RegeditParser
{

    // 1) wheare is the parser class?
    // 2) what the structure of parsed data?

    //[keyPath]
    //"value1"="value1data"
    //"value2"=dword:"0x11212"

    // RegData regData = parser.Parse("bla.reg");
    // foreach(RegKey key in regData.Keys)
    // {
    //   foreach(RegValue val in key.Values)
    //   {
    //     Console.WriteLine(val.type);
    //   }
    // }

    struct RegFile
    {
        public string name;
        public string type;
        public object data;

        public RegFile(string name, string type, object data)
        {
            this.name = name;
            this.type = type;
            this.data = data;
        }
    }

    struct RegFolder
    {
        public string keyPath;
        public List<RegFile> RegFile;

        public RegFolder(string keyPath)
        {
            this.keyPath = keyPath;
            RegFile = new List<RegFile>();
        }
    }

    class Parser
    {
        public Parser() { }

        public List<RegFolder> Parse(string text)
        {
            List<RegFolder> lines = new List<RegFolder>();

            using (Stream st = File.OpenRead(text))
            using (StreamReader sr = new StreamReader(st))
            {
                int nextFileData = 0;
                int semicolon;
                int singCount;
                bool enterKeyReg = false;
                string[] splitLine;

                for (string? line = string.Empty; line != null; line = sr.ReadLine())
                {
                    // parsing code 
                    if ((line.StartsWith("[HKEY") && line.EndsWith("]")) && !enterKeyReg)
                    {
                        lines.Add(new RegFolder(line));
                        enterKeyReg = true;
                    }
                    else if (!string.IsNullOrWhiteSpace(line) && enterKeyReg)
                    {
                        try
                        {
                            semicolon = 0;
                            singCount = -1;
                            if (!line.StartsWith("@"))
                            {
                                foreach (char sing in line)
                                {
                                    singCount++;
                                    if (sing == '"')
                                    {
                                        semicolon++;
                                    }
                                    if (semicolon == 2)
                                    {
                                        char c_1 = line[singCount];
                                        char c_2 = line[singCount + 1];
                                        char c_3 = line[singCount + 2];

                                        if ((c_1 == '"' && c_2 == '=' && c_3 == '"') || (c_1 == '"' && c_2 == '=' && (c_3 == 'd' || c_3 == 'q' || c_3 == 'h')))
                                        {
                                            splitLine = line.Split(new string[] { "\"=" }, StringSplitOptions.None);
                                            lines[nextFileData].RegFile.Add(new RegFile(splitLine[0], "REG_SZ", splitLine[1]));

                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                splitLine = line.Split(new string[] { "=" }, StringSplitOptions.None);
                                lines[nextFileData].RegFile.Add(new RegFile(splitLine[0], "REG_SZ", splitLine[1]));
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(line) && enterKeyReg)
                    {
                        if (!lines[nextFileData].RegFile.Any()) { lines[nextFileData].RegFile.Add(new RegFile("(Default)", "REG_SZ", "(value not set)")); }
                        nextFileData++;
                        enterKeyReg = false;
                    }
                }
            }

            return lines;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Parser parser = new Parser();
            List<RegFolder> reg = parser.Parse(@"Software.reg");
            //List<RegFolder> reg = parser.Parse(@"hard_test.reg");

            Console.WriteLine("Key path>: " + reg[1].keyPath);
            Console.WriteLine(reg[1].RegFile[0].name + " | " + reg[1].RegFile[0].type + " | " + reg[1].RegFile[0].data);

            Console.ReadKey();
        }
    }
}