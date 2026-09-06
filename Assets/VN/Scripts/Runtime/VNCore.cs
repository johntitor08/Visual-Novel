// VNCore.cs -- input, theme, procedural textures/backgrounds, asset loading, easing.
using System;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VN
{
    /// <summary>
    /// Thin input layer. The project ships with Active Input Handling = Input System (New),
    /// so UnityEngine.Input must not be touched unless the legacy manager is actually enabled.
    /// Mouse clicks go through a full-screen UI button instead of polling, so only keys live here.
    /// </summary>
    public static class VNInput
    {
        public static bool AdvancePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb == null) return false;
                return kb.spaceKey.wasPressedThisFrame
                    || kb.enterKey.wasPressedThisFrame
                    || kb.numpadEnterKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#else
                return false;
#endif
            }
        }

        public static bool SkipHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb == null) return false;
                return kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#else
                return false;
#endif
            }
        }

        public static bool CancelPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb == null) return false;
                return kb.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.Escape);
#else
                return false;
#endif
            }
        }

        /// <summary>Mouse wheel delta, positive when scrolling up. Opens the backlog.</summary>
        public static float ScrollDelta
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var m = Mouse.current;
                if (m == null) return 0f;
                return m.scroll.ReadValue().y;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.mouseScrollDelta.y;
#else
                return 0f;
#endif
            }
        }
    }

    /// <summary>Every colour and metric the UI uses, in one place.</summary>
    public static class VNTheme
    {
        public static readonly Color Ink        = new Color(0.960f, 0.953f, 0.933f, 1f);
        public static readonly Color InkDim     = new Color(0.760f, 0.755f, 0.740f, 1f);
        public static readonly Color Accent     = new Color(0.949f, 0.706f, 0.408f, 1f); // warm amber
        public static readonly Color AccentCool = new Color(0.478f, 0.749f, 0.827f, 1f); // signal cyan
        public static readonly Color Panel      = new Color(0.055f, 0.063f, 0.086f, 0.880f);
        public static readonly Color PanelSolid = new Color(0.055f, 0.063f, 0.086f, 0.980f);
        public static readonly Color PanelLift  = new Color(0.118f, 0.133f, 0.169f, 0.960f);
        public static readonly Color Hairline   = new Color(1f, 1f, 1f, 0.140f);
        public static readonly Color Shade      = new Color(0f, 0f, 0f, 0.720f);

        public const float RefWidth  = 1920f;
        public const float RefHeight = 1080f;

        public const int SizeBody   = 40;
        public const int SizeName   = 34;
        public const int SizeButton = 30;
        public const int SizeSmall  = 24;
        public const int SizeTitle  = 130;
    }

    /// <summary>Sprites generated at runtime, so the UI needs no imported art at all.</summary>
    public static class VNTextures
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Solid()
        {
            return Get("solid", () =>
            {
                var t = NewTex(4, 4);
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                t.SetPixels(px); t.Apply();
                return Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            });
        }

        /// <summary>9-sliced rounded rectangle drawn on a 96px tile.</summary>
        public static Sprite Rounded(int radius)
        {
            return Get("round" + radius, () =>
            {
                const int S = 96;
                int r = Mathf.Clamp(radius, 1, S / 2);
                var t = NewTex(S, S);
                var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        float dx = Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (S - r));
                        float dy = Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (S - r));
                        float a = 1f;
                        if (dx > 0f && dy > 0f)
                        {
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            a = Mathf.Clamp01(r - d + 0.5f);
                        }
                        px[y * S + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                t.SetPixels(px); t.Apply();
                return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            });
        }

        public static Sprite Circle()
        {
            return Get("circle", () =>
            {
                const int S = 256;
                var t = NewTex(S, S);
                var px = new Color[S * S];
                float c = S * 0.5f;
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                        px[y * S + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(c - d));
                    }
                }
                t.SetPixels(px); t.Apply();
                return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            });
        }

        /// <summary>Vertical fade, opaque at the bottom. Sinks standing sprites behind the dialogue box.</summary>
        public static Sprite BottomFade()
        {
            return Get("bfade", () =>
            {
                const int H = 128;
                var t = NewTex(4, H);
                var px = new Color[4 * H];
                for (int y = 0; y < H; y++)
                {
                    float a = 1f - Mathf.Clamp01(y / (float)(H - 1));
                    a = a * a;
                    for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(1f, 1f, 1f, a);
                }
                t.SetPixels(px); t.Apply();
                return Sprite.Create(t, new Rect(0, 0, 4, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            });
        }

        /// <summary>Horizontal fade, opaque at the left. Keeps title text legible over painted art.</summary>
        public static Sprite SideFade()
        {
            return Get("sfade", () =>
            {
                const int W = 128;
                var t = NewTex(W, 4);
                var px = new Color[W * 4];
                for (int x = 0; x < W; x++)
                {
                    float a = 1f - Mathf.Clamp01(x / (float)(W - 1));
                    a = a * a;
                    for (int y = 0; y < 4; y++) px[y * W + x] = new Color(1f, 1f, 1f, a);
                }
                t.SetPixels(px); t.Apply();
                return Sprite.Create(t, new Rect(0, 0, W, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            });
        }

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        static Sprite Get(string key, Func<Sprite> make)
        {
            Sprite s;
            if (Cache.TryGetValue(key, out s) && s != null) return s;
            s = make();
            s.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = s;
            return s;
        }
    }

    /// <summary>
    /// Painted backgrounds are optional: a PNG in Resources/VN/Backgrounds wins if it exists.
    /// Otherwise a named palette is rendered procedurally so every scene in the script has art.
    /// </summary>
    public static class VNProcBg
    {
        enum Kind { Sky, Room, Flat }

        struct Def
        {
            public Kind kind;
            public Color top, bottom, accent;
            public float horizon;
            public float glow;
        }

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Color Hex(string h)
        {
            Color c;
            if (ColorUtility.TryParseHtmlString(h, out c)) return c;
            return Color.magenta;
        }

        static Def Lookup(string name)
        {
            switch (name)
            {
                case "classroom":  return New(Kind.Room, "#F2DFBC", "#B08A63", "#FFF3D6", 0.34f, 0.55f);
                case "hallway":    return New(Kind.Room, "#C9D4DE", "#5A6472", "#EAF2F8", 0.30f, 0.35f);
                case "rooftop":    return New(Kind.Sky,  "#4C8BC6", "#CFE6F2", "#FFF6DA", 0.22f, 0.75f);
                case "seawall":    return New(Kind.Sky,  "#7E8C99", "#B8C3C9", "#DCE6EA", 0.34f, 0.30f);
                case "beach_dusk": return New(Kind.Sky,  "#3B2E56", "#F0A06A", "#FFD9A0", 0.30f, 0.95f);
                case "town_night": return New(Kind.Sky,  "#080D1C", "#22304E", "#F5C86B", 0.28f, 0.55f);
                case "shrine":     return New(Kind.Room, "#3E5741", "#1B2620", "#D9A441", 0.36f, 0.40f);
                case "bunker":     return New(Kind.Room, "#2A3038", "#12161C", "#6FD0E0", 0.30f, 0.30f);
                case "infirmary":  return New(Kind.Room, "#E6EEF0", "#9FB2B8", "#FFFFFF", 0.32f, 0.35f);
                case "fog":        return New(Kind.Flat, "#C6CCCE", "#9AA3A6", "#E8EDEE", 0.5f,  0.20f);
                case "sea_gate":   return New(Kind.Sky,  "#0B1A2B", "#1E5A6B", "#7FE8E0", 0.32f, 1.00f);
                case "white":      return New(Kind.Flat, "#FFFFFF", "#F2F2F2", "#FFFFFF", 0.5f,  0f);
                case "black":      return New(Kind.Flat, "#000000", "#000000", "#000000", 0.5f,  0f);
                case "title":      return New(Kind.Sky,  "#131B33", "#5E4A6B", "#F0A06A", 0.26f, 0.85f);
                default:           return New(Kind.Flat, "#1A1F2B", "#0C1017", "#3A4759", 0.5f,  0.15f);
            }
        }

        static Def New(Kind k, string top, string bottom, string accent, float horizon, float glow)
        {
            var d = new Def();
            d.kind = k; d.top = Hex(top); d.bottom = Hex(bottom); d.accent = Hex(accent);
            d.horizon = horizon; d.glow = glow;
            return d;
        }

        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) name = "black";
            Sprite cached;
            if (Cache.TryGetValue(name, out cached) && cached != null) return cached;

            var painted = VNAssets.LoadSprite("VN/Backgrounds/" + name);
            if (painted != null) { Cache[name] = painted; return painted; }

            var s = Render(name, Lookup(name));
            Cache[name] = s;
            return s;
        }

        static Sprite Render(string name, Def d)
        {
            const int W = 480, H = 270;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;

            var px = new Color[W * H];
            // Deterministic per-name grain, so a scene looks identical every time it is shown.
            var rng = new System.Random(name.GetHashCode());
            float sunX = 0.5f + (float)(rng.NextDouble() - 0.5) * 0.5f;

            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1);          // 0 at the bottom of the image
                float fromTop = 1f - v;
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1);
                    Color c;

                    if (d.kind == Kind.Sky)
                    {
                        if (fromTop < 1f - d.horizon)
                        {
                            float t = Mathf.Clamp01(fromTop / Mathf.Max(0.001f, 1f - d.horizon));
                            c = Color.Lerp(d.top, d.bottom, Mathf.Pow(t, 0.85f));
                            float du = (u - sunX) * 1.9f;
                            float dv = (fromTop - (1f - d.horizon) * 0.82f) * 2.6f;
                            float glow = Mathf.Exp(-(du * du + dv * dv) * 5.5f) * d.glow;
                            c = Color.Lerp(c, d.accent, Mathf.Clamp01(glow));
                        }
                        else
                        {
                            // Water below the horizon, with a rippled reflection of the light source.
                            float t = Mathf.Clamp01((fromTop - (1f - d.horizon)) / Mathf.Max(0.001f, d.horizon));
                            Color deep = Color.Lerp(d.bottom, d.top, 0.55f) * 0.62f;
                            deep.a = 1f;
                            c = Color.Lerp(d.bottom, deep, Mathf.Pow(t, 0.7f));
                            float ripple = Mathf.Sin((v * 90f) + Mathf.Sin(u * 7f) * 1.7f) * 0.5f + 0.5f;
                            float band = Mathf.Exp(-Mathf.Abs(u - sunX) * 3.2f) * d.glow * 0.45f * ripple * (1f - t);
                            c = Color.Lerp(c, d.accent, Mathf.Clamp01(band));
                        }
                    }
                    else if (d.kind == Kind.Room)
                    {
                        float floorLine = d.horizon;
                        if (v < floorLine)
                        {
                            float t = Mathf.Clamp01(v / Mathf.Max(0.001f, floorLine));
                            Color floorCol = Color.Lerp(d.bottom * 0.72f, d.bottom, t);
                            floorCol.a = 1f;
                            c = floorCol;
                        }
                        else
                        {
                            float t = Mathf.Clamp01((v - floorLine) / Mathf.Max(0.001f, 1f - floorLine));
                            c = Color.Lerp(d.bottom, d.top, Mathf.Pow(t, 0.9f));
                            // Soft window columns catching the light.
                            float win = Mathf.Sin(u * Mathf.PI * 3f) * 0.5f + 0.5f;
                            win = Mathf.Pow(win, 6f) * Mathf.Clamp01((t - 0.18f) * 1.6f);
                            c = Color.Lerp(c, d.accent, win * d.glow * 0.55f);
                        }
                    }
                    else
                    {
                        c = Color.Lerp(d.bottom, d.top, Mathf.Pow(v, 0.9f));
                        float haze = Mathf.Exp(-Mathf.Abs(v - 0.55f) * 3f) * d.glow;
                        c = Color.Lerp(c, d.accent, haze * 0.5f);
                    }

                    // Vignette plus fine grain, which also keeps the flat gradients from banding.
                    float dxv = (u - 0.5f) * 2f, dyv = (v - 0.5f) * 2f;
                    float vig = 1f - Mathf.Clamp01((dxv * dxv + dyv * dyv) * 0.30f);
                    c *= vig;
                    float g = (float)rng.NextDouble() * 0.022f - 0.011f;
                    c.r += g; c.g += g; c.b += g;
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }
    }

    /// <summary>Resource lookups with caching and quiet failure -- a missing sprite must never stop the script.</summary>
    public static class VNAssets
    {
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

        public static Sprite LoadSprite(string path)
        {
            Sprite s;
            if (Sprites.TryGetValue(path, out s)) return s;
            s = Resources.Load<Sprite>(path);
            Sprites[path] = s;
            return s;
        }

        public static AudioClip LoadClip(string path)
        {
            AudioClip c;
            if (Clips.TryGetValue(path, out c)) return c;
            c = Resources.Load<AudioClip>(path);
            Clips[path] = c;
            return c;
        }

        public static Sprite Character(string spriteSet, string kind, string frame)
        {
            if (string.IsNullOrEmpty(spriteSet) || string.IsNullOrEmpty(frame)) return null;
            return LoadSprite("VN/Characters/" + spriteSet + "/" + kind + "/" + frame);
        }
    }

    public static class VNEase
    {
        public static float OutCubic(float t) { t = Mathf.Clamp01(t); float f = 1f - t; return 1f - f * f * f; }

        public static float InOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        public static float OutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float f = t - 1f;
            return 1f + c3 * f * f * f + c1 * f * f;
        }
    }
}
