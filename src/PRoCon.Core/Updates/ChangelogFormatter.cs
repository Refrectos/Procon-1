using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PRoCon.Core.Updates
{
    public enum ChangelogCategory { New, Fix, Enhancement, Breaking, Other }

    public class ChangelogSection
    {
        public ChangelogCategory Category { get; set; }
        public string Heading { get; set; }
        public List<string> Items { get; set; } = new List<string>();
    }

    public static class ChangelogFormatter
    {
        private static readonly Regex MarkdownLink = new Regex(@"\[([^\]]+)\]\([^)]+\)", RegexOptions.Compiled);
        private static readonly Regex HeadingPattern = new Regex(@"^###\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex BulletPattern = new Regex(@"^\s*[-*]\s+(.+)$", RegexOptions.Compiled);

        public static List<ChangelogSection> Parse(string markdownBody)
        {
            var sections = new List<ChangelogSection>();
            if (string.IsNullOrWhiteSpace(markdownBody))
                return sections;

            string cleaned = CleanMarkdown(markdownBody);
            string[] lines = cleaned.Split('\n');

            ChangelogSection current = null;
            var ungroupedItems = new List<string>();

            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd();

                var headingMatch = HeadingPattern.Match(line);
                if (headingMatch.Success)
                {
                    if (current == null && ungroupedItems.Count > 0)
                    {
                        sections.Add(new ChangelogSection
                        {
                            Category = ChangelogCategory.Other,
                            Heading = "",
                            Items = new List<string>(ungroupedItems)
                        });
                        ungroupedItems.Clear();
                    }

                    string heading = headingMatch.Groups[1].Value.Trim();
                    current = new ChangelogSection
                    {
                        Category = CategorizeHeading(heading),
                        Heading = heading
                    };
                    sections.Add(current);
                    continue;
                }

                var bulletMatch = BulletPattern.Match(line);
                if (bulletMatch.Success)
                {
                    string item = bulletMatch.Groups[1].Value.Trim();
                    if (current != null)
                        current.Items.Add(item);
                    else
                        ungroupedItems.Add(item);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                {
                    if (current != null)
                        current.Items.Add(line.Trim());
                    else
                        ungroupedItems.Add(line.Trim());
                }
            }

            if (sections.Count == 0 && ungroupedItems.Count > 0)
            {
                sections.Add(new ChangelogSection
                {
                    Category = ChangelogCategory.Other,
                    Heading = "",
                    Items = ungroupedItems
                });
            }

            return sections;
        }

        private static ChangelogCategory CategorizeHeading(string heading)
        {
            string lower = heading.ToLowerInvariant();

            if (lower.Contains("feature") || lower.Contains("added") || lower == "new")
                return ChangelogCategory.New;

            if (lower.Contains("fix") || lower.Contains("bug"))
                return ChangelogCategory.Fix;

            if (lower.Contains("enhance") || lower.Contains("changed") ||
                lower.Contains("improv") || lower.Contains("updated") || lower.Contains("update"))
                return ChangelogCategory.Enhancement;

            if (lower.Contains("break") || lower.Contains("removed") || lower.Contains("deprecat"))
                return ChangelogCategory.Breaking;

            return ChangelogCategory.Other;
        }

        public static string CleanMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            text = MarkdownLink.Replace(text, "$1");
            text = text.Replace("**", "");
            text = Regex.Replace(text, @"^#{1,2}\s+", "", RegexOptions.Multiline);

            return text;
        }
    }
}
