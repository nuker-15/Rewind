using System;
using System.Globalization;
using Rewind.Core;
using UnityEngine;

namespace Rewind.Data
{
    /// <summary>
    /// One-line level codes for sharing levels made in the game: <c>RWD2|name|par|row/row/row|t=20|l=3,4>10,4|r=19x9|c=12,4,2,15</c>
    /// (t: seconds per loop, l: plate-to-door links, r: rooms, c: checkpoint settings).
    /// Fields after the map are optional key=value pairs, so new ones can be added later; unknown keys are ignored.
    /// RWD1 codes (no extra fields) and plain multi-line text maps are accepted when decoding too.
    /// </summary>
    public static class LevelCode
    {
        public const string Prefix = "RWD2";
        const string OldPrefix = "RWD1";

        public static string Encode(CustomLevel level)
        {
            var rows = LevelLayout.SplitRows(level.map);
            var code = $"{Prefix}|{CleanName(level.name)}|{Mathf.Max(1, level.par)}|{string.Join("/", rows)}";
            if (level.loopSeconds > 0f) code += "|t=" + level.loopSeconds.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(level.links)) code += "|l=" + level.links.Trim();
            if (!string.IsNullOrWhiteSpace(level.rooms)) code += "|r=" + level.rooms.Trim();
            if (!string.IsNullOrWhiteSpace(level.checkpoints)) code += "|c=" + level.checkpoints.Trim();
            return code;
        }

        public static bool TryDecode(string code, out CustomLevel level, out string error)
        {
            level = new CustomLevel { name = "Imported level" };
            error = null;
            if (string.IsNullOrWhiteSpace(code))
            {
                error = "There is no level code.";
                return false;
            }

            code = code.Trim();
            if (code.StartsWith(Prefix + "|", StringComparison.Ordinal) || code.StartsWith(OldPrefix + "|", StringComparison.Ordinal))
            {
                var parts = code.Split('|');
                if (parts.Length < 4)
                {
                    error = "The level code is incomplete.";
                    return false;
                }
                level.name = CleanName(parts[1]);
                level.par = int.TryParse(parts[2], out int p) ? Mathf.Clamp(p, 1, 99) : 2;
                level.map = parts[3].Trim().Replace("/", "\n");
                for (int i = 4; i < parts.Length; i++)
                {
                    int eq = parts[i].IndexOf('=');
                    if (eq < 0) continue;
                    string key = parts[i].Substring(0, eq).Trim(), value = parts[i].Substring(eq + 1).Trim();
                    if (key == "t" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float t) && t > 0f)
                        level.loopSeconds = Mathf.Clamp(t, LevelData.MinLoopSeconds, LevelData.MaxLoopSeconds);
                    else if (key == "l") level.links = value;
                    else if (key == "c") level.checkpoints = value;
                    else if (key == "r") level.rooms = value;
                }
            }
            else
            {
                level.map = code.Replace("\r", "");
            }

            try { LevelLayout.Parse(level.map, level.links, level.checkpoints); }
            catch (FormatException e)
            {
                error = e.Message;
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
