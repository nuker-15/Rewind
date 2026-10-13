using System;
using Rewind.Core;
using UnityEngine;

namespace Rewind.Data
{
    /// <summary>
    /// One-line level codes for sharing levels made in the game: <c>RWD1|name|par|row/row/row</c>.
    /// A plain multi-line text map is accepted when decoding too.
    /// </summary>
    public static class LevelCode
    {
        public const string Prefix = "RWD1";

        public static string Encode(string name, int par, string map)
        {
            var rows = LevelLayout.SplitRows(map);
            return $"{Prefix}|{CleanName(name)}|{Mathf.Max(1, par)}|{string.Join("/", rows)}";
        }

        public static bool TryDecode(string code, out string name, out int par, out string map, out string error)
        {
            name = "Imported level";
            par = 2;
            map = null;
            error = null;
            if (string.IsNullOrWhiteSpace(code))
            {
                error = "There is no level code.";
                return false;
            }

            code = code.Trim();
            if (code.StartsWith(Prefix + "|", StringComparison.Ordinal))
            {
                var parts = code.Split(new[] { '|' }, 4);
                if (parts.Length < 4)
                {
                    error = "The level code is incomplete.";
                    return false;
                }
                name = CleanName(parts[1]);
                par = int.TryParse(parts[2], out int p) ? Mathf.Clamp(p, 1, 99) : 2;
                map = parts[3].Trim().Replace("/", "\n");
            }
            else
            {
                map = code.Replace("\r", "");
            }

            try { LevelLayout.Parse(map); }
            catch (FormatException e)
            {
                error = e.Message;
                map = null;
                return false;
            }
            return true;
        }

        public static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Untitled";
            return name.Replace("|", " ").Replace("/", " ").Replace("\n", " ").Trim();
        }
    }
}
