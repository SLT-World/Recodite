using CommunityToolkit.Maui.Storage;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;

#if WINDOWS
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
#elif IOS || MACCATALYST
using UIKit;
using Foundation;
#endif

namespace Recodite
{
    public enum AttachmentState
    {
        Complete,
        Process,
        Pending,
        Fail,
        Cancel
    }
    public enum CompressionSpeed
    {
        Fastest = 0,
        Fast = 1,
        Medium = 2,
        Slow = 3,
        Slowest = 4
    }

    public class CompressionPreset : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        private void RaisePropertyChanged([CallerMemberName] string Name = null) =>
            PropertyChanged(this, new PropertyChangedEventArgs(Name));
        public string Icon { get; set; } = "";
        private string _Name = "";
        public string Name
        {
            get { return _Name; }
            set
            {
                if (value == _Name || value.Length == 0)
                    return;
                _Name = value;
                Icon = value[0].ToString();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(Icon));
            }
        }
        private int _TargetSizeMB = 8;
        public int TargetSizeMB
        {
            get { return _TargetSizeMB; }
            set
            {
                if (value == _TargetSizeMB)
                    return;
                _TargetSizeMB = value;
                RaisePropertyChanged();
            }
        }
        private CompressionSpeed _Speed = CompressionSpeed.Fastest;
        public CompressionSpeed Speed
        {
            get { return _Speed; }
            set
            {
                if (value == _Speed)
                    return;
                _Speed = value;
                RaisePropertyChanged();
            }
        }
        private bool _Mute = false;
        public bool Mute
        {
            get { return _Mute; }
            set
            {
                if (value == _Mute)
                    return;
                _Mute = value;
                RaisePropertyChanged();
            }
        }
        private bool _Default = false;
        public bool Default
        {
            get { return _Default; }
            set
            {
                if (value == _Default)
                    return;
                _Default = value;
                RaisePropertyChanged();
            }
        }
    }

    public class Attachment : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        private void RaisePropertyChanged([CallerMemberName] string Name = null) =>
            PropertyChanged(this, new PropertyChangedEventArgs(Name));
        public string FileName { get; set; } = "";
        private string _SubText = "";
        public string SubText
        {
            get { return _SubText; }
            set
            {
                if (value == _SubText)
                    return;
                _SubText = value;
                RaisePropertyChanged();
            }
        }
        public string MediaPathOriginal = "";
        private string _OriginalSize = "";
        public string OriginalSize
        {
            get { return _OriginalSize; }
            set
            {
                if (value == _OriginalSize)
                    return;
                _OriginalSize = value;
                RaisePropertyChanged();
            }
        }
        private string _CompressedSize = "-";
        public string CompressedSize
        {
            get { return _CompressedSize; }
            set
            {
                if (value == _CompressedSize)
                    return;
                _CompressedSize = value;
                RaisePropertyChanged();
            }
        }
        private string _MediaPath = "";
        public string MediaPath
        {
            get { return _MediaPath; }
            set
            {
                if (value == _MediaPath)
                    return;
                _MediaPath = value;
                RaisePropertyChanged();
            }
        }
        private ImageSource? _Thumbnail;
        public ImageSource? Thumbnail
        {
            get => _Thumbnail;
            set
            {
                if (_Thumbnail == value) return;
                _Thumbnail = value;
                RaisePropertyChanged();
            }
        }

        private CompressionPreset _Preset;
        public CompressionPreset Preset
        {
            get => _Preset;
            set
            {
                if (_Preset == value) return;
                _Preset = value;
                RaisePropertyChanged();
            }
        }

        public string StateText { get; set; } = "";
        public string StateIcon { get; set; } = "";
        public Color StateColor { get; set; }
        public float Progress { get; set; } = 0;
        public TimeSpan Duration { get; set; }
        public bool CanSave { get; set; } = false;
        public bool CanCancel { get; set; } = false;
        public bool CanModify { get; set; } = true;
        public CancellationTokenSource? CancellationTokenSource { get; set; }

        public void SetState(AttachmentState State, float _Progress = 0)
        {
            Progress = _Progress;
            switch (State)
            {
                case AttachmentState.Complete:
                    if (_Progress <= 0)
                    {
                        StateText = $"Expanded";
                        StateIcon = "\ue74a";
                        StateColor = Colors.Red;
                        Progress = 0;
                    }
                    else
                    {
                        StateText = $"Compressed: {_Progress * 100:0}%";
                        StateIcon = "\ue74b";
                        StateColor = Colors.SpringGreen;
                    }
                    CanSave = File.Exists(MediaPath);
                    CanCancel = false;
                    CanModify = false;
                    RaisePropertyChanged(nameof(CanSave));
                    RaisePropertyChanged(nameof(CanCancel));
                    RaisePropertyChanged(nameof(CanModify));
                    break;
                case AttachmentState.Process:
                    StateText = $"Processing: {_Progress * 100:0}%";
                    StateIcon = "\ue9f5";
                    StateColor = Colors.MediumPurple;
                    CanCancel = true;
                    CanModify = false;
                    RaisePropertyChanged(nameof(CanCancel));
                    RaisePropertyChanged(nameof(CanModify));
                    break;
                case AttachmentState.Pending:
                    StateText = "Pending";
                    StateIcon = "\ue916";
                    StateColor = Colors.Orange;
                    CanCancel = true;
                    CanModify = true;
                    RaisePropertyChanged(nameof(CanCancel));
                    RaisePropertyChanged(nameof(CanModify));
                    break;
                case AttachmentState.Fail:
                    StateText = "Failed";
                    StateIcon = "\ue7ba";
                    StateColor = Colors.Red;
                    CanSave = false;
                    CanCancel = false;
                    CanModify = false;
                    RaisePropertyChanged(nameof(CanSave));
                    RaisePropertyChanged(nameof(CanCancel));
                    RaisePropertyChanged(nameof(CanModify));
                    break;
                case AttachmentState.Cancel:
                    StateText = "Cancelled";
                    StateIcon = "\ue711";
                    StateColor = Colors.Gold;
                    CanSave = false;
                    CanCancel = false;
                    CanModify = false;
                    RaisePropertyChanged(nameof(CanSave));
                    RaisePropertyChanged(nameof(CanCancel));
                    RaisePropertyChanged(nameof(CanModify));
                    break;
            }
            RaisePropertyChanged(nameof(Progress));
            RaisePropertyChanged(nameof(StateText));
            RaisePropertyChanged(nameof(StateIcon));
            RaisePropertyChanged(nameof(StateColor));
        }
    }

    public partial class MainPage : ContentPage, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        private void RaisePropertyChanged([CallerMemberName] string Name = null) =>
            PropertyChanged(this, new PropertyChangedEventArgs(Name));
        #endregion
        private string PresetsPath => Path.Combine(FileSystem.AppDataDirectory, "presets.json");
        public bool CanCompress => MediaEntries.Any() && MediaEntries.Any(a => string.IsNullOrEmpty(a.StateText));
        public bool CanRemovePresets => Presets.Any() && Presets.Count > 1;

        private ObservableCollection<Attachment> _MediaEntries = new();
        public ObservableCollection<Attachment> MediaEntries
        {
            get { return _MediaEntries; }
            set
            {
                if (value == _MediaEntries)
                    return;
                _MediaEntries = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(CanCompress));
            }
        }

        private ObservableCollection<CompressionPreset> _Presets = new();
        public ObservableCollection<CompressionPreset> Presets
        {
            get { return _Presets; }
            set
            {
                if (value == _Presets)
                    return;
                _Presets = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(CanRemovePresets));
            }
        }

        private Attachment? _CurrentEntry = null;
        public Attachment? CurrentEntry
        {
            get { return _CurrentEntry; }
            set
            {
                if (value == _CurrentEntry)
                    return;
                _CurrentEntry = value;
                RaisePropertyChanged();
            }
        }

        private CompressionPreset? _CurrentPresetOptions = null;
        public CompressionPreset? CurrentPresetOptions
        {
            get { return _CurrentPresetOptions; }
            set
            {
                if (value == _CurrentPresetOptions)
                    return;
                _CurrentPresetOptions = value;
                RaisePropertyChanged();
            }
        }

        public ICommand DeleteCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        string FFmpegPath = "";

        public MainPage()
        {
            InitializeComponent();
            DeleteCommand = new Command<Attachment>(DeleteAttachment);
            SaveCommand = new Command<Attachment>(SaveAttachment);
            CancelCommand = new Command<Attachment>(CancelAttachment);
            if (File.Exists(PresetsPath))
            {
                List<CompressionPreset> Loaded = JsonSerializer.Deserialize<List<CompressionPreset>>(File.ReadAllText(PresetsPath)) ?? [
                    GeneratePreset("Small", 8, CompressionSpeed.Fastest, true)
                ];
                foreach (var Preset in Loaded)
                {
                    Preset.PropertyChanged += (_, e) => SavePresets();
                    Presets.Add(Preset);
                }
            }
            else
            {
                Presets = [
                    GeneratePreset("Small", 8, CompressionSpeed.Fastest, true),
                    GeneratePreset("Balanced", 15, CompressionSpeed.Fast)
                ];
            }
            Presets.CollectionChanged += (_, e) => SavePresets();

            DefaultPreset = Presets.FirstOrDefault(i => i.Default) ?? Presets[0];
            DefaultPreset.Default = true;
            PresetsMenu.SelectedItem = DefaultPreset;
            RaisePropertyChanged(nameof(CanRemovePresets));
        }

        public CompressionPreset GeneratePreset(string Name, int TargetSizeMB, CompressionSpeed Speed, bool Default = false)
        {
            CompressionPreset Preset = new() { Name = Name, TargetSizeMB = TargetSizeMB, Speed = Speed, Default = Default };
            Preset.PropertyChanged += (_, e) => SavePresets();
            return Preset;
        }

        private void SavePresets()
        {
            File.WriteAllText(PresetsPath, JsonSerializer.Serialize(Presets, new JsonSerializerOptions { WriteIndented = false }));
        }

        string CompressioSpeedToFFmpegPreset(CompressionSpeed Speed)
        {
            switch (Speed)
            {
                case CompressionSpeed.Fastest: return "veryfast";
                case CompressionSpeed.Fast: return "fast";
                case CompressionSpeed.Medium: return "medium";
                case CompressionSpeed.Slow: return "slow";
                case CompressionSpeed.Slowest: return "veryslow";
            }
            return "veryfast";
        }

        private async void AppPage_Loaded(object sender, EventArgs e)
        {
            await ExtractFFmpeg();
        }

        private async void DeleteAttachment(Attachment _Attachment)
        {
            if (_Attachment == null)
                return;
            if (_Attachment.StateText.StartsWith("Compressed") && !await DisplayAlertAsync("Remove file?", _Attachment.FileName, "Remove", "Cancel"))
                return;
            MediaEntries.Remove(_Attachment);
            RaisePropertyChanged(nameof(CanCompress));
            ConvertMenuPlaceholder.IsVisible = !MediaEntries.Any();
            if (CurrentEntry == _Attachment)
                CurrentEntry = null;
        }

        private async void SaveAttachment(Attachment _Attachment)
        {
            if (_Attachment == null || !File.Exists(_Attachment.MediaPath))
                return;
            using var Stream = File.OpenRead(_Attachment.MediaPath);
            await FileSaver.Default.SaveAsync(Path.GetFileNameWithoutExtension(_Attachment.FileName) + "_compressed.mp4", Stream);
        }

        private async void CancelAttachment(Attachment _Attachment)
        {
            if (_Attachment == null)
                return;
            if (_Attachment.StateText == "Pending")
                _Attachment.SetState(AttachmentState.Cancel);
            _Attachment?.CancellationTokenSource?.Cancel();
        }

        private async void InsertAttachmentButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                IEnumerable<FileResult> Files = await FilePicker.Default.PickMultipleAsync(new PickOptions { FileTypes = FilePickerFileType.Videos });
                if (Files != null && Files.Any())
                {
                    foreach (FileResult Video in Files)
                        AddAttachment(new FileInfo(Video.FullPath));
                }
            }
            catch { }
        }

        private async void CompressButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                if (CurrentEntry == null)
                    ConvertMenu.SelectedItem = MediaEntries.FirstOrDefault();
                foreach (Attachment _Attachment in MediaEntries)
                {
                    if (string.IsNullOrEmpty(_Attachment.StateText))
                        _Attachment.SetState(AttachmentState.Pending);
                }

                foreach (Attachment _Attachment in MediaEntries)
                    await Compress(_Attachment);
            }
            catch { }
        }

        public async Task Compress(Attachment _Attachment)
        {
            if (!File.Exists(_Attachment.MediaPath))
            {
                _Attachment.SetState(AttachmentState.Fail);
                return;
            }
            if (_Attachment.StateText != "Pending")
                return;
            string Output = Path.Combine(FileSystem.CacheDirectory, Path.GetFileNameWithoutExtension(_Attachment.FileName) + "_compressed.mp4");
            _Attachment.SetState(AttachmentState.Process, 0f);

            _Attachment.CancellationTokenSource = new CancellationTokenSource();

            await Task.Run(() =>
            {
                string AudioArguments = _Attachment.Preset.Mute ? "-an" : "-c:a aac -b:a 128k";
                string Arguments = $"-i \"{_Attachment.MediaPath}\" -c:v libx264 -preset {CompressioSpeedToFFmpegPreset(_Attachment.Preset.Speed)} -vf scale=-2:720 -b:v {GetTargetVideoBitrate(_Attachment.Duration, _Attachment.Preset.TargetSizeMB)}";
                string NullDevice = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
                RunFFmpeg($"-y {Arguments} -pass 1 {AudioArguments} -f mp4 {NullDevice}", _Attachment);
                RunFFmpeg(
                    $"-y {Arguments} -pass 2 {AudioArguments} \"{Output}\"",
                    _Attachment,
                    StandardError =>
                    {
                        if (_Attachment.Duration.TotalSeconds <= 0)
                            return;
                        Match _Match = Regex.Match(StandardError, @"time=(\d+):(\d+):(\d+.\d+)");
                        if (!_Match.Success)
                            return;
                        TimeSpan Current = new(0, int.Parse(_Match.Groups[1].Value), int.Parse(_Match.Groups[2].Value), (int)double.Parse(_Match.Groups[3].Value));
                        float Progress = (float)(Current.TotalSeconds / _Attachment.Duration.TotalSeconds);
                        Progress = Math.Clamp(Progress, 0f, 1f);
                        long CurrentBytes = File.Exists(Output) ? new FileInfo(Output).Length : 0;
                        _Attachment.CompressedSize = $"{CurrentBytes / 1024f / 1024f:0.0}";
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            _Attachment.SetState(AttachmentState.Process, Progress);
                            _Attachment.SubText = $"{_Attachment.OriginalSize} MB 🡢 {_Attachment.CompressedSize} MB";
                        });
                    }
                );
            });

            if (_Attachment.StateText.StartsWith("Processing"))
            {
                _Attachment.MediaPath = Output;
                _Attachment.SetState(AttachmentState.Complete, GetCompressionRatio(_Attachment));
            }

            try
            {
                File.Delete("ffmpeg2pass-0.log");
                File.Delete("ffmpeg2pass-0.log.mbtree");
            }
            catch { }
        }

        void RunFFmpeg(string Arguments, Attachment _Attachment, Action<string>? OnStandardError = null)
        {
            using Process FFmpegProcess = new()
            {
                StartInfo = new()
                {
                    FileName = FFmpegPath,
                    Arguments = Arguments,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            if (OnStandardError != null)
            {
                FFmpegProcess.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                        OnStandardError(e.Data);
                };
            }

            FFmpegProcess.Start();
            FFmpegProcess.BeginErrorReadLine();

            CancellationToken Token = _Attachment.CancellationTokenSource.Token;

            try
            {
                while (!FFmpegProcess.HasExited)
                {
                    if (Token.IsCancellationRequested)
                    {
                        try { FFmpegProcess.Kill(entireProcessTree: true); } catch { }
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            _Attachment.SetState(AttachmentState.Cancel);
                        });
                        return;
                    }
                    Thread.Sleep(100);
                }
            }
            finally
            {
                if (!FFmpegProcess.HasExited) try { FFmpegProcess.Kill(); } catch { }
            }
        }

        long GetTargetVideoBitrate(TimeSpan Duration, int TargetSizeMB, int AudioBitrateKbps = 128)
        {
            long TargetBytes = TargetSizeMB * 1024L * 1024L;
            double TotalBitrate = TargetBytes * 8d / Duration.TotalSeconds;
            long VideoBitrate = (long)(TotalBitrate - AudioBitrateKbps * 1000);
            return Math.Max(VideoBitrate, 300_000);
        }

        CompressionPreset DefaultPreset;

        TimeSpan GetDuration(string input)
        {
            Process FFmpegProcess = new()
            {
                StartInfo = new()
                {
                    FileName = FFmpegPath,
                    Arguments = $"-i \"{input}\"",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            FFmpegProcess.Start();
            string StandardError = FFmpegProcess.StandardError.ReadToEnd();
            FFmpegProcess.WaitForExit();

            Match _Match = Regex.Match(StandardError, @"Duration:\s(\d+):(\d+):(\d+.\d+)");
            if (!_Match.Success) return TimeSpan.Zero;

            return new(0, int.Parse(_Match.Groups[1].Value), int.Parse(_Match.Groups[2].Value), (int)double.Parse(_Match.Groups[3].Value));
        }

        private async void AddAttachment(FileInfo File)
        {
            Attachment _Attachment = new()
            {
                FileName = File.Name,
                MediaPath = File.FullName,
                MediaPathOriginal = File.FullName,
                OriginalSize = $"{File.Length / 1024f / 1024f:0.0}",
                Preset = DefaultPreset
            };
            _Attachment.PropertyChanged += (_, e) =>
            {
                RaisePropertyChanged(nameof(CanCompress));
            };
            _Attachment.SubText = $"{_Attachment.OriginalSize} MB";
            _Attachment.Duration = GetDuration(_Attachment.MediaPath);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                MediaEntries.Add(_Attachment);
            });
            _Attachment.Thumbnail = await GetThumbnail(_Attachment.MediaPath);
            ConvertMenuPlaceholder.IsVisible = false;
        }

        async Task<ImageSource?> GetThumbnail(string VideoPath)
        {
            string ThumbnailPath = Path.Combine(FileSystem.CacheDirectory, Path.GetFileNameWithoutExtension(VideoPath) + "_thumbnail.jpg");
            if (File.Exists(ThumbnailPath))
                return ImageSource.FromFile(ThumbnailPath);
            await Task.Run(() =>
            {
                Process FFmpegProcess = new()
                {
                    StartInfo = new()
                    {
                        FileName = FFmpegPath,
                        Arguments = $"-y -i \"{VideoPath}\" -ss 00:00:01 -vframes 1 -vf scale=150:-1 \"{ThumbnailPath}\"",
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                FFmpegProcess.Start();
                FFmpegProcess.WaitForExit();
            });
            if (!File.Exists(ThumbnailPath))
                return null;
            return ImageSource.FromFile(ThumbnailPath);
        }

        private void ConvertMenu_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            CurrentEntry = e.CurrentSelection.FirstOrDefault() as Attachment;

        private float GetCompressionRatio(Attachment _Attachment) =>
            1f - ((float)new FileInfo(_Attachment.MediaPath).Length / new FileInfo(_Attachment.MediaPathOriginal).Length);

        async Task ExtractFFmpeg()
        {
            string FilePath = "";
            if (OperatingSystem.IsWindows())
                FilePath = Path.Combine("ffmpeg", "windows", "ffmpeg.exe");
            else if (OperatingSystem.IsMacOS())
                FilePath = Path.Combine("ffmpeg", "macos", "ffmpeg");
            //else if (OperatingSystem.IsLinux())
            //    FilePath = Path.Combine("ffmpeg", "linux", "ffmpeg");
            FFmpegPath = Path.Combine(FileSystem.AppDataDirectory, FilePath);
            if (File.Exists(FFmpegPath))
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(FFmpegPath)!);
            using var Stream = await FileSystem.OpenAppPackageFileAsync(FilePath);
            using var _File = File.Create(FFmpegPath);
            await Stream.CopyToAsync(_File);
            if (!OperatingSystem.IsWindows())
                Process.Start("chmod", $"+x \"{FFmpegPath}\"")?.WaitForExit();
        }

        static readonly string[] SupportedExtensions =
        [
            ".webm",
            ".mp4", ".mov", ".avi", ".wmw", ".m4v", ".mpg", ".mpeg", ".mp2", ".mkv", ".flv", ".gifv", ".qt"
        ];

        private void DropGestureRecognizer_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Microsoft.Maui.Controls.DataPackageOperation.Copy;
        }

        private async void DropGestureRecognizer_Drop(object sender, DropEventArgs e)
        {
            var FilePaths = new List<string>();

#if WINDOWS
            if (e.PlatformArgs is not null && e.PlatformArgs.DragEventArgs.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var Items = await e.PlatformArgs.DragEventArgs.DataView.GetStorageItemsAsync();
                if (Items.Any())
                {
                    foreach (var Item in Items)
                    {
                        if (Item is StorageFile file)
                            FilePaths.Add(Item.Path);
                    }
                }
            }
#elif IOS || MACCATALYST
            var Session = e.PlatformArgs?.DropSession;
            if (Session == null)
                return;
            foreach (UIDragItem Item in Session.Items)
            {
                var Result = await LoadItemAsync(Item.ItemProvider, Item.ItemProvider.RegisteredTypeIdentifiers.ToList());
                if (Result is not null)
                    FilePaths.Add(Result.FileUrl?.Path!);
            }
            static async Task<LoadInPlaceResult?> LoadItemAsync(NSItemProvider ItemProvider, List<string> TypeIdentifiers)
            {
                if (TypeIdentifiers is null || TypeIdentifiers.Count == 0)
                    return null;
                var TypeIdent = TypeIdentifiers.First();
                if (ItemProvider.HasItemConformingTo(TypeIdent))
                    return await ItemProvider.LoadInPlaceFileRepresentationAsync(TypeIdent);
                TypeIdentifiers.Remove(TypeIdent);
                return await LoadItemAsync(ItemProvider, TypeIdentifiers);
            }
#endif

            foreach (string _Path in FilePaths)
            {
                if (File.Exists(_Path) && SupportedExtensions.Contains(Path.GetExtension(_Path).ToLowerInvariant()))
                    AddAttachment(new FileInfo(_Path));
            }
        }

        private void OptionsButton_Clicked(object sender, EventArgs e) =>
            OptionsMenu.IsVisible = !OptionsMenu.IsVisible;

        bool AllowPresetSelection = true;

        private void PresetsMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!AllowPresetSelection)
                return;
            CurrentPresetOptions = e.CurrentSelection.FirstOrDefault() as CompressionPreset;
            if (CurrentPresetOptions == null)
                return;
            SpeedSlider.Value = (int)CurrentPresetOptions.Speed;
        }

        private void SpeedSlider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (CurrentPresetOptions == null)
                return;
            int Rounded = (int)Math.Round(e.NewValue);
            CurrentPresetOptions.Speed = (CompressionSpeed)Rounded;
        }

        private void SpeedSlider_DragCompleted(object sender, EventArgs e)
        {
            if (CurrentPresetOptions == null)
                return;
            SpeedSlider.Value = (int)CurrentPresetOptions.Speed;
        }

        private void SetDefaultButton_Clicked(object sender, EventArgs e)
        {
            if (CurrentPresetOptions == null)
                return;
            foreach (CompressionPreset _Preset in Presets)
                _Preset.Default = false;
            CurrentPresetOptions.Default = true;
            DefaultPreset = CurrentPresetOptions;
        }

        private void AddPresetButton_Clicked(object sender, EventArgs e)
        {
            CompressionPreset Preset = GeneratePreset("New Preset", 10, CompressionSpeed.Fastest);
            Presets.Add(Preset);
            PresetsMenu.SelectedItem = Preset;
            RaisePropertyChanged(nameof(CanRemovePresets));
        }

        private async void RemovePresetButton_Clicked(object sender, EventArgs e)
        {
            if (CurrentPresetOptions == null || Presets.Count == 1)
                return;
            if (!await DisplayAlertAsync("Remove preset?", CurrentPresetOptions.Name, "Remove", "Cancel"))
                return;

            CompressionPreset PresetToRemove = CurrentPresetOptions;
            bool PreviousEnabled = AllowPresetSelection;
            AllowPresetSelection = false;

            Presets.Remove(PresetToRemove);
            if (DefaultPreset == PresetToRemove)
            {
                DefaultPreset = Presets.FirstOrDefault(i => i.Default) ?? Presets[0];
                DefaultPreset.Default = true;
            }
            AllowPresetSelection = PreviousEnabled;
            PresetsMenu.SelectedItem = DefaultPreset;
            RaisePropertyChanged(nameof(CanRemovePresets));
        }
    }
}
