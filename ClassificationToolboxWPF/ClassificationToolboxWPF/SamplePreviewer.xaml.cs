using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Globalization;
using System.Threading;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for SamplePreviewer.xaml
    /// </summary>
    public partial class SamplePreviewer : UserControl
    {
        IEnumerable<string> list;
        int sampleWidth;
        int sampleHeigth;

        #region Constructors
        public SamplePreviewer()
        {
            InitializeComponent();
        }
        #endregion

        #region Methods
        private void LoadImage(string path)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            PixelFormat pf = PixelFormats.Bgr24;
            byte[] data;
            int height;
            int width;
            int stride;

            StringComparison sc = StringComparison.OrdinalIgnoreCase;
            if (path.EndsWith(".gray.txt", sc))
            {
                using (StreamReader sr = new StreamReader(path))
                {
                    string file = sr.ReadToEnd().Replace("  ", " ");
                    string[] lines = file.Split(new char[] { '\n' });

                    height = lines.Count();
                    width = lines[0].Trim().Split().Count();
                    
                    int widthbyte = (width * pf.BitsPerPixel + 7) / 8;
                    stride = ((widthbyte + 3) / 4) * 4;

                    data = new byte[stride * height];
                    for (int h = 0; h < height; h++)
                    {
                        string[] values = lines[h].Trim().Split();
                        for (int w = 0; w < width; w++)
                        {
                            int id = h * stride + w * pf.BitsPerPixel / 8;
                            data[id + 0] = (byte)(Double.Parse(values[w], CultureInfo.InvariantCulture) * 255);
                            data[id + 1] = data[id];
                            data[id + 2] = data[id];
                        }
                    }
                }
            }
            else if (path.EndsWith(".gray.8bin", sc))
            {
                using (BinaryReader br = new BinaryReader(File.Open(path, FileMode.Open)))
                {
                    width = br.ReadInt32();
                    height = br.ReadInt32();

                    int widthbyte = (width * pf.BitsPerPixel + 7) / 8;
                    stride = ((widthbyte + 3) / 4) * 4;

                    data = new byte[stride * height];
                    for (int h = 0; h < height; h++)
                    {
                        for (int w = 0; w < width; w++)
                        {
                            int id = h * stride + w * pf.BitsPerPixel / 8;
                            data[id] = br.ReadByte();
                            data[id + 1] = data[id];
                            data[id + 2] = data[id];
                        }
                    }
                }
            }
            else
            {
                using (BinaryReader br = new BinaryReader(File.Open(path, FileMode.Open)))
                {
                    width = br.ReadInt32();
                    height = br.ReadInt32();

                    int widthbyte = (width * pf.BitsPerPixel + 7) / 8;
                    stride = ((widthbyte + 3) / 4) * 4;

                    data = new byte[stride * height];
                    for (int h = 0; h < height; h++)
                    {
                        for (int w = 0; w < width; w++)
                        {
                            int id = h * stride + w * pf.BitsPerPixel / 8;
                            data[id] = (byte)(br.ReadDouble() * 255);
                            data[id + 1] = data[id];
                            data[id + 2] = data[id];
                        }
                    }
                }
            }

            sampleHeigth = height;
            sampleWidth = width;

            if (width > imageBorder.ActualWidth || height > imageBorder.ActualHeight)
                sampleImage.Stretch = System.Windows.Media.Stretch.Uniform;
            else
                sampleImage.Stretch = System.Windows.Media.Stretch.None;
            sampleImage.Source = BitmapImage.Create(width, height, 96, 96, pf, null, data, stride);
        }
        #endregion

        private void SelectSamplesFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalFunctions.SelectFolder(ref samplesFolderTextBox, "samplesPath") == System.Windows.Forms.DialogResult.OK)
            {
                nextButton.IsEnabled = false;
                previousButton.IsEnabled = false;

                SearchOption so = SearchOption.TopDirectoryOnly;
                StringComparison sc = StringComparison.OrdinalIgnoreCase;

                list = Directory.EnumerateFiles(samplesFolderTextBox.Text, "*.*", so)
                            .Where(s => s.EndsWith(".gray.txt", sc) || s.EndsWith(".gray.8bin", sc) || s.EndsWith(".gray.64bin", sc));

                //int corrupted = 0;
                //foreach (string file in list)
                //{
                //    using (BinaryReader br = new BinaryReader(File.Open(file, FileMode.Open)))
                //    {
                //        int width = br.ReadInt32();
                //        int height = br.ReadInt32();
                //        if (width < 48 || height < 48)
                //            corrupted++;
                //    }
                //}
                //MessageBox.Show(corrupted.ToString());

                if (list.Count() > 0)
                {
                    LoadImage(list.First());
                    sampleNumberNumericUpDown.Value = 0;
                    sampleNumberNumericUpDown.Maximum = list.Count() - 1;
                    titleTextBox.Text = Path.GetFileName(list.First());
                }
                if (list.Count() > 1)
                {
                   nextButton.IsEnabled = true;
                }
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            nextButton.IsEnabled = true;

            if (sampleNumberNumericUpDown.Value > 0)
                sampleNumberNumericUpDown.Value--;
            if (sampleNumberNumericUpDown.Value == 0)
                previousButton.IsEnabled = false;
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            previousButton.IsEnabled = true;

            if (sampleNumberNumericUpDown.Value < sampleNumberNumericUpDown.Maximum)
                sampleNumberNumericUpDown.Value++;
            if (sampleNumberNumericUpDown.Value == sampleNumberNumericUpDown.Maximum)
                nextButton.IsEnabled = false;
        }

        private void SampleNumberNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            string path = list.ElementAt((int)sampleNumberNumericUpDown.Value);
            LoadImage(path);
            titleTextBox.Text = Path.GetFileName(path) + " (" + sampleWidth + " x " + sampleHeigth + ")";
        }
    }
}
