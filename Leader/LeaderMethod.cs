using Sharp.Shared.GameEntities;
using Sharp.Shared.Units;
using System.Collections.Generic;

namespace Leader
{
    public static class LeaderMethod
    {
        // 儲存 Leader 的 SteamID（64位）ULONG format
        private static HashSet<ulong> _leaderSteamIds = new();

        /// <summary>
        /// 指定一位玩家為 Leader
        /// </summary>
        public static bool AssignLeader(IBaseEntity entity)
        {
            var controller = entity.AsPlayerController();
            if (controller == null || !entity.IsValid())
                return false;

            _leaderSteamIds.Add(controller.SteamId);
            return true;
        }

        /// <summary>
        /// 移除一位 Leader
        /// </summary>
        public static bool RemoveLeader(IBaseEntity entity)
        {
            var controller = entity.AsPlayerController();
            if (controller == null || !entity.IsValid())
                return false;

            return _leaderSteamIds.Remove(controller.SteamId);
        }

        /// <summary>
        /// 清空所有 Leader
        /// </summary>
        public static void ClearAllLeaders()
        {
            _leaderSteamIds.Clear();
        }

        /// <summary>
        /// 檢查某玩家是否是 Leader
        /// </summary>
        public static bool IsLeader(IBaseEntity entity)
        {
            var controller = entity.AsPlayerController();
            return controller != null && _leaderSteamIds.Contains(controller.SteamId);
        }

        /// <summary>
        /// 取得所有 Leader 的 SteamID
        /// </summary>
        public static IEnumerable<ulong> GetAllLeaderSteamIds()
        {
            return _leaderSteamIds;
        }
    }
}