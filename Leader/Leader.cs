using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Listeners;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;
using System;

namespace Leader
{
    public sealed class Leader : IModSharpModule, IEventListener
    {
        public string DisplayName => "Leader";
        public string DisplayAuthor => "Widez";

        private readonly ILogger<Leader> _logger;
        private readonly ISharedSystem _sharedSystem;
        private readonly IEventManager _events;
        private readonly IClientManager _clientManager;
        private readonly ITransmitManager _transmitManager;
        private readonly IEntityManager _entityManager;
        private readonly IModSharp _modSharp;

        private Guid _visibilityTimerId;
        private IPlayerController? _callerController;
        private float _callerDistance;
        private float _lastDistance = -1f;
        private bool _isHiding = false;

        public int ListenerVersion => IEventListener.ApiVersion;
        public int ListenerPriority => 0;

        public Leader(ISharedSystem sharedSystem,
            string? dllPath = null,
            string? sharpPath = null,
            Version? version = null,
            Microsoft.Extensions.Configuration.IConfiguration? coreConfiguration = null,
            bool hotReload = false)
        {
            _sharedSystem = sharedSystem ?? throw new ArgumentNullException(nameof(sharedSystem));
            _logger = _sharedSystem.GetLoggerFactory().CreateLogger<Leader>();
            _events = _sharedSystem.GetEventManager();
            _clientManager = _sharedSystem.GetClientManager();
            _transmitManager = _sharedSystem.GetTransmitManager();
            _entityManager = _sharedSystem.GetEntityManager();
            _modSharp = _sharedSystem.GetModSharp();
        }

        public bool Init() => true;

        public void PostInit()
        {
            _clientManager.InstallCommandCallback("leader", OnLeaderCommand);
            _clientManager.InstallCommandCallback("hide", OnHideCommand);

            _events.HookEvent("player_connect_full");
            _events.InstallEventListener(this);
        }

        public void Shutdown()
        {
            StopVisibilityMonitor(true);
        }

        public void FireGameEvent(IGameEvent e)
        {
            if (e.Name == "player_connect_full")
            {
                var slot = (PlayerSlot)e.GetInt("slot");
                var controller = _entityManager.FindPlayerControllerBySlot(slot);
                if (controller != null && controller.IsValid())
                {
                    if (!_transmitManager.IsEntityHooked(controller))
                        _transmitManager.AddEntityHooks(controller, defaultTransmit: true);
                }
            }
        }

        private ECommandAction OnLeaderCommand(IGameClient client, StringCommand command)
        {
            var controller = _entityManager.FindPlayerControllerBySlot(client.Slot);
            if (controller is null || !controller.IsValid())
                return ECommandAction.Handled;

            if (LeaderMethod.IsLeader(controller))
            {
                controller.Print(HudPrintChannel.Chat, "ℹ️ 你已經是指揮官！");
                return ECommandAction.Handled;
            }

            if (LeaderMethod.AssignLeader(controller))
                controller.Print(HudPrintChannel.Chat, "✅ 你現在是指揮官！");
            else
                controller.Print(HudPrintChannel.Chat, "⚠️ 指定失敗，請確認你是有效玩家！");

            return ECommandAction.Handled;
        }

        private ECommandAction OnHideCommand(IGameClient client, StringCommand command)
        {
            var controller = _entityManager.FindPlayerControllerBySlot(client.Slot);
            if (controller is null || !controller.IsValid())
                return ECommandAction.Handled;

            if (!float.TryParse(command.ArgString, out float distance))
            {
                controller.Print(HudPrintChannel.Chat, "⚠️ 請輸入距離，例如 !hide 500, !hide 0 (全範圍), !hide -1 (取消)");
                return ECommandAction.Handled;
            }

            // -1 或重複輸入相同距離 → 取消隱藏
            if (distance == -1 || (_isHiding && Math.Abs(distance - _lastDistance) < 0.01f))
            {
                StopVisibilityMonitor(true);
                controller.Print(HudPrintChannel.Chat, "👁 已取消隱藏，恢復顯示所有玩家");
                _isHiding = false;
                _lastDistance = -1f;
                _callerController = null;
                _callerDistance = 0f;
                return ECommandAction.Handled;
            }

            // 0 = 全範圍
            if (distance == 0)
                distance = float.MaxValue;

            _callerController = controller;
            _callerDistance = distance;
            _lastDistance = distance;
            _isHiding = true;

            // 立即更新一次
            DistanceUtils.UpdateVisibility(_callerController, _callerDistance, _sharedSystem);

            StartVisibilityMonitor();
            controller.Print(HudPrintChannel.Chat, $"🔄 開始實時隱藏範圍內玩家（距離 {(distance == float.MaxValue ? "全範圍" : distance.ToString("0"))} HU）");

            return ECommandAction.Handled;
        }

        private void StartVisibilityMonitor()
        {
            if (_modSharp.IsValidTimer(_visibilityTimerId))
                _modSharp.StopTimer(_visibilityTimerId);

            // 使用 REPEAT 確保持續執行
            _visibilityTimerId = _modSharp.PushTimer(() =>
            {
                if (_isHiding && _callerController != null && _callerController.IsValid())
                    DistanceUtils.UpdateVisibility(_callerController, _callerDistance, _sharedSystem);
            }, interval: 1.0, flags: GameTimerFlags.Repeatable);
        }

        private void StopVisibilityMonitor(bool stopAndUnhide)
        {
            if (_modSharp.IsValidTimer(_visibilityTimerId))
                _modSharp.StopTimer(_visibilityTimerId);

            if (stopAndUnhide && _callerController != null && _callerController.IsValid())
                DistanceUtils.UnhideAllForCaller(_callerController, _sharedSystem);
        }
    }
}





