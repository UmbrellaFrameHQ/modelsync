using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    /// <summary>
    /// Normalizes SQL definitions before hash comparison.
    /// </summary>
    public static class SqlDefinitionNormalizer
    {
        private static readonly Regex LineCommentPattern =
            new Regex(@"--.*?$", RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex BlockCommentPattern =
            new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex WhitespacePattern =
            new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>Normalizes SQL text for stable comparison.</summary>
        public static string Normalize(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return string.Empty;

            var normalized = sql.Replace("\r\n", "\n").Replace("\r", "\n");
            normalized = BlockCommentPattern.Replace(normalized, " ");
            normalized = LineCommentPattern.Replace(normalized, " ");
            normalized = WhitespacePattern.Replace(normalized, " ");
            return normalized.Trim();
        }

        /// <summary>Returns a SHA-256 hash for normalized SQL text.</summary>
        public static string ComputeHash(string sql)
        {
            var normalized = Normalize(sql);
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
