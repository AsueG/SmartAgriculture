using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Soft dependency layer. Nothing here ever hard-references a third party assembly:
    /// mods are detected through LoadedModManager and their content through
    /// DefDatabase&lt;T&gt;.GetNamedSilentFail, so a missing mod can never throw.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ModCompat
    {
        private static readonly string[] SkylightModPackageIds =
        {
            "dubwise.dubsskylights", "dubs.skylights", "dubwise.dubsmintmenus.skylights"
        };

        private static readonly string[] SkylightRoofDefNames =
        {
            "SACL_GlassRoof", "Skylight", "RoofTransparent", "TransparentRoof", "RoofSkylight", "DubsSkylight"
        };

        private static readonly string[] PuahPackageIds = { "mehni.pickupandhaul" };

        public static bool ThirdPartySkylightMod { get; private set; }
        public static bool PickUpAndHaul { get; private set; }

        /// <summary>Every roof def that lets sunlight (and therefore solar heat) through.</summary>
        public static readonly HashSet<RoofDef> TransparentRoofs = new HashSet<RoofDef>();

        public static RoofDef FallbackGlassRoof { get; private set; }

        public static string TransparentRoofSourceLabel { get; private set; } = "-";

        static ModCompat()
        {
            ThirdPartySkylightMod = AnyModActive(SkylightModPackageIds) || AnyModNameContains("skylight");
            PickUpAndHaul = AnyModActive(PuahPackageIds) || AnyModNameContains("pick up and haul");

            FallbackGlassRoof = DefDatabase<RoofDef>.GetNamedSilentFail("SACL_GlassRoof");

            foreach (string name in SkylightRoofDefNames)
            {
                RoofDef roof = DefDatabase<RoofDef>.GetNamedSilentFail(name);
                if (roof != null)
                {
                    TransparentRoofs.Add(roof);
                }
            }

            // Catch-all discovery: any roof def flagged by our extension, or named like glass.
            foreach (RoofDef roof in DefDatabase<RoofDef>.AllDefsListForReading)
            {
                if (roof.HasModExtension<TransparentRoofExtension>() || LooksTransparent(roof.defName) || LooksTransparent(roof.label))
                {
                    TransparentRoofs.Add(roof);
                }
            }

            List<string> sources = TransparentRoofs.Select(r => r.defName).ToList();
            TransparentRoofSourceLabel = sources.Count == 0 ? "-" : string.Join(", ", sources.ToArray());

            if (ThirdPartySkylightMod && SmartAgricultureMod.Settings != null && SmartAgricultureMod.Settings.hideFallbackSkylightWhenModded)
            {
                HideFromArchitect("SACL_Skylight");
            }

            Log.Message("[SACL] transparent roofs: " + TransparentRoofSourceLabel
                        + " | third party skylight mod: " + ThirdPartySkylightMod
                        + " | Pick Up And Haul: " + PickUpAndHaul);
        }

        public static bool IsTransparentRoof(RoofDef roof) => roof != null && TransparentRoofs.Contains(roof);

        private static bool LooksTransparent(string s)
        {
            if (s.NullOrEmpty())
            {
                return false;
            }
            string l = s.ToLowerInvariant();
            return l.Contains("skylight") || l.Contains("glassroof") || l.Contains("glass roof") || l.Contains("transparentroof");
        }

        private static bool AnyModActive(string[] packageIds)
        {
            foreach (ModContentPack pack in LoadedModManager.RunningModsListForReading)
            {
                string id = pack.PackageId;
                if (id.NullOrEmpty())
                {
                    continue;
                }
                id = id.ToLowerInvariant();
                if (id.EndsWith("_steam"))
                {
                    id = id.Substring(0, id.Length - "_steam".Length);
                }
                for (int i = 0; i < packageIds.Length; i++)
                {
                    if (id == packageIds[i])
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool AnyModNameContains(string fragment)
        {
            foreach (ModContentPack pack in LoadedModManager.RunningModsListForReading)
            {
                if (!pack.Name.NullOrEmpty() && pack.Name.ToLowerInvariant().Contains(fragment))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Removes our fallback building from the build menu when a third party mod already provides one.</summary>
        private static void HideFromArchitect(string thingDefName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
            if (def?.designationCategory == null)
            {
                return;
            }
            try
            {
                DesignationCategoryDef category = def.designationCategory;
                def.designationCategory = null;
                category.ResolveReferences();
            }
            catch (Exception e)
            {
                Log.Warning("[SACL] could not hide " + thingDefName + " from the architect menu: " + e.Message);
            }
        }
    }

    public class TransparentRoofExtension : DefModExtension
    {
    }
}
