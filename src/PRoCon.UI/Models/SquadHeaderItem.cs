namespace PRoCon.UI.Models
{
    /// <summary>
    /// Lightweight item inserted into player list to show squad group headers.
    /// </summary>
    public class SquadHeaderItem
    {
        public string SquadName { get; set; }
        public int PlayerCount { get; set; }
        public string DisplayText => $"{SquadName} ({PlayerCount})";
    }
}
