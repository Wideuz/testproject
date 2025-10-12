using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Managers;
using Sharp.Shared.Types;
using Sharp.Shared.Units;

public static class DistanceUtils
{
    public static float GetPlayerDistance(IPlayerPawn a, IPlayerPawn b)
    {
        if (a == null || !a.IsValid() || b == null || !b.IsValid())
            return -1f;

        Vector posA = a.GetAbsOrigin();
        Vector posB = b.GetAbsOrigin();

        float dx = posA.X - posB.X;
        float dy = posA.Y - posB.Y;
        float dz = posA.Z - posB.Z;

        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// 隱藏指定距離內的玩家（caller 看不到他們）
    /// </summary>
    public static void HideNearbyPlayers(IBaseEntity caller, float maxDistance, ISharedSystem sharedSystem)
    {
        if (caller == null || !caller.IsValid())
            return;

        var entityManager = sharedSystem.GetEntityManager();
        var transmitManager = sharedSystem.GetTransmitManager();
        var modSharp = sharedSystem.GetModSharp();
        var clientManager = sharedSystem.GetClientManager();

        var controller = caller.AsPlayerController();
        if (controller == null || !controller.IsValid())
            return;

        // ✅ 用 Pawn 的位置當基準
        var callerPawn = controller.GetPawn();
        if (callerPawn == null || !callerPawn.IsValid())
            return;

        Vector callerPos = callerPawn.GetAbsOrigin();
        EntityIndex callerControllerIndex = controller.Index;
        EntityIndex callerPawnIndex = callerPawn.Index;

        int hiddenCount = 0;

        IBaseEntity? entity = null;
        while ((entity = entityManager.FindEntityInSphere(entity, callerPos, maxDistance)) != null)
        {
            if (!entity.IsValid() || !entity.IsPlayerPawn)
                continue;

            var pawn = entity.AsPlayerPawn();
            if (pawn == null || !pawn.IsValid())
                continue;

            // 排除自己
            if (pawn.Index == callerPawnIndex)
                continue;

            var targetController = pawn.GetController();
            if (targetController == null || !targetController.IsValid())
                continue;

            var gameClient = clientManager.GetGameClient(targetController.PlayerSlot);
            if (gameClient == null || !gameClient.IsValid || gameClient.IsHltv)
                continue;

            // 隱藏 Pawn + Controller
            transmitManager.SetEntityState(pawn.Index, callerControllerIndex, false, channel: -1);
            transmitManager.SetEntityState(targetController.Index, callerControllerIndex, false, channel: -1);

            hiddenCount++;

            // Debug：用 Pawn 對 Pawn 的距離
            float distance = GetPlayerDistance(callerPawn, pawn);
            controller.Print(HudPrintChannel.Console,
                $"[DEBUG] 隱藏玩家: {targetController.PlayerName} (SteamId={gameClient.SteamId}, PawnIndex={pawn.Index}, ControllerIndex={targetController.Index}) 距離={distance:0.0} HU");
        }

        // 結果提示
        var filter = new RecipientFilter(controller.PlayerSlot);
        modSharp.PrintChannelFilter(HudPrintChannel.Chat,
            $"🔒 已隱藏 {hiddenCount} 名真人玩家（範圍 {maxDistance} HU）", filter);

        controller.Print(HudPrintChannel.Console,
            $"[DEBUG] 呼叫者: {controller.PlayerName} (ControllerIndex={callerControllerIndex}, PawnIndex={callerPawnIndex}) 總共隱藏 {hiddenCount} 名真人玩家");
    }

}


