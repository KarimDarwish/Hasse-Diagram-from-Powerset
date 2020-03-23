using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GraphVizWrapper;
using GraphVizWrapper.Commands;
using GraphVizWrapper.Queries;
using SharpVectors.Converters;

namespace HasseDiagram2._0
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        #region "INotifyPropertyChanged"
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetProperty<Tprop>(ref Tprop storage, Tprop value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            NotifyPropertyChanged(propertyName);
            return true;
        }
        #endregion

        private readonly GraphGeneration _wrapperForGraph;
        private Point _origin;
        private Point _start;

        private byte[] _ImageData = null;
        public bool HasImageData { get => _ImageData != null; }

        private string _Vars = "a,b,c,d";
        public string Vars { get => _Vars; set => SetProperty(ref _Vars, value); }

        private double _Distance = 0.8;
        public double Distance { get => _Distance; set => SetProperty(ref _Distance, value); }

        private double _Subsets = 2;
        public double Subsets { get => _Subsets; set => SetProperty(ref _Subsets, value); }

        private string _GraphVizCode = "";
        public string GraphVizCode { get => _GraphVizCode; set => SetProperty(ref _GraphVizCode, value); }

        private ImageSource _Image;
        public ImageSource Image { get => _Image; set => SetProperty(ref _Image, value); }

        private Enums.GraphReturnType _SelectedType = Enums.GraphReturnType.Png;
        public Enums.GraphReturnType SelectedType { get => _SelectedType; set => SetProperty(ref _SelectedType, value); }

        public static Enums.GraphReturnType[] AvailableTypes { get; } = new Enums.GraphReturnType[] {
                Enums.GraphReturnType.Jpg,
                Enums.GraphReturnType.Png,
                Enums.GraphReturnType.Svg,
            };

        public MainWindow()
        {
            InitializeComponent();

            #region "zooming"
            var group = new TransformGroup();
            var xform = new ScaleTransform();
            group.Children.Add(xform);
            var tt = new TranslateTransform();
            group.Children.Add(tt);
            ImgGraph.RenderTransform = group;
            ImgGraph.MouseWheel += image_MouseWheel;
            ImgGraph.MouseLeftButtonDown += image_MouseLeftButtonDown;
            ImgGraph.MouseLeftButtonUp += image_MouseLeftButtonUp;
            ImgGraph.MouseMove += image_MouseMove;
            #endregion

            #region "Initializing GraphViz.NET"
            var getStartProcessQuery = new GetStartProcessQuery();
            var getProcessStartInfoQuery = new GetProcessStartInfoQuery();
            var registerLayoutPluginCommand = new RegisterLayoutPluginCommand(getProcessStartInfoQuery,
                getStartProcessQuery);

            var wrapper = new GraphGeneration(getStartProcessQuery,
                getProcessStartInfoQuery,
                registerLayoutPluginCommand);
            _wrapperForGraph = wrapper;
            #endregion

            this.DataContext = this;
        }


        private void GetGraph(IGraphGeneration wrapper)
        {
            var set = Vars.Split(',');
            Subsets = Math.Pow(2, set.Length);

            // Build the DOT code for GraphViz
            var sb = new StringBuilder();
            sb.Append("digraph{");
            sb.AppendLine("graph [ranksep=\"" + Distance + "\", nodesep=\"" + Distance + "\"];");
            for (var i = 0; i < Math.Pow(2, set.Length); i++)
            {
                var newList = new List<string>();
                for (var j = 0; j < set.Length; j++)
                {
                    if ((i & (1 << j)) > 0)
                        newList.Add(set[j]);
                }
                if (newList.Count != set.Length)
                    PrintLinks(newList, set, sb);
            }
            sb.Append("}");

            // Display the generated code
            GraphVizCode = sb.ToString();

            // Call GraphViz to generate the graph
            var output = wrapper.GenerateGraph(sb.ToString(), SelectedType);

            if (SelectedType == Enums.GraphReturnType.Svg)
                Image = LoadSvgImage(output, (int)this.Height, (int)this.Width);
            else
                Image = LoadBitmapImage(output);

            // Store ImageData
            _ImageData = output;
            NotifyPropertyChanged(nameof(HasImageData));
        }

        private static BitmapImage LoadBitmapImage(byte[] imageData)
        {
            if ((imageData == null) || (imageData.Length == 0)) return null;
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }

        private static ImageSource LoadSvgImage(byte[] imageData, int height, int width)
        {
            if ((imageData == null) || (imageData.Length == 0)) return null;

            using (var memIn = new MemoryStream(imageData))
            {
                var settings = new SharpVectors.Renderers.Wpf.WpfDrawingSettings()
                {
                    PixelHeight = height,
                    PixelWidth = width,
                    IncludeRuntime = true,

                };

                var ssc = new StreamSvgConverter(settings);

                using (var memOut = new MemoryStream())
                {
                    if (ssc.Convert(memIn, memOut))
                    {
                        // The docs were bad, but this seems to be working fine
                        return new DrawingImage(ssc.Drawing);
                    }
                }
            }

            return null;
        }

        private static void PrintLinks(List<string> list, IEnumerable<string> arr, StringBuilder sb)
        {
            foreach (var value in arr)
            {
                if (list.Contains(value)) continue;
                var newList = new List<string>();
                newList.AddRange(list);
                newList.Add(value);
                sb.Append(GetListAsString(newList) + " -> " + GetListAsString(list) + "[dir=back];"
                          + " \n");
            }
        }

        private static string GetListAsString(List<string> set)
        {
            return "\"{" + string.Join(",", set.OrderBy(s => s)) + "}\"";
        }

        private void image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ImgGraph.ReleaseMouseCapture();
        }

        private void image_MouseMove(object sender, MouseEventArgs e)
        {
            if (!ImgGraph.IsMouseCaptured) return;

            var tt =
                (TranslateTransform)
                ((TransformGroup)ImgGraph.RenderTransform).Children.First(tr => tr is TranslateTransform);
            var v = _start - e.GetPosition(Border);
            tt.X = _origin.X - v.X;
            tt.Y = _origin.Y - v.Y;
        }

        private void image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ImgGraph.CaptureMouse();
            var tt =
                (TranslateTransform)
                ((TransformGroup)ImgGraph.RenderTransform).Children.First(tr => tr is TranslateTransform);
            _start = e.GetPosition(Border);
            _origin = new Point(tt.X, tt.Y);
        }

        private void image_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var transformGroup = (TransformGroup)ImgGraph.RenderTransform;
            var transform = (ScaleTransform)transformGroup.Children[0];

            var zoom = e.Delta > 0 ? .2 : -.2;
            transform.ScaleX += zoom;
            transform.ScaleY += zoom;
        }

        private void txt_vars_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender.GetType() == typeof(TextBox))
            {
                var tb = (TextBox)sender;
                tb.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                GetGraph(_wrapperForGraph);
            }
        }

        private void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            GetGraph(_wrapperForGraph);
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if(_ImageData == null)
            {
                MessageBox.Show("No Image Data generated yet.");
                return;
            }

            try
            {
                var ext = _SelectedType.ToString().ToLower();

                var sfd = new Microsoft.Win32.SaveFileDialog()
                {
                    Filter = $"{ext.ToUpper()} File|*.{ext}",
                    AddExtension = true,
                    OverwritePrompt = true,
                    FileName = $"hasse.{ext}",
                };

                var result = sfd.ShowDialog(this);
                if (result.HasValue && result == true)
                {
                    File.WriteAllBytes(sfd.FileName, _ImageData);
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show("Error saving file: " + ex.Message);
            }
        }
    }
}