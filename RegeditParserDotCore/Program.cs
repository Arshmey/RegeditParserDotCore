using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

namespace RegeditParserDotCore
{
    enum RegType
    {
        RG_STRING,
        RG_MULTISTRING,
        RG_EXTENDEDSTRING,
        RG_DWORD,
        RG_QWORD,
        RG_BINARY
    }

    abstract class RegValueBase
    {
        public abstract RegType Type { get; }

        public abstract byte[] BinValue { get; set; }
    }

    abstract class RegValueBaseGeneric<T> : RegValueBase
    {
        public abstract T Value { get; set; }

    }

    class RegValueString : RegValueBaseGeneric<string>
    {

        public override RegType Type { get => RegType.RG_STRING; }

        public override string Value { get; set; }

        public override byte[] BinValue
        {
            get => Encoding.ASCII.GetBytes(Value);
            set => Value = Encoding.ASCII.GetString(value);
        }
       
    }

    class RegValueMultiString : RegValueBaseGeneric<string>//hex(7)
    {
        public override RegType Type { get => RegType.RG_MULTISTRING; }

        public override string Value { get; set; }

        public override byte[] BinValue
        {
            get => Encoding.ASCII.GetBytes(Value);
            set => Value = Encoding.ASCII.GetString(value);
        }
 
    }

    class RegValueExtendedString : RegValueBaseGeneric<string>//hex(2)
    {
        public override RegType Type { get => RegType.RG_EXTENDEDSTRING; }
        public override string Value { get; set; }

        public override byte[] BinValue
        {
            get => Encoding.ASCII.GetBytes(Value);
            set => Value = Encoding.ASCII.GetString(value);
        }
    }

    class RegValueDword : RegValueBaseGeneric<uint>
    {
        public override RegType Type { get => RegType.RG_DWORD; }

        public override uint Value { get; set; }

        public override byte[] BinValue
        {
            get => BitConverter.GetBytes(Value);
            set => Value = BitConverter.ToUInt32(value);
        }

    }

    class RegValueQword : RegValueBaseGeneric<ulong>
    {
        public override RegType Type { get => RegType.RG_DWORD; }

        public override ulong Value { get; set; }

        public override byte[] BinValue
        {
            get => BitConverter.GetBytes(Value);
            set => Value = BitConverter.ToUInt64(value);
        }
    }

    class RegValueBinary : RegValueBaseGeneric<byte[]>
    {
        public override RegType Type { get => RegType.RG_BINARY; }

        public override byte[] Value { get; set; }

        public override byte[] BinValue
        {
            get => Value;
            set => Value = value;
        }

    }

    class RegValuesList : Dictionary<string, RegValueBase>
    {
        public RegValuesList() : base(StringComparer.InvariantCultureIgnoreCase) { }
    }

    class RegKeysList : Dictionary<string, RegValuesList>
    {
        public RegKeysList() : base(StringComparer.InvariantCultureIgnoreCase) { }
    }

    class Parser
    {
        public Parser() { }

        public string GetEscapedString(string str, char endChar = '\n')
        {
            int subStrStart = 0;
            int subStrEnd = -1;
            if (str.StartsWith('"'))
            {
                subStrStart = 1;

                for (int i = 1; i < str.Length; i++)
                {
                    if (str[i] == '\\')
                    {
                        i++;
                        continue;
                    }
                    else if (str[i] == '"')
                    {
                        subStrEnd = i - 1;
                        break;
                    }
                }
            }
            else
            {
                subStrEnd = str.IndexOf(endChar);
            }

            if (subStrEnd < 0)
            {
                return str;
            }
            else
            {
                return str.Substring(subStrStart, subStrEnd);
            }
        }

        public RegKeysList Parse(Stream stream)
        {
            RegKeysList result = new RegKeysList();
            using (StreamReader reader = new StreamReader(stream))
            {
                string magic = reader.ReadLine();
                if (magic != @"Windows Registry Editor Version 5.00")
                {
                    throw new ArgumentException();
                }

                string currentKey = null;

                for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    // check how windows work with keys starting from spaces
                    line.Trim();
                    // skip empty strings
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    // skip comments
                    if (line.StartsWith(';'))
                    {
                        continue;
                    }

                    // parsing code 
                    if (line.StartsWith('[') && line.EndsWith(']'))
                    {
                        currentKey = line.Substring(1, line.Length - 2);
                        if (!result.TryAdd(currentKey, new RegValuesList()))
                        {
                            Console.WriteLine($"Warning, repeating key definition found for key {currentKey}");
                        }
                    }
                    else
                    {
                        Debug.Assert(currentKey != null, "Value before defining the key");

                        string name = GetEscapedString(line, '=');
                        string val = line.Substring(line[0] == '"' ? name.Length + 2 : name.Length);

                        Debug.Assert(val[0] == '=');
                        val = val.Substring(1);

                        if (val.StartsWith('"'))
                        {
                            bool flag = false;
                            for (int i = 1; i < val.Length; i++)
                            {
                                if (val[i] == '\\')
                                {
                                    i++;
                                    continue;
                                }
                                else if (val[i] == '"')
                                {
                                    flag = true;
                                    break;
                                }
                            }

                            if (!flag)
                            {
                                for (string hexLine = reader.ReadLine(); hexLine != null; hexLine = reader.ReadLine())
                                {
                                    for (int i = 1; i < hexLine.Length; i++)
                                    {
                                        if (hexLine[i] == '\\')
                                        {
                                            i++;
                                            continue;
                                        }
                                        else if (hexLine[i] == '"')
                                        {
                                            flag = true;
                                            break;
                                        }
                                    }

                                    val += hexLine;

                                    if (flag)
                                    {
                                        break;
                                    }
                                }
                            }

                            result[currentKey].Add(name, new RegValueString() { Value = val });
                        }
                        else if (val.StartsWith("dword:"))
                        {
                            val = val.Substring(6);

                            result[currentKey].Add(name, new RegValueDword() { Value = uint.Parse(val, NumberStyles.HexNumber) });
                        }
                        else if (val.StartsWith("qword:"))
                        {
                            val = val.Substring(6);

                            result[currentKey].Add(name, new RegValueQword() { Value = ulong.Parse(val, NumberStyles.HexNumber) });
                        }
                        else if (val.StartsWith("hex"))
                        {
                                
                            if (val.EndsWith('\\'))
                            {
                                val = val.Substring(0, val.Length - 1);
                                for (string hexLine = reader.ReadLine(); hexLine != null; hexLine = reader.ReadLine())
                                {
                                    hexLine.Trim();

                                    if (hexLine.EndsWith('\\'))
                                    {
                                        val += hexLine.Substring(0, hexLine.Length - 1);
                                    }
                                    else
                                    {
                                        val += hexLine;
                                        break;
                                    }
                                }
                            }

                            RegValueBase hexVal;

                            if (val.StartsWith("hex:"))
                            {
                                val = val.Substring(4);
                                hexVal = new RegValueBinary();
                            }
                            else if (val.StartsWith("hex(0):"))
                            {
                                val = val.Substring(7);
                                hexVal = new RegValueBinary();
                            }
                            else if (val.StartsWith("hex(2):"))
                            {
                                val = val.Substring(7);
                                hexVal = new RegValueMultiString();
                            }
                            else if (val.StartsWith("hex(7):"))
                            {
                                val = val.Substring(7);
                                hexVal = new RegValueExtendedString();
                            }
                            else if (val.StartsWith("hex(b):"))
                            {
                                val = val.Substring(7);
                                hexVal = new RegValueQword();
                            }
                            else
                            {
                                hexVal = null;
                                Debug.Assert(false, "Hex is not correct");
                            }

                            if (!string.IsNullOrWhiteSpace(val))
                            {
                                string[] hex = val.Split(',');
                                byte[] array = new byte[hex.Length];

                                for (int i = 0; i < hex.Length; i++)
                                {
                                    array[i] = byte.Parse(hex[i], NumberStyles.HexNumber);
                                }

                                hexVal.BinValue = array;
                            }
                            
                            result[currentKey].Add(name, hexVal);
                        }
                        else
                        {
                            throw new Exception("Unknown value type");
                        }
                    }
                }
            }
            return result;
        }
        class Program
        {
            static void Main(string[] args)
            {
                Parser p = new Parser();
                Dictionary<string, RegValuesList> test = p.Parse(File.OpenRead(@"TEST.reg"));

            }
        }
    }
}