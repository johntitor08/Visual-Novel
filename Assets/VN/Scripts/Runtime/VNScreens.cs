// VNScreens.cs -- title, settings, save/load, backlog, name entry, chapter and ending cards.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VN
{
    /// <summary>Full-screen black (or white) curtain used for every hard transition.</summary>
    public class VNFader : MonoBehaviour
    {
        Image _img;

        public static VNFader Build(Transform parent)
        {
            var img = UIKit.Img("Fader", parent, VNTextures.Solid(), new Color(0f, 0f, 0f, 1f), false);
            UIKit.Stretch(img.rectTransform);
            var f = img.gameObject.AddComponent<VNFader>();
            f._img = img;
            return f;
        }

        public bool Blocking { get { return _img.color.a > 0.98f; } }

        public void SetImmediate(float alpha, Color? color = null)
        {
            var c = color ?? _img.color;
            c.a = alpha;
            _img.color = c;
            _img.raycastTarget = alpha > 0.98f;
        }

        public IEnumerator To(float alpha, float time, Color? color = null)
        {
            if (color.HasValue)
            {
                var c = color.Value; c.a = _img.color.a; _img.color = c;
            }
            float from = _img.color.a;
            _img.raycastTarget = true;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                UIKit.SetAlpha(_img, Mathf.Lerp(from, alpha, VNEase.InOutCubic(t / Mathf.Max(0.0001f, time))));
                yield return null;
            }
            UIKit.SetAlpha(_img, alpha);
            _img.raycastTarget = alpha > 0.98f;
        }
    }

    /// <summary>Base for the modal panels: a scrim, a card, and show/hide with a small rise.</summary>
    public abstract class VNScreen : MonoBehaviour
    {
        protected RectTransform Root;
        protected RectTransform Card;
        protected CanvasGroup Group;

        public bool IsOpen { get { return Root != null && Root.gameObject.activeSelf; } }

        protected void MakeShell(RectTransform root, Vector2 cardSize, float scrimAlpha = 0.72f)
        {
            Root = root;
            UIKit.Stretch(root);
            Group = UIKit.Group(root);

            var scrim = UIKit.Img("Scrim", root, VNTextures.Solid(), new Color(0.02f, 0.03f, 0.05f, scrimAlpha), true);
            UIKit.Stretch(scrim.rectTransform);

            var card = UIKit.Panel("Card", root, VNTheme.PanelSolid, 28, true);
            card.rectTransform.sizeDelta = cardSize;
            Card = card.rectTransform;

            root.gameObject.SetActive(false);
        }

        public virtual void Open()
        {
            Root.gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(Animate(true));
        }

        public virtual void Close()
        {
            if (Root == null || !Root.gameObject.activeSelf) return;
            StopAllCoroutines();
            StartCoroutine(Animate(false));
        }

        IEnumerator Animate(bool opening)
        {
            const float time = 0.18f;
            float fromA = opening ? 0f : 1f, toA = opening ? 1f : 0f;
            Vector2 home = Card.anchoredPosition;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                float k = t / time;
                Group.alpha = Mathf.Lerp(fromA, toA, k);
                float rise = Mathf.Lerp(opening ? 26f : 0f, opening ? 0f : 18f, VNEase.OutCubic(k));
                Card.anchoredPosition = new Vector2(home.x, home.y - rise);
                yield return null;
            }
            Group.alpha = toA;
            Card.anchoredPosition = home;
            if (!opening) Root.gameObject.SetActive(false);
        }
    }

    // ------------------------------------------------------------------ title

    public class VNTitleScreen : VNScreen
    {
        public Action OnNewGame, OnContinue, OnLoad, OnSettings, OnQuit;

        Button _continueBtn;
        TextMeshProUGUI _progress;

        public static VNTitleScreen Build(Transform parent)
        {
            var root = UIKit.Rect("TitleScreen", parent);
            var s = root.gameObject.AddComponent<VNTitleScreen>();
            s.Construct(root);
            return s;
        }

        void Construct(RectTransform root)
        {
            Root = root;
            UIKit.Stretch(root);
            Group = UIKit.Group(root);

            var bg = UIKit.Img("TitleBg", root, VNProcBg.Get("title"), Color.white, true);
            UIKit.Stretch(bg.rectTransform);

            // A standing sprite anchors the right side of the frame.
            var art = VNAssets.Character("v1-schoolgirl", "poses", "01-standing");
            if (art != null)
            {
                var img = UIKit.Img("TitleArt", root, art, new Color(1f, 1f, 1f, 0.96f));
                img.preserveAspect = true;
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                float h = 1120f;
                rt.sizeDelta = new Vector2(h * (art.rect.width / art.rect.height), h);
                rt.anchoredPosition = new Vector2(430f, -60f);
            }

            var vignette = UIKit.Img("LeftShade", root, VNTextures.Solid(), new Color(0.03f, 0.04f, 0.07f, 0.55f));
            UIKit.Stretch(vignette.rectTransform, 0f, 980f, 0f, 0f);

            var title = UIKit.Text("Title", root, "WHERE THE\nSIGNAL ENDS", VNTheme.SizeTitle, VNTheme.Ink,
                TextAlignmentOptions.TopLeft, true);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(140f, -170f);
            title.rectTransform.sizeDelta = new Vector2(900f, 340f);
            title.characterSpacing = 6f;
            title.lineSpacing = -14f;

            var rule = UIKit.Img("Rule", root, VNTextures.Solid(), VNTheme.Accent);
            rule.rectTransform.anchorMin = new Vector2(0f, 1f);
            rule.rectTransform.anchorMax = new Vector2(0f, 1f);
            rule.rectTransform.pivot = new Vector2(0f, 1f);
            rule.rectTransform.anchoredPosition = new Vector2(146f, -520f);
            rule.rectTransform.sizeDelta = new Vector2(180f, 5f);

            var tag = UIKit.Text("Tagline", root, "Kanamori Bay  ·  three days before the town is closed",
                26f, VNTheme.InkDim, TextAlignmentOptions.TopLeft);
            tag.rectTransform.anchorMin = new Vector2(0f, 1f);
            tag.rectTransform.anchorMax = new Vector2(0f, 1f);
            tag.rectTransform.pivot = new Vector2(0f, 1f);
            tag.rectTransform.anchoredPosition = new Vector2(146f, -556f);
            tag.rectTransform.sizeDelta = new Vector2(820f, 60f);

            var menu = UIKit.Rect("Menu", root);
            menu.anchorMin = new Vector2(0f, 0f);
            menu.anchorMax = new Vector2(0f, 0f);
            menu.pivot = new Vector2(0f, 0f);
            menu.anchoredPosition = new Vector2(146f, 210f);
            menu.sizeDelta = new Vector2(420f, 100f);

            var layout = menu.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = menu.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddItem(menu, "New Game", () => Fire(OnNewGame), true);
            _continueBtn = AddItem(menu, "Continue", () => Fire(OnContinue), false);
            AddItem(menu, "Load", () => Fire(OnLoad), false);
            AddItem(menu, "Settings", () => Fire(OnSettings), false);
            AddItem(menu, "Quit", () => Fire(OnQuit), false);

            _progress = UIKit.Text("Progress", root, "", VNTheme.SizeSmall, VNTheme.InkDim, TextAlignmentOptions.BottomLeft);
            _progress.rectTransform.anchorMin = new Vector2(0f, 0f);
            _progress.rectTransform.anchorMax = new Vector2(0f, 0f);
            _progress.rectTransform.pivot = new Vector2(0f, 0f);
            _progress.rectTransform.anchoredPosition = new Vector2(146f, 120f);
            _progress.rectTransform.sizeDelta = new Vector2(760f, 40f);

            root.gameObject.SetActive(false);
        }

        static void Fire(Action a) { if (a != null) a(); }

        Button AddItem(Transform parent, string label, Action onClick, bool primary)
        {
            var btn = UIKit.Btn("Menu_" + label, parent, label, new Vector2(420f, 68f), onClick, 32f, primary);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 68f;
            le.preferredWidth = 420f;
            var t = UIKit.LabelOf(btn);
            if (t != null) t.alignment = TextAlignmentOptions.Left;
            return btn;
        }

        public override void Open()
        {
            bool hasSave = VNSaveSystem.MostRecentSlot() >= 0;
            if (_continueBtn != null) _continueBtn.interactable = hasSave;

            var g = VNSaveSystem.ReadGlobal();
            _progress.text = g.endingsSeen.Count == 0
                ? "No endings recorded yet."
                : "Endings found: " + g.endingsSeen.Count + " / " + VNEndings.Total;

            Root.gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }

        IEnumerator FadeIn()
        {
            const float time = 0.35f;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                Group.alpha = t / time;
                yield return null;
            }
            Group.alpha = 1f;
        }

        public override void Close()
        {
            StopAllCoroutines();
            Group.alpha = 0f;
            Root.gameObject.SetActive(false);
        }
    }

    public static class VNEndings
    {
        public const int Total = 5;
    }

    // ------------------------------------------------------------------ settings

    public class VNSettingsScreen : VNScreen
    {
        public Action OnClosed;
        public Action OnAudioChanged;

        public static VNSettingsScreen Build(Transform parent)
        {
            var root = UIKit.Rect("SettingsScreen", parent);
            var s = root.gameObject.AddComponent<VNSettingsScreen>();
            s.Construct(root);
            return s;
        }

        void Construct(RectTransform root)
        {
            MakeShell(root, new Vector2(960f, 760f));

            var title = UIKit.Text("Header", Card, "Settings", 52f, VNTheme.Ink, TextAlignmentOptions.TopLeft, true);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(56f, -110f);
            title.rectTransform.offsetMax = new Vector2(-56f, -44f);

            var rows = UIKit.Rect("Rows", Card);
            rows.anchorMin = new Vector2(0f, 1f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.pivot = new Vector2(0.5f, 1f);
            rows.offsetMin = new Vector2(56f, -640f);
            rows.offsetMax = new Vector2(-56f, -140f);

            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            SliderRow(rows, "Text speed", 10f, 140f, VNSettings.TextSpeed,
                v => VNSettings.TextSpeed = v,
                v => v >= 139f ? "Instant" : Mathf.RoundToInt(v) + " cps");

            SliderRow(rows, "Auto-advance hold", 0.2f, 4f, VNSettings.AutoDelay,
                v => VNSettings.AutoDelay = v,
                v => v.ToString("0.0", CultureInfo.InvariantCulture) + " s");

            SliderRow(rows, "Music volume", 0f, 1f, VNSettings.BgmVolume,
                v => { VNSettings.BgmVolume = v; if (OnAudioChanged != null) OnAudioChanged(); },
                v => Mathf.RoundToInt(v * 100f) + "%");

            SliderRow(rows, "Effects volume", 0f, 1f, VNSettings.SfxVolume,
                v => VNSettings.SfxVolume = v,
                v => Mathf.RoundToInt(v * 100f) + "%");

            var toggles = UIKit.Rect("Toggles", rows);
            toggles.sizeDelta = new Vector2(800f, 62f);
            var tLayout = toggles.gameObject.AddComponent<HorizontalLayoutGroup>();
            tLayout.spacing = 18f;
            tLayout.childAlignment = TextAnchor.MiddleLeft;
            tLayout.childControlWidth = true;
            tLayout.childControlHeight = true;
            tLayout.childForceExpandWidth = false;
            tLayout.childForceExpandHeight = false;
            var tle = toggles.gameObject.AddComponent<LayoutElement>();
            tle.preferredHeight = 62f;

            AddToggle(toggles, "Typing sound", VNSettings.TypeSound, v => VNSettings.TypeSound = v);
            AddToggle(toggles, "Skip unread", VNSettings.SkipUnread, v => VNSettings.SkipUnread = v);

            var close = UIKit.Btn("Close", Card, "Back", new Vector2(260f, 68f), () =>
            {
                Close();
                if (OnClosed != null) OnClosed();
            }, 30f, true);
            close.image.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            close.image.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            close.image.rectTransform.pivot = new Vector2(0.5f, 0f);
            close.image.rectTransform.anchoredPosition = new Vector2(0f, 44f);
        }

        static void AddToggle(Transform parent, string label, bool state, Action<bool> onChange)
        {
            var btn = UIKit.Toggle("T_" + label, parent, label, state, onChange);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 340f;
            le.preferredHeight = 62f;
        }

        static void SliderRow(Transform parent, string label, float min, float max, float value,
                              Action<float> onChange, Func<float, string> format)
        {
            var row = UIKit.Rect("Row_" + label, parent);
            row.sizeDelta = new Vector2(800f, 74f);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 74f;

            var name = UIKit.Text("Label", row, label, 30f, VNTheme.Ink, TextAlignmentOptions.Left);
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(0f, 1f);
            name.rectTransform.pivot = new Vector2(0f, 0.5f);
            name.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            name.rectTransform.sizeDelta = new Vector2(330f, 0f);

            var readout = UIKit.Text("Value", row, format(value), 28f, VNTheme.Accent, TextAlignmentOptions.Right);
            readout.rectTransform.anchorMin = new Vector2(1f, 0f);
            readout.rectTransform.anchorMax = new Vector2(1f, 1f);
            readout.rectTransform.pivot = new Vector2(1f, 0.5f);
            readout.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            readout.rectTransform.sizeDelta = new Vector2(190f, 0f);

            var slider = UIKit.MakeSlider("Slider", row, min, max, value, v =>
            {
                onChange(v);
                readout.text = format(v);
            });
            slider.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0.5f);
            slider.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0.5f);
            slider.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);
            slider.GetComponent<RectTransform>().anchoredPosition = new Vector2(340f, 0f);
            slider.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 30f);
        }
    }

    // ------------------------------------------------------------------ save / load

    public class VNSaveScreen : VNScreen
    {
        public Func<int, bool> OnSaveSlot;    // returns true when the save succeeded
        public Func<int, bool> OnLoadSlot;
        public Action OnClosed;

        bool _saveMode;
        readonly List<Button> _slotButtons = new List<Button>();
        readonly List<TextMeshProUGUI> _slotLabels = new List<TextMeshProUGUI>();
        readonly List<Image> _slotThumbs = new List<Image>();
        TextMeshProUGUI _header;

        public static VNSaveScreen Build(Transform parent)
        {
            var root = UIKit.Rect("SaveScreen", parent);
            var s = root.gameObject.AddComponent<VNSaveScreen>();
            s.Construct(root);
            return s;
        }

        void Construct(RectTransform root)
        {
            MakeShell(root, new Vector2(1420f, 880f));

            _header = UIKit.Text("Header", Card, "Save", 52f, VNTheme.Ink, TextAlignmentOptions.TopLeft, true);
            _header.rectTransform.anchorMin = new Vector2(0f, 1f);
            _header.rectTransform.anchorMax = new Vector2(1f, 1f);
            _header.rectTransform.pivot = new Vector2(0.5f, 1f);
            _header.rectTransform.offsetMin = new Vector2(56f, -110f);
            _header.rectTransform.offsetMax = new Vector2(-56f, -44f);

            var grid = UIKit.Rect("Grid", Card);
            grid.anchorMin = new Vector2(0f, 0f);
            grid.anchorMax = new Vector2(1f, 1f);
            grid.pivot = new Vector2(0.5f, 0.5f);
            grid.offsetMin = new Vector2(56f, 128f);
            grid.offsetMax = new Vector2(-56f, -132f);

            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(636f, 140f);
            g.spacing = new Vector2(24f, 18f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;
            g.childAlignment = TextAnchor.UpperCenter;

            for (int i = 0; i < VNSaveSystem.SlotCount; i++)
            {
                int slot = i;
                var btn = UIKit.Btn("Slot" + i, grid, "", new Vector2(636f, 140f), () => Activate(slot), 26f);
                _slotButtons.Add(btn);

                var thumb = UIKit.Img("Thumb", btn.transform, null, new Color(1f, 1f, 1f, 0.9f));
                thumb.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                thumb.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                thumb.rectTransform.pivot = new Vector2(0f, 0.5f);
                thumb.rectTransform.anchoredPosition = new Vector2(14f, 0f);
                thumb.rectTransform.sizeDelta = new Vector2(196f, 112f);
                thumb.preserveAspect = true;
                thumb.enabled = false;
                _slotThumbs.Add(thumb);

                var label = UIKit.LabelOf(btn);
                label.alignment = TextAlignmentOptions.TopLeft;
                UIKit.Stretch(label.rectTransform, 226f, 60f, 14f, 14f);
                label.fontSize = 24f;
                _slotLabels.Add(label);

                UIKit.Btn("Del" + i, btn.transform, "×", new Vector2(44f, 44f), () =>
                {
                    VNSaveSystem.Delete(slot);
                    Refresh();
                }, 30f).image.rectTransform.anchoredPosition = new Vector2(636f * 0.5f - 34f, 140f * 0.5f - 34f);
            }

            var close = UIKit.Btn("Close", Card, "Back", new Vector2(260f, 68f), () =>
            {
                Close();
                if (OnClosed != null) OnClosed();
            }, 30f, true);
            close.image.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            close.image.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            close.image.rectTransform.pivot = new Vector2(0.5f, 0f);
            close.image.rectTransform.anchoredPosition = new Vector2(0f, 40f);
        }

        public void Open(bool saveMode)
        {
            _saveMode = saveMode;
            _header.text = saveMode ? "Save" : "Load";
            Refresh();
            base.Open();
        }

        void Activate(int slot)
        {
            if (_saveMode)
            {
                if (OnSaveSlot != null && OnSaveSlot(slot)) Refresh();
            }
            else
            {
                if (!VNSaveSystem.Exists(slot)) return;
                if (OnLoadSlot != null && OnLoadSlot(slot)) Close();
            }
        }

        public void Refresh()
        {
            for (int i = 0; i < VNSaveSystem.SlotCount; i++)
            {
                var data = VNSaveSystem.Read(i);
                var label = _slotLabels[i];
                var thumb = _slotThumbs[i];

                if (data == null)
                {
                    label.text = "<color=#8A8F99>Slot " + (i + 1) + "\nEmpty</color>";
                    thumb.enabled = false;
                    _slotButtons[i].interactable = _saveMode;
                    continue;
                }

                DateTime when;
                string stamp = DateTime.TryParse(data.savedAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out when)
                    ? when.ToLocalTime().ToString("d MMM yyyy  HH:mm", CultureInfo.InvariantCulture)
                    : "";

                string preview = data.preview ?? "";
                if (preview.Length > 74) preview = preview.Substring(0, 72) + "…";

                label.text = "<b>Slot " + (i + 1) + "</b>   <color=#B9BEC7><size=20>" + stamp + "</size></color>\n" +
                             "<color=#E6C089>" + (string.IsNullOrEmpty(data.chapter) ? "—" : data.chapter) + "</color>\n" +
                             "<color=#C9CDD4><size=20>" + preview + "</size></color>";

                var sprite = DecodeThumb(data.screenshot);
                thumb.sprite = sprite;
                thumb.enabled = sprite != null;
                _slotButtons[i].interactable = true;
            }
        }

        static Sprite DecodeThumb(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                var bytes = Convert.FromBase64String(base64);
                var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                tex.hideFlags = HideFlags.HideAndDontSave;
                if (!tex.LoadImage(bytes)) return null;
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            catch { return null; }
        }
    }

    // ------------------------------------------------------------------ backlog

    public class VNBacklogScreen : VNScreen
    {
        public Action OnClosed;
        RectTransform _content;
        ScrollRect _scroll;

        public static VNBacklogScreen Build(Transform parent)
        {
            var root = UIKit.Rect("Backlog", parent);
            var s = root.gameObject.AddComponent<VNBacklogScreen>();
            s.Construct(root);
            return s;
        }

        void Construct(RectTransform root)
        {
            MakeShell(root, new Vector2(1420f, 880f));

            var header = UIKit.Text("Header", Card, "History", 52f, VNTheme.Ink, TextAlignmentOptions.TopLeft, true);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.offsetMin = new Vector2(56f, -110f);
            header.rectTransform.offsetMax = new Vector2(-56f, -44f);

            var listRoot = UIKit.ScrollList("List", Card, out _scroll, 16f, new RectOffset(8, 24, 8, 8));
            _content = listRoot;
            var viewport = (RectTransform)listRoot.parent;
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.offsetMin = new Vector2(48f, 128f);
            viewport.offsetMax = new Vector2(-48f, -128f);

            var close = UIKit.Btn("Close", Card, "Back", new Vector2(260f, 68f), () =>
            {
                Close();
                if (OnClosed != null) OnClosed();
            }, 30f, true);
            close.image.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            close.image.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            close.image.rectTransform.pivot = new Vector2(0.5f, 0f);
            close.image.rectTransform.anchoredPosition = new Vector2(0f, 40f);
        }

        public void Open(List<VNHistoryEntry> entries)
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            if (entries != null)
            {
                foreach (var e in entries)
                {
                    string speaker = string.IsNullOrEmpty(e.speaker)
                        ? ""
                        : "<color=" + (string.IsNullOrEmpty(e.colorHex) ? "#F0B468" : e.colorHex) + "><b>" +
                          e.speaker + "</b></color>\n";

                    // The layout group sizes the row from the text's own preferred height,
                    // which TMP reports against the width it has just been given.
                    var text = UIKit.Text("Entry", _content, speaker + e.text, 30f,
                        string.IsNullOrEmpty(e.speaker) ? VNTheme.InkDim : VNTheme.Ink, TextAlignmentOptions.TopLeft);
                    text.margin = new Vector4(14f, 6f, 14f, 10f);
                }
            }

            base.Open();
            StartCoroutine(ScrollToBottom());
        }

        IEnumerator ScrollToBottom()
        {
            yield return null;
            yield return null;
            if (_scroll != null) _scroll.verticalNormalizedPosition = 0f;
        }
    }

    // ------------------------------------------------------------------ name entry

    public class VNNamePrompt : VNScreen
    {
        TMP_InputField _field;
        Action<string> _done;

        public static VNNamePrompt Build(Transform parent)
        {
            var root = UIKit.Rect("NamePrompt", parent);
            var s = root.gameObject.AddComponent<VNNamePrompt>();
            s.Construct(root);
            return s;
        }

        void Construct(RectTransform root)
        {
            MakeShell(root, new Vector2(880f, 420f));

            var header = UIKit.Text("Header", Card, "What should they call you?", 40f, VNTheme.Ink,
                TextAlignmentOptions.Center, true);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.offsetMin = new Vector2(48f, -130f);
            header.rectTransform.offsetMax = new Vector2(-48f, -56f);

            _field = UIKit.Input("Field", Card, "Enter a name", new Vector2(640f, 86f));
            ((RectTransform)_field.transform).anchoredPosition = new Vector2(0f, 6f);
            _field.onSubmit.AddListener(_ => Accept());

            var ok = UIKit.Btn("OK", Card, "Begin", new Vector2(260f, 70f), Accept, 30f, true);
            ok.image.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            ok.image.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            ok.image.rectTransform.pivot = new Vector2(0.5f, 0f);
            ok.image.rectTransform.anchoredPosition = new Vector2(0f, 46f);
        }

        public void Ask(string suggestion, Action<string> done)
        {
            _done = done;
            _field.text = suggestion ?? "";
            base.Open();
            StartCoroutine(Focus());
        }

        IEnumerator Focus()
        {
            yield return null;
            _field.Select();
            _field.ActivateInputField();
        }

        void Accept()
        {
            string value = (_field.text ?? "").Trim();
            if (value.Length == 0) value = "Kai";
            var cb = _done;
            _done = null;
            Close();
            if (cb != null) cb(value);
        }
    }

    // ------------------------------------------------------------------ cards

    /// <summary>The chapter title card, shown briefly over a darkened stage.</summary>
    public class VNChapterCard : MonoBehaviour
    {
        RectTransform _root;
        CanvasGroup _group;
        TextMeshProUGUI _title, _subtitle;

        public static VNChapterCard Build(Transform parent)
        {
            var root = UIKit.Rect("ChapterCard", parent);
            UIKit.Stretch(root);
            var c = root.gameObject.AddComponent<VNChapterCard>();
            c.Construct(root);
            return c;
        }

        void Construct(RectTransform root)
        {
            _root = root;
            _group = UIKit.Group(root);
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            var scrim = UIKit.Img("Scrim", root, VNTextures.Solid(), new Color(0.02f, 0.03f, 0.05f, 0.86f));
            UIKit.Stretch(scrim.rectTransform);

            _title = UIKit.Text("Title", root, "", 96f, VNTheme.Ink, TextAlignmentOptions.Center, true);
            _title.rectTransform.sizeDelta = new Vector2(1400f, 150f);
            _title.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            _title.characterSpacing = 8f;

            var rule = UIKit.Img("Rule", root, VNTextures.Solid(), VNTheme.Accent);
            rule.rectTransform.sizeDelta = new Vector2(160f, 4f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, -40f);

            _subtitle = UIKit.Text("Sub", root, "", 34f, VNTheme.InkDim, TextAlignmentOptions.Center);
            _subtitle.rectTransform.sizeDelta = new Vector2(1200f, 80f);
            _subtitle.rectTransform.anchoredPosition = new Vector2(0f, -96f);

            root.gameObject.SetActive(false);
        }

        public IEnumerator Show(string title, string subtitle, float hold = 2.2f)
        {
            _title.text = title ?? "";
            _subtitle.text = subtitle ?? "";
            _root.gameObject.SetActive(true);

            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                _group.alpha = VNEase.OutCubic(t / 0.5f);
                yield return null;
            }
            _group.alpha = 1f;
            yield return new WaitForSeconds(hold);

            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                _group.alpha = 1f - VNEase.InOutCubic(t / 0.6f);
                yield return null;
            }
            _group.alpha = 0f;
            _root.gameObject.SetActive(false);
        }
    }

    /// <summary>Shown when the scenario reaches an @end.</summary>
    public class VNEndingCard : VNScreen
    {
        public Action OnDismiss;
        TextMeshProUGUI _title, _subtitle, _tally;

        public static VNEndingCard Build(Transform parent)
        {
            var root = UIKit.Rect("EndingCard", parent);
            var c = root.gameObject.AddComponent<VNEndingCard>();
            c.Construct(root);
            return c;
        }

        void Construct(RectTransform root)
        {
            MakeShell(root, new Vector2(1100f, 520f), 0.9f);

            var eyebrow = UIKit.Text("Eyebrow", Card, "ENDING", 26f, VNTheme.Accent, TextAlignmentOptions.Center);
            eyebrow.rectTransform.anchorMin = new Vector2(0f, 1f);
            eyebrow.rectTransform.anchorMax = new Vector2(1f, 1f);
            eyebrow.rectTransform.pivot = new Vector2(0.5f, 1f);
            eyebrow.rectTransform.offsetMin = new Vector2(40f, -108f);
            eyebrow.rectTransform.offsetMax = new Vector2(-40f, -64f);
            eyebrow.characterSpacing = 10f;

            _title = UIKit.Text("Title", Card, "", 76f, VNTheme.Ink, TextAlignmentOptions.Center, true);
            _title.rectTransform.anchorMin = new Vector2(0f, 1f);
            _title.rectTransform.anchorMax = new Vector2(1f, 1f);
            _title.rectTransform.pivot = new Vector2(0.5f, 1f);
            _title.rectTransform.offsetMin = new Vector2(40f, -220f);
            _title.rectTransform.offsetMax = new Vector2(-40f, -116f);

            _subtitle = UIKit.Text("Sub", Card, "", 32f, VNTheme.InkDim, TextAlignmentOptions.Top);
            _subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            _subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            _subtitle.rectTransform.offsetMin = new Vector2(72f, -330f);
            _subtitle.rectTransform.offsetMax = new Vector2(-72f, -232f);

            _tally = UIKit.Text("Tally", Card, "", 26f, VNTheme.AccentCool, TextAlignmentOptions.Center);
            _tally.rectTransform.anchorMin = new Vector2(0f, 0f);
            _tally.rectTransform.anchorMax = new Vector2(1f, 0f);
            _tally.rectTransform.pivot = new Vector2(0.5f, 0f);
            _tally.rectTransform.offsetMin = new Vector2(40f, 132f);
            _tally.rectTransform.offsetMax = new Vector2(-40f, 176f);

            var back = UIKit.Btn("ToTitle", Card, "Return to title", new Vector2(340f, 70f), () =>
            {
                Close();
                if (OnDismiss != null) OnDismiss();
            }, 30f, true);
            back.image.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            back.image.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            back.image.rectTransform.pivot = new Vector2(0.5f, 0f);
            back.image.rectTransform.anchoredPosition = new Vector2(0f, 44f);
        }

        public void Show(string endingId, string title, string subtitle)
        {
            VNSaveSystem.MarkEndingSeen(endingId);
            var g = VNSaveSystem.ReadGlobal();

            _title.text = title;
            _subtitle.text = subtitle;
            _tally.text = "Endings found: " + g.endingsSeen.Count + " / " + VNEndings.Total;
            Open();
        }
    }
}
