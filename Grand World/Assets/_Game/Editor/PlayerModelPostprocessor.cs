using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GrandWorld.EditorTools
{
    /// <summary>
    /// Настройки импорта модели персонажа из Blender (Player.fbx):
    /// Humanoid-аватар, имена клипов без префикса «Armature|GW_», зацикливание и «запекание» корня в позу.
    /// </summary>
    public class PlayerModelPostprocessor : AssetPostprocessor
    {
        const string PlayerFbx = "Assets/_Game/Art/Characters/Player/Player.fbx";

        // Зацикленные клипы; остальные проигрываются один раз
        static readonly string[] LoopClips = { "Idle", "Walk", "Run", "Sprint", "CrouchIdle", "CrouchWalk", "JumpAir" };

        void OnPreprocessModel()
        {
            if (assetPath != PlayerFbx) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.importAnimation = true;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        void OnPreprocessAnimation()
        {
            if (assetPath != PlayerFbx) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            foreach (var clip in clips)
            {
                string name = clip.takeName;
                int bar = name.LastIndexOf('|');
                if (bar >= 0) name = name.Substring(bar + 1);
                if (name.StartsWith("GW_")) name = name.Substring(3);
                clip.name = name;

                bool loop = LoopClips.Contains(name);
                clip.loopTime = loop;
                clip.loopPose = false; // первый и последний кадры уже совпадают
                // Анимации «на месте»: корень не двигает персонажа, вертикальные покачивания таза сохраняются
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                clip.heightFromFeet = false;
            }
            importer.clipAnimations = clips;
        }
    }
}
