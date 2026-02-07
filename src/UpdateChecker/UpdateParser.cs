using System;
using System.Collections.Generic;

namespace UpdateChecker;

public static class UpdateParser
{
    public static List<string> Parse(string wingetOutput)
    {
        var updates = new List<string>();
        var lines = wingetOutput.Split('\n', StringSplitOptions.None);

        // Find separator line (----------)
        int separatorIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("----------"))
            {
                separatorIndex = i;
                break;
            }
        }

        if (separatorIndex == -1)
            return updates;

        // Parse lines after separator
        for (int i = separatorIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;
            // Skip summary line like "12 upgrades available."
            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d+\s+(Upgrades?|upgrades?)"))
                continue;
            if (line.Length > 20)
                updates.Add(line);
        }

        return updates;
    }
}
