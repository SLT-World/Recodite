using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Recodite
{
    public class Attachment : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        private void RaisePropertyChanged([CallerMemberName] string Name = null) =>
            PropertyChanged(this, new PropertyChangedEventArgs(Name));
        public string FileName { get; set; } = "";

        private string _StateText = "";
        public string StateText
        {
            get { return _StateText; }
            set
            {
                _StateText = value;
                if (value == "Pending")
                {
                    StateIcon = "\ue916";
                    StateColor = Colors.Orange;
                }
                else if (value.StartsWith("Compressed"))
                {
                    StateIcon = "\ue74b";
                    StateColor = Colors.SpringGreen;
                }
                else if (value.StartsWith("Processing"))
                {
                    StateIcon = "\ue9f5";
                    StateColor = Colors.MediumPurple;
                }
                else if (value == "Failed")
                {
                    StateIcon = "\ue7ba";
                    StateColor = Colors.Red;
                }
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(StateIcon));
                RaisePropertyChanged(nameof(StateColor));
            }
        }

        public string StateIcon { get; set; }
        public Color StateColor { get; set; }
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
        public MainPage()
        {
            InitializeComponent();
            MediaEntries.Add(new() { FileName = "HelloWorld.mp4", StateText = "Pending" });
            MediaEntries.Add(new() { FileName = "HelloWorld.mp4", StateText = "Compressed: 95%" });
            MediaEntries.Add(new() { FileName = "HelloWorld.mp4", StateText = "Processing: 45%" });
            MediaEntries.Add(new() { FileName = "HelloWorld.mp4", StateText = "Failed" });
            MediaEntries.Add(new() { FileName = "HelloWorld.mp4" });
        }

        private void CompressButton_Clicked(object sender, EventArgs e)
        {

        }
    }
}
