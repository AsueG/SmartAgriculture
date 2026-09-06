using UnityEngine;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Command icons, resolved once at startup. GetGizmos runs every frame for every selected thing,
    /// so the lookup does not belong there.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SaclTex
    {
        public static readonly Texture2D FieldPlan = ContentFinder<Texture2D>.Get("UI/Commands/SetPlantToGrow", false);
        public static readonly Texture2D Crate = ContentFinder<Texture2D>.Get("UI/Commands/Install", false);
        public static readonly Texture2D Vent = ContentFinder<Texture2D>.Get("UI/Commands/Vent", false);
        public static readonly Texture2D TempLower = ContentFinder<Texture2D>.Get("UI/Commands/TempLower", false);
        public static readonly Texture2D TempRaise = ContentFinder<Texture2D>.Get("UI/Commands/TempRaise", false);
    }
}
