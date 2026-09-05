// VNScenarioValidator.cs -- checks the scenario without entering play mode.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using VN;

namespace VNEditor
{
    public static class VNScenarioValidator
    {
        const string CharacterRoot = "Assets/VN/Resources/VN/Characters/";

        [MenuItem("Visual Novel/Validate Scenario", false, 21)]
        public static void Validate()
        {
            var data = VNScriptParser.LoadFromResources();
            var problems = new List<string>(data.Errors);
            var warnings = new List<string>();
            var endings = new HashSet<string>();
            var missingSprites = new HashSet<string>();

            foreach (var c in data.Commands)
            {
                string where = c.source + ":" + c.line + "  ";

                switch (c.op)
                {
                    case VNOp.Jump:
                        if (data.LabelIndex(c.a) < 0) problems.Add(where + "@jump to unknown label '" + c.a + "'");
                        break;

                    case VNOp.If:
                        if (data.LabelIndex(c.b) < 0) problems.Add(where + "@if targets unknown label '" + c.b + "'");
                        break;

                    case VNOp.Choice:
                        foreach (var opt in c.options)
                            if (data.LabelIndex(opt.target) < 0)
                                problems.Add(where + "choice targets unknown label '" + opt.target + "'");
                        break;

                    case VNOp.Show:
                    {
                        var def = data.Character(c.a);
                        if (def == null) { problems.Add(where + "@show for undefined character '" + c.a + "'"); break; }
                        if (!string.IsNullOrEmpty(c.d))
                            CheckSprite(def, c.c ?? "poses", c.d, where, missingSprites, problems);
                        break;
                    }

                    case VNOp.Move:
                    case VNOp.Hide:
                        if (!string.IsNullOrEmpty(c.a) && data.Character(c.a) == null)
                            problems.Add(where + "'@" + c.op.ToString().ToLowerInvariant() + "' for undefined character '" + c.a + "'");
                        break;

                    case VNOp.Face:
                    {
                        var def = data.Character(c.a);
                        if (def == null) { problems.Add(where + "@face for undefined character '" + c.a + "'"); break; }
                        CheckSprite(def, "portraits", c.b, where, missingSprites, problems);
                        break;
                    }

                    case VNOp.Say:
                        if (!string.IsNullOrEmpty(c.a) && !c.a.Equals("you", System.StringComparison.OrdinalIgnoreCase))
                        {
                            var def = data.Character(c.a);
                            if (def == null) problems.Add(where + "line spoken by undefined character '" + c.a + "'");
                            else if (!string.IsNullOrEmpty(c.c))
                                CheckSprite(def, "portraits", c.c, where, missingSprites, problems);
                        }
                        break;

                    case VNOp.End:
                        if (!endings.Add(c.a)) warnings.Add(where + "duplicate ending id '" + c.a + "'");
                        break;
                }
            }

            if (endings.Count != VNEndings.Total)
                warnings.Add("VNEndings.Total is " + VNEndings.Total + " but the scenario contains " +
                             endings.Count + " distinct endings.");

            var report = new StringBuilder();
            report.AppendLine("[VN] Scenario check");
            report.AppendLine("  commands : " + data.Commands.Count);
            report.AppendLine("  labels   : " + data.Labels.Count);
            report.AppendLine("  cast     : " + data.Cast.Count);
            report.AppendLine("  endings  : " + endings.Count);

            foreach (var w in warnings) report.AppendLine("  warning  : " + w);

            if (problems.Count == 0)
            {
                report.AppendLine("  no problems found.");
                Debug.Log(report.ToString());
            }
            else
            {
                foreach (var p in problems) report.AppendLine("  ERROR    : " + p);
                Debug.LogError(report.ToString());
            }
        }

        static void CheckSprite(VNCharacterDef def, string kind, string frame, string where,
                                HashSet<string> reported, List<string> problems)
        {
            if (string.IsNullOrEmpty(def.spriteSet) || string.IsNullOrEmpty(frame)) return;

            string path = CharacterRoot + def.spriteSet + "/" + kind + "/" + frame + ".png";
            if (File.Exists(path)) return;
            if (!reported.Add(path)) return;
            problems.Add(where + "missing sprite " + path);
        }
    }
}
