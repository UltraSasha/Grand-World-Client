using System.IO;
using UnityEditor;
using UnityEngine;

namespace GrandWorld.EditorTools
{
    /// <summary>Пути генерируемых ассетов и общие вспомогательные методы генератора.</summary>
    public static class BuilderPaths
    {
        public const string Root = "Assets/_Game";
        public const string GeneratedDir = Root + "/Generated";
        public const string TexturesDir = GeneratedDir + "/Textures";
        public const string MaterialsDir = GeneratedDir + "/Materials";
        public const string FontsDir = GeneratedDir + "/Fonts";
        public const string RenderingDir = Root + "/Rendering";
        public const string AnimationDir = Root + "/Animation";
        public const string PrefabsDir = Root + "/Prefabs";
        public const string ScenesDir = Root + "/Scenes";
        public const string EnvironmentDir = GeneratedDir + "/Environment";

        public const string PlayerFbx = Root + "/Art/Characters/Player/Player.fbx";
        public const string InputActions = Root + "/Input/GameControls.inputactions";
        public const string FontRegularTtf = Root + "/Art/Fonts/NotoSans-Regular.ttf";
        public const string FontBoldTtf = Root + "/Art/Fonts/NotoSans-Bold.ttf";

        public const string MainMenuScene = ScenesDir + "/MainMenu.unity";
        public const string GameScene = ScenesDir + "/Game.unity";

        /// <summary>Создаёт папку (со всеми родителями) в базе ассетов.</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Создаёт или заменяет ассет по пути, сохраняя GUID, если ассет того же типа уже есть.</summary>
        public static T CreateOrReplace<T>(T asset, string path) where T : Object
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(asset, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(asset, true);
                return existing;
            }
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Сохраняет текстуру в PNG и настраивает импорт.</summary>
        public static Texture2D SavePng(Texture2D tex, string path, bool sprite, Vector4 border = default,
                                        bool linear = false, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.spritePixelsPerUnit = 100;
            }
            importer.sRGBTexture = !linear;
            importer.alphaIsTransparency = sprite;
            importer.mipmapEnabled = !sprite;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
