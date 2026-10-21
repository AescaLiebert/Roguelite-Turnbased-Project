using System;
using System.Collections.Generic;
using System.Linq;

namespace FightingAllstar.Core.Content
{
    public enum DescriptionHighlightRole { Keyword, Number, Constellation, Custom }

    [Serializable]
    public sealed class DescriptionHighlight
    {
        public string Text = "";
        public DescriptionHighlightRole Role;
        public string Color = "#60A5FA";
        public bool Bold = true;
        public DescriptionHighlight Clone() => (DescriptionHighlight)MemberwiseClone();
    }

    /// <summary>Optional designer-owned text. Null retains the existing combat text generator.</summary>
    [Serializable]
    public sealed class AuthoredDescription
    {
        public string Text = "";
        public bool AutoHighlight;
        public bool AutomaticKeywords = true;
        public string KeywordNotes = "";
        public List<DescriptionHighlight> Highlights = new List<DescriptionHighlight>();
        public AuthoredDescription Clone() => new AuthoredDescription {
            Text = Text, AutoHighlight = AutoHighlight, AutomaticKeywords = AutomaticKeywords, KeywordNotes = KeywordNotes,
            Highlights = Highlights?.Select(h => h?.Clone()).ToList() ?? new List<DescriptionHighlight>() };
    }
}
