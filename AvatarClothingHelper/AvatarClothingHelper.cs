using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BaseX;
using CodeX;
using FrooxEngine;
using FrooxEngine.CommonAvatar;
using FrooxEngine.LogiX;
using FrooxEngine.LogiX.Data;
using FrooxEngine.LogiX.ProgramFlow;
using FrooxEngine.UIX;
using HarmonyLib;
using NeosModLoader;

namespace AvatarClothingHelper
{
    public class AvatarClothingHelper : NeosMod
    {
        public static ModConfiguration Config;
        private static readonly string blendshapeSyncSlotName = "Blendshape Sync";

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<bool> EnableInspectorButtons = new ModConfigurationKey<bool>("EnableInspectorButtons", "Enable Setup Blendshape Source Buttons appearing on Inspectors constructed by you.", () => true);

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<bool> GenerateSlotPerBlendshape = new ModConfigurationKey<bool>("GenerateSlotPerBlendshape", "Generate each Blendshape ValueCopy and MultiDriver on a nested slot.", () => true);

        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosAvatarClothingHelper";
        public override string Name => "AvatarClothingHelper";
        public override string Version => "1.1.0";

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony($"{Author}.{Name}");
            Config = GetConfiguration();
            Config.Save(true);
            harmony.PatchAll();
        }

        private static void driveSecondaryBlendshapes(Slot parent, SkinnedMeshRenderer primaryRenderer = null)
        {
            var skinnedRenderers = parent.GetComponentsInChildren<SkinnedMeshRenderer>(renderer => renderer.BlendShapeCount > 0).ToArray();

            primaryRenderer = primaryRenderer ?? skinnedRenderers.OrderByDescending(renderer => renderer.BlendShapeCount).First();

            if (primaryRenderer.Slot.Find(blendshapeSyncSlotName) != null)
                return;

            foreach (var skinnedRenderer in skinnedRenderers.Where(renderer => renderer.BlendShapeWeights.Count < renderer.BlendShapeCount))
                skinnedRenderer.BlendShapeWeights.AddRange(Enumerable.Repeat(0f, skinnedRenderer.BlendShapeCount - skinnedRenderer.BlendShapeWeights.Count));

            var blendshapeGroups = skinnedRenderers
                .SelectMany(renderer =>
                    Enumerable.Range(0, renderer.BlendShapeCount)
                    .Select(i => new Blendshape(renderer.BlendShapeName(i), renderer.BlendShapeWeights.GetElement(i), renderer == primaryRenderer)))
                .GroupBy(blendshape => blendshape.Name)
                .Where(group => group.Count() > 1 && group.Any(blendshape => blendshape.Primary));

            if (!blendshapeGroups.Any())
                return;

            var root = primaryRenderer.Slot.AddSlot(blendshapeSyncSlotName);

            foreach (var group in blendshapeGroups)
            {
                var slot = Config.GetValue(GenerateSlotPerBlendshape) ? root.AddSlot(group.Key) : root;
                var primaryBlendshape = group.First(blendshape => blendshape.Primary);

                var multiDriver = slot.AttachComponent<ValueMultiDriver<float>>();
                multiDriver.Value.DriveFrom(primaryBlendshape.Field);

                foreach (var blendshape in group.Where(blendshape => !blendshape.Primary))
                    multiDriver.Drives.Add().Target = blendshape.Field;
            }
        }

        private static Slot getObjectRoot(Slot slot)
        {
            var implicitRoot = slot.GetComponentInParents<IImplicitObjectRoot>(null, true, false);
            var objectRoot = slot.GetObjectRoot();

            if (implicitRoot == null)
                return objectRoot;

            if (objectRoot == slot || implicitRoot.Slot.HierachyDepth > objectRoot.HierachyDepth)
                return implicitRoot.Slot;

            return objectRoot;
        }

        [HarmonyPatch(typeof(ModelImporter))]
        private static class ModelImporterPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch(nameof(ModelImporter.ImportModel))]
            private static void ImportModelPostfix(Slot targetSlot, ref IEnumerator<Context> __result)
            {
                __result = new EnumerableInjector<Context>(__result)
                {
                    Postfix = () => driveSecondaryBlendshapes(targetSlot)
                }.GetEnumerator();
            }
        }

        [HarmonyPatch(typeof(SkinnedMeshRenderer))]
        private static class SkinnedMeshRendererPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch(nameof(SkinnedMeshRenderer.BuildInspectorUI))]
            private static void BuildInspectorUIPostfix(SkinnedMeshRenderer __instance, UIBuilder ui)
            {
                if (!Config.GetValue(EnableInspectorButtons) || __instance.Slot.Find(blendshapeSyncSlotName) != null)
                    return;

                var button = ui.Button("Setup as Primary Blendshape Source", color.Pink);
                var button2 = ui.Button("Setup best Blendshape Source", color.Pink);

                var root = getObjectRoot(__instance.Slot);

                button.LocalPressed += (sender, data) =>
                {
                    driveSecondaryBlendshapes(root, __instance);
                    button.Slot.Destroy();
                    button2.Slot.Destroy();
                };

                button2.LocalPressed += (sender, data) =>
                {
                    driveSecondaryBlendshapes(root);
                    button.Slot.Destroy();
                    button2.Slot.Destroy();
                };
            }
        }
    }
}