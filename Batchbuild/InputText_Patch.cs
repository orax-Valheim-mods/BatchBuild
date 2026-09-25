using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Batchbuild
{
    [HarmonyPatch(typeof(Terminal), "InputText")]
    class InputText_Patch
    {
        static bool Prefix(Terminal __instance)
        {
            string command = Plugin.configCommand.Value;
            string commandLine = __instance.m_input.text;

            if (commandLine.Equals(command) ||
                commandLine.Equals(command + " -?") ||
                commandLine.Equals(command + " /?"))
            {
                Plugin.Log.LogInfo(command + " id [pos_x pos_y pos_z [angle_x angle_y angle_z [point_x point_y point_z axis_x axis_y axis_z angle [scale_x scale_y scale_z]]]]");

                return false;
            }

            if (!commandLine.StartsWith(command + " "))
            {
                return true;
            }

            Vector3 offset = Vector3.zero;
            commandLine = Regex.Replace(commandLine, command + @"\s+", ""); // Removes the command prefix.
            commandLine = Regex.Replace(commandLine, @"\s*#.*", ""); // Removes comments.

            char[] charSeparators = new char[] { ' ' };

            // command -f example.txt
            Match match = Regex.Match(commandLine, @"-f\s+[""]?(?<file>.+)[""]?\s*");
            if (match.Success)
            {
                string filePath = match.Groups["file"].Value;
                Plugin.Log.LogDebug("Read file " + filePath);

                string text = File.ReadAllText(filePath);

                string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];

                    Plugin.Log.LogDebug("Line #" + (i + 1) + "/" + lines.Length + " \"" + line + "\"");

                    line = Regex.Replace(line, @"\s*#.*", ""); // Removes comments.
                    if (line == "")
                    {
                        continue;
                    }

                    line = Plugin.InterpolateString(line);

                    Match matchLine = Regex.Match(line, @"^\s*!\s*offset_x\s*=\s*(.+)\s*");
                    if (matchLine.Success)
                    {
                        offset.x = Plugin.Parse(matchLine.Groups[1].Value);
                        Plugin.Log.LogDebug("Offset x = " + offset.x);
                        continue;
                    }
                    matchLine = Regex.Match(line, @"^\s*!\s*offset_y\s*=\s*(.+)\s*");
                    if (matchLine.Success)
                    {
                        offset.y = Plugin.Parse(matchLine.Groups[1].Value);
                        Plugin.Log.LogDebug("Offset y = " + offset.y);
                        continue;
                    }
                    matchLine = Regex.Match(line, @"^\s*!\s*offset_z\s*=\s*(.+)\s*");
                    if (matchLine.Success)
                    {
                        offset.z = Plugin.Parse(matchLine.Groups[1].Value);
                        Plugin.Log.LogDebug("Offset z = " + offset.z);
                        continue;
                    }

                    // Gets the columns.
                    // https://stackoverflow.com/a/14655145
                    string[] columns = Regex.Matches(line, @"[\""].+?[\""]|[^ ]+")
                        .Cast<Match>()
                        .Select(m => m.Value.Replace("\"", string.Empty))
                        .ToArray();
                    if (columns.Length < 1)
                    {
                        return true;
                    }
                    Plugin.InstantiatePrefab(columns, offset);
                }
            }
            else
            {
                commandLine = Plugin.InterpolateString(commandLine);
                // Gets the columns.
                // https://stackoverflow.com/a/14655145
                string[] columns = Regex.Matches(commandLine, @"[\""].+?[\""]|[^ ]+")
                    .Cast<Match>()
                    .Select(m => m.Value.Replace("\"", string.Empty))
                    .ToArray();
                if (columns.Length < 1)
                {
                    return true;
                }
                Plugin.InstantiatePrefab(columns, offset);
            }

            return false;
        }
    }
}
