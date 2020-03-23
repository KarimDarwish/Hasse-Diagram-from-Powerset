using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GraphVizWrapper;
using GraphVizWrapper.Commands;
using GraphVizWrapper.Queries;

namespace HasseDiagram2._0
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly GraphGeneration _wrapperForGraph;
        private Point _origin;
        private Point _start;

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
        }

      
        private void GetGraph(IGraphGeneration wrapper)
        {
            var set = TxtVars.Text.Split(',');
            TxtNumOfSubsets.Text = Math.Pow(2, set.Length).ToString(CultureInfo.InvariantCulture);
            var sb = new StringBuilder();
            sb.Append("digraph{");
            sb.AppendLine("graph [ranksep=\"" + TxtDistance.Text + "\", nodesep=\"" + TxtDistance.Text +"\"];");
            for (var i = 0; i < Math.Pow(2,set.Length); i++)
            {
                var newList = new List<string>();
                for (var j = 0; j < set.Length; j++)
                {
                    var isList = i & (1 << j); 
                   
                    if (isList > 0)
                        newList.Add(set[j]);
                }
                if (newList.Count != set.Length)
                    PrintLinks(newList, set, sb);
            }
            sb.Append("}");

            // Display the generated code
            GraphVizCode.Text = sb.ToString();

            var output = wrapper.GenerateGraph(sb.ToString(), Enums.GraphReturnType.Png);
            var img = LoadImage(output);
            Width = img.Width + 20;
            Height = img.Height + 20;
            ImgGraph.Source = LoadImage(output);
        }

        private static BitmapImage LoadImage(byte[] imageData)
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
            set.Sort();
            if (set.Count == 0)
                return "\"{}\"";
            return "\"{" + string.Join(",", set.ToArray()) + "}\"";
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
                ((TransformGroup) ImgGraph.RenderTransform).Children.First(tr => tr is TranslateTransform);
            var v = _start - e.GetPosition(Border);
            tt.X = _origin.X - v.X;
            tt.Y = _origin.Y - v.Y;
        }

        private void image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ImgGraph.CaptureMouse();
            var tt =
                (TranslateTransform)
                ((TransformGroup) ImgGraph.RenderTransform).Children.First(tr => tr is TranslateTransform);
            _start = e.GetPosition(Border);
            _origin = new Point(tt.X, tt.Y);
        }

        private void image_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var transformGroup = (TransformGroup) ImgGraph.RenderTransform;
            var transform = (ScaleTransform) transformGroup.Children[0];

            var zoom = e.Delta > 0 ? .2 : -.2;
            transform.ScaleX += zoom;
            transform.ScaleY += zoom;
        }

        private void txt_vars_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                GetGraph(_wrapperForGraph);
        }
    }
}