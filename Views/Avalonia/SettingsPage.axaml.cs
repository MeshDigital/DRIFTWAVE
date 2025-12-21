using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;
using SLSKDONET.ViewModels;

namespace SLSKDONET.Views.Avalonia
{
    public partial class SettingsPage : UserControl
    {
        public SettingsPage()
        {
            InitializeComponent();
            
            // Phase 8: Wire up FFmpeg download button
            var downloadButton = this.FindControl<Button>("DownloadFfmpegButton");
            if (downloadButton != null)
            {
                downloadButton.Click += OnDownloadFfmpegClick;
            }
            
            // Wire up Reset Auth State button directly
            var resetAuthButton = this.FindControl<Button>("ResetAuthStateButton");
            if (resetAuthButton != null)
            {
                resetAuthButton.Click += OnResetAuthStateClick;
            }
        }

        private void OnDownloadFfmpegClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Open browser to official FFmpeg download page
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://ffmpeg.org/download.html",
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                // Log error (logger not available in code-behind, but graceful fallback)
                System.Diagnostics.Debug.WriteLine($"Failed to open FFmpeg download page: {ex.Message}");
            }
        }
        
        private void OnResetAuthStateClick(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("[RESET BUTTON] Click handler triggered");
            
            // DataContext is MainViewModel, SettingsViewModel is a nested property
            if (DataContext is SLSKDONET.Views.MainViewModel mainViewModel)
            {
                var settingsVM = mainViewModel.SettingsViewModel;
                Console.WriteLine("[RESET BUTTON] Found MainViewModel, accessing SettingsViewModel");
                
                // Dispatch on UI thread to ensure property changes update the UI
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    settingsVM.ResetAuthState();
                    Console.WriteLine("[RESET BUTTON] ResetAuthState() completed");
                });
            }
            else
            {
                Console.WriteLine($"[RESET BUTTON] DataContext is: {DataContext?.GetType().Name ?? "null"}");
            }
        }
    }
}
