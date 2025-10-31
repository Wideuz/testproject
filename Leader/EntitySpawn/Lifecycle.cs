
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Sharp.Shared.GameEntities;


namespace Leader.EntitySpawn
{
    /// <summary>
    /// 追蹤單一實體的生命週期資訊
    /// </summary>
    public class LifecycleEntry
    {
        public IBaseEntity Entity { get; }
        public DateTime SpawnTime { get; }
        public TimeSpan Lifetime { get; }

        public LifecycleEntry(IBaseEntity entity, TimeSpan lifetime)
        {
            Entity = entity;
            SpawnTime = DateTime.UtcNow;
            Lifetime = lifetime;
        }

        public bool IsExpired()
            => Lifetime > TimeSpan.Zero && DateTime.UtcNow - SpawnTime > Lifetime;
    }

    /// <summary>
    /// 管理所有實體的生命週期
    /// </summary>
    public class LifecycleManager
    {
        private readonly Dictionary<IBaseEntity, LifecycleEntry> tracked = new();
        private readonly ILogger logger;

        /// <summary>
        /// 當實體被移除時觸發
        /// </summary>
        public event Action<IBaseEntity>? OnEntityRemoved;

        public LifecycleManager(ILogger logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// 追蹤一個實體，並設定壽命（0 = 永久）
        /// </summary>
        public void TrackEntity(IBaseEntity entity, TimeSpan lifetime)
        {
            if (entity == null || !entity.IsValid())
                return;

            tracked[entity] = new LifecycleEntry(entity, lifetime);
            logger.LogDebug("Tracking entity {Entity} with lifetime {Lifetime}", entity, lifetime);
        }

        /// <summary>
        /// 手動取消追蹤某個實體
        /// </summary>
        public void UntrackEntity(IBaseEntity entity)
        {
            if (tracked.TryGetValue(entity, out var entry))
            {
                tracked.Remove(entity);
                try
                {
                    entry.Entity?.Kill();
                    OnEntityRemoved?.Invoke(entry.Entity);
                    logger.LogDebug("Untracked entity {Entity}", entry.Entity);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error untracking entity {Entity}", entry.Entity);
                }
            }
        }

        public void Update()
        {
            var expired = new List<IBaseEntity>();

            foreach (var kvp in tracked)
            {
                var entry = kvp.Value;
                var entity = entry.Entity;

                if (entity == null || !entity.IsValid() || entry.IsExpired())
                {
                    expired.Add(kvp.Key);
                }
            }

            foreach (var key in expired)
            {
                if (tracked.TryGetValue(key, out var entry))
                {
                    tracked.Remove(key);
                    var entity = entry.Entity;
                    try
                    {
                        entity?.Kill();
                        if (entity != null)
                            OnEntityRemoved?.Invoke(entity);

                        logger.LogDebug("Removed expired entity {Entity}", entity);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error removing entity {Entity}", entity);
                    }
                }
            }
        }

        public void CleanupAll()
        {
            foreach (var kvp in tracked)
            {
                var entry = kvp.Value;
                var entity = entry.Entity;

                try
                {
                    entity?.Kill();
                    if (entity != null)
                        OnEntityRemoved?.Invoke(entity);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error cleaning up entity {Entity}", entity);
                }
            }

            tracked.Clear();
            logger.LogInformation("All tracked entities cleaned up");
        }

        /// <summary>
        /// 目前追蹤中的實體數量
        /// </summary>
        public int Count => tracked.Count;
    }
}







