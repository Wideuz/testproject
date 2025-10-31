using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;
using System.Collections.Generic;

public static class DistanceUtils
{
    private static readonly HashSet<(EntityIndex target, EntityIndex caller)> _hiddenPairs = new();

    /// <summary>
    /// 動態更新：範圍內隱藏，範圍外恢復
    /// </summary>
    public static void UpdateVisibility(IPlayerController caller, float maxDistance, ISharedSystem sharedSystem)
    {
        if (caller == null || !caller.IsValid())
            return;

        var entityManager = sharedSystem.GetEntityManager();
        var transmitManager = sharedSystem.GetTransmitManager();

        var callerPawn = caller.GetPawn();
        if (callerPawn == null || !callerPawn.IsValid())
            return;

        Vector callerPos = callerPawn.GetAbsOrigin();
        EntityIndex callerIndex = caller.Index;

        var currentPairs = new HashSet<(EntityIndex, EntityIndex)>();

        IBaseEntity? entity = null;
        while ((entity = entityManager.FindEntityInSphere(entity, callerPos, maxDistance)) != null)
        {
            if (!entity.IsPlayerPawn)
                continue;

            var pawn = entity.AsPlayerPawn();
            if (pawn == null || !pawn.IsValid() || pawn.Index == callerPawn.Index)
                continue;

            var targetController = pawn.GetController();
            if (targetController == null || !targetController.IsValid())
                continue;

            var pair = (targetController.Index, callerIndex);
            currentPairs.Add(pair);

            if (!_hiddenPairs.Contains(pair))
            {
                if (!transmitManager.IsEntityHooked(targetController))
                    transmitManager.AddEntityHooks(targetController, defaultTransmit: true);

                transmitManager.SetEntityState(targetController.Index, callerIndex, false, channel: 2);
                _hiddenPairs.Add(pair);
            }
        }

        // 找出需要恢復的
        var toUnhide = new List<(EntityIndex, EntityIndex)>();
        foreach (var pair in _hiddenPairs)
        {
            if (pair.caller == callerIndex && !currentPairs.Contains(pair))
            {
                transmitManager.SetEntityState(pair.target, callerIndex, true, channel: 2);
                toUnhide.Add(pair);
            }
        }

        foreach (var p in toUnhide)
            _hiddenPairs.Remove(p);
    }

    /// <summary>
    /// 取消某位呼叫者對所有目標的隱藏
    /// </summary>
    public static void UnhideAllForCaller(IPlayerController caller, ISharedSystem sharedSystem)
    {
        if (caller == null || !caller.IsValid())
            return;

        var transmitManager = sharedSystem.GetTransmitManager();
        EntityIndex callerIndex = caller.Index;

        var toUnhide = new List<(EntityIndex, EntityIndex)>();
        foreach (var pair in _hiddenPairs)
        {
            if (pair.caller == callerIndex)
            {
                transmitManager.SetEntityState(pair.target, callerIndex, true, channel: 2);
                toUnhide.Add(pair);
            }
        }

        foreach (var p in toUnhide)
            _hiddenPairs.Remove(p);
    }
}




