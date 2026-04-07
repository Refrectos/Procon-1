using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PRoCon.UI.Models
{
    public class PlayerDisplayInfo : INotifyPropertyChanged
    {
        private static readonly string[] SquadNames =
        {
            "-", "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot",
            "Golf", "Hotel", "India", "Juliet", "Kilo", "Lima", "Mike",
            "November", "Oscar", "Papa", "Quebec", "Romeo", "Sierra",
            "Tango", "Uniform", "Victor", "Whiskey", "X-Ray", "Yankee", "Zulu"
        };

        public string Name { get; set; }
        public string ClanTag { get; set; }
        public int TeamID { get; set; }
        public string GUID { get; set; }

        private int _score;
        public int Score
        {
            get => _score;
            set { _score = value; OnPropertyChanged(nameof(Score)); OnPropertyChanged(nameof(ScoreText)); }
        }

        private int _kills;
        public int Kills
        {
            get => _kills;
            set { _kills = value; OnPropertyChanged(nameof(Kills)); OnPropertyChanged(nameof(KillsText)); }
        }

        private int _deaths;
        public int Deaths
        {
            get => _deaths;
            set { _deaths = value; OnPropertyChanged(nameof(Deaths)); OnPropertyChanged(nameof(DeathsText)); }
        }

        private int _ping;
        public int Ping
        {
            get => _ping;
            set { _ping = value; OnPropertyChanged(nameof(Ping)); OnPropertyChanged(nameof(PingText)); OnPropertyChanged(nameof(PingBrush)); }
        }

        public int Squad { get; set; }
        public string IP { get; set; }

        public int PlayerType { get; set; }
        public bool IsSpectator => PlayerType == 1;
        public bool IsCommander => PlayerType == 2;

        private bool _isAlive = true;
        public bool IsAlive
        {
            get => _isAlive;
            set { _isAlive = value; OnPropertyChanged(nameof(IsAlive)); OnPropertyChanged(nameof(StatusIcon)); OnPropertyChanged(nameof(RowOpacity)); }
        }

        private bool _isNewJoin;
        public bool IsNewJoin
        {
            get => _isNewJoin;
            set { _isNewJoin = value; OnPropertyChanged(nameof(IsNewJoin)); }
        }

        public string StatusIcon => IsAlive ? "" : "X";
        public double RowOpacity => IsAlive ? 1.0 : 0.5;

        private string _country = "";
        public string Country
        {
            get => _country;
            set { _country = value; OnPropertyChanged(nameof(Country)); OnPropertyChanged(nameof(CountryText)); OnPropertyChanged(nameof(FlagText)); }
        }

        private string _countryCode = "";
        public string CountryCode
        {
            get => _countryCode;
            set { _countryCode = value; OnPropertyChanged(nameof(CountryCode)); OnPropertyChanged(nameof(FlagText)); OnPropertyChanged(nameof(FlagImage)); }
        }

        private bool _isVPN;
        public bool IsVPN
        {
            get => _isVPN;
            set { _isVPN = value; OnPropertyChanged(nameof(IsVPN)); OnPropertyChanged(nameof(ThreatText)); }
        }

        private bool _isProxy;
        public bool IsProxy
        {
            get => _isProxy;
            set { _isProxy = value; OnPropertyChanged(nameof(IsProxy)); OnPropertyChanged(nameof(ThreatText)); }
        }

        public string ScoreText => Score.ToString("N0");
        public string KillsText => Kills.ToString();
        public string DeathsText => Deaths.ToString();
        public string PingText => Ping.ToString();
        public string SquadText => Squad > 0 && Squad < SquadNames.Length ? SquadNames[Squad] : Squad > 0 ? Squad.ToString() : "-";
        public string CountryText => !string.IsNullOrEmpty(Country) ? Country : "";
        public string FlagText => !string.IsNullOrEmpty(CountryCode) ? CountryCode.ToUpper() : "";
        public string ThreatText => IsVPN ? "VPN" : IsProxy ? "PROXY" : "";

        public IBrush PingBrush
        {
            get
            {
                if (Ping <= 0) return new SolidColorBrush(Color.Parse("#666666"));
                if (Ping <= 50) return new SolidColorBrush(Color.Parse("#81c784"));
                if (Ping <= 120) return new SolidColorBrush(Color.Parse("#ffd740"));
                return new SolidColorBrush(Color.Parse("#ef5350"));
            }
        }

        private Bitmap _flagImage;
        public Bitmap FlagImage
        {
            get => _flagImage;
            set { _flagImage = value; OnPropertyChanged(nameof(FlagImage)); OnPropertyChanged(nameof(HasFlagImage)); }
        }

        public bool HasFlagImage => _flagImage != null;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
