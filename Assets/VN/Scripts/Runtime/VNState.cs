// VNState.cs -- variables, conditions, backlog, settings and the save-slot system.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace VN
{
    /// <summary>Flat numeric variable store. Booleans are just 0 and 1.</summary>
    public class VNVars
    {
        readonly Dictionary<string, float> _values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        public void Clear() { _values.Clear(); }

        public float Get(string name)
        {
            float v;
            return name != null && _values.TryGetValue(name, out v) ? v : 0f;
        }

        public void Set(string name, float value) { if (!string.IsNullOrEmpty(name)) _values[name] = value; }

        public void Apply(string name, string op, float value)
        {
            switch (op)
            {
                case "+=": Set(name, Get(name) + value); break;
                case "-=": Set(name, Get(name) - value); break;
                case "*=": Set(name, Get(name) * value); break;
                default:   Set(name, value); break;
            }
        }

        public IEnumerable<KeyValuePair<string, float>> All { get { return _values; } }

        /// <summary>Evaluates "trust &gt;= 3", "met_mira", "flag != 0". Unknown names read as 0.</summary>
        public bool Evaluate(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return true;
            string s = condition.Trim();

            string[] ops = { ">=", "<=", "==", "!=", ">", "<", "=" };
            foreach (string op in ops)
            {
                int at = s.IndexOf(op, StringComparison.Ordinal);
                if (at < 0) continue;

                float left = Resolve(s.Substring(0, at));
                float right = Resolve(s.Substring(at + op.Length));
                switch (op)
                {
                    case ">=": return left >= right;
                    case "<=": return left <= right;
                    case ">":  return left > right;
                    case "<":  return left < right;
                    case "!=": return Mathf.Abs(left - right) > 0.0001f;
                    default:   return Mathf.Abs(left - right) <= 0.0001f;
                }
            }
            return Mathf.Abs(Resolve(s)) > 0.0001f;
        }

        float Resolve(string term)
        {
            term = term.Trim();
            float f;
            if (float.TryParse(term, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) return f;
            if (term.Equals("true", StringComparison.OrdinalIgnoreCase)) return 1f;
            if (term.Equals("false", StringComparison.OrdinalIgnoreCase)) return 0f;
            return Get(term);
        }

        public void LoadFrom(List<string> names, List<float> values)
        {
            _values.Clear();
            if (names == null || values == null) return;
            for (int i = 0; i < names.Count && i < values.Count; i++) _values[names[i]] = values[i];
        }

        public void SaveInto(List<string> names, List<float> values)
        {
            names.Clear(); values.Clear();
            foreach (var kv in _values) { names.Add(kv.Key); values.Add(kv.Value); }
        }
    }

    [Serializable]
    public class VNHistoryEntry
    {
        public string speaker;
        public string text;
        public string colorHex;
    }

    // ------------------------------------------------------------------ settings

    /// <summary>Player preferences, backed by PlayerPrefs so they survive between runs.</summary>
    public static class VNSettings
    {
        const string P = "vn.";

        public static float TextSpeed          // characters per second; 0 means instant
        {
            get { return PlayerPrefs.GetFloat(P + "textSpeed", 55f); }
            set { PlayerPrefs.SetFloat(P + "textSpeed", value); PlayerPrefs.Save(); }
        }

        public static float AutoDelay          // extra seconds to hold a finished line in auto mode
        {
            get { return PlayerPrefs.GetFloat(P + "autoDelay", 1.6f); }
            set { PlayerPrefs.SetFloat(P + "autoDelay", value); PlayerPrefs.Save(); }
        }

        public static float BgmVolume
        {
            get { return PlayerPrefs.GetFloat(P + "bgm", 0.55f); }
            set { PlayerPrefs.SetFloat(P + "bgm", value); PlayerPrefs.Save(); }
        }

        public static float SfxVolume
        {
            get { return PlayerPrefs.GetFloat(P + "sfx", 0.7f); }
            set { PlayerPrefs.SetFloat(P + "sfx", value); PlayerPrefs.Save(); }
        }

        public static bool TypeSound
        {
            get { return PlayerPrefs.GetInt(P + "typeSound", 1) == 1; }
            set { PlayerPrefs.SetInt(P + "typeSound", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool SkipUnread
        {
            get { return PlayerPrefs.GetInt(P + "skipUnread", 0) == 1; }
            set { PlayerPrefs.SetInt(P + "skipUnread", value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }

    // ------------------------------------------------------------------ save data

    [Serializable]
    public class VNActorSave
    {
        public string id;
        public string slot;
        public string kind;
        public string frame;
        public string portrait;
        public bool flipped;
    }

    [Serializable]
    public class VNSaveData
    {
        public int version = 1;
        public int pc;
        public string playerName = "You";
        public string background = "black";
        public string bgm = "";
        public string chapter = "";
        public string preview = "";           // the last line of text, shown on the slot button
        public string savedAtUtc = "";
        public List<VNActorSave> actors = new List<VNActorSave>();
        public List<string> varNames = new List<string>();
        public List<float> varValues = new List<float>();
        public List<VNHistoryEntry> history = new List<VNHistoryEntry>();
        public string screenshot = "";        // base64 JPG, best-effort
    }

    [Serializable]
    public class VNGlobalData
    {
        public List<string> endingsSeen = new List<string>();
        public int completions;
    }

    /// <summary>JSON save slots under Application.persistentDataPath/saves.</summary>
    public static class VNSaveSystem
    {
        public const int SlotCount = 8;

        static string Dir
        {
            get
            {
                string d = Path.Combine(Application.persistentDataPath, "saves");
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
        }

        static string SlotPath(int slot) { return Path.Combine(Dir, "slot" + slot + ".json"); }
        static string GlobalPath { get { return Path.Combine(Dir, "global.json"); } }

        public static bool Exists(int slot) { return File.Exists(SlotPath(slot)); }

        public static void Write(int slot, VNSaveData data)
        {
            try
            {
                data.savedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(data, true), Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VN] Could not write save slot " + slot + ": " + e.Message);
            }
        }

        public static VNSaveData Read(int slot)
        {
            try
            {
                string p = SlotPath(slot);
                if (!File.Exists(p)) return null;
                return JsonUtility.FromJson<VNSaveData>(File.ReadAllText(p, Encoding.UTF8));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VN] Could not read save slot " + slot + ": " + e.Message);
                return null;
            }
        }

        public static void Delete(int slot)
        {
            try { if (File.Exists(SlotPath(slot))) File.Delete(SlotPath(slot)); }
            catch (Exception e) { Debug.LogWarning("[VN] Could not delete save slot " + slot + ": " + e.Message); }
        }

        public static int MostRecentSlot()
        {
            int best = -1;
            DateTime bestTime = DateTime.MinValue;
            for (int i = 0; i < SlotCount; i++)
            {
                var d = Read(i);
                if (d == null) continue;
                DateTime t;
                if (!DateTime.TryParse(d.savedAtUtc, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out t)) t = DateTime.MinValue;
                if (best < 0 || t > bestTime) { best = i; bestTime = t; }
            }
            return best;
        }

        public static VNGlobalData ReadGlobal()
        {
            try
            {
                if (!File.Exists(GlobalPath)) return new VNGlobalData();
                var g = JsonUtility.FromJson<VNGlobalData>(File.ReadAllText(GlobalPath, Encoding.UTF8));
                return g ?? new VNGlobalData();
            }
            catch { return new VNGlobalData(); }
        }

        public static void WriteGlobal(VNGlobalData g)
        {
            try { File.WriteAllText(GlobalPath, JsonUtility.ToJson(g, true), Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning("[VN] Could not write global data: " + e.Message); }
        }

        public static void MarkEndingSeen(string endingId)
        {
            if (string.IsNullOrEmpty(endingId)) return;
            var g = ReadGlobal();
            if (!g.endingsSeen.Contains(endingId))
            {
                g.endingsSeen.Add(endingId);
                g.completions++;
                WriteGlobal(g);
            }
        }
    }
}
