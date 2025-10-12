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
        }

        public bool Init()
        {
            _logger.LogInformation("Leader initializing");
            return true;
        }

        public void PostInit()
        {
            // 註冊指令
            _clientManager.InstallCommandCallback("leader", OnLeaderCommand);
            _clientManager.InstallCommandCallback("hide", OnHideCommand);

            // 註冊事件監聽器
            _events.HookEvent("player_connect_full");
            _events.HookEvent("player_spawn");
            _events.InstallEventListener(this);

            _logger.LogInformation("Leader post-initialized");
        }

        public void Shutdown()
        {

            _logger.LogInformation($"Shutdown Leaders");
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
                    {
                        _transmitManager.AddEntityHooks((IBaseEntity)controller, defaultTransmit: true);
                        _logger.LogInformation($"[Leader] Hooked transmit for {controller.PlayerName} (Slot {slot}) via {e.Name}");
                    }
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
            {
                controller.Print(HudPrintChannel.Chat, "✅ 你現在是指揮官！");
                _logger.LogInformation($"Leader assigned via command: {controller.PlayerName} ({controller.SteamId})");
            }
            else
            {
                controller.Print(HudPrintChannel.Chat, "⚠️ 指定失敗，請確認你是有效玩家！");
            }

            return ECommandAction.Handled;
        }

        private ECommandAction OnHideCommand(IGameClient client, StringCommand command)
        {
            var controller = _entityManager.FindPlayerControllerBySlot(client.Slot);
            if (controller is null || !controller.IsValid())
                return ECommandAction.Handled;

            if (string.IsNullOrWhiteSpace(command.ArgString))
            {
                controller.Print(HudPrintChannel.Chat, "⚠️ 請輸入距離，例如 /hide 500");
                return ECommandAction.Handled;
            }

            if (!float.TryParse(command.ArgString, out float distance) || distance <= 0)
            {
                controller.Print(HudPrintChannel.Chat, "⚠️ 距離必須是正數，例如 /hide 500");
                return ECommandAction.Handled;
            }

            DistanceUtils.HideNearbyPlayers(controller, distance, _sharedSystem);
            return ECommandAction.Handled;
        }
    }
}

