using HarmonyLib;

namespace EndGameStats;

[HarmonyPatch(typeof(ValuableObject), "Start")]
internal static class ValuableRegistrationPatch
{
    private static void Postfix(ValuableObject __instance) => Plugin.Instance.RegisterValuable(__instance);
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabPlayerAddRPC")]
internal static class GrabStartedPatch
{
    private static void Postfix(PhysGrabObject __instance, int photonViewID) =>
        Plugin.Instance.RecordGrab(__instance, photonViewID, released: false);
}

[HarmonyPatch(typeof(PhysGrabObject), "GrabPlayerRemoveRPC")]
internal static class GrabEndedPatch
{
    private static void Prefix(PhysGrabObject __instance, int photonViewID) =>
        Plugin.Instance.RecordGrab(__instance, photonViewID, released: true);
}

[HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.PlayerDeathRPC))]
internal static class PlayerDeathPatch
{
    private static void Postfix(PlayerAvatar __instance) => Plugin.Instance.RecordDeath(__instance);
}

[HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.ReviveRPC))]
internal static class PlayerRevivePatch
{
    private static void Prefix(PlayerAvatar __instance, bool _revivedByTruck, out PlayerAvatar? __state) =>
        __state = Plugin.Instance.CaptureRescuer(__instance, _revivedByTruck);

    private static void Postfix(PlayerAvatar? __state) => Plugin.Instance.RecordRescue(__state);
}

[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "BreakRPC")]
internal static class ValuableDamagePatch
{
    private static void Prefix(PhysGrabObjectImpactDetector __instance, float valueLost, bool _loseValue) =>
        Plugin.Instance.RecordDamage(__instance, valueLost, _loseValue);
}

[HarmonyPatch(typeof(ExtractionPoint), "ExtractionPointSurplus")]
internal static class SuccessfulExtractionPatch
{
    private static void Prefix() => Plugin.Instance.RecordSuccessfulExtraction();
}

[HarmonyPatch(typeof(HurtCollider), "EnemyHurt")]
internal static class WeaponEnemyHitPatch
{
    private static void Prefix(HurtCollider __instance, Enemy _enemy) =>
        Plugin.Instance.RecordWeaponEnemyHit(__instance, _enemy);
}

[HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.DeathImpulseRPC))]
internal static class EnemyDeathPatch
{
    private static void Postfix(EnemyHealth __instance) => Plugin.Instance.RecordEnemyDeath(__instance);
}
