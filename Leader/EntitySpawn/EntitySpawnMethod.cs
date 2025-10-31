using Microsoft.Extensions.Logging;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Managers;
using Sharp.Shared.Types;
using System;
using System.Collections.Generic;

namespace Leader.EntitySpawn
{
    public class EntitySpawnResult
    {
        public IBaseEntity? Entity { get; set; }
        public Vector Position { get; set; }
        public float Scale { get; set; }
    }

    public class EntitySpawnMethod
    {
        private readonly IEntityManager entityManager;
        private readonly ILogger logger;

        public EntitySpawnMethod(IEntityManager entityManager, ILogger logger)
        {
            this.entityManager = entityManager;
            this.logger = logger;
        }

        public EntitySpawnResult SpawnEntity<T>(
            string classname,
            Vector position,
            string model,
            float scale = 1.0f,
            CollisionGroupType collisionGroup = CollisionGroupType.Interactive,
            int spawnFlags = 0,
            Dictionary<string, KeyValuesVariantValueItem>? extraKeys = null,
            Action<T>? configure = null
        ) where T : class, IBaseEntity
        {
            try
            {
                var kv = new Dictionary<string, KeyValuesVariantValueItem>
                {
                    {"model", model},
                    {"origin", $"{position.X} {position.Y} {position.Z}"},
                    {"scale", scale},
                    {"spawnflags", spawnFlags}
                };

                if (extraKeys != null)
                {
                    foreach (var kvp in extraKeys)
                        kv[kvp.Key] = kvp.Value;
                }

                var entity = entityManager.SpawnEntitySync<T>(classname, kv);
                if (entity == null)
                {
                    logger.LogWarning("Failed to spawn entity {Classname} at {Position}", classname, position);
                    return new EntitySpawnResult { Entity = null, Position = position, Scale = scale };
                }

                // 設定碰撞
                entity.SetCollisionGroup(collisionGroup);
                entity.CollisionRulesChanged();

                // 呼叫額外設定
                configure?.Invoke(entity);

                return new EntitySpawnResult
                {
                    Entity = entity,
                    Position = position,
                    Scale = scale
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error spawning entity {Classname}", classname);
                return new EntitySpawnResult { Entity = null, Position = position, Scale = scale };
            }
        }
    }
}


