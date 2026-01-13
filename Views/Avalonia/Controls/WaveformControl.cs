using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using SLSKDONET.Models;

namespace SLSKDONET.Views.Avalonia.Controls
{
    public class WaveformControl : Control
    {
        public static readonly StyledProperty<WaveformAnalysisData> WaveformDataProperty =
            AvaloniaProperty.Register<WaveformControl, WaveformAnalysisData>(nameof(WaveformData));

        public WaveformAnalysisData WaveformData
        {
            get => GetValue(WaveformDataProperty);
            set => SetValue(WaveformDataProperty, value);
        }

        public static readonly StyledProperty<float> ProgressProperty =
            AvaloniaProperty.Register<WaveformControl, float>(nameof(Progress), 0f);

        public float Progress
        {
            get => GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public static readonly StyledProperty<System.Windows.Input.ICommand?> SeekCommandProperty =
            AvaloniaProperty.Register<WaveformControl, System.Windows.Input.ICommand?>(nameof(SeekCommand));

        public System.Windows.Input.ICommand? SeekCommand
        {
            get => GetValue(SeekCommandProperty);
            set => SetValue(SeekCommandProperty, value);
        }

        public static readonly StyledProperty<byte[]?> LowBandProperty =
            AvaloniaProperty.Register<WaveformControl, byte[]?>(nameof(LowBand));

        public byte[]? LowBand
        {
            get => GetValue(LowBandProperty);
            set => SetValue(LowBandProperty, value);
        }

        public static readonly StyledProperty<byte[]?> MidBandProperty =
            AvaloniaProperty.Register<WaveformControl, byte[]?>(nameof(MidBand));

        public byte[]? MidBand
        {
            get => GetValue(MidBandProperty);
            set => SetValue(MidBandProperty, value);
        }

        public static readonly StyledProperty<byte[]?> HighBandProperty =
            AvaloniaProperty.Register<WaveformControl, byte[]?>(nameof(HighBand));

        public byte[]? HighBand
        {
            get => GetValue(HighBandProperty);
            set => SetValue(HighBandProperty, value);
        }

        public static readonly StyledProperty<IBrush?> ForegroundProperty =
            AvaloniaProperty.Register<WaveformControl, IBrush?>(nameof(Foreground));

        public IBrush? Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly StyledProperty<IBrush?> BackgroundProperty =
            AvaloniaProperty.Register<WaveformControl, IBrush?>(nameof(Background));

        public IBrush? Background
        {
            get => GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        public static readonly StyledProperty<System.Collections.Generic.IEnumerable<OrbitCue>?> CuesProperty =
            AvaloniaProperty.Register<WaveformControl, System.Collections.Generic.IEnumerable<OrbitCue>?>(nameof(Cues));

        public System.Collections.Generic.IEnumerable<OrbitCue>? Cues
        {
            get => GetValue(CuesProperty);
            set => SetValue(CuesProperty, value);
        }

        static WaveformControl()
        {
            AffectsRender<WaveformControl>(WaveformDataProperty, ProgressProperty, LowBandProperty, MidBandProperty, HighBandProperty, CuesProperty, ForegroundProperty, BackgroundProperty);
        }
        
        public static readonly StyledProperty<System.Windows.Input.ICommand?> CueUpdatedCommandProperty =
            AvaloniaProperty.Register<WaveformControl, System.Windows.Input.ICommand?>(nameof(CueUpdatedCommand));

        public System.Windows.Input.ICommand? CueUpdatedCommand
        {
            get => GetValue(CueUpdatedCommandProperty);
            set => SetValue(CueUpdatedCommandProperty, value);
        }

        private OrbitCue? _draggedCue;
        private bool _isDraggingCue;
        private const double CueHitThreshold = 10.0;

        protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
        {
            var point = e.GetPosition(this);
            var data = WaveformData;
            var cues = Cues;

            // 1. Hit Test for Cues
            if (cues != null && data != null && data.DurationSeconds > 0)
            {
                foreach (var cue in cues)
                {
                    double x = (cue.Timestamp / data.DurationSeconds) * Bounds.Width;
                    if (Math.Abs(point.X - x) <= CueHitThreshold)
                    {
                        _draggedCue = cue;
                        _isDraggingCue = true;
                        e.Pointer.Capture(this);
                        e.Handled = true; // Prevent Seek
                        return;
                    }
                }
            }

            // 2. Base Seek Logic (if no cue hit)
            base.OnPointerPressed(e);
            
            var progress = (float)(point.X / Bounds.Width);
            progress = Math.Clamp(progress, 0f, 1f);
            
            if (SeekCommand != null && SeekCommand.CanExecute(progress))
            {
                SeekCommand.Execute(progress);
            }
        }

        protected override void OnPointerMoved(global::Avalonia.Input.PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_isDraggingCue && _draggedCue != null)
            {
                var point = e.GetPosition(this);
                var data = WaveformData;
                
                if (data != null && data.DurationSeconds > 0)
                {
                    // Calculate new timestamp
                    // Clamp X to bounds
                    double x = Math.Clamp(point.X, 0, Bounds.Width);
                    double newTimestamp = (x / Bounds.Width) * data.DurationSeconds;
                    
                    _draggedCue.Timestamp = newTimestamp;
                    InvalidateVisual(); // Redraw immediately
                }
            }
        }

        protected override void OnPointerReleased(global::Avalonia.Input.PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (_isDraggingCue && _draggedCue != null)
            {
                _isDraggingCue = false;
                e.Pointer.Capture(null);
                
                // Notify ViewModel of update
                if (CueUpdatedCommand != null && CueUpdatedCommand.CanExecute(_draggedCue))
                {
                    CueUpdatedCommand.Execute(_draggedCue);
                }
                
                _draggedCue = null;
            }
        }

        public override void Render(DrawingContext context)
        {
            var data = WaveformData;
            if (data == null || data.IsEmpty)
            {
                context.DrawLine(new Pen(Brushes.Gray, 1), new Point(0, Bounds.Height / 2), new Point(Bounds.Width, Bounds.Height / 2));
                return;
            }

            var width = Bounds.Width;
            var height = Bounds.Height;
            var mid = height / 2;

            var unplayedRmsPen = new Pen(new SolidColorBrush(Color.Parse("#4000BFFF")), 1); // Dim Blue
            var unplayedPeakPen = new Pen(new SolidColorBrush(Color.Parse("#80FFFFFF")), 1); // Dim White
            
            var playedRmsPen = new Pen(new SolidColorBrush(Color.Parse("#00BFFF")), 1); // Bright Blue
            var playedPeakPen = new Pen(Brushes.White, 1);

            if (data.PeakData == null || data.RmsData == null) return;

            var lowData = LowBand ?? data.LowData;
            var midData = MidBand ?? data.MidData;
            var highData = HighBand ?? data.HighData;

            bool hasRgb = lowData != null && lowData.Length > 0 &&
                          midData != null && midData.Length > 0 &&
                          highData != null && highData.Length > 0;

            int samples = Math.Min(data.PeakData.Length, data.RmsData.Length);
            double step = width / samples;

            for (int i = 0; i < samples; i++)
            {
                double x = i * step;
                if (x > width) break;

                bool isPlayed = (float)i / samples <= Progress;
                
                if (hasRgb && i < lowData!.Length && i < midData!.Length && i < highData!.Length)
                {
                    // --- Layered RGB Rendering ---
                    float low = lowData![i] / 255f;
                    float midB = midData![i] / 255f;
                    float high = highData![i] / 255f;
                    float peak = data.PeakData[i] / 255f;

                    // Energy-based Opacity (Breathing Effect)
                    // We use the peak value to modulate the brightness/opacity
                    float energyMod = 0.5f + (peak * 0.5f); 
                    float baseOpacity = isPlayed ? 1.0f : 0.35f;
                    float finalOpacity = baseOpacity * energyMod;

                    double lowH = low * mid;
                    double midH = midB * mid;
                    double highH = high * mid;
                    double peakH = peak * mid;

                    // 1. Red Layer (Bass/Foundation)
                    if (lowH > 0.5)
                    {
                        var color = isPlayed ? Color.FromRgb(255, 30, 30) : Color.FromRgb(150, 40, 40);
                        var bassPen = new Pen(new SolidColorBrush(color, finalOpacity * 0.85f), 1);
                        context.DrawLine(bassPen, new Point(x, mid - lowH), new Point(x, mid + lowH));
                    }

                    // 2. Green Layer (Mids/Vocals)
                    if (midH > 0.5)
                    {
                        var color = isPlayed ? Color.FromRgb(30, 255, 30) : Color.FromRgb(40, 150, 40);
                        var midPen = new Pen(new SolidColorBrush(color, finalOpacity * 0.75f), 1);
                        context.DrawLine(midPen, new Point(x, mid - midH), new Point(x, mid + midH));
                    }

                    // 3. Blue Layer (Highs/Percussion)
                    if (highH > 0.5)
                    {
                        var color = isPlayed ? Color.FromRgb(0, 220, 255) : Color.FromRgb(30, 120, 150);
                        var highPen = new Pen(new SolidColorBrush(color, finalOpacity), 1);
                        context.DrawLine(highPen, new Point(x, mid - highH), new Point(x, mid + highH));
                        
                        // "Blue Sparkle" - slight glow for very high energy peaks
                        if (isPlayed && high > 0.7)
                        {
                            var sparklePen = new Pen(new SolidColorBrush(Colors.White, finalOpacity * 0.5f), 1);
                            context.DrawLine(sparklePen, new Point(x, mid - highH - 1), new Point(x, mid - highH + 1));
                            context.DrawLine(sparklePen, new Point(x, mid + highH - 1), new Point(x, mid + highH + 1));
                        }
                    }

                    // 4. White Silhouette (Peak transients)
                    if (isPlayed && peak > 0.1)
                    {
                         var peakPen = new Pen(new SolidColorBrush(Colors.White, finalOpacity * 0.2f), 1);
                         context.DrawLine(peakPen, new Point(x, mid - peakH), new Point(x, mid + peakH));
                    }
                    
                    // 5. Unplayed Outline (Thin desaturated structure)
                    if (!isPlayed)
                    {
                        var outlinePen = new Pen(new SolidColorBrush(Colors.White, 0.1f), 0.5);
                        context.DrawLine(outlinePen, new Point(x, mid - peakH), new Point(x, mid + peakH));
                    }
                }
                else
                {
                    // Classic Blue/White Rendering Fallback
                    float peakVal = data.PeakData[i] / 255f;
                    float rmsVal = data.RmsData[i] / 255f;

                    double peakH = peakVal * mid;
                    double rmsH = rmsVal * mid;

                    var currentRmsPen = isPlayed ? playedRmsPen : unplayedRmsPen;
                    var currentPeakPen = isPlayed ? playedPeakPen : unplayedPeakPen;

                    context.DrawLine(currentRmsPen, new Point(x, mid - rmsH), new Point(x, mid + rmsH));

                    if (peakH > rmsH)
                    {
                        context.DrawLine(currentPeakPen, new Point(x, mid - peakH), new Point(x, mid - rmsH));
                        context.DrawLine(currentPeakPen, new Point(x, mid + rmsH), new Point(x, mid + peakH));
                    }
                }
            }
            
            RenderCues(context, width, height);
        }

        private void RenderCues(DrawingContext context, double width, double height)
        {
            var cues = Cues;
            var data = WaveformData;
            if (cues == null || data == null || data.DurationSeconds <= 0) return;

            foreach (var cue in cues)
            {
                double x = (cue.Timestamp / data.DurationSeconds) * width;
                if (x < 0 || x > width) continue;

                var color = Color.Parse(cue.Color ?? "#FFFFFF");
                var pen = new Pen(new SolidColorBrush(color, 0.8), 2);
                
                // Draw vertical marker line
                context.DrawLine(pen, new Point(x, 0), new Point(x, height));

                // Draw label at the top
                var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
                var formattedText = new FormattedText(
                    cue.Name ?? cue.Role.ToString(),
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    10,
                    new SolidColorBrush(color));
                
                // Background for text to make it readable
                var textRect = new Rect(x + 4, 2, formattedText.Width + 4, formattedText.Height);
                context.DrawRectangle(new SolidColorBrush(Colors.Black, 0.6), null, textRect);
                context.DrawText(formattedText, new Point(x + 6, 2));
            }
        }
    }
}
