using Microsoft.Extensions.DependencyInjection;

namespace Recodite
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }

    static class IconFont
    {
        public const string Mic = "\ue720";
        public const string Calculator = "\ue8ef";
        public const string Camera = "\ue722";
        public const string Backspace = "\ue94f";
        public const string Equal = "\ue94e";
        public const string Multiply = "\ue947";
        public const string Addition = "\ue948";
        public const string Subtract = "\ue949";
        public const string Divide = "\ue94a";
        public const string Checkmark = "\ue73e";
        public const string Download = "\ue896";
        public const string Cloud = "\ue753";
        public const string NetworkOffline = "\uf384";
        public const string Progress = "\uf16a";
        public const string Close = "\ue8bb";
        public const string Menu = "\ue712";
        public const string Delete = "\ue74d";
        public const string Correct = "\ue8fb";
        public const string Search = "\ue721";
        public const string Up = "\ue70e";
        public const string Down = "\ue70d";
        public const string NoWifi = "\ueb5e";
        public const string Settings = "\ue713";
    }
}