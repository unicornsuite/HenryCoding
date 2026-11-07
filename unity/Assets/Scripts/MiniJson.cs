// Kleiner JSON-Parser/-Serialisierer (Objekte -> Dictionary<string, object>, Arrays -> List<object>, Zahlen -> double)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ZA
{
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int i = 0;
            return ParseValue(json, ref i);
        }

        static void SkipWs(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't') { i += 4; return true; }
            if (c == 'f') { i += 5; return false; }
            if (c == 'n') { i += 4; return null; }
            return ParseNumber(s, ref i);
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++; // {
            for (;;)
            {
                SkipWs(s, ref i);
                if (s[i] == '}') { i++; return d; }
                if (s[i] == ',') { i++; continue; }
                string key = ParseString(s, ref i);
                SkipWs(s, ref i); i++; // :
                d[key] = ParseValue(s, ref i);
            }
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var l = new List<object>();
            i++; // [
            for (;;)
            {
                SkipWs(s, ref i);
                if (s[i] == ']') { i++; return l; }
                if (s[i] == ',') { i++; continue; }
                l.Add(ParseValue(s, ref i));
            }
        }

        static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // "
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\')
                {
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                        default: sb.Append(e); break;
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        static object ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------ Serialisieren
        public static string Serialize(object o)
        {
            var sb = new StringBuilder();
            Write(sb, o);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, object o)
        {
            switch (o)
            {
                case null: sb.Append("null"); break;
                case string str: WriteString(sb, str); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case IDictionary dict:
                    {
                        sb.Append('{'); bool first = true;
                        foreach (DictionaryEntry e in dict)
                        {
                            if (!first) sb.Append(','); first = false;
                            WriteString(sb, e.Key.ToString()); sb.Append(':'); Write(sb, e.Value);
                        }
                        sb.Append('}'); break;
                    }
                case IEnumerable list:
                    {
                        sb.Append('['); bool first = true;
                        foreach (var e in list) { if (!first) sb.Append(','); first = false; Write(sb, e); }
                        sb.Append(']'); break;
                    }
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                default: sb.Append(Convert.ToString(o, CultureInfo.InvariantCulture)); break;
            }
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c); break;
                }
            }
            sb.Append('"');
        }
    }

    /// <summary>Bequemer Zugriff auf geparstes JSON.</summary>
    public static class J
    {
        public static Dictionary<string, object> Obj(object o) => o as Dictionary<string, object>;
        public static List<object> Arr(object o) => o as List<object>;
        public static object Get(object o, string key) { var d = Obj(o); return d != null && d.TryGetValue(key, out var v) ? v : null; }
        public static bool Has(object o, string key) { var d = Obj(o); return d != null && d.ContainsKey(key); }
        public static double Num(object o, double def = 0) => o is double d ? d : o is bool b ? (b ? 1 : 0) : def;
        public static float F(object o, string key, float def = 0) => (float)Num(Get(o, key), def);
        public static int I(object o, string key, int def = 0) => (int)Math.Round(Num(Get(o, key), def));
        public static bool B(object o, string key, bool def = false) { var v = Get(o, key); return v is bool b ? b : v is double d ? d != 0 : def; }
        public static string S(object o, string key, string def = "") { var v = Get(o, key); return v == null ? def : v.ToString(); }
        public static List<object> A(object o, string key) => Arr(Get(o, key)) ?? new List<object>();
        public static Dictionary<string, object> O(object o, string key) => Obj(Get(o, key));
        public static float F(List<object> a, int i, float def = 0) => i < a.Count ? (float)Num(a[i], def) : def;
        public static int I(List<object> a, int i, int def = 0) => i < a.Count ? (int)Math.Round(Num(a[i], def)) : def;
    }
}
