// VNDirector.cs -- walks the compiled scenario and drives the stage, box and menus.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VN
{
    public class VNDirector : MonoBehaviour
    {
        public VNGame Game;

        public VNScriptData Script { get; private set; }
        public VNVars Vars { get; private set; }
        public string PlayerName = "Kai";
        public string ChapterTitle = "";
        public readonly List<VNHistoryEntry> History = new List<VNHistoryEntry>();

        const int HistoryLimit = 250;

        int _pc;
        int _sayPc = -1;                    // the line a save should resume from
        string _lastLine = "";
        bool _running;
        Coroutine _loop;

        readonly Dictionary<string, string> _portraits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool Running { get { return _running; } }

        // ---------------------------------------------------------------- lifecycle

        public void Bind(VNScriptData script)
        {
            Script = script;
            Vars = new VNVars();
        }

        public void StartNew()
        {
            Vars.Clear();
            History.Clear();
            _portraits.Clear();
            ChapterTitle = "";
            _lastLine = "";
            _pc = 0;
            Resume();
        }

        public void StartAt(int pc)
        {
            _pc = Mathf.Clamp(pc, 0, Script.Commands.Count);
            Resume();
        }

        public void Resume()
        {
            Stop();
            _running = true;
            _loop = StartCoroutine(Loop());
        }

        public void Stop()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            _running = false;
        }

        // ---------------------------------------------------------------- main loop

        IEnumerator Loop()
        {
            while (_pc >= 0 && _pc < Script.Commands.Count)
            {
                var c = Script.Commands[_pc];
                int before = _pc;

                switch (c.op)
                {
                    case VNOp.Say:      yield return Say(c); break;
                    case VNOp.Bg:       yield return Game.Stage.SetBackground(c.a, c.num); break;
                    case VNOp.Show:     yield return DoShow(c); break;
                    case VNOp.Move:     yield return Game.Stage.Move(c.a, c.b, c.num); break;
                    case VNOp.Hide:     yield return Game.Stage.Hide(c.a, c.num); break;
                    case VNOp.HideAll:  yield return Game.Stage.HideAll(c.num); break;
                    case VNOp.Face:     SetPortrait(c.a, c.b); yield return SyncExpression(c.a, c.b); break;
                    case VNOp.Wait:     yield return Wait(c.num); break;
                    case VNOp.Bgm:      Game.Audio.PlayBgm(c.a); break;
                    case VNOp.Sfx:      Game.Audio.PlaySfx(c.a); break;
                    case VNOp.Flash:    yield return DoFlash(c); break;
                    case VNOp.Shake:    yield return Game.Stage.Shake(c.num); break;
                    case VNOp.Chapter:  yield return DoChapter(c); break;
                    case VNOp.AskName:  yield return DoAskName(); break;
                    case VNOp.Set:      Vars.Apply(c.a, c.b, c.num); break;
                    case VNOp.Jump:     if (!GoTo(c.a, c)) yield break; continue;
                    case VNOp.If:
                        if (Vars.Evaluate(c.a)) { if (!GoTo(c.b, c)) yield break; continue; }
                        break;
                    case VNOp.Choice:   yield return DoChoice(c); continue;
                    case VNOp.End:      yield return DoEnd(c); yield break;
                }

                // A command that jumped has already moved the counter.
                if (_pc == before) _pc++;
            }

            _running = false;
            Game.OnScriptRanOut();
        }

        bool GoTo(string label, VNCmd from)
        {
            int index = Script.LabelIndex(label);
            if (index < 0)
            {
                Debug.LogError("[VN] " + from.source + ":" + from.line + " jump to unknown label '" + label + "'");
                _running = false;
                Game.OnScriptRanOut();
                return false;
            }
            _pc = index;
            return true;
        }

        // ---------------------------------------------------------------- dialogue

        IEnumerator Say(VNCmd c)
        {
            _sayPc = _pc;

            string speakerId = c.a ?? "";
            string display = null;
            Color color = VNTheme.Ink;
            Sprite portrait = null;

            if (speakerId.Length > 0)
            {
                if (speakerId.Equals("you", StringComparison.OrdinalIgnoreCase))
                {
                    display = PlayerName;
                    color = Script.ProtagonistColor;
                }
                else
                {
                    var def = Script.Character(speakerId);
                    display = def != null ? def.display : speakerId;
                    color = def != null ? def.color : VNTheme.Accent;

                    if (!string.IsNullOrEmpty(c.c))
                    {
                        SetPortrait(speakerId, c.c);
                        yield return SyncExpression(speakerId, c.c);
                    }
                    if (def != null && !string.IsNullOrEmpty(def.spriteSet))
                        portrait = VNAssets.Character(def.spriteSet, "portraits", PortraitOf(speakerId));
                }
                Game.Stage.Highlight(speakerId.Equals("you", StringComparison.OrdinalIgnoreCase) ? null : speakerId);
            }
            else
            {
                Game.Stage.Highlight(null);
            }

            string body = Substitute(c.b);
            _lastLine = body;

            Game.Dialogue.SetVisible(true);
            Game.Dialogue.SetSpeaker(display, color, portrait);
            Game.Dialogue.SetText(body, 0);
            Game.Dialogue.SetIndicator(false);

            AddHistory(display, body, color);

            int total = Game.Dialogue.TotalCharacters();
            float cps = VNSettings.TextSpeed;
            bool instant = Game.Skipping || cps >= 139f;

            if (instant)
            {
                Game.Dialogue.SetText(body, total);
            }
            else
            {
                float shown = 0f;
                int lastPlayed = 0;
                while (shown < total)
                {
                    if (Game.ConsumeAdvance() || Game.Skipping) break;
                    shown += cps * Time.deltaTime;
                    int visible = Mathf.Min(total, Mathf.FloorToInt(shown));
                    Game.Dialogue.SetText(body, visible);
                    if (visible > lastPlayed)
                    {
                        // One blip every few glyphs is enough; per-character is a machine gun.
                        if (visible / 3 > lastPlayed / 3) Game.Audio.PlayType();
                        lastPlayed = visible;
                    }
                    yield return null;
                }
                Game.Dialogue.SetText(body, total);
            }

            Game.Dialogue.SetIndicator(true);
            yield return WaitForAdvance(total);
            Game.Dialogue.SetIndicator(false);
        }

        IEnumerator WaitForAdvance(int charCount)
        {
            Game.ConsumeAdvance();   // drop the click that finished the typewriter

            if (Game.Skipping)
            {
                yield return new WaitForSeconds(0.045f);
                yield break;
            }

            if (Game.AutoPlay)
            {
                float hold = VNSettings.AutoDelay + charCount * 0.022f;
                for (float t = 0f; t < hold; t += Time.deltaTime)
                {
                    if (Game.ConsumeAdvance()) yield break;
                    if (!Game.AutoPlay) break;
                    if (Game.Skipping) yield break;
                    yield return null;
                }
                if (Game.AutoPlay) yield break;
            }

            while (!Game.ConsumeAdvance())
            {
                if (Game.Skipping || Game.AutoPlay) yield break;
                yield return null;
            }
        }

        void AddHistory(string speaker, string text, Color color)
        {
            History.Add(new VNHistoryEntry
            {
                speaker = speaker,
                text = text,
                colorHex = "#" + ColorUtility.ToHtmlStringRGB(color)
            });
            if (History.Count > HistoryLimit) History.RemoveAt(0);
        }

        // ---------------------------------------------------------------- commands

        IEnumerator DoShow(VNCmd c)
        {
            var def = Script.Character(c.a);
            string set = def != null ? def.spriteSet : null;
            string kind = c.c ?? "poses";
            string frame = c.d;

            if (string.IsNullOrEmpty(frame))
            {
                var existing = Game.Stage.Find(c.a);
                frame = existing != null ? existing.frame : (kind == "expressions" ? "01-neutral" : "01-standing");
            }

            // Expression sheets and portrait sheets share their frame names, so keep the face in step.
            if (kind == "expressions") SetPortrait(c.a, frame);

            yield return Game.Stage.Show(c.a, set, c.b, kind, frame, c.num);
        }

        /// <summary>When a character is standing in an expression pose, an @face swaps the body too.</summary>
        IEnumerator SyncExpression(string id, string frame)
        {
            var actor = Game.Stage.Find(id);
            if (actor == null || actor.kind != "expressions" || actor.frame == frame) yield break;
            yield return Game.Stage.Show(id, actor.spriteSet, actor.slot, "expressions", frame, 0.12f);
        }

        void SetPortrait(string id, string frame)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(frame)) return;
            _portraits[id] = frame;
        }

        string PortraitOf(string id)
        {
            string f;
            return _portraits.TryGetValue(id, out f) ? f : "01-neutral";
        }

        IEnumerator Wait(float seconds)
        {
            if (Game.Skipping) yield break;
            float t = 0f;
            while (t < seconds)
            {
                if (Game.ConsumeAdvance()) yield break;
                t += Time.deltaTime;
                yield return null;
            }
        }

        IEnumerator DoFlash(VNCmd c)
        {
            Color col;
            if (!ColorUtility.TryParseHtmlString(c.a.StartsWith("#") ? c.a : "#" + c.a, out col)) col = Color.white;
            yield return Game.Stage.FlashScreen(col, c.num);
        }

        IEnumerator DoChapter(VNCmd c)
        {
            ChapterTitle = c.a;
            if (Game.Skipping) yield break;
            Game.Dialogue.SetVisible(false);
            yield return Game.ChapterCard.Show(c.a, c.b);
        }

        IEnumerator DoAskName()
        {
            Game.Dialogue.SetVisible(false);
            bool done = false;
            Game.NamePrompt.Ask(PlayerName, n => { PlayerName = n; done = true; });
            while (!done) yield return null;
            yield return new WaitForSeconds(0.15f);
        }

        IEnumerator DoChoice(VNCmd c)
        {
            var labels = new List<string>();
            var targets = new List<string>();

            foreach (var opt in c.options)
            {
                if (!string.IsNullOrEmpty(opt.condition) && !Vars.Evaluate(opt.condition)) continue;
                labels.Add(Substitute(opt.text));
                targets.Add(opt.target);
            }

            if (labels.Count == 0)
            {
                Debug.LogWarning("[VN] " + c.source + ":" + c.line + " every choice was filtered out; skipping.");
                _pc++;
                yield break;
            }

            Game.SetSkipping(false);
            Game.SetAutoPlay(false);
            Game.Dialogue.SetIndicator(false);

            int picked = -1;
            Game.Choices.Open(labels, i => picked = i);
            while (picked < 0) yield return null;

            Game.Audio.PlayClick();
            AddHistory(null, "> " + labels[picked], VNTheme.AccentCool);

            int index = Script.LabelIndex(targets[picked]);
            if (index < 0)
            {
                Debug.LogError("[VN] " + c.source + ":" + c.line + " choice points at unknown label '" + targets[picked] + "'");
                _pc++;
                yield break;
            }
            _pc = index;
        }

        IEnumerator DoEnd(VNCmd c)
        {
            Game.SetSkipping(false);
            Game.SetAutoPlay(false);
            _running = false;

            yield return Game.Fader.To(1f, 1.0f, Color.black);
            Game.Dialogue.SetVisible(false, true);
            Game.Stage.ClearAllImmediate();
            Game.Audio.StopBgm(1.0f);
            yield return new WaitForSeconds(0.35f);

            Game.ShowEnding(c.a, c.b, c.c);
        }

        // ---------------------------------------------------------------- text

        /// <summary>Expands {name} and {variable} inside a line.</summary>
        public string Substitute(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;

            var sb = new StringBuilder(text.Length + 16);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '{') { sb.Append(text[i]); continue; }

                int close = text.IndexOf('}', i + 1);
                if (close < 0) { sb.Append(text[i]); continue; }

                string key = text.Substring(i + 1, close - i - 1).Trim();
                if (key.Equals("name", StringComparison.OrdinalIgnoreCase)) sb.Append(PlayerName);
                else
                {
                    float v = Vars.Get(key);
                    sb.Append(Mathf.Approximately(v, Mathf.Round(v))
                        ? Mathf.RoundToInt(v).ToString()
                        : v.ToString("0.##"));
                }
                i = close;
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- persistence

        public VNSaveData Capture()
        {
            var data = new VNSaveData
            {
                pc = _sayPc >= 0 ? _sayPc : _pc,
                playerName = PlayerName,
                background = Game.Stage.CurrentBackground,
                bgm = Game.Audio.CurrentBgm,
                chapter = ChapterTitle,
                preview = _lastLine
            };

            foreach (var a in Game.Stage.Actors)
            {
                data.actors.Add(new VNActorSave
                {
                    id = a.id,
                    slot = a.slot,
                    kind = a.kind,
                    frame = a.frame,
                    portrait = PortraitOf(a.id)
                });
            }

            Vars.SaveInto(data.varNames, data.varValues);

            int from = Mathf.Max(0, History.Count - 120);
            for (int i = from; i < History.Count; i++) data.history.Add(History[i]);

            return data;
        }

        public void Restore(VNSaveData data)
        {
            Stop();

            PlayerName = string.IsNullOrEmpty(data.playerName) ? "Kai" : data.playerName;
            ChapterTitle = data.chapter ?? "";
            _lastLine = data.preview ?? "";

            Vars.LoadFrom(data.varNames, data.varValues);

            History.Clear();
            if (data.history != null) History.AddRange(data.history);

            _portraits.Clear();
            Game.Stage.ClearAllImmediate();
            Game.Stage.SetBackgroundImmediate(data.background);

            foreach (var a in data.actors)
            {
                var def = Script.Character(a.id);
                string set = def != null ? def.spriteSet : null;
                SetPortrait(a.id, string.IsNullOrEmpty(a.portrait) ? "01-neutral" : a.portrait);
                StartCoroutine(Game.Stage.Show(a.id, set, a.slot, a.kind, a.frame, 0f));
            }

            if (!string.IsNullOrEmpty(data.bgm)) Game.Audio.PlayBgm(data.bgm, 0.6f);
            else Game.Audio.StopBgm(0.3f);

            StartAt(data.pc);
        }
    }
}
