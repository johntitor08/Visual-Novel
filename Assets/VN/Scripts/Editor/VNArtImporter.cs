// VNArtImporter.cs -- import settings for the generated character art, applied on first import.
using UnityEditor;
using UnityEngine;

namespace VNEditor
{
    public class VNArtImporter : AssetPostprocessor
    {
        const string CharacterRoot = "Assets/VN/Resources/VN/Characters/";
        const string BackgroundRoot = "Assets/VN/Resources/VN/Backgrounds/";

        void OnPreprocessTexture()
        {
            bool isCharacter = assetPath.StartsWith(CharacterRoot);
            bool isBackground = assetPath.StartsWith(BackgroundRoot);
            if (!isCharacter && !isBackground) return;

            var importer = (TextureImporter)assetImporter;

            // Only take over the very first import, so hand-tuned settings are never clobbered.
            if (!importer.importSettingsMissing) return;

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
