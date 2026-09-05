// VNGame.cs -- assembles the whole game at runtime and owns the flow between title and story.
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VN
{
    /// <summary>
    /// One object runs everything: it builds the canvas, loads the scenario from
    /// Resources/VN/Story, and switches between the title screen and the played story.
    /// Nothing needs to be wired up in the inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public class VNGame : MonoBehaviour
    {
        public static VNGame Instance { get; private set; }

        public VNStage Stage { get; private set; }
        public VNDialogueBox Dialogue { get; private set; }
        public VNChoiceMenu Choices { get; private set; }
        public VNAudio Audio { get; private set; }
        public VNFader Fader { get; private set; }
        public VNChapterCard ChapterCard { get; private set; }
        public VNNamePrompt NamePrompt { get; private set; }
        public VNDirector Director { get; private set; }

        VNTitleScreen _title;
        VNSettingsScreen _settings;
        VNSaveScreen _saves;
        VNBacklogScreen _backlog;
        VNEndingCard _ending;

        RectTransform _menuBar;
        Button _autoBtn, _skipBtn;
        Canvas _canvas;

        bool _advanceQueued;
        bool _inStory;
        bool _pendingCtrlSkip;
        string _pendingThumb = "";

        public bool AutoPlay { get; private set; }
        public bool Skipping { get; private set; }

        bool AnyScreenOpen
        {
            get
            {
                return (_title != null && _title.IsOpen)
                    || (_settings != null && _settings.IsOpen)
                    || (_saves != null && _saves.IsOpen)
                    || (_backlog != null && _backlog.IsOpen)
                    || (_ending != null && _ending.IsOpen)
                    || (NamePrompt != null && NamePrompt.IsOpen)
                    || (Choices != null && Choices.IsOpen);
            }
        }

        // ---------------------------------------------------------------- setup

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureCamera();
            UIKit.EnsureEventSystem();
            BuildCanvas();

            Audio = gameObject.AddComponent<VNAudio>();

            var script = VNScriptParser.LoadFromResources();
            foreach (var e in script.Errors) Debug.LogError("[VN] " + e);
            if (script.Commands.Count == 0)
                Debug.LogError("[VN] The scenario is empty. Expected .txt files in Assets/VN/Resources/VN/Story.");

            Director = gameObject.AddComponent<VNDirector>();
            Director.Game = this;
            Director.Bind(script);
            Director.PlayerName = script.ProtagonistDefaultName;

            Fader.SetImmediate(1f, Color.black);
            StartCoroutine(OpenTitle(true));
        }

        void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";
            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            cam.orthographic = true;
        }

        void BuildCanvas()
        {
            var canvasGo = new GameObject("VNCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(VNTheme.RefWidth, VNTheme.RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var root = (RectTransform)canvasGo.transform;

            Stage = VNStage.Build(root);
            UIKit.ClickCatcher("AdvanceArea", root, QueueAdvance);
            Dialogue = VNDialogueBox.Build(root);
            BuildMenuBar(root);
            Choices = VNChoiceMenu.Build(root);
            ChapterCard = VNChapterCard.Build(root);

            _backlog = VNBacklogScreen.Build(root);
            _backlog.OnClosed = () => { };

            _saves = VNSaveScreen.Build(root);
            _saves.OnSaveSlot = SaveToSlot;
            _saves.OnLoadSlot = LoadFromSlot;

            _settings = VNSettingsScreen.Build(root);
            _settings.OnAudioChanged = () => Audio.RefreshVolume();

            NamePrompt = VNNamePrompt.Build(root);

            _ending = VNEndingCard.Build(root);
            _ending.OnDismiss = () => StartCoroutine(OpenTitle(false));

            _title = VNTitleScreen.Build(root);
            _title.OnNewGame = NewGame;
            _title.OnContinue = ContinueLatest;
            _title.OnLoad = () => _saves.Open(false);
            _title.OnSettings = () => _settings.Open();
            _title.OnQuit = Quit;

            Fader = VNFader.Build(root);
        }

        void BuildMenuBar(RectTransform root)
        {
            _menuBar = UIKit.Rect("MenuBar", root);
            _menuBar.anchorMin = new Vector2(1f, 1f);
            _menuBar.anchorMax = new Vector2(1f, 1f);
            _menuBar.pivot = new Vector2(1f, 1f);
            _menuBar.anchoredPosition = new Vector2(-40f, -28f);
            _menuBar.sizeDelta = new Vector2(880f, 56f);

            var layout = _menuBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            _autoBtn = Bar("Auto", () => SetAutoPlay(!AutoPlay));
            _skipBtn = Bar("Skip", () => SetSkipping(!Skipping));
            Bar("History", OpenBacklog);
            Bar("Save", RequestSave);
            Bar("Load", () => _saves.Open(false));
            Bar("Config", () => _settings.Open());
            Bar("Title", () => StartCoroutine(OpenTitle(false)));

            _menuBar.gameObject.SetActive(false);
        }

        Button Bar(string label, Action onClick)
        {
            var btn = UIKit.Btn("Bar_" + label, _menuBar, label, new Vector2(120f, 52f), () =>
            {
                Audio.PlayClick();
                onClick();
            }, VNTheme.SizeSmall);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = label.Length > 5 ? 148f : 120f;
            le.preferredHeight = 52f;
            UIKit.SetAlpha(btn.image, 0.62f);
            return btn;
        }

        // ---------------------------------------------------------------- flow

        IEnumerator OpenTitle(bool firstRun)
        {
            _inStory = false;
            SetAutoPlay(false);
            SetSkipping(false);

            if (!firstRun) yield return Fader.To(1f, 0.5f, Color.black);

            Director.Stop();
            Stage.ClearAllImmediate();
            Dialogue.SetVisible(false, true);
            Choices.Close();
            _menuBar.gameObject.SetActive(false);

            Audio.PlayBgm("title", 1.4f);
            _title.Open();

            yield return Fader.To(0f, firstRun ? 1.1f : 0.6f);
        }

        void NewGame()
        {
            Audio.PlayClick();
            StartCoroutine(BeginStory(null));
        }

        void ContinueLatest()
        {
            int slot = VNSaveSystem.MostRecentSlot();
            if (slot < 0) return;
            Audio.PlayClick();
            LoadFromSlot(slot);
        }

        IEnumerator BeginStory(VNSaveData data)
        {
            yield return Fader.To(1f, 0.55f, Color.black);

            _title.Close();
            _settings.Close();
            _saves.Close();
            Stage.ClearAllImmediate();
            Stage.SetBackgroundImmediate("black");
            Dialogue.SetVisible(false, true);
            Audio.StopBgm(0.4f);

            _inStory = true;
            _menuBar.gameObject.SetActive(true);
            _advanceQueued = false;

            yield return Fader.To(0f, 0.5f);

            if (data == null) Director.StartNew();
            else Director.Restore(data);
        }

        public void OnScriptRanOut()
        {
            if (!_inStory) return;
            StartCoroutine(OpenTitle(false));
        }

        public void ShowEnding(string id, string title, string subtitle)
        {
            _inStory = false;
            _menuBar.gameObject.SetActive(false);
            StartCoroutine(EndingRoutine(id, title, subtitle));
        }

        IEnumerator EndingRoutine(string id, string title, string subtitle)
        {
            yield return Fader.To(0f, 0.6f);
            _ending.Show(id, title, subtitle);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------------------------------------------------------- modes

        public void SetAutoPlay(bool on)
        {
            AutoPlay = on;
            if (on) Skipping = false;
            RefreshModeButtons();
        }

        public void SetSkipping(bool on)
        {
            Skipping = on;
            if (on) AutoPlay = false;
            RefreshModeButtons();
        }

        void RefreshModeButtons()
        {
            var autoLabel = UIKit.LabelOf(_autoBtn);
            if (autoLabel != null) autoLabel.color = AutoPlay ? VNTheme.Accent : VNTheme.Ink;
            var skipLabel = UIKit.LabelOf(_skipBtn);
            if (skipLabel != null) skipLabel.color = Skipping ? VNTheme.Accent : VNTheme.Ink;
        }

        void QueueAdvance()
        {
            if (!_inStory || AnyScreenOpen) return;
            _advanceQueued = true;
            if (AutoPlay) SetAutoPlay(false);
        }

        public bool ConsumeAdvance()
        {
            if (!_advanceQueued) return false;
            _advanceQueued = false;
            return true;
        }

        // ---------------------------------------------------------------- input

        void Update()
        {
            if (VNInput.CancelPressed)
            {
                if (_backlog.IsOpen) _backlog.Close();
                else if (_saves.IsOpen) _saves.Close();
                else if (_settings.IsOpen) _settings.Close();
                else if (_inStory && !AnyScreenOpen) _settings.Open();
                return;
            }

            if (!_inStory || AnyScreenOpen)
            {
                if (_pendingCtrlSkip) { _pendingCtrlSkip = false; SetSkipping(false); }
                return;
            }

            if (VNInput.AdvancePressed) QueueAdvance();

            if (VNInput.ScrollDelta > 0.1f) OpenBacklog();

            // Holding Ctrl skips while held; the Skip button latches instead.
            bool ctrl = VNInput.SkipHeld;
            if (ctrl && !Skipping) { Skipping = true; _pendingCtrlSkip = true; RefreshModeButtons(); }
            else if (!ctrl && _pendingCtrlSkip) { _pendingCtrlSkip = false; SetSkipping(false); }
        }

        void OpenBacklog()
        {
            if (!_inStory) return;
            _backlog.Open(Director.History);
        }

        // ---------------------------------------------------------------- saving

        void RequestSave()
        {
            StartCoroutine(CaptureThenOpenSave());
        }

        IEnumerator CaptureThenOpenSave()
        {
            yield return CaptureThumbnail();
            _saves.Open(true);
        }

        IEnumerator CaptureThumbnail()
        {
            _pendingThumb = "";
            yield return new WaitForEndOfFrame();

            Texture2D shot = null;
            Texture2D small = null;
            try
            {
                shot = ScreenCapture.CaptureScreenshotAsTexture();
                const int w = 256, h = 144;
                small = new Texture2D(w, h, TextureFormat.RGB24, false);
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        px[y * w + x] = shot.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
                small.SetPixels(px);
                small.Apply();
                _pendingThumb = Convert.ToBase64String(small.EncodeToJPG(70));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VN] Thumbnail capture skipped: " + e.Message);
            }
            finally
            {
                if (shot != null) Destroy(shot);
                if (small != null) Destroy(small);
            }
        }

        bool SaveToSlot(int slot)
        {
            if (!_inStory) return false;
            var data = Director.Capture();
            data.screenshot = _pendingThumb;
            VNSaveSystem.Write(slot, data);
            Audio.PlaySfx("click", 0.6f);
            return true;
        }

        bool LoadFromSlot(int slot)
        {
            var data = VNSaveSystem.Read(slot);
            if (data == null) return false;
            _saves.Close();
            _settings.Close();
            _backlog.Close();
            StartCoroutine(BeginStory(data));
            return true;
        }
    }
}
