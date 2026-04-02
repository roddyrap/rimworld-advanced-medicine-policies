
using Verse;

using System.Reflection;
using HarmonyLib;

namespace AdvancedMedicinePolicies;

[StaticConstructorOnStartup]
public static class Start
{
    static Start()
    {
        Harmony harmony = new("AdvancedMedicinePolicies");
        harmony.PatchAll( Assembly.GetExecutingAssembly() );

        Log.Message("Advanced Medicine Policies loaded successfully!");
    }
}
