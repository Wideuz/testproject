using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;

public static class DistanceUtils
{
    /// <summary>
    /// 取得所有有效的玩家 Controller
    /// </summary>
    public static IEnumerable<IPlayerController> GetAllControllers(ISharedSystem sharedSystem, bool ignoreFakeClient = true)
    {
        var entityManager = sharedSystem.GetEntityManager();
        var clientManager = sharedSystem.GetClientManager();

        int maxSlots = (int)PlayerSlot.MaxPlayerSlot;

        for (int slot = 0; slot < maxSlots; slot++)
        {
            IGameClient? client = null;

            try
            {
                client = clientManager.GetGameClient((PlayerSlot)slot);
            }
            catch
            {
                continue; // slot 還沒初始化，跳過
            }

            if (client == null || !client.IsValid)
                continue;

            if (ignoreFakeClient && client.IsFakeClient)
                continue;

            if (client.SignOnState < SignOnState.Connected)
                continue;

            var controller = entityManager.FindPlayerControllerBySlot((PlayerSlot)slot);
            if (controller != null && controller.IsValid())
                yield return controller;
        }
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

        var controller = caller.AsPlayerController()!;
        if (!controller.IsValid())
            return;

        var callerPawn = controller.GetPawn()!;
        if (!callerPawn.IsValid())
            return;

        Vector callerPos = callerPawn.GetAbsOrigin();
        EntityIndex callerControllerIndex = controller.Index;
        EntityIndex callerPawnIndex = callerPawn.Index;

        int hiddenCount = 0;

        IBaseEntity? entity = null;
        while ((entity = entityManager.FindEntityInSphere(entity, callerPos, maxDistance)) != null)
        {
            if (!entity.IsPlayerPawn)
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

            var gameClient = clientManager.GetGameClient(targetController.PlayerSlot)!;
            if (!gameClient.IsValid || gameClient.IsHltv)
                continue;

            // ✅ 確保 Controller 已經 Hook
            if (!transmitManager.IsEntityHooked(targetController))
                transmitManager.AddEntityHooks(targetController, defaultTransmit: true);

            // ✅ 隱藏「目標玩家的 Controller」對呼叫者的可見性
            transmitManager.SetEntityState(
                targetController.Index,   // sender = 被隱藏的實體 (必須是 Controller)
                callerControllerIndex,    // receiver = 呼叫者的 Controller
                false,                    // 不可見
                channel: -1
            );

            hiddenCount++;

            // Debug：顯示距離
            float distance = callerPawn.GetAbsOrigin().DistTo(pawn.GetAbsOrigin());
            controller.Print(HudPrintChannel.Console,
                $"[DEBUG] 隱藏玩家: {targetController.PlayerName} (SteamId={gameClient.SteamId}, ControllerIndex={targetController.Index}) 距離={distance:0.0} HU");
        }

        // 結果提示
        var filter = new RecipientFilter(controller.PlayerSlot);
        modSharp.PrintChannelFilter(HudPrintChannel.Chat,
            $"🔒 已隱藏 {hiddenCount} 名玩家（範圍 {maxDistance} HU）", filter);

        controller.Print(HudPrintChannel.Console,
            $"[DEBUG] 呼叫者: {controller.PlayerName} (ControllerIndex={callerControllerIndex}, PawnIndex={callerPawnIndex}) 總共隱藏 {hiddenCount} 名玩家");
    }

}


