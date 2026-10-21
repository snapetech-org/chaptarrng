using System;
using System.Collections.Generic;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.MyAnonaMouse
{
    public class MyAnonaMouseSettingsValidator : AbstractValidator<MyAnonaMouseSettings>
    {
        public MyAnonaMouseSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).ValidRootUrl();
            RuleFor(c => c.MamId).NotEmpty();

            RuleFor(c => c.UnsatisfiedSlotReserve).GreaterThanOrEqualTo(0);
            RuleFor(c => c.ManualGrabBuffer).GreaterThanOrEqualTo(0);
            RuleFor(c => c.SeedCriteria).SetValidator(_ => new SeedCriteriaSettingsValidator(seedTimeMinimum: 72 * 60));
        }
    }

    public class MyAnonaMouseSettings : ITorrentIndexerSettings
    {
        private static readonly MyAnonaMouseSettingsValidator Validator = new MyAnonaMouseSettingsValidator();
        private const int LegacyRequiredWedgeAction = 2;
        private int _useFreeleechWedge;

        public MyAnonaMouseSettings()
        {
            BaseUrl = "https://www.myanonamouse.net";
            MinimumSeeders = IndexerDefaults.MINIMUM_SEEDERS;
            EnableDeepSearch = true; // Default to enabled for better metadata
            IntegrateWithAbs = false; // Default to disabled (hidden in UI)
            UseFreeleechOnlyForAudiobooks = true; // Default to audiobooks-only
            MamSsl = true; // Default to true since most sessions use mam_ssl
            ProtectUnsatisfiedSlots = true;
            UnsatisfiedSlotReserve = 5;
            ManualGrabBuffer = 0;
            SearchInSeries = true;
            SearchLanguages = Array.Empty<int>();
        }

        [FieldDefinition(0, Label = "Website URL", Hidden = HiddenType.Hidden)]
        public string BaseUrl { get; set; }

        private string _mamId;

        [FieldDefinition(1, Label = "mam_id", Type = FieldType.Password, Privacy = PrivacyLevel.Password, HelpText = "The mam_id is found under Preferences > Security")]
        public string MamId
        {
            get => _mamId;
            set => _mamId = string.IsNullOrEmpty(value) ? value : value.Trim();
        }

        [FieldDefinition(1, Label = "mam_ssl", Type = FieldType.Checkbox, HelpText = "Set if your MAM session uses the mam_ssl cookie (most accounts do). This adds mam_ssl=1 to requests.", Advanced = true)]
        public bool MamSsl { get; set; }

        [FieldDefinition(2, Type = FieldType.Select, Label = "Freeleech Wedges", SelectOptions = typeof(MyAnonaMouseFreeleechWedgeAction), HelpText = "Follow your MAM site preference, or ask MAM to apply a personal freeleech wedge when grabbing an eligible torrent. Prefer Wedge falls back to a normal download if MAM cannot apply one.")]
        public int UseFreeleechWedge
        {
            get => _useFreeleechWedge;
            set => _useFreeleechWedge = value == LegacyRequiredWedgeAction
                ? (int)MyAnonaMouseFreeleechWedgeAction.Never
                : value;
        }

        [FieldDefinition(3, Type = FieldType.Checkbox, Label = "Only Use Wedges for Audiobooks", HelpText = "Do not request a wedge for ebook torrents. Chaptarr also avoids requesting wedges for torrents already marked free, personal freeleech, or VIP.")]
        public bool UseFreeleechOnlyForAudiobooks { get; set; }

        [FieldDefinition(4, Type = FieldType.Textbox, Label = "Minimum Seeders", HelpText = "Minimum number of seeders required.")]
        public int MinimumSeeders { get; set; }

        [FieldDefinition(5, Type = FieldType.Checkbox, Label = "Enable Deep Search", HelpText = "Use JSON API with enhanced metadata including title, description, narrator, and duration information when available", Hidden = HiddenType.Hidden)]
        public bool EnableDeepSearch { get; set; }

        [FieldDefinition(6, Type = FieldType.Checkbox, Label = "Integrate with Audiobookshelf", HelpText = "If you've connected ABS this will enhance your MAM searching", Hidden = HiddenType.Hidden)]
        public bool IntegrateWithAbs { get; set; }

        [FieldDefinition(7, Type = FieldType.Number, Label = "Seed Time", Unit = "hours", HelpText = "Time in hours to seed before the download client stops the torrent. Leave empty to use the download client's default seeding rules. MAM requires at least 72 hours within 30 days; values below 72 hours will warn.")]
        public int? SeedTimeHours { get; set; }

        [FieldDefinition(8, Type = FieldType.Number, Label = "Early Download Limit", Unit = "days", HelpText = "Time before release date Chaptarr will download from this indexer, empty is no limit", Advanced = true)]
        public int? EarlyReleaseLimit { get; set; }

        // VIP Status - populated during connection test, hidden from editing (display-only elsewhere)
        [FieldDefinition(9, Type = FieldType.Textbox, Label = "MAM User Class", HelpText = "Detected on connection test", Hidden = HiddenType.Hidden)]
        public string UserClass { get; set; }

        [FieldDefinition(10, Type = FieldType.Checkbox, Label = "VIP Member", HelpText = "Detected on connection test", Hidden = HiddenType.Hidden)]
        public bool IsVip { get; set; }

        [FieldDefinition(11, Type = FieldType.Checkbox, Label = "Protect Unsatisfied Slots", HelpText = "Pause MAM grabs before your unsatisfied-torrent limit is reached. Disabling this can trigger MAM's 24-hour download suspension.", Advanced = true)]
        public bool ProtectUnsatisfiedSlots { get; set; }

        [FieldDefinition(12, Type = FieldType.Number, Label = "Safety Reserve", Unit = "slots", HelpText = "Slots Chaptarr will always keep free to protect against MAM's reporting delay and downloads started outside Chaptarr. Neither automatic nor Interactive Search grabs can use this reserve.")]
        public int UnsatisfiedSlotReserve { get; set; }

        [FieldDefinition(13, Type = FieldType.Number, Label = "User Buffer", Unit = "slots", HelpText = "Additional slots automatic grabs will keep free for releases you choose in Interactive Search or download outside Chaptarr. Interactive Search can use this buffer, but never the Safety Reserve.")]
        public int ManualGrabBuffer { get; set; }

        [FieldDefinition(14, Type = FieldType.Checkbox, Label = "IndexerSettingsNeverMoveOnImport", HelpText = "IndexerSettingsNeverMoveOnImportHelpText")]
        public bool NeverMoveOnImport { get; set; }

        // BookshelfNG exposed these MAM browse filters. Keep their serialized values and apply
        // them to Chaptarr's richer JSON search so existing indexer settings remain effective.
        [FieldDefinition(15, Type = FieldType.Select, Label = "Search Type", SelectOptions = typeof(MyAnonaMouseSearchType), HelpText = "Filter MAM search results by account and freeleech status. Empty uses Chaptarr's seeder-aware default.")]
        public int? SearchType { get; set; }

        [FieldDefinition(16, Type = FieldType.Checkbox, Label = "Search in description")]
        public bool SearchInDescription { get; set; }

        [FieldDefinition(17, Type = FieldType.Checkbox, Label = "Search in series")]
        public bool SearchInSeries { get; set; }

        [FieldDefinition(18, Type = FieldType.Checkbox, Label = "Search in filenames")]
        public bool SearchInFilenames { get; set; }

        [FieldDefinition(19, Type = FieldType.Select, Label = "Search Languages", SelectOptions = typeof(MyAnonaMouseSearchLanguages), HelpText = "Optional MAM language filter. When empty, the author's metadata profile languages are used.")]
        public IEnumerable<int> SearchLanguages { get; set; }

        // Persisted provider snapshot used by the grab guard. These are not user-editable.
        public int? UnsatisfiedCount { get; set; }
        public int? UnsatisfiedLimit { get; set; }
        public DateTime? UnsatisfiedSnapshotUtc { get; set; }
        public DateTime? UnsatisfiedStatusRefreshedUtc { get; set; }

        // Required by ITorrentIndexerSettings but hidden from UI
        public SeedCriteriaSettings SeedCriteria
        {
            get
            {
                var criteria = new SeedCriteriaSettings();
                if (SeedTimeHours.HasValue && SeedTimeHours.Value > 0)
                {
                    criteria.SeedTime = SeedTimeHours.Value * 60;
                }

                criteria.NeverMoveOnImport = NeverMoveOnImport;

                return criteria;
            }

            set
            {
            }
        }

        // Required by ITorrentIndexerSettings but hidden from UI
        public bool RejectBlocklistedTorrentHashesWhileGrabbing { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }

    public enum MyAnonaMouseSearchType
    {
        [FieldOption(Label = "All torrents", Hint = "Search everything")]
        All = 0,

        [FieldOption(Label = "Only active", Hint = "Last update had 1+ seeders")]
        Active = 1,

        [FieldOption(Label = "Freeleech", Hint = "Freeleech torrents")]
        Freeleech = 2,

        [FieldOption(Label = "Freeleech or VIP", Hint = "Freeleech or VIP torrents")]
        FreeleechOrVip = 3,

        [FieldOption(Label = "VIP", Hint = "VIP torrents")]
        Vip = 4,

        [FieldOption(Label = "Not VIP", Hint = "Torrents not VIP")]
        NotVip = 5
    }

    public enum MyAnonaMouseSearchLanguages
    {
        [FieldOption(Label = "English")] English = 1,
        [FieldOption(Label = "Afrikaans")] Afrikaans = 17,
        [FieldOption(Label = "Arabic")] Arabic = 32,
        [FieldOption(Label = "Bengali")] Bengali = 35,
        [FieldOption(Label = "Bosnian")] Bosnian = 51,
        [FieldOption(Label = "Bulgarian")] Bulgarian = 18,
        [FieldOption(Label = "Burmese")] Burmese = 6,
        [FieldOption(Label = "Cantonese")] Cantonese = 44,
        [FieldOption(Label = "Catalan")] Catalan = 19,
        [FieldOption(Label = "Chinese")] Chinese = 2,
        [FieldOption(Label = "Croatian")] Croatian = 49,
        [FieldOption(Label = "Czech")] Czech = 20,
        [FieldOption(Label = "Danish")] Danish = 21,
        [FieldOption(Label = "Dutch")] Dutch = 22,
        [FieldOption(Label = "Estonian")] Estonian = 61,
        [FieldOption(Label = "Farsi")] Farsi = 39,
        [FieldOption(Label = "Finnish")] Finnish = 23,
        [FieldOption(Label = "French")] French = 36,
        [FieldOption(Label = "German")] German = 37,
        [FieldOption(Label = "Greek")] Greek = 26,
        [FieldOption(Label = "Greek, Ancient")] GreekAncient = 59,
        [FieldOption(Label = "Gujarati")] Gujarati = 3,
        [FieldOption(Label = "Hebrew")] Hebrew = 27,
        [FieldOption(Label = "Hindi")] Hindi = 8,
        [FieldOption(Label = "Hungarian")] Hungarian = 28,
        [FieldOption(Label = "Icelandic")] Icelandic = 63,
        [FieldOption(Label = "Indonesian")] Indonesian = 53,
        [FieldOption(Label = "Irish")] Irish = 56,
        [FieldOption(Label = "Italian")] Italian = 43,
        [FieldOption(Label = "Japanese")] Japanese = 38,
        [FieldOption(Label = "Javanese")] Javanese = 12,
        [FieldOption(Label = "Kannada")] Kannada = 5,
        [FieldOption(Label = "Korean")] Korean = 41,
        [FieldOption(Label = "Lithuanian")] Lithuanian = 50,
        [FieldOption(Label = "Latin")] Latin = 46,
        [FieldOption(Label = "Latvian")] Latvian = 62,
        [FieldOption(Label = "Malay")] Malay = 33,
        [FieldOption(Label = "Malayalam")] Malayalam = 58,
        [FieldOption(Label = "Manx")] Manx = 57,
        [FieldOption(Label = "Marathi")] Marathi = 9,
        [FieldOption(Label = "Norwegian")] Norwegian = 48,
        [FieldOption(Label = "Polish")] Polish = 45,
        [FieldOption(Label = "Portuguese")] Portuguese = 34,
        [FieldOption(Label = "Brazilian Portuguese (BP)")] BrazilianPortuguese = 52,
        [FieldOption(Label = "Punjabi")] Punjabi = 14,
        [FieldOption(Label = "Romanian")] Romanian = 30,
        [FieldOption(Label = "Russian")] Russian = 16,
        [FieldOption(Label = "Scottish Gaelic")] ScottishGaelic = 24,
        [FieldOption(Label = "Sanskrit")] Sanskrit = 60,
        [FieldOption(Label = "Serbian")] Serbian = 31,
        [FieldOption(Label = "Slovenian")] Slovenian = 54,
        [FieldOption(Label = "Spanish")] Spanish = 4,
        [FieldOption(Label = "Castilian Spanish")] CastilianSpanish = 55,
        [FieldOption(Label = "Swedish")] Swedish = 40,
        [FieldOption(Label = "Tagalog")] Tagalog = 29,
        [FieldOption(Label = "Tamil")] Tamil = 11,
        [FieldOption(Label = "Telugu")] Telugu = 10,
        [FieldOption(Label = "Thai")] Thai = 7,
        [FieldOption(Label = "Turkish")] Turkish = 42,
        [FieldOption(Label = "Ukrainian")] Ukrainian = 25,
        [FieldOption(Label = "Urdu")] Urdu = 15,
        [FieldOption(Label = "Vietnamese")] Vietnamese = 13,
        [FieldOption(Label = "Other")] Other = 47
    }

    public enum MyAnonaMouseFreeleechWedgeAction
    {
        [FieldOption(Label = "Follow MAM Preferences", Hint = "Do not force a wedge; let your MAM account preferences decide")]
        Never = 0,

        [FieldOption(Label = "Prefer Wedge", Hint = "Request a personal freeleech wedge when eligible, then fall back to a normal download if unavailable")]
        Preferred = 1,

        [Obsolete("MAM's zero-wedge response is not documented well enough to guarantee required-wedge behavior.")]
        Required = 2,
    }
}
