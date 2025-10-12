using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using System;

/////
///
/// Leader 
///
/////
namespace Leader
{
    public sealed class Leader : IModSharpModule, IEventListener
    {
        public string DisplayName => "Leader";
        public string DisplayAuthor => "Widez";

        public int ListenerVersion => 1;
        public int ListenerPriority => 0;

        private readonly ILogger<Leader> _logger;
        private readonly ISharedSystem _sharedSystem;
        private readonly IEventManager _events;
        private readonly IClientManager _clientManager;

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
        }

        public bool Init()
        {
            _logger.LogInformation("Leader initializing");
            return true;
        }

        public void PostInit()
        {
            var clientManager = _sharedSystem.GetClientManager();
            clientManager.InstallCommandCallback("leader", OnLeaderCommand); // 註冊 ms_leader 指令
            _clientManager.InstallCommandCallback("hide", OnHideCommand);
            _logger.LogInformation("Leader post-initialized");
        }

        public void Shutdown()
        {
            _logger.LogInformation("Leader shutting down");
        }

        public void FireGameEvent(IGameEvent ev)
        {
            
        }


        private ECommandAction OnLeaderCommand(IGameClient client, StringCommand command)
        {
            var entityManager = _sharedSystem.GetEntityManager();
            var controller = entityManager.FindPlayerControllerBySlot(client.Slot);
            if (controller == null || !controller.IsValid())
                return ECommandAction.Handled;

            // ✅ 先檢查是否已經是 Leader
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
            var entityManager = _sharedSystem.GetEntityManager();
            var controller = entityManager.FindPlayerControllerBySlot(client.Slot);
            if (controller == null || !controller.IsValid())
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
