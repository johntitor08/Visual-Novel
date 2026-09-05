// VNScript.cs -- the .txt scenario format: tokenizer, parser and compiled program.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VN
{
    public enum VNOp
    {
        Nop,
        Say,        // a = speaker id ("" = narration), b = text, c = optional portrait frame
        Bg,         // a = background name, num = fade seconds
        Show,       // a = id, b = slot, c = "poses"/"expressions", d = frame, num = fade
        Move,       // a = id, b = slot, num = seconds
        Hide,       // a = id, num = fade
        HideAll,    // num = fade
        Face,       // a = id, b = portrait frame
        Wait,       // num = seconds
        Bgm,        // a = clip name or "stop"
        Sfx,        // a = clip name
        Jump,       // a = label
        Choice,     // options
        Set,        // a = variable, b = "=" / "+=" / "-=", num = value
        If,         // a = condition, b = target label
        Flash,      // a = colour, num = seconds
        Shake,      // num = seconds
        Chapter,    // a = title, b = subtitle
        AskName,    // prompts for the protagonist's name
        End         // a = ending id, b = title, c = subtitle
    }

    [Serializable]
    public class VNChoiceOption
    {
        public string text;
        public string target;
        public string condition;   // optional; the option is hidden when it fails
    }

    public class VNCmd
    {
        public VNOp op;
        public string a, b, c, d;
        public float num;
        public List<VNChoiceOption> options;
        public int line;
        public string source;
    }

    public class VNCharacterDef
    {
        public string id;
        public string display;
        public string spriteSet;
        public Color color = VNTheme.Accent;
    }

    /// <summary>A parsed scenario: a flat command list plus label and cast lookups.</summary>
    public class VNScriptData
    {
        public readonly List<VNCmd> Commands = new List<VNCmd>();
        public readonly Dictionary<string, int> Labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, VNCharacterDef> Cast = new Dictionary<string, VNCharacterDef>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Errors = new List<string>();

        public string ProtagonistDefaultName = "You";
        public Color ProtagonistColor = VNTheme.AccentCool;

        public int LabelIndex(string label)
        {
            int i;
            if (label != null && Labels.TryGetValue(label.Trim(), out i)) return i;
            return -1;
        }

        public VNCharacterDef Character(string id)
        {
            VNCharacterDef d;
            if (id != null && Cast.TryGetValue(id, out d)) return d;
            return null;
        }
    }

    public static class VNScriptParser
    {
        /// <summary>Loads and concatenates every TextAsset under Resources/VN/Story, ordered by name.</summary>
        public static VNScriptData LoadFromResources(string folder = "VN/Story")
        {
            var assets = new List<TextAsset>(Resources.LoadAll<TextAsset>(folder));
            assets.Sort((x, y) => string.CompareOrdinal(x.name, y.name));

            var data = new VNScriptData();
            if (assets.Count == 0)
            {
                data.Errors.Add("No scenario files found in Resources/" + folder);
                return data;
            }
            foreach (var ta in assets) Parse(ta.text, ta.name, data);
            return data;
        }

        public static void Parse(string text, string sourceName, VNScriptData data)
        {
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("//") || line.StartsWith("#")) continue;

                if (line.StartsWith("::"))
                {
                    string label = line.Substring(2).Trim();
                    if (label.Length == 0) { Err(data, sourceName, i, "empty label"); continue; }
                    if (data.Labels.ContainsKey(label)) { Err(data, sourceName, i, "duplicate label '" + label + "'"); continue; }
                    data.Labels[label] = data.Commands.Count;
                    continue;
                }

                if (line.StartsWith(">"))
                {
                    ParseChoiceLine(line, sourceName, i, data);
                    continue;
                }

                if (line.StartsWith("@"))
                {
                    ParseDirective(line, sourceName, i, data);
                    continue;
                }

                ParseDialogue(line, sourceName, i, data);
            }
        }

        // ---------------------------------------------------------------- dialogue

        static void ParseDialogue(string line, string src, int lineNo, VNScriptData data)
        {
            string speaker = null;
            string face = null;
            string body = line;

            int colon = line.IndexOf(':');
            if (colon > 0)
            {
                string head = line.Substring(0, colon).Trim();
                string tail = line.Substring(colon + 1).Trim();

                // "Rin @05-angry: text" -- the portrait swaps on the same line as the line it belongs to.
                string headName = head;
                int at = head.IndexOf('@');
                if (at >= 0)
                {
                    headName = head.Substring(0, at).Trim();
                    face = head.Substring(at + 1).Trim();
                }

                if (IsKnownSpeaker(headName, data))
                {
                    speaker = ResolveId(headName, data);
                    body = tail;
                }
            }

            var cmd = New(VNOp.Say, src, lineNo);
            cmd.a = speaker ?? "";
            cmd.b = body;
            cmd.c = face;
            data.Commands.Add(cmd);
        }

        static bool IsKnownSpeaker(string name, VNScriptData data)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.Equals("you", StringComparison.OrdinalIgnoreCase)) return true;
            if (data.Cast.ContainsKey(name)) return true;
            foreach (var kv in data.Cast)
                if (kv.Value.display != null && kv.Value.display.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        static string ResolveId(string name, VNScriptData data)
        {
            if (data.Cast.ContainsKey(name)) return name.ToLowerInvariant();
            foreach (var kv in data.Cast)
                if (kv.Value.display != null && kv.Value.display.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return kv.Key;
            return name.ToLowerInvariant();
        }

        // ---------------------------------------------------------------- choices

        static void ParseChoiceLine(string line, string src, int lineNo, VNScriptData data)
        {
            string rest = line.Substring(1).Trim();
            string cond = null;

            if (rest.StartsWith("["))
            {
                int close = rest.IndexOf(']');
                if (close < 0) { Err(data, src, lineNo, "unclosed [condition] on choice"); return; }
                cond = rest.Substring(1, close - 1).Trim();
                rest = rest.Substring(close + 1).Trim();
            }

            int arrow = rest.LastIndexOf("->", StringComparison.Ordinal);
            if (arrow < 0) { Err(data, src, lineNo, "choice is missing '-> label'"); return; }

            var opt = new VNChoiceOption
            {
                text = rest.Substring(0, arrow).Trim(),
                target = rest.Substring(arrow + 2).Trim(),
                condition = cond
            };

            // Consecutive '>' lines gather into one menu.
            VNCmd last = data.Commands.Count > 0 ? data.Commands[data.Commands.Count - 1] : null;
            if (last != null && last.op == VNOp.Choice)
            {
                last.options.Add(opt);
                return;
            }

            var cmd = New(VNOp.Choice, src, lineNo);
            cmd.options = new List<VNChoiceOption> { opt };
            data.Commands.Add(cmd);
        }

        // ---------------------------------------------------------------- directives

        static void ParseDirective(string line, string src, int lineNo, VNScriptData data)
        {
            var tok = Tokenize(line.Substring(1));
            if (tok.Count == 0) { Err(data, src, lineNo, "empty directive"); return; }

            string key = tok[0].ToLowerInvariant();
            tok.RemoveAt(0);

            switch (key)
            {
                case "char":
                {
                    if (tok.Count == 0) { Err(data, src, lineNo, "@char needs an id"); return; }
                    var def = new VNCharacterDef { id = tok[0].ToLowerInvariant() };
                    def.display = def.id;
                    for (int i = 1; i < tok.Count; i++)
                    {
                        string k, v;
                        if (!KeyValue(tok[i], out k, out v)) continue;
                        if (k == "name") def.display = v;
                        else if (k == "sprites") def.spriteSet = v;
                        else if (k == "color" || k == "colour")
                        {
                            Color c;
                            if (ColorUtility.TryParseHtmlString(v.StartsWith("#") ? v : "#" + v, out c)) def.color = c;
                        }
                    }
                    data.Cast[def.id] = def;
                    return;
                }

                case "you":
                {
                    for (int i = 0; i < tok.Count; i++)
                    {
                        string k, v;
                        if (!KeyValue(tok[i], out k, out v)) continue;
                        if (k == "name") data.ProtagonistDefaultName = v;
                        else if (k == "color" || k == "colour")
                        {
                            Color c;
                            if (ColorUtility.TryParseHtmlString(v.StartsWith("#") ? v : "#" + v, out c)) data.ProtagonistColor = c;
                        }
                    }
                    return;
                }

                case "bg":
                {
                    var cmd = New(VNOp.Bg, src, lineNo);
                    cmd.a = tok.Count > 0 ? tok[0] : "black";
                    cmd.num = Named(tok, "fade", 0.5f);
                    data.Commands.Add(cmd);
                    return;
                }

                case "show":
                {
                    if (tok.Count == 0) { Err(data, src, lineNo, "@show needs a character id"); return; }
                    var cmd = New(VNOp.Show, src, lineNo);
                    cmd.a = tok[0].ToLowerInvariant();
                    cmd.b = NamedStr(tok, "at", null);
                    string pose = NamedStr(tok, "pose", null);
                    string expr = NamedStr(tok, "expr", null);
                    if (!string.IsNullOrEmpty(expr)) { cmd.c = "expressions"; cmd.d = expr; }
                    else if (!string.IsNullOrEmpty(pose)) { cmd.c = "poses"; cmd.d = pose; }
                    cmd.num = Named(tok, "fade", 0.35f);
                    data.Commands.Add(cmd);
                    return;
                }

                case "move":
                {
                    if (tok.Count == 0) { Err(data, src, lineNo, "@move needs a character id"); return; }
                    var cmd = New(VNOp.Move, src, lineNo);
                    cmd.a = tok[0].ToLowerInvariant();
                    cmd.b = NamedStr(tok, "at", "center");
                    cmd.num = Named(tok, "time", 0.4f);
                    data.Commands.Add(cmd);
                    return;
                }

                case "hide":
                {
                    var cmd = New(VNOp.Hide, src, lineNo);
                    cmd.a = tok.Count > 0 ? tok[0].ToLowerInvariant() : "";
                    cmd.num = Named(tok, "fade", 0.3f);
                    data.Commands.Add(cmd);
                    return;
                }

                case "hideall":
                case "clear":
                {
                    var cmd = New(VNOp.HideAll, src, lineNo);
                    cmd.num = Named(tok, "fade", 0.3f);
                    data.Commands.Add(cmd);
                    return;
                }

                case "face":
                {
                    if (tok.Count < 2) { Err(data, src, lineNo, "@face needs an id and a frame"); return; }
                    var cmd = New(VNOp.Face, src, lineNo);
                    cmd.a = tok[0].ToLowerInvariant();
                    cmd.b = tok[1];
                    data.Commands.Add(cmd);
                    return;
                }

                case "wait":
                {
                    var cmd = New(VNOp.Wait, src, lineNo);
                    cmd.num = tok.Count > 0 ? Num(tok[0], 0.5f) : 0.5f;
                    data.Commands.Add(cmd);
                    return;
                }

                case "bgm":
                {
                    var cmd = New(VNOp.Bgm, src, lineNo);
                    cmd.a = tok.Count > 0 ? tok[0] : "stop";
                    data.Commands.Add(cmd);
                    return;
                }

                case "sfx":
                {
                    var cmd = New(VNOp.Sfx, src, lineNo);
                    cmd.a = tok.Count > 0 ? tok[0] : "";
                    data.Commands.Add(cmd);
                    return;
                }

                case "jump":
                {
                    if (tok.Count == 0) { Err(data, src, lineNo, "@jump needs a label"); return; }
                    var cmd = New(VNOp.Jump, src, lineNo);
                    cmd.a = tok[0];
                    data.Commands.Add(cmd);
                    return;
                }

                case "set":
                {
                    // @set trust += 1   /   @set met_mira = 1
                    if (tok.Count < 3) { Err(data, src, lineNo, "@set needs 'var op value'"); return; }
                    var cmd = New(VNOp.Set, src, lineNo);
                    cmd.a = tok[0];
                    cmd.b = tok[1];
                    cmd.num = Num(tok[2], 0f);
                    data.Commands.Add(cmd);
                    return;
                }

                case "if":
                {
                    // @if trust >= 3 -> good_end
                    string joined = string.Join(" ", tok.ToArray());
                    int arrow = joined.LastIndexOf("->", StringComparison.Ordinal);
                    if (arrow < 0) { Err(data, src, lineNo, "@if is missing '-> label'"); return; }
                    var cmd = New(VNOp.If, src, lineNo);
                    cmd.a = joined.Substring(0, arrow).Trim();
                    cmd.b = joined.Substring(arrow + 2).Trim();
                    data.Commands.Add(cmd);
                    return;
                }

                case "flash":
                {
                    // "@flash #FFFFFF 0.6" and "@flash #FFFFFF time=0.6" both work.
                    var cmd = New(VNOp.Flash, src, lineNo);
                    cmd.a = tok.Count > 0 ? tok[0] : "#FFFFFF";
                    float dur = Named(tok, "time", -1f);
                    if (dur < 0f) dur = tok.Count > 1 && tok[1].IndexOf('=') < 0 ? Num(tok[1], 0.35f) : 0.35f;
                    cmd.num = dur;
                    data.Commands.Add(cmd);
                    return;
                }

                case "shake":
                {
                    var cmd = New(VNOp.Shake, src, lineNo);
                    cmd.num = tok.Count > 0 ? Num(tok[0], 0.4f) : 0.4f;
                    data.Commands.Add(cmd);
                    return;
                }

                case "chapter":
                {
                    var cmd = New(VNOp.Chapter, src, lineNo);
                    cmd.a = tok.Count > 0 ? tok[0] : "";
                    cmd.b = tok.Count > 1 ? tok[1] : "";
                    data.Commands.Add(cmd);
                    return;
                }

                case "askname":
                {
                    data.Commands.Add(New(VNOp.AskName, src, lineNo));
                    return;
                }

                case "end":
                {
                    var cmd = New(VNOp.End, src, lineNo);
                    cmd.a = tok.Count > 0 ? tok[0] : "end";
                    cmd.b = tok.Count > 1 ? tok[1] : "The End";
                    cmd.c = tok.Count > 2 ? tok[2] : "";
                    data.Commands.Add(cmd);
                    return;
                }

                default:
                    Err(data, src, lineNo, "unknown directive '@" + key + "'");
                    return;
            }
        }

        // ---------------------------------------------------------------- helpers

        static VNCmd New(VNOp op, string src, int line)
        {
            return new VNCmd { op = op, source = src, line = line + 1 };
        }

        static void Err(VNScriptData data, string src, int line, string message)
        {
            data.Errors.Add(src + ":" + (line + 1) + "  " + message);
        }

        /// <summary>Splits on whitespace but keeps "quoted phrases" together.</summary>
        public static List<string> Tokenize(string s)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '"') { quoted = !quoted; continue; }
                if (!quoted && char.IsWhiteSpace(ch))
                {
                    if (sb.Length > 0) { result.Add(sb.ToString()); sb.Length = 0; }
                    continue;
                }
                sb.Append(ch);
            }
            if (sb.Length > 0) result.Add(sb.ToString());
            return result;
        }

        static bool KeyValue(string token, out string key, out string value)
        {
            int eq = token.IndexOf('=');
            if (eq <= 0) { key = null; value = null; return false; }
            key = token.Substring(0, eq).Trim().ToLowerInvariant();
            value = token.Substring(eq + 1).Trim();
            return true;
        }

        static string NamedStr(List<string> tokens, string name, string fallback)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                string k, v;
                if (KeyValue(tokens[i], out k, out v) && k == name) return v;
            }
            return fallback;
        }

        static float Named(List<string> tokens, string name, float fallback)
        {
            string s = NamedStr(tokens, name, null);
            return s == null ? fallback : Num(s, fallback);
        }

        static float Num(string s, float fallback)
        {
            float f;
            if (float.TryParse(s, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out f)) return f;
            return fallback;
        }
    }
}
