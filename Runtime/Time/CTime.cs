using System;

namespace CoffeeBean
{
    /// <summary>
    /// 时间工具：Unix 时间戳与时长格式化（倒计时 / 在线时长 / 排行榜显示等）。
    ///
    /// 用法：
    /// <code>
    /// long now = CTime.UnixNow;                       // 当前时间戳（秒）
    /// string text = CTime.FormatDuration(3661);       // "01:01:01"
    /// string clock = CTime.FormatClock(90);           // "01:30"
    /// </code>
    /// </summary>
    public static class CTime
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>当前 Unix 时间戳（UTC，秒）。</summary>
        public static long UnixNow => (long)(DateTime.UtcNow - UnixEpoch).TotalSeconds;

        /// <summary>把秒数格式化为 "HH:mm:ss"；超过 24 小时显示 "N天HH:mm:ss"。</summary>
        public static string FormatDuration(long totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            long days = totalSeconds / 86400;
            long hours = (totalSeconds % 86400) / 3600;
            long minutes = (totalSeconds % 3600) / 60;
            long seconds = totalSeconds % 60;

            if (days > 0) return $"{days}天{hours:00}:{minutes:00}:{seconds:00}";
            return $"{hours:00}:{minutes:00}:{seconds:00}";
        }

        /// <summary>把秒数格式化为 "mm:ss"（游戏内倒计时常用，超出 1 小时显示 "HH:mm:ss"）。</summary>
        public static string FormatClock(long totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            if (totalSeconds >= 3600) return FormatDuration(totalSeconds);
            return $"{(totalSeconds / 60):00}:{(totalSeconds % 60):00}";
        }
    }
}
