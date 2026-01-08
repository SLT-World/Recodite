using CommunityToolkit.Maui.Storage;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace Recodite
{
    public enum AttachmentState
    {
        Complete,
        Process,
        Pending,
        Fail
    }

    public class Attachment : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        private void RaisePropertyChanged([CallerMemberName] string Name = null) =>
            PropertyChanged(this, new PropertyChangedEventArgs(Name));
        public string FileName { get; set; }
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

        public string StateText { get; set; }
        public string StateIcon { get; set; }
        public Color StateColor { get; set; }
        public float Progress { get; set; }
        public TimeSpan Duration { get; set; }
        public bool CanSave { get; set; } = false;

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
                    RaisePropertyChanged(nameof(CanSave));
                    break;
                case AttachmentState.Process:
                    StateText = $"Processing: {_Progress * 100:0}%";
                    StateIcon = "\ue9f5";
                    StateColor = Colors.MediumPurple;
                    break;
                case AttachmentState.Pending:
                    StateText = "Pending";
                    StateIcon = "\ue916";
                    StateColor = Colors.Orange;
                    break;
                case AttachmentState.Fail:
                    StateText = "Failed";
                    StateIcon = "\ue7ba";
                    StateColor = Colors.Red;
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

        public ICommand DeleteCommand { get; }
        public ICommand SaveCommand { get; }

        string FFmpegPath = "";

        public MainPage()
        {
            InitializeComponent();
            DeleteCommand = new Command<Attachment>(DeleteAttachment);
            SaveCommand = new Command<Attachment>(SaveAttachment);
        }
        //TODO: Add settings

        private async void AppPage_Loaded(object sender, EventArgs e)
        {
            await ExtractFFmpeg();
        }

        private async void DeleteAttachment(Attachment _Attachment)
        {
            if (_Attachment == null)
                return;
            if (await DisplayAlertAsync("Remove file?", _Attachment.FileName, "Remove", "Cancel"))
            {
                MediaEntries.Remove(_Attachment);
                if (CurrentEntry == _Attachment)
                    CurrentEntry = null;
            }
        }

        private async void SaveAttachment(Attachment _Attachment)
        {
            if (_Attachment == null || !File.Exists(_Attachment.MediaPath))
                return;
            using var Stream = File.OpenRead(_Attachment.MediaPath);
            await FileSaver.Default.SaveAsync(Path.GetFileNameWithoutExtension(_Attachment.FileName) + "_compressed.mp4", Stream);
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

        CancellationTokenSource CompressToken;

        private async void CompressButton_Clicked(object sender, EventArgs e)
        {
            if (!CompressButton.IsEnabled)
                return;
            CompressButton.IsEnabled = false;
            CompressToken = new CancellationTokenSource();
            //TODO: Add individual cancel button
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
                {
                    if (CompressToken.IsCancellationRequested)
                        break;
                    if (!string.IsNullOrEmpty(_Attachment.StateText) && _Attachment.StateText != "Pending")
                        continue;
                    await Compress(_Attachment);
                }
            }
            finally
            {
                CompressButton.IsEnabled = true;
                CompressToken = null;
            }
        }

        public async Task Compress(Attachment _Attachment)
        {
            string Output = Path.Combine(FileSystem.CacheDirectory, Path.GetFileNameWithoutExtension(_Attachment.FileName) + "_compressed.mp4");
            _Attachment.SetState(AttachmentState.Process, 0f);

            await Task.Run(() =>
            {
                string Arguments = $"-i \"{_Attachment.MediaPath}\" -c:v libx264 -preset veryfast -vf scale=-2:720 -b:v {GetTargetVideoBitrate(_Attachment.Duration, TargetSizeMB: 9)}";
                string NullDevice = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
                RunFFmpeg($"-y {Arguments} -pass 1 -an -f mp4 {NullDevice}");
                RunFFmpeg(
                    $"-y {Arguments} -pass 2 -c:a aac -b:a 128k \"{Output}\"",
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

            _Attachment.MediaPath = Output;
            _Attachment.SetState(AttachmentState.Complete, GetCompressionRatio(_Attachment));

            try
            {
                File.Delete("ffmpeg2pass-0.log");
                File.Delete("ffmpeg2pass-0.log.mbtree");
            }
            catch { }
        }

        void RunFFmpeg(string Arguments, Action<string>? OnStandardError = null)
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
                }
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
            FFmpegProcess.WaitForExit();

            //if (FFmpegProcess.ExitCode != 0)
        }

        long GetTargetVideoBitrate(TimeSpan Duration, int TargetSizeMB, int AudioBitrateKbps = 128)
        {
            long TargetBytes = TargetSizeMB * 1024L * 1024L;
            double TotalBitrate = TargetBytes * 8d / Duration.TotalSeconds;
            long VideoBitrate = (long)(TotalBitrate - AudioBitrateKbps * 1000);
            return Math.Max(VideoBitrate, 300_000);
        }

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
            };
            _Attachment.SubText = $"{_Attachment.OriginalSize} MB";
            _Attachment.Duration = GetDuration(_Attachment.MediaPath);
            MediaEntries.Add(_Attachment);
            _Attachment.Thumbnail = await GetThumbnail(_Attachment.MediaPath);
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
    }
}
