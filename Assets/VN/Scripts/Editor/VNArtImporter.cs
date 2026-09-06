// VNArtImporter.cs -- import settings for the VN art, enforced on every import.
using UnityEditor;
using UnityEngine;

namespace VNEditor
{
    public class VNArtImporter : AssetPostprocessor
    {
        const string CharacterRoot = "Assets/VN/Resources/VN/Characters/";
        const string BackgroundRoot = "Assets/VN/Resources/VN/Backgrounds/";

        /// <summary>
        /// Bumping this makes Unity reimport everything this postprocessor handles.
        ///
        /// v2: the art was imported before this script existed and every sheet came in as
        /// Sprite Mode = Multiple, auto-sliced into four sub-sprites. Resources.Load&lt;Sprite&gt;
        /// resolves nothing in that state, so the whole cast rendered as empty space. The
        /// settings the game depends on are now applied unconditionally rather than only on
        /// first import, and this version bump forces the existing art back through them.
        /// </summary>
        public override uint GetVersion() { return 2; }

        void OnPreprocessTexture()
        {
            bool isCharacter = assetPath.StartsWith(CharacterRoot);
            bool isBackground = assetPath.StartsWith(BackgroundRoot);
            if (!isCharacter && !isBackground) return;

            var importer = (TextureImporter)assetImporter;

            // Non-negotiable: the game addresses this art by path with Resources.Load<Sprite>,
            // which only resolves when the file imports as one whole-texture sprite.
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;

            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.spritePixelsPerUnit = 100f;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            // Full Rect keeps the transparent margin, so every pose shares one canvas
            // and the character does not jump between frames.
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;

            if (isCharacter)
            {
                bool portrait = assetPath.Contains("/portraits/");
                settings.spriteAlignment = (int)(portrait ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
            }
            else
            {
                settings.spriteAlignment = (int)SpriteAlignment.Center;
            }

            importer.SetTextureSettings(settings);
        }
    }
}
