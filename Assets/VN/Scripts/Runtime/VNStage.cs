// VNStage.cs -- the played scene: background, standing sprites, dialogue box and choice menu.
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VN
{
    /// <summary>Background plate plus the character layer, with cross-fades, shakes and flashes.</summary>
    public class VNStage : MonoBehaviour
    {
        public class Actor
        {
            public string id;
            public string spriteSet;
            public string slot = "center";
            public string kind = "poses";
            public string frame = "01-standing";
            public string portrait = "01-neutral";
            public Image image;
            public CanvasGroup group;
            public RectTransform rt;
        }

        const float CharHeight = 1010f;

        RectTransform _bgLayer;
        Image _bgA, _bgB;
        RectTransform _charLayer;
        Image _flash;

        readonly Dictionary<string, Actor> _actors = new Dictionary<string, Actor>(StringComparer.OrdinalIgnoreCase);

        public string CurrentBackground { get; private set; }
        public IEnumerable<Actor> Actors { get { return _actors.Values; } }

        public static VNStage Build(Transform parent)
        {
            var root = UIKit.Rect("Stage", parent);
            UIKit.Stretch(root);
            var stage = root.gameObject.AddComponent<VNStage>();
            stage.Construct(root);
            return stage;
        }

        void Construct(RectTransform root)
        {
            _bgLayer = UIKit.Rect("Backgrounds", root);
            UIKit.Stretch(_bgLayer);

            _bgA = UIKit.Img("BgA", _bgLayer, VNProcBg.Get("black"), Color.white);
            UIKit.Stretch(_bgA.rectTransform);
            _bgA.preserveAspect = false;

            _bgB = UIKit.Img("BgB", _bgLayer, null, new Color(1f, 1f, 1f, 0f));
            UIKit.Stretch(_bgB.rectTransform);
            _bgB.preserveAspect = false;
            _bgB.enabled = false;

            _charLayer = UIKit.Rect("Characters", root);
            UIKit.Stretch(_charLayer);

            _flash = UIKit.Img("Flash", root, VNTextures.Solid(), new Color(1f, 1f, 1f, 0f));
            UIKit.Stretch(_flash.rectTransform);

            CurrentBackground = "black";
        }

        // ---------------------------------------------------------------- background

        public IEnumerator SetBackground(string name, float fade)
        {
            var next = VNProcBg.Get(name);
            CurrentBackground = name;
            if (next == null) yield break;

            if (fade <= 0.01f || _bgA.sprite == null)
            {
                _bgA.sprite = next;
                _bgA.color = Color.white;
                yield break;
            }

            _bgB.enabled = true;
            _bgB.sprite = next;
            _bgB.color = new Color(1f, 1f, 1f, 0f);

            for (float t = 0f; t < fade; t += Time.deltaTime)
            {
                UIKit.SetAlpha(_bgB, VNEase.InOutCubic(t / fade));
                yield return null;
            }

            _bgA.sprite = next;
            _bgA.color = Color.white;
            _bgB.color = new Color(1f, 1f, 1f, 0f);
            _bgB.enabled = false;
        }

        public void SetBackgroundImmediate(string name)
        {
            var s = VNProcBg.Get(name);
            CurrentBackground = name;
            if (s == null) return;
            _bgA.sprite = s;
            _bgA.color = Color.white;
            _bgB.color = new Color(1f, 1f, 1f, 0f);
            _bgB.enabled = false;
        }

        // ---------------------------------------------------------------- characters

        static float SlotX(string slot)
        {
            switch ((slot ?? "center").ToLowerInvariant())
            {
                case "farleft":  return -640f;
                case "left":     return -400f;
                case "midleft":  return -210f;
                case "right":    return 400f;
                case "midright": return 210f;
                case "farright": return 640f;
                default:         return 0f;
            }
        }

        public Actor Find(string id)
        {
            Actor a;
            return id != null && _actors.TryGetValue(id, out a) ? a : null;
        }

        public IEnumerator Show(string id, string spriteSet, string slot, string kind, string frame, float fade)
        {
            var actor = Find(id);
            bool isNew = actor == null;

            if (isNew)
            {
                actor = new Actor { id = id, spriteSet = spriteSet };
                actor.rt = UIKit.Rect("Actor_" + id, _charLayer);
                actor.rt.anchorMin = new Vector2(0.5f, 0f);
                actor.rt.anchorMax = new Vector2(0.5f, 0f);
                actor.rt.pivot = new Vector2(0.5f, 0f);

                actor.image = actor.rt.gameObject.AddComponent<Image>();
                actor.image.raycastTarget = false;
                actor.image.preserveAspect = true;

                actor.group = actor.rt.gameObject.AddComponent<CanvasGroup>();
                actor.group.alpha = 0f;

                _actors[id] = actor;
            }

            if (!string.IsNullOrEmpty(spriteSet)) actor.spriteSet = spriteSet;
            if (!string.IsNullOrEmpty(slot)) actor.slot = slot;
            if (!string.IsNullOrEmpty(kind)) actor.kind = kind;
            if (!string.IsNullOrEmpty(frame)) actor.frame = frame;

            var sprite = VNAssets.Character(actor.spriteSet, actor.kind, actor.frame);
            if (sprite == null)
            {
                Debug.LogWarning("[VN] Missing sprite: " + actor.spriteSet + "/" + actor.kind + "/" + actor.frame);
                if (isNew) { _actors.Remove(id); Destroy(actor.rt.gameObject); }
                yield break;
            }

            bool spriteChanged = actor.image.sprite != sprite;
            ApplySprite(actor, sprite);
            actor.rt.anchoredPosition = new Vector2(SlotX(actor.slot), -24f);

            if (isNew)
            {
                // New arrivals rise a little as they fade in.
                float from = -70f, to = -24f;
                for (float t = 0f; t < fade; t += Time.deltaTime)
                {
                    float k = VNEase.OutCubic(t / Mathf.Max(0.0001f, fade));
                    actor.group.alpha = k;
                    actor.rt.anchoredPosition = new Vector2(SlotX(actor.slot), Mathf.Lerp(from, to, k));
                    yield return null;
                }
                actor.group.alpha = 1f;
                actor.rt.anchoredPosition = new Vector2(SlotX(actor.slot), to);
            }
            else if (spriteChanged && fade > 0.01f)
            {
                // A pose or expression swap reads better as a short dip than a hard cut.
                const float dip = 0.10f;
                for (float t = 0f; t < dip; t += Time.deltaTime)
                {
                    actor.group.alpha = Mathf.Lerp(0.55f, 1f, t / dip);
                    yield return null;
                }
                actor.group.alpha = 1f;
            }
            else
            {
                actor.group.alpha = 1f;
            }
        }

        void ApplySprite(Actor actor, Sprite sprite)
        {
            actor.image.sprite = sprite;
            float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            actor.rt.sizeDelta = new Vector2(CharHeight * aspect, CharHeight);
        }

        public IEnumerator Hide(string id, float fade)
        {
            var actor = Find(id);
            if (actor == null) yield break;
            _actors.Remove(id);

            float start = actor.group.alpha;
            for (float t = 0f; t < fade; t += Time.deltaTime)
            {
                if (actor.group == null) yield break;
                actor.group.alpha = Mathf.Lerp(start, 0f, t / Mathf.Max(0.0001f, fade));
                yield return null;
            }
            if (actor.rt != null) Destroy(actor.rt.gameObject);
        }

        public IEnumerator HideAll(float fade)
        {
            var ids = new List<string>(_actors.Keys);
            foreach (var id in ids) StartCoroutine(Hide(id, fade));
            yield return new WaitForSeconds(fade);
        }

        public IEnumerator Move(string id, string slot, float time)
        {
            var actor = Find(id);
            if (actor == null) yield break;
            actor.slot = slot;

            float from = actor.rt.anchoredPosition.x;
            float to = SlotX(slot);
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = VNEase.InOutCubic(t / Mathf.Max(0.0001f, time));
                actor.rt.anchoredPosition = new Vector2(Mathf.Lerp(from, to, k), actor.rt.anchoredPosition.y);
                yield return null;
            }
            actor.rt.anchoredPosition = new Vector2(to, actor.rt.anchoredPosition.y);
        }

        /// <summary>Everyone but the speaker steps back into shadow.</summary>
        public void Highlight(string speakerId)
        {
            foreach (var kv in _actors)
            {
                bool lit = string.IsNullOrEmpty(speakerId) || kv.Key.Equals(speakerId, StringComparison.OrdinalIgnoreCase);
                var img = kv.Value.image;
                if (img == null) continue;
                img.color = lit ? Color.white : new Color(0.52f, 0.54f, 0.62f, 1f);
                kv.Value.rt.SetSiblingIndex(lit ? _charLayer.childCount - 1 : 0);
            }
        }

        public void ClearAllImmediate()
        {
            foreach (var kv in _actors)
                if (kv.Value.rt != null) Destroy(kv.Value.rt.gameObject);
            _actors.Clear();
        }

        // ---------------------------------------------------------------- effects

        public IEnumerator Shake(float duration, float magnitude = 26f)
        {
            var rt = (RectTransform)transform;
            Vector2 home = rt.anchoredPosition;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float damp = 1f - (t / Mathf.Max(0.0001f, duration));
                rt.anchoredPosition = home + new Vector2(
                    UnityEngine.Random.Range(-1f, 1f) * magnitude * damp,
                    UnityEngine.Random.Range(-1f, 1f) * magnitude * damp * 0.6f);
                yield return null;
            }
            rt.anchoredPosition = home;
        }

        public IEnumerator FlashScreen(Color color, float duration)
        {
            _flash.color = new Color(color.r, color.g, color.b, 0f);
            float up = duration * 0.22f;
            for (float t = 0f; t < up; t += Time.deltaTime)
            {
                UIKit.SetAlpha(_flash, t / up);
                yield return null;
            }
            float down = duration - up;
            for (float t = 0f; t < down; t += Time.deltaTime)
            {
                UIKit.SetAlpha(_flash, 1f - VNEase.OutCubic(t / Mathf.Max(0.0001f, down)));
                yield return null;
            }
            UIKit.SetAlpha(_flash, 0f);
        }
    }

    /// <summary>The dialogue box: portrait, name plate, typewritten body text and the advance hint.</summary>
    public class VNDialogueBox : MonoBehaviour
    {
        RectTransform _root;
        CanvasGroup _group;
        Image _portraitMask;
        Image _portrait;
        Image _namePlate;
        TextMeshProUGUI _nameText;
        TextMeshProUGUI _bodyText;
        TextMeshProUGUI _indicator;

        bool _portraitVisible;

        public bool Visible { get { return _group.alpha > 0.01f; } }
        public TextMeshProUGUI Body { get { return _bodyText; } }

        public static VNDialogueBox Build(Transform parent)
        {
            var root = UIKit.Rect("DialogueBox", parent);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.offsetMin = new Vector2(96f, 44f);
            root.offsetMax = new Vector2(-96f, 44f + 340f);

            var box = root.gameObject.AddComponent<VNDialogueBox>();
            box.Construct(root);
            return box;
        }

        void Construct(RectTransform root)
        {
            _root = root;
            _group = UIKit.Group(root);

            var panel = UIKit.Panel("Plate", root, VNTheme.Panel, 26);
            UIKit.Stretch(panel.rectTransform);

            var edge = UIKit.Panel("Edge", root, new Color(1f, 1f, 1f, 0.10f), 26);
            UIKit.Stretch(edge.rectTransform, -2f, -2f, -2f, -2f);
            edge.transform.SetSiblingIndex(0);

            // Portrait sits at the left, breaking out over the top edge of the plate.
            var holder = UIKit.Rect("Portrait", root);
            holder.anchorMin = new Vector2(0f, 1f);
            holder.anchorMax = new Vector2(0f, 1f);
            holder.pivot = new Vector2(0.5f, 0.5f);
            holder.anchoredPosition = new Vector2(158f, -132f);
            holder.sizeDelta = new Vector2(268f, 268f);

            var ring = UIKit.Img("Ring", holder, VNTextures.Circle(), new Color(1f, 1f, 1f, 0.22f));
            UIKit.Stretch(ring.rectTransform, -6f, -6f, -6f, -6f);

            _portraitMask = UIKit.Img("Mask", holder, VNTextures.Circle(), Color.white);
            UIKit.Stretch(_portraitMask.rectTransform);
            var mask = _portraitMask.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            _portrait = UIKit.Img("Face", _portraitMask.rectTransform, null, Color.white);
            UIKit.Stretch(_portrait.rectTransform, -14f, -14f, -14f, -14f);
            _portrait.preserveAspect = true;

            _namePlate = UIKit.Panel("NamePlate", root, VNTheme.PanelLift, 16);
            _namePlate.rectTransform.anchorMin = new Vector2(0f, 1f);
            _namePlate.rectTransform.anchorMax = new Vector2(0f, 1f);
            _namePlate.rectTransform.pivot = new Vector2(0f, 1f);
            _namePlate.rectTransform.anchoredPosition = new Vector2(310f, 26f);
            _namePlate.rectTransform.sizeDelta = new Vector2(340f, 58f);

            _nameText = UIKit.Text("Name", _namePlate.rectTransform, "", VNTheme.SizeName, VNTheme.Accent,
                TextAlignmentOptions.Center, true);
            UIKit.Stretch(_nameText.rectTransform, 24f, 24f, 4f, 4f);

            _bodyText = UIKit.Text("Body", root, "", VNTheme.SizeBody, VNTheme.Ink, TextAlignmentOptions.TopLeft);
            UIKit.Stretch(_bodyText.rectTransform, 316f, 72f, 44f, 62f);
            _bodyText.lineSpacing = 12f;

            _indicator = UIKit.Text("More", root, "▼", 30f, VNTheme.Accent, TextAlignmentOptions.Center);
            _indicator.rectTransform.anchorMin = new Vector2(1f, 0f);
            _indicator.rectTransform.anchorMax = new Vector2(1f, 0f);
            _indicator.rectTransform.pivot = new Vector2(1f, 0f);
            _indicator.rectTransform.anchoredPosition = new Vector2(-34f, 26f);
            _indicator.rectTransform.sizeDelta = new Vector2(40f, 40f);
            _indicator.alpha = 0f;

            _group.alpha = 0f;
        }

        public void SetVisible(bool visible, bool instant = false)
        {
            StopAllCoroutines();
            if (instant) { _group.alpha = visible ? 1f : 0f; return; }
            StartCoroutine(FadeGroup(visible ? 1f : 0f, 0.22f));
        }

        IEnumerator FadeGroup(float target, float time)
        {
            float from = _group.alpha;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                _group.alpha = Mathf.Lerp(from, target, t / time);
                yield return null;
            }
            _group.alpha = target;
        }

        public void SetSpeaker(string displayName, Color color, Sprite portrait)
        {
            bool named = !string.IsNullOrEmpty(displayName);
            _namePlate.gameObject.SetActive(named);
            _nameText.text = displayName ?? "";
            _nameText.color = color;

            _portraitVisible = portrait != null;
            _portraitMask.transform.parent.gameObject.SetActive(_portraitVisible);
            if (_portraitVisible) _portrait.sprite = portrait;

            var plateRt = _namePlate.rectTransform;
            plateRt.anchoredPosition = new Vector2(_portraitVisible ? 310f : 40f, 26f);
            UIKit.Stretch(_bodyText.rectTransform, _portraitVisible ? 316f : 56f, 72f, 44f, 62f);

            // The plate hugs its label instead of being a fixed slab.
            _nameText.ForceMeshUpdate();
            float w = Mathf.Clamp(_nameText.preferredWidth + 56f, 180f, 620f);
            plateRt.sizeDelta = new Vector2(w, 58f);
        }

        public void SetText(string text, int visibleChars)
        {
            _bodyText.text = text;
            _bodyText.maxVisibleCharacters = visibleChars;
        }

        public int TotalCharacters()
        {
            _bodyText.ForceMeshUpdate();
            return _bodyText.textInfo.characterCount;
        }

        public void SetIndicator(bool on) { _indicator.alpha = on ? 1f : 0f; }

        void Update()
        {
            if (_indicator.alpha > 0.01f)
                _indicator.rectTransform.anchoredPosition =
                    new Vector2(-34f, 26f + Mathf.Sin(Time.unscaledTime * 4.5f) * 5f);
        }

        public RectTransform Root { get { return _root; } }
    }

    /// <summary>Modal list of branch options.</summary>
    public class VNChoiceMenu : MonoBehaviour
    {
        RectTransform _root;
        RectTransform _list;
        Image _scrim;
        Action<int> _onPick;

        public bool IsOpen { get { return _root.gameObject.activeSelf; } }

        public static VNChoiceMenu Build(Transform parent)
        {
            var root = UIKit.Rect("Choices", parent);
            UIKit.Stretch(root);
            var menu = root.gameObject.AddComponent<VNChoiceMenu>();
            menu.Construct(root);
            return menu;
        }

        void Construct(RectTransform root)
        {
            _root = root;

            _scrim = UIKit.Img("Scrim", root, VNTextures.Solid(), new Color(0f, 0f, 0f, 0.42f), true);
            UIKit.Stretch(_scrim.rectTransform);

            _list = UIKit.Rect("List", root);
            _list.anchorMin = new Vector2(0.5f, 0.5f);
            _list.anchorMax = new Vector2(0.5f, 0.5f);
            _list.pivot = new Vector2(0.5f, 0.5f);
            _list.anchoredPosition = new Vector2(0f, 60f);
            _list.sizeDelta = new Vector2(1080f, 100f);

            var layout = _list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            root.gameObject.SetActive(false);
        }

        public void Open(List<string> labels, Action<int> onPick)
        {
            _onPick = onPick;

            // Detach before destroying: Destroy is deferred, and stale children would
            // otherwise still take part in this frame's layout pass.
            for (int i = _list.childCount - 1; i >= 0; i--)
            {
                var child = _list.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            // The buttons run their own pop-in coroutines, so the root has to be live first.
            _root.gameObject.SetActive(true);

            for (int i = 0; i < labels.Count; i++)
            {
                int index = i;
                var btn = UIKit.Btn("Choice" + i, _list, labels[i], new Vector2(1080f, 94f), () => Pick(index), 34f);
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 94f;
                le.preferredWidth = 1080f;

                var label = UIKit.LabelOf(btn);
                if (label != null) label.alignment = TextAlignmentOptions.Center;

                StartCoroutine(PopIn(btn.transform as RectTransform, i * 0.05f));
            }
        }

        IEnumerator PopIn(RectTransform rt, float delay)
        {
            if (rt == null) yield break;
            var cg = UIKit.Group(rt);
            cg.alpha = 0f;
            rt.localScale = new Vector3(0.96f, 0.96f, 1f);
            yield return new WaitForSeconds(delay);

            const float time = 0.22f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                float k = VNEase.OutBack(t / time);
                cg.alpha = Mathf.Clamp01(t / time * 1.6f);
                rt.localScale = Vector3.Lerp(new Vector3(0.96f, 0.96f, 1f), Vector3.one, k);
                yield return null;
            }
            cg.alpha = 1f;
            rt.localScale = Vector3.one;
        }

        void Pick(int index)
        {
            var cb = _onPick;
            _onPick = null;
            Close();
            if (cb != null) cb(index);
        }

        public void Close()
        {
            _root.gameObject.SetActive(false);
        }
    }
}
