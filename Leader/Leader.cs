using Leader.ModuleMethod;
using Leader.Precache;
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
    public sealed class Leader : IModSharpModule, IEventListener, IGameListener
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

        private readonly ParticlePrecache _particlePrecache;

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

            // 初始化粒子預載模組
            _particlePrecache = new ParticlePrecache(
                _sharedSystem.GetLoggerFactory().CreateLogger<ParticlePrecache>(),
                _sharedSystem
            );
        }

        public bool Init()
        {
            _logger.LogInformation("Leader initializing");
            _modSharp.InstallGameListener(this);   // 註冊 GameListener
            return true;
        }

        public void PostInit()
        {
            // 掃描 vpcf 清單（不直接 Precache）
            _particlePrecache.Init();

            // 註冊指令
            _clientManager.InstallCommandCallback("marker", OnMarkerCommand);
            _clientManager.InstallCommandCallback("leader", OnLeaderCommand);
            _clientManager.InstallCommandCallback("hide", OnHideCommand);

            // 註冊事件
            _events.InstallEventListener(this);
            _events.HookEvent("player_connect_full");
        }

        public void Shutdown()
        {
            _clientManager.RemoveCommandCallback("marker", OnMarkerCommand);
            _clientManager.RemoveCommandCallback("leader", OnLeaderCommand);
            _clientManager.RemoveCommandCallback("hide", OnHideCommand);

            StopVisibilityMonitor(true);

            _modSharp.RemoveGameListener(this);   // 移除 GameListener
            _events.RemoveEventListener(this);
        }

        // 這裡就是引擎進入資源預載階段時會被呼叫
        public void OnResourcePrecache()
        {
            _logger.LogInformation("Leader.OnResourcePrecache triggered");
            try
            {
                _particlePrecache.PrecacheAll();
                _logger.LogInformation("Leader.ResourcePrecache completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to precache particle resources");
            }

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

        private ECommandAction OnMarkerCommand(IGameClient client, StringCommand command)
        {
            var controller = _entityManager.FindPlayerControllerBySlot(client.Slot);
            if (controller is null || !controller.IsValid())
                return ECommandAction.Handled;

            var pawn = controller.GetPawn();
            if (pawn is null || !pawn.IsAlive)
            {
                controller.Print(HudPrintChannel.Chat, "你必須活著才能放置標記！");
                return ECommandAction.Handled;
            }

            const float maxDistance = 3000f;

            // 1) 從 Pawn 取得眼睛位置與角度
            var eyePos = pawn.GetEyePosition();
            var eyeAngles = pawn.GetEyeAngles();
            var forward = AngleToForward(eyeAngles);
            forward.Normalize(); // 這裡用 Normalize()

            var endPos = eyePos + forward * maxDistance;

            // 2) 呼叫物理檢測
            var physics = _sharedSystem.GetPhysicsQueryManager();

            var flags = TraceQueryFlag.All;

            var trace = physics.TraceLineNoPlayers(
                eyePos,
                endPos,
                InteractionLayers.Solid,
                (CollisionGroupType)0, // 或 CollisionGroupType.COLLISION_GROUP_NONE
                flags,
                InteractionLayers.None,
                pawn
            );

            // TraceResult 沒有 HitPosition，要用 EndPos
            var hitPos = trace.DidHit() ? trace.HitPoint : trace.EndPosition;
            var placePos = hitPos + new Vector(0, 0, 1.0f);

            var gameRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\.."));
            var fullPath = Path.Combine(gameRoot, "custom", "particles", "leader_a_1.vpcf");
            _modSharp.DispatchParticleEffect(fullPath, placePos, new Vector(0, 0, 0));

            controller.Print(HudPrintChannel.Chat, "已在準心位置放置標記！");
            return ECommandAction.Stopped;
        }

        // 工具函式：角度轉 forward 向量
        private static Vector AngleToForward(Vector angles)
        {
            var pitch = MathF.PI / 180f * angles.X;
            var yaw = MathF.PI / 180f * angles.Y;

            var cp = MathF.Cos(pitch);
            var sp = MathF.Sin(pitch);
            var cy = MathF.Cos(yaw);
            var sy = MathF.Sin(yaw);

            return new Vector(cp * cy, cp * sy, -sp);
        }



        private ECommandAction OnLeaderCommand(IGameClient client, StringCommand command)
        {
            var controller = _entityManager.FindPlayerControllerBySlot(client.Slot);
            if (controller is null || !controller.IsValid())
                return ECommandAction.Handled;

            if (LeaderMethod.IsLeader(controller))
            {
                controller.Print(HudPrintChannel.Chat, "You are already a leader！");
                return ECommandAction.Handled;
            }

            if (LeaderMethod.AssignLeader(controller))
                controller.Print(HudPrintChannel.Chat, "You are leader now！");
            else
                controller.Print(HudPrintChannel.Chat, "Make sure the target is valid！");

            return ECommandAction.Stopped;
        }

        private ECommandAction OnHideCommand(IGameClient client, StringCommand command)
        {
            var controller = _entityManager.FindPlayerControllerBySlot(client.Slot);
            if (controller is null || !controller.IsValid())
                return ECommandAction.Handled;

            if (!float.TryParse(command.ArgString, out float distance))
            {
                controller.Print(HudPrintChannel.Chat, "Usage : /hide <distance> | -1 = cancel | 0 = full range");
                return ECommandAction.Handled;
            }

            if (distance == -1 || (_isHiding && Math.Abs(distance - _lastDistance) < 0.01f))
            {
                StopVisibilityMonitor(true);
                controller.Print(HudPrintChannel.Chat, "show the all players");
                _isHiding = false;
                _lastDistance = -1f;
                _callerController = null;
                _callerDistance = 0f;
                return ECommandAction.Stopped;
            }

            if (distance == 0)
                distance = float.MaxValue;

            _callerController = controller;
            _callerDistance = distance;
            _lastDistance = distance;
            _isHiding = true;

            DistanceUtils.UpdateVisibility(_callerController, _callerDistance, _sharedSystem);

            StartVisibilityMonitor();
            controller.Print(HudPrintChannel.Chat, $"hide players beyond {distance} units");

            return ECommandAction.Handled;
        }

        private void StartVisibilityMonitor()
        {
            if (_modSharp.IsValidTimer(_visibilityTimerId))
                _modSharp.StopTimer(_visibilityTimerId);

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






