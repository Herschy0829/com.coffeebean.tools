using System;
using System.Text;

namespace CoffeeBean.Tools
{
    /// <summary>
    /// 字符串工具：常见文本处理（数字提取 / 容错解析 / 十六进制转换 / 大小写）。
    ///
    /// 用法：
    /// <code>
    /// string name = CString.FirstUpper("hero");          // "Hero"
    /// float price = CString.ExtractLeadingNumber("¥68");  // 68
    /// string hex = CString.BytesToHex(new byte[]{0x1A, 0x2B}); // "1A2B"
    /// </code>
    /// </summary>
    public static class CString
    {
        /// <summary>首字母大写（"hero" → "Hero"）；空字符串原样返回。</summary>
        public static string FirstUpper(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        /// <summary>仅保留字符串中的数字字符（"abc12x3" → "123"）。</summary>
        public static string ExtractDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c >= '0' && c <= '9') sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 提取开头数字（含正负号与小数点）："¥68"→68、"$1.99"→1.99、"v1.2"→1.2；
        /// 无数字返回 0。用于解析带货币符号/单位前缀的数值字符串。
        /// </summary>
        public static float ExtractLeadingNumber(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0f;

            int start = -1;
            int end = s.Length;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (start < 0)
                {
                    if (char.IsDigit(c) || c == '-' || c == '+') start = i;
                }
                else if (!char.IsDigit(c) && c != '.')
                {
                    end = i;
                    break;
                }
            }
            if (start < 0) return 0f;

            string number = s.Substring(start, end - start);
            return float.TryParse(number, out float value) ? value : 0f;
        }

        /// <summary>是否为合法数字（decimal 可解析）。</summary>
        public static bool IsNumeric(string s)
            => !string.IsNullOrWhiteSpace(s) && decimal.TryParse(s, out _);

        /// <summary>字节数组 → 大写十六进制字符串（"1A2B"）。</summary>
        public static string BytesToHex(byte[] bytes)
        {
            if (bytes == null) return string.Empty;
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("X2"));
            return sb.ToString();
        }

        /// <summary>十六进制字符串 → 字节数组；长度非偶数或含非法字符返回 null。</summary>
        public static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0) return null;

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2),
                        System.Globalization.NumberStyles.HexNumber, null, out byte value))
                {
                    return null;
                }
                bytes[i] = value;
            }
            return bytes;
        }
    }
}
