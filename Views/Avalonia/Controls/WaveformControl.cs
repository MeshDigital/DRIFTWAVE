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

        static WaveformControl()
        {
            AffectsRender<WaveformControl>(WaveformDataProperty, ProgressProperty, LowBandProperty, MidBandProperty, HighBandProperty);
        }
        
        protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            
            var point = e.GetPosition(this);
            var progress = (float)(point.X / Bounds.Width);
            progress = Math.Clamp(progress, 0f, 1f);
            
            if (SeekCommand != null && SeekCommand.CanExecute(progress))
            {
                SeekCommand.Execute(progress);
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

                float peakVal = data.PeakData[i] / 255f;
                float rmsVal = data.RmsData[i] / 255f;

                double peakH = peakVal * mid;
                double rmsH = rmsVal * mid;

                if (hasRgb && i < lowData.Length && i < midData.Length && i < highData.Length)
                {
                    // RGB Rendering
                    float low = lowData[i] / 255f;
                    float midB = midData[i] / 255f;
                    float high = highData[i] / 255f;

                    // Mix Colors: Red (Bass), Green (Mid), Blue (High)
                    // We sum them and normalize to get the color mix
                    float total = low + midB + high;
                    Color waveColor;
                    
                    if (total > 0.05f)
                    {
                        // Normalize colors but keep brightness based on total energy
                        float r = low / total;
                        float g = midB / total;
                        float b = high / total;
                        
                        // Scale by intensity (RMS or total)
                        float intensity = Math.Min(1.0f, total * 1.5f);
                        
                        byte rByte = (byte)(r * 255 * intensity);
                        byte gByte = (byte)(g * 255 * intensity);
                        byte bByte = (byte)(b * 255 * intensity);
                        
                        waveColor = Color.FromRgb(rByte, gByte, bByte);
                    }
                    else
                    {
                        waveColor = Color.Parse("#00BFFF"); // Fallback to blue for very quiet parts
                    }

                    var rgbPen = new Pen(new SolidColorBrush(waveColor, isPlayed ? 1.0 : 0.4), 1);
                    context.DrawLine(rgbPen, new Point(x, mid - rmsH), new Point(x, mid + rmsH));
                    
                    if (peakH > rmsH)
                    {
                        var peakRgbPen = new Pen(new SolidColorBrush(isPlayed ? Colors.White : Color.Parse("#80FFFFFF"), 0.8), 1);
                        context.DrawLine(peakRgbPen, new Point(x, mid - peakH), new Point(x, mid - rmsH));
                        context.DrawLine(peakRgbPen, new Point(x, mid + rmsH), new Point(x, mid + peakH));
                    }
                }
                else
                {
                    // Classic Blue/White Rendering
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
        }
    }
}
