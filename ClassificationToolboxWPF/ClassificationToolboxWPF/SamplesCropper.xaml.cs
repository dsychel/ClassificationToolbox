using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Globalization;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for SamplesCropper.xaml
    /// </summary>
    public partial class SamplesCropper : UserControl
    {
        #region Fields
        BackgroundWorker worker;

        BitmapSource imageToCrop;
        BitmapSource imageToCropClean;
        int penSize;
        int fontSize;
        bool working = false;
        bool ctrl = false;

        List<string> imagesPaths;
        int currentImageIndex = -1;

        private Brush TemporaryColor { get; } = Brushes.Blue;
        private Brush SelectedColor { get; } = Brushes.Red;
        private Typeface Font { get; } = new Typeface("Segoe UI");
        #endregion

        public SamplesCropper()
        {
            InitializeComponent();

            GlobalFunctions.InitializeDirectory(ref saveSamplesInTextBox, "cropImageSaveInFolder");
        }

        #region Methods
        public void StopTask()
        {
            if (worker != null && worker.IsBusy)
                worker.CancelAsync();
        }

        private void CalculateImageCoordinate(ref double X, ref double Y)
        {
            int windowWidth = (int)windowToCropSizeNumericUpDown.Value;
            int windowHeight = (int)windowToCropSizeNumericUpDown.Value;

            int boxWidth = (int)imageToCropPictureBox.ActualWidth;
            int boxHeight = (int)imageToCropPictureBox.ActualHeight;

            int imgWidth = imageToCrop.PixelWidth;
            int imgHeight = imageToCrop.PixelHeight;

            double widthScale = (double)imgWidth / (double)boxWidth;
            double heightScale = (double)imgHeight / (double)boxHeight;

            X = (int)(X * widthScale);
            Y = (int)(Y * heightScale);

            if (X - windowWidth / 2 < 0)
                X = windowWidth / 2;
            else if (X + windowWidth / 2 > imgWidth - 1)
                X = imgWidth - 1 - windowWidth / 2;

            if (Y - windowHeight / 2 < 0)
                Y = windowHeight / 2;
            else if (Y + windowHeight / 2 > imgHeight - 1)
                Y = imgHeight - 1 - windowHeight / 2;

            X -= windowWidth / 2;
            Y -= windowHeight / 2;
        }

        private void DrawWindow(double X, double Y, int width, int height, Brush color, bool temporary)
        {
            bool newWindow = true;
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext drawingContext = visual.RenderOpen())
            {
                drawingContext.DrawImage(imageToCrop, new Rect(0, 0, imageToCrop.PixelWidth, imageToCrop.PixelHeight));
                drawingContext.DrawRectangle(null, new Pen(color, penSize), new Rect(X, Y, width, height));

                if (!temporary)
                {
                    int count = 1;
                    if (cropExcludeListBox.Items.Count > 0)
                        count = (int)Int32.Parse(cropExcludeListBox.Items[cropExcludeListBox.Items.Count - 1].ToString().Split(' ')[0]) + 1;

                    for (int i = 0; i < count; i++)
                        if (cropExcludeListBox.Items.Contains(i + " " + X + " " + Y + " " + width + " " + height))
                        {
                            newWindow = false;
                            break;
                        }

                    if (newWindow)
                    {
                        drawingContext.DrawText(new FormattedText(count.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, fontSize, color, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(X, Y));
                        cropExcludeListBox.Items.Add(count + " " + X + " " + Y + " " + width + " " + height);
                    }
                }
            }
            visual.Drawing.Freeze();
            if (!temporary && newWindow)
            {
                RenderTargetBitmap bmp = new RenderTargetBitmap(imageToCrop.PixelWidth, imageToCrop.PixelHeight, imageToCrop.DpiX, imageToCrop.DpiY, PixelFormats.Pbgra32);
                bmp.Render(visual);
                imageToCrop = bmp;
                imageToCrop = new FormatConvertedBitmap(imageToCrop, PixelFormats.Bgr24, null, 0);
                imageToCrop.Freeze();
                //imageToCropPictureBox.Source = imageToCrop;
            }
            visual.Drawing.Freeze();
            imageToCropPictureBox.Source = new DrawingImage(visual.Drawing);
            winCoordsTextBox.Text = "X: " + X + ", Y: " + Y;
        }

        private void DrawWindows(List<Rect> windows, Brush color)
        {
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext drawingContext = visual.RenderOpen())
            {
                int count = 1;
                if (cropExcludeListBox.Items.Count > 0)
                    count = (int)Int32.Parse(cropExcludeListBox.Items[cropExcludeListBox.Items.Count - 1].ToString().Split(' ')[0]) + 1;

                drawingContext.DrawImage(imageToCrop, new Rect(0, 0, imageToCrop.PixelWidth, imageToCrop.PixelHeight));
                foreach (Rect window in windows)
                {
                    drawingContext.DrawRectangle(null, new Pen(color, penSize), window);
                    drawingContext.DrawText(new FormattedText(count.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, fontSize, color, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(window.X, window.Y));
                    cropExcludeListBox.Items.Add(count + " " + window.X + " " + window.Y + " " + window.Width + " " + window.Height);
                    count++;
                }
            }
            RenderTargetBitmap bmp = new RenderTargetBitmap(imageToCrop.PixelWidth, imageToCrop.PixelHeight, imageToCrop.DpiX, imageToCrop.DpiY, PixelFormats.Pbgra32);
            bmp.Render(visual);
            imageToCrop = bmp;
            imageToCrop = new FormatConvertedBitmap(imageToCrop, PixelFormats.Bgr24, null, 0);
            imageToCrop.Freeze();

            visual.Drawing.Freeze();
            imageToCropPictureBox.Source = new DrawingImage(visual.Drawing);
        }

        private void SaveWindow(ref BitmapSource cropedWindow, string saveInPath)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            string date = DateTime.Now.Ticks.ToString();
            string fileName = saveInPath + "window_" + Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + date.Substring(date.Length - 7);
            if (saveSettings.SelectedFileType == ImageFileTypeSelector.FileType.binary8bit ||
                saveSettings.SelectedFileType == ImageFileTypeSelector.FileType.binary64bit ||
                saveSettings.SelectedFileType == ImageFileTypeSelector.FileType.text)
            {
                int widthbyte = (cropedWindow.PixelWidth * cropedWindow.Format.BitsPerPixel + 7) / 8;
                int stride = ((widthbyte + 3) / 4) * 4;
                int bytePerPixel = cropedWindow.Format.BitsPerPixel / 8;
                byte[] data = new byte[stride * cropedWindow.PixelHeight];
                cropedWindow.CopyPixels(data, stride, 0);

                if (saveSettings.SelectedFileType == ImageFileTypeSelector.FileType.binary8bit)
                {
                    byte outData;
                    using (BinaryWriter sw = new BinaryWriter(File.Open(fileName + ".gray.8bin", FileMode.Create)))
                    {
                        sw.Write(cropedWindow.PixelWidth);
                        sw.Write(cropedWindow.PixelHeight);

                        for (int h = 0; h < cropedWindow.PixelHeight; h++)
                        {
                            for (int w = 0; w < cropedWindow.PixelWidth; w++)
                            {
                                int id = h * stride + w * bytePerPixel;
                                outData = data[id + 0];
                                sw.Write(outData);
                            }
                        }
                    }
                }
                else if (saveSettings.SelectedFileType == ImageFileTypeSelector.FileType.binary64bit)
                {
                    double outData;
                    using (BinaryWriter sw = new BinaryWriter(File.Open(fileName + ".gray.64bin", FileMode.Create)))
                    {
                        sw.Write(cropedWindow.PixelWidth);
                        sw.Write(cropedWindow.PixelHeight);

                        for (int h = 0; h < cropedWindow.PixelHeight; h++)
                        {
                            for (int w = 0; w < cropedWindow.PixelWidth; w++)
                            {
                                int id = h * stride + w * bytePerPixel;
                                outData = data[id + 0] / 255.0;
                                sw.Write(outData);
                            }
                        }
                    }
                }
                else if (saveSettings.SelectedFileType == ImageFileTypeSelector.FileType.text)
                {
                    double outData;
                    string format = "{0:0." + new string('#', Dispatcher.Invoke(new Func<int>(()=> { return (int)saveSettings.DecimalPlaces; }))) + "} ";
                    using (StreamWriter sw = new StreamWriter(fileName + ".gray.txt"))
                    {
                        for (int h = 0; h < cropedWindow.PixelHeight; h++)
                        {
                            for (int w = 0; w < cropedWindow.PixelWidth; w++)
                            {
                                int id = h * stride + w * bytePerPixel;
                                outData = data[id + 0] / 255.0;

                                if (w < cropedWindow.PixelWidth - 1)
                                    sw.Write(String.Format(CultureInfo.InvariantCulture, format, outData) + " ");
                                else
                                    sw.Write(String.Format(CultureInfo.InvariantCulture, format, outData));
                            }
                            if (h < cropedWindow.PixelHeight - 1)
                                sw.WriteLine();
                        }
                    }
                }
            }
            else if (saveSettings.SelectedFileType == ImageFileTypeSelector.FileType.jpeg)
            {
                using (var fileStream = new FileStream(fileName + ".jpg", FileMode.Create))
                {
                    BitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(cropedWindow));
                    encoder.Save(fileStream);
                }
            }
            else if (saveSettings.SelectedFileType == ImageFileTypeSelector.FileType.bitmap)
            {
                using (var fileStream = new FileStream(fileName + ".bmp", FileMode.Create))
                {
                    BitmapEncoder encoder = new BmpBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(cropedWindow));
                    encoder.Save(fileStream);
                }
            }
            else if (saveSettings.SelectedFileType == ImageFileTypeSelector.FileType.png)
            {
                using (var fileStream = new FileStream(fileName + ".png", FileMode.Create))
                {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(cropedWindow));
                    encoder.Save(fileStream);
                }
            }
        }

        private void CropImage(out BitmapSource cropedWindow, ref BitmapSource image, int X, int Y, int windowWidth, int windowHeight, bool resize, bool resizeAlways, double rSize)
        {
            if ((resize && resizeAlways) ||
                (resize && !resizeAlways && rSize < windowWidth))
            {
                double size = (double)resizeNumericUpDown.Value;

                cropedWindow = new CroppedBitmap(image, new Int32Rect(X, Y, windowWidth, windowHeight));
                cropedWindow = new TransformedBitmap(cropedWindow, new ScaleTransform(size / cropedWindow.PixelWidth, size / cropedWindow.PixelHeight));
            }
            else
            {
                cropedWindow = new CroppedBitmap(image, new Int32Rect(X, Y, windowWidth, windowHeight));
            }
        }

        private void LoadImage(string fileName)
        {
            PixelFormat pf = PixelFormats.Bgr24;

            imageToCropTextBox.Text = fileName;
            BitmapSource bmp = new BitmapImage(new Uri(fileName, UriKind.Relative));
            if (bmp.Format != PixelFormats.Bgr24)
                bmp = new FormatConvertedBitmap(bmp, pf, null, 0);

            int height = bmp.PixelHeight;
            int width = bmp.PixelWidth;
            int widthbyte = (width * pf.BitsPerPixel + 7) / 8;
            int stride = ((widthbyte + 3) / 4) * 4;
            byte[] data = new byte[stride * height];
            bmp.CopyPixels(data, stride, 0);

            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    int id = h * stride + w * pf.BitsPerPixel / 8;
                    data[id + 0] = (byte)Math.Round((0.1140 * data[id + 0] + 0.5870 * data[id + 1] + 0.2990 * data[id + 2]));
                    data[id + 1] = data[id];
                    data[id + 2] = data[id];
                }
            }
            imageToCropClean = WriteableBitmap.Create(width, height, 96, 96, pf, null, data, stride);
            imageToCrop = WriteableBitmap.Create(width, height, 96, 96, pf, null, data, stride);
            imageToCrop.Freeze();
            imageToCropClean.Freeze();

            //if (imageToCrop.PixelWidth > imageBorder.ActualWidth || imageToCrop.PixelHeight > imageBorder.ActualHeight)
            //    imageToCropPictureBox.Stretch = System.Windows.Media.Stretch.Uniform;
            //else
            imageToCropPictureBox.Width = imageToCrop.PixelWidth;
            imageToCropPictureBox.Height = imageToCrop.PixelHeight;
            imageToCropPictureBox.Stretch = System.Windows.Media.Stretch.None;
            imageToCropPictureBox.Source = imageToCrop;

            if (windowToCropSizeNumericUpDown.Value > imageToCrop.PixelWidth || windowToCropSizeNumericUpDown.Value > imageToCrop.PixelHeight)
                windowToCropSizeNumericUpDown.Value = Math.Min(imageToCrop.PixelWidth, imageToCrop.PixelHeight);
            windowToCropSizeNumericUpDown.Maximum = Math.Min(imageToCrop.PixelWidth, imageToCrop.PixelHeight);

            penSize = 3; //(int)Math.Max(2, 0.002 * imageToCrop.Width);
            fontSize = 20;//(int)Math.Max(12, 0.015 * imageToCrop.Width);
            cropExcludeListBox.Items.Clear();

            if(loadCoordinatesCheckBox.IsChecked == true && File.Exists(fileName + ".txt"))
            {
                List<Rect> windows = new List<Rect>();
                using (StreamReader sr = new StreamReader(fileName + ".txt"))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        line = line.Trim().Replace("   ", " ");
                        string[] values = line.Split(' ');
                        int x = (int)double.Parse(values[0]);
                        int y = (int)double.Parse(values[1]);
                        int w = (int)double.Parse(values[2]);

                        windows.Add(new Rect(x, y, w, w));
                    }
                }
                DrawWindows(windows, SelectedColor);
            }
        }
        #endregion

        #region Triggers
        public event EventHandler<StartedEventsArg> CropingStarted;
        protected virtual void OnCropingStarted(StartedEventsArg e)
        {
            CropingStarted?.Invoke(this, e);
        }

        public event EventHandler<CompletionEventsArg> CropingCompletion;
        protected virtual void OnCropingCompletion(CompletionEventsArg e)
        {
            CropingCompletion?.Invoke(this, e);
        }
        #endregion

        #region Events
        private void LoadCoordsButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(imageToCropTextBox.Text) && GlobalFunctions.SelectCooridantes("cropSamplesCoordinates") == System.Windows.Forms.DialogResult.OK)
            {
                bool? load = loadCoordinatesCheckBox.IsChecked;
                loadCoordinatesCheckBox.IsChecked = false;
                LoadImage(imageToCropTextBox.Text);
                loadCoordinatesCheckBox.IsChecked = load;

                List<Rect> windows = new List<Rect>();
                using (StreamReader sr = new StreamReader(Properties.Settings.Default.cropSamplesCoordinates))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        line = line.Trim().Replace("   ", " ");
                        string[] values = line.Split(' ');
                        int x = (int)double.Parse(values[0]);
                        int y = (int)double.Parse(values[1]);
                        int w = (int)double.Parse(values[2]);
                        w -= w % 2;

                        windows.Add(new Rect(x, y, w, w));
                    }
                }
                DrawWindows(windows, SelectedColor);
            }
        }

        private void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            imageToCropPictureBox.MouseUp -= ImageToCropPictureBox_MouseUp;

            if (GlobalFunctions.SelectImage(out string fileName, "cropImageFolder") == System.Windows.Forms.DialogResult.OK)
            {
                SearchOption so = SearchOption.TopDirectoryOnly;
                StringComparison sc = StringComparison.OrdinalIgnoreCase;
                imagesPaths = Directory.EnumerateFiles(Path.GetDirectoryName(fileName), "*.*", so)
                           .Where(s => s.EndsWith(".jpg", sc) || s.EndsWith(".png", sc) || s.EndsWith(".bmp", sc) || s.EndsWith(".jpeg", sc)).ToList();

                currentImageIndex = imagesPaths.IndexOf(fileName);
                LoadImage(fileName);

                prevImageButton.IsEnabled = true;
                nextImageButton.IsEnabled = true;
            }

            Task.Delay(200).ContinueWith(_ => { imageToCropPictureBox.MouseUp += ImageToCropPictureBox_MouseUp; });
        }

        private void PrevImageButton_Click(object sender, RoutedEventArgs e)
        {
            imageToCropPictureBox.MouseUp -= ImageToCropPictureBox_MouseUp;

            currentImageIndex--;
            if (currentImageIndex == -1)
                currentImageIndex = imagesPaths.Count - 1;

            LoadImage(imagesPaths[currentImageIndex]);

            Task.Delay(200).ContinueWith(_ => { imageToCropPictureBox.MouseUp += ImageToCropPictureBox_MouseUp; });
        }

        private void NextImageButton_Click(object sender, RoutedEventArgs e)
        {
            imageToCropPictureBox.MouseUp -= ImageToCropPictureBox_MouseUp;

            currentImageIndex++;
            if (currentImageIndex == imagesPaths.Count)
                currentImageIndex = 0;

            LoadImage(imagesPaths[currentImageIndex]);

            Task.Delay(200).ContinueWith(_ => { imageToCropPictureBox.MouseUp += ImageToCropPictureBox_MouseUp; });
        }

        private void SelectSaveFolderButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFolder(ref saveSamplesInTextBox, "cropImageSaveInFolder");
        }

        private void SaveCoordinatesButton_Click(object sender, RoutedEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            string fileName = imageToCropTextBox.Text + ".txt";

            //int itemsCount = cropExcludeListBox.Items.Count;
            //for (int i = 0; i < itemsCount; i++)
            //{
            //    string coordiante = (string)cropExcludeListBox.Items[0];
            //    for (int j = i+1; j < itemsCount; j++)
            //    {
            //        if((string)cropExcludeListBox.Items[j] == coordiante)
            //        {
            //            cropExcludeListBox.Items.RemoveAt(j);
            //            itemsCount--;
            //            j--;
            //        }
            //    }
            //}
            //for (int i = 0; i < count; i++)
            //    if (cropExcludeListBox.Items.Contains(i + " " + X + " " + Y + " " + width + " " + height))
            //    {
            //        newWindow = false;
            //        break;
            //    }


            using (StreamWriter sw = new StreamWriter(fileName))
            {
                int coordsCount = cropExcludeListBox.Items.Count;
                for (int i = 0; i < coordsCount - 1; i++)
                {
                    string[] values = ((string)cropExcludeListBox.Items[i]).Split();
                    sw.WriteLine(values[1] + " " + values[2] + " " + values[3]);
                }
                string[] valuesEnd = ((string)cropExcludeListBox.Items[coordsCount - 1]).Split();
                sw.Write(valuesEnd[1] + " " + valuesEnd[2] + " " + valuesEnd[3]);
            }

            MessageBox.Show(fileName + " saved.");
        }

        private void CropSelectedRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (cropSettingsGroupBox != null && mainGrid != null)
            {
                cropSettingsGroupBox.Visibility = Visibility.Hidden;
                mainGrid.RowDefinitions[5].Height = new GridLength(2, GridUnitType.Pixel);
            }
        }

        private void ExcludeSelectedRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (cropSettingsGroupBox != null && mainGrid != null)
            {
                cropSettingsGroupBox.Visibility = Visibility.Visible;
                mainGrid.RowDefinitions[5].Height = new GridLength(100, GridUnitType.Pixel);
            }
        }

        private void WindowToCropSizeNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            if (windowToCropSizeNumericUpDown.Value % 2 == 1)
                if (windowToCropSizeNumericUpDown.Value + 1 < windowToCropSizeNumericUpDown.Maximum)
                    windowToCropSizeNumericUpDown.Value -= 1;
                else
                    windowToCropSizeNumericUpDown.Value += 1;
        }

        private void CropExcludeListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                try
                {
                    cropExcludeListBox.Items.Remove(cropExcludeListBox.SelectedItem);

                    DrawingVisual visual = new DrawingVisual();
                    using (DrawingContext drawingContext = visual.RenderOpen())
                    {
                        drawingContext.DrawImage(imageToCropClean, new Rect(0, 0, imageToCropClean.PixelWidth, imageToCropClean.PixelHeight));
                        foreach (string window in cropExcludeListBox.Items)
                        {
                            string[] windowParameters = window.Split(' ');
                            int count = Int32.Parse(windowParameters[0]);
                            int X = Int32.Parse(windowParameters[1]);
                            int Y = Int32.Parse(windowParameters[2]);
                            int windowWidth = Int32.Parse(windowParameters[3]);
                            int windowHeight = Int32.Parse(windowParameters[4]);

                            drawingContext.DrawRectangle(null, new Pen(SelectedColor, penSize), new Rect(X, Y, windowWidth, windowHeight));
                            Pen pen = new Pen(SelectedColor, penSize);

                            drawingContext.DrawText(new FormattedText(count.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, fontSize, SelectedColor, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(X, Y));
                        }
                    }
                    RenderTargetBitmap bmp = new RenderTargetBitmap(imageToCrop.PixelWidth, imageToCrop.PixelHeight, imageToCrop.DpiX, imageToCrop.DpiY, PixelFormats.Pbgra32);
                    bmp.Render(visual);
                    imageToCrop = bmp;
                    imageToCrop = new FormatConvertedBitmap(imageToCrop, PixelFormats.Bgr24, null, 0);
                    imageToCrop.Freeze();
                    imageToCropPictureBox.Source = imageToCrop;
                }
                catch
                {
                }
            }
        }

        private void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.OemPlus || e.Key == Key.Add || e.Key == Key.A) && windowToCropSizeNumericUpDown.Value < windowToCropSizeNumericUpDown.Maximum)
            {
                int width = imageToCrop.PixelWidth;
                int increment = (int)(0.003 * width);
                if (increment % 2 == 1)
                    increment++;
                windowToCropSizeNumericUpDown.Value += increment;

                Point mouse = Mouse.GetPosition(imageToCropPictureBox);
                double X = mouse.X, Y = mouse.Y;
                CalculateImageCoordinate(ref X, ref Y);
                DrawWindow(X, Y, (int)windowToCropSizeNumericUpDown.Value, (int)windowToCropSizeNumericUpDown.Value, TemporaryColor, true);
            }
            else if ((e.Key == Key.OemMinus || e.Key == Key.Subtract || e.Key == Key.Z) && windowToCropSizeNumericUpDown.Value > windowToCropSizeNumericUpDown.Minimum)
            {
                int width = imageToCrop.PixelWidth;
                int increment = (int)(0.003 * width);
                if (increment % 2 == 1)
                    increment++;
                if (windowToCropSizeNumericUpDown.Value - increment >= windowToCropSizeNumericUpDown.Minimum)
                    windowToCropSizeNumericUpDown.Value -= increment;
                else
                    windowToCropSizeNumericUpDown.Value = windowToCropSizeNumericUpDown.Minimum;

                Point mouse = Mouse.GetPosition(imageToCropPictureBox);
                double X = mouse.X, Y = mouse.Y;
                CalculateImageCoordinate(ref X, ref Y);
                DrawWindow(X, Y, (int)windowToCropSizeNumericUpDown.Value, (int)windowToCropSizeNumericUpDown.Value, TemporaryColor, true);
            }       
            else if ((e.Key == Key.S) && windowToCropSizeNumericUpDown.Value < windowToCropSizeNumericUpDown.Maximum)
            {
                int width = imageToCrop.PixelWidth;
                int increment = (int)(0.001 * width);
                if (increment % 2 == 1)
                    increment++;
                windowToCropSizeNumericUpDown.Value += increment;

                Point mouse = Mouse.GetPosition(imageToCropPictureBox);
                double X = mouse.X, Y = mouse.Y;
                CalculateImageCoordinate(ref X, ref Y);
                DrawWindow(X, Y, (int)windowToCropSizeNumericUpDown.Value, (int)windowToCropSizeNumericUpDown.Value, TemporaryColor, true);
            }
            else if ((e.Key == Key.X) && windowToCropSizeNumericUpDown.Value > windowToCropSizeNumericUpDown.Minimum)
            {
                int width = imageToCrop.PixelWidth;
                int increment = (int)(0.001 * width);
                if (increment % 2 == 1)
                    increment++;
                if (windowToCropSizeNumericUpDown.Value - increment >= windowToCropSizeNumericUpDown.Minimum)
                    windowToCropSizeNumericUpDown.Value -= increment;
                else
                    windowToCropSizeNumericUpDown.Value = windowToCropSizeNumericUpDown.Minimum;

                Point mouse = Mouse.GetPosition(imageToCropPictureBox);
                double X = mouse.X, Y = mouse.Y;
                CalculateImageCoordinate(ref X, ref Y);
                DrawWindow(X, Y, (int)windowToCropSizeNumericUpDown.Value, (int)windowToCropSizeNumericUpDown.Value, TemporaryColor, true);
            }
            else if ((e.Key == Key.D) && windowToCropSizeNumericUpDown.Value < windowToCropSizeNumericUpDown.Maximum)
            {
                int width = imageToCrop.PixelWidth;
                int increment = (int)(0.01 * width);
                if (increment % 2 == 1)
                    increment++;
                windowToCropSizeNumericUpDown.Value += increment;

                Point mouse = Mouse.GetPosition(imageToCropPictureBox);
                double X = mouse.X, Y = mouse.Y;
                CalculateImageCoordinate(ref X, ref Y);
                DrawWindow(X, Y, (int)windowToCropSizeNumericUpDown.Value, (int)windowToCropSizeNumericUpDown.Value, TemporaryColor, true);
            }
            else if ((e.Key == Key.C) && windowToCropSizeNumericUpDown.Value > windowToCropSizeNumericUpDown.Minimum)
            {
                int width = imageToCrop.PixelWidth;
                int increment = (int)(0.01 * width);
                if (increment % 2 == 1)
                    increment++;
                if (windowToCropSizeNumericUpDown.Value - increment >= windowToCropSizeNumericUpDown.Minimum)
                    windowToCropSizeNumericUpDown.Value -= increment;
                else
                    windowToCropSizeNumericUpDown.Value = windowToCropSizeNumericUpDown.Minimum;

                Point mouse = Mouse.GetPosition(imageToCropPictureBox);
                double X = mouse.X, Y = mouse.Y;
                CalculateImageCoordinate(ref X, ref Y);
                DrawWindow(X, Y, (int)windowToCropSizeNumericUpDown.Value, (int)windowToCropSizeNumericUpDown.Value, TemporaryColor, true);
            }
            else if (e.Key == Key.LeftCtrl)
            {
                ctrl = true;
            }
        }

        private void UserControl_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl)
                ctrl = false;
        }

        private void UserControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double jump = 0.1;

            double minH = 0.1 * imageToCrop.PixelHeight;
            double minW = 0.1 * imageToCrop.PixelWidth;
            double maxH = imageToCrop.PixelHeight;
            double maxW = imageToCrop.PixelWidth;

            imageScrollViewer.UpdateLayout();
            double middleH = imageScrollViewer.HorizontalOffset + imageScrollViewer.ActualWidth / 2;
            double middleV = imageScrollViewer.VerticalOffset + imageScrollViewer.ActualHeight / 2;
            if (e.Delta > 0 && ctrl)
            {
                if (imageToCropPictureBox.ActualWidth < maxW && imageToCropPictureBox.ActualHeight < maxH)
                {
                    double jumpH = 1.0 + jump;
                    double jumpW = 1.0 + jump;

                    double height = imageToCropPictureBox.ActualHeight * (jumpH);
                    double width = imageToCropPictureBox.ActualWidth * (jumpW);

                    if (height > maxH || width > maxW)
                    {
                        height = maxH;
                        width = maxW;

                        jumpH = height/ imageToCropPictureBox.ActualHeight;
                        jumpW = width/ imageToCropPictureBox.ActualWidth;
                    }
                    imageToCropPictureBox.Stretch = System.Windows.Media.Stretch.Uniform;
                    imageToCropPictureBox.Width = width;
                    imageToCropPictureBox.Height = height;

                    middleH *= (jumpH);
                    middleH -= imageScrollViewer.ActualWidth / 2;
                    middleV *= (jumpW);
                    middleV -= imageScrollViewer.ActualHeight / 2;
                    imageScrollViewer.ScrollToHorizontalOffset(middleH);
                    imageScrollViewer.ScrollToVerticalOffset(middleV);
                    imageScrollViewer.UpdateLayout();
                }

                e.Handled = true;
            }
            else if (e.Delta < 0 && ctrl)
            {
                if (imageToCropPictureBox.ActualWidth > minW && imageToCropPictureBox.ActualHeight > minH)
                {
                    double height = imageToCropPictureBox.ActualHeight * (1.0 - jump);
                    double width = imageToCropPictureBox.ActualWidth * (1.0 -  jump);

                    if (height > minH && width > minW)
                    {
                        imageToCropPictureBox.Stretch = System.Windows.Media.Stretch.Uniform;
                        imageToCropPictureBox.Width = width;
                        imageToCropPictureBox.Height = height;

                        middleH *= (1.0 - jump);
                        middleH -= imageScrollViewer.ActualWidth / 2;
                        middleV *= (1.0 - jump);
                        middleV -= imageScrollViewer.ActualHeight / 2;
                        imageScrollViewer.ScrollToHorizontalOffset(middleH);
                        imageScrollViewer.ScrollToVerticalOffset(middleV);
                        imageScrollViewer.UpdateLayout();
                    }
                }

                e.Handled = true;
            }
        }

        private void ImageScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ctrl)
                e.Handled = true;
        }

        private void ImageBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (imageToCrop != null)
            {
                if (imageToCrop.PixelWidth > imageBorder.ActualWidth || imageToCrop.PixelHeight > imageBorder.ActualHeight)
                    imageToCropPictureBox.Stretch = System.Windows.Media.Stretch.Uniform;
                else
                    imageToCropPictureBox.Stretch = System.Windows.Media.Stretch.None;
            }
        }

        private void ImageToCropPictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (imageToCrop != null)
            {
                Point mouse = e.GetPosition(imageToCropPictureBox);
                double X = mouse.X, Y = mouse.Y;
                CalculateImageCoordinate(ref X, ref Y);
                DrawWindow(X, Y, (int)windowToCropSizeNumericUpDown.Value, (int)windowToCropSizeNumericUpDown.Value, TemporaryColor, true);
            }
        }

        private void ImageToCropPictureBox_MouseUp(object sender, MouseButtonEventArgs e)
        {
            imageToCropPictureBox.MouseUp -= ImageToCropPictureBox_MouseUp;

            if (imageToCrop != null && !working)
            {
                working = true;

                Point mouse = e.GetPosition(imageToCropPictureBox);
                double X = mouse.X, Y = mouse.Y;
                CalculateImageCoordinate(ref X, ref Y);
                DrawWindow(X, Y, (int)windowToCropSizeNumericUpDown.Value, (int)windowToCropSizeNumericUpDown.Value, SelectedColor, false);

                working = false;
            }

            Task.Delay(200).ContinueWith(_ => { imageToCropPictureBox.MouseUp += ImageToCropPictureBox_MouseUp; });
        }

        private void CropButton_Click(object sender, RoutedEventArgs e)
        {
            if (saveSamplesInTextBox.Text == "" || !Directory.Exists(saveSamplesInTextBox.Text))
            {
                MessageBox.Show("Save path is empty or doesn't exist.");
                return;
            }

            if (worker != null && worker.IsBusy)
            {
                MessageBox.Show("Application is busy.");
                return;
            }

            string logMessage = "Croping started.";
            StartedEventsArg args = new StartedEventsArg("Status: Working", logMessage, DateTime.Now, 0, false);
            OnCropingStarted(args);

            worker = new BackgroundWorker() { WorkerSupportsCancellation = true };
            worker.DoWork += CropBackgroundWorker_DoWork;
            worker.RunWorkerCompleted += CropBackgroundWorker_RunWorkerCompleted;
            worker.RunWorkerAsync();
        }

        private void CropBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            bool cropSelected = false, excludeSelected = true;
            bool resize = false, resizeAlways = true;
            double resizeSize = 48;
            int windowSize = 48;
            double probability = 1, intersection = 1, step = 1;
            string saveInPath = "";
            int imageWidth = 0, imageHeight = 0, stride = 0;
            byte[] data = null;

            Dispatcher.Invoke(new Action(() =>
            {
                cropSelected = (cropSelectedRadioButton.IsChecked == null || cropSelectedRadioButton.IsChecked == false) ? false : true;
                excludeSelected = (excludeSelectedRadioButton.IsChecked == null || excludeSelectedRadioButton.IsChecked == false) ? false : true;

                resize = (resizeCheckBox.IsChecked == null || resizeCheckBox.IsChecked == false) ? false : true;
                resizeAlways = (reiszeAlwaysRadioButton.IsChecked == null || reiszeAlwaysRadioButton.IsChecked == false) ? false : true;
                resizeSize = (double)resizeNumericUpDown.Value;

                windowSize = (int)windowToCropSizeNumericUpDown.Value;
                imageWidth = imageToCropClean.PixelWidth;
                imageHeight = imageToCropClean.PixelHeight;

                probability = (double)cropProbabiltyNumericUpDown.Value;
                intersection = (double)cropIntersectioNumericUpDown.Value;
                step = (double)cropStepNumericUpDown.Value;

                saveInPath = saveSamplesInTextBox.Text;

                int widthbyte = (imageWidth * imageToCropClean.Format.BitsPerPixel + 7) / 8;
                stride = ((widthbyte + 3) / 4) * 4;
                data = new byte[stride * imageHeight];
                imageToCropClean.CopyPixels(data, stride, 0);
            }));

            BitmapSource image = BitmapSource.Create(imageWidth, imageHeight, 96, 96, PixelFormats.Bgr24, null, data, stride);
            data = null;

            Random randomGenerator = new Random();
            if (cropSelected)
            {
                foreach (string window in cropExcludeListBox.Items)
                {
                    if (this.worker.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }

                    string[] windowParameters = window.Split(' ');
                    int count = Int32.Parse(windowParameters[0]);
                    int X = Int32.Parse(windowParameters[1]);
                    int Y = Int32.Parse(windowParameters[2]);
                    int windowWidth = Int32.Parse(windowParameters[3]);
                    int windowHeight = Int32.Parse(windowParameters[4]);
                    

                        CropImage(out BitmapSource cropedWindow, ref image, X, Y, windowWidth, windowHeight, resize, resizeAlways, resizeSize);
                        SaveWindow(ref cropedWindow, saveInPath);
                }
            }
            else if (excludeSelected)
            {
                int ignoredArea = (int)(windowSize * intersection);
                double stepX = (imageWidth - windowSize) * step;
                double stepY = (imageHeight - windowSize) * step;

                for (double i = 0; (int)i <= imageWidth - windowSize; i += stepX)
                {
                    for (double j = 0; (int)j <= imageHeight - windowSize; j += stepY)
                    {
                        if (this.worker.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }

                        Rect CollisionBox = new Rect((int)i + ignoredArea, (int)j + ignoredArea, windowSize - 2 * ignoredArea, windowSize - 2 * ignoredArea);

                        bool intersectWithExcludedArea = false;
                        foreach (string window in cropExcludeListBox.Items)
                        {
                            string[] windowParameters = window.Split(' ');
                            int count = Int32.Parse(windowParameters[0]);
                            int X = Int32.Parse(windowParameters[1]);
                            int Y = Int32.Parse(windowParameters[2]);
                            int windowWidth = Int32.Parse(windowParameters[3]);
                            int windowHeight = Int32.Parse(windowParameters[4]);

                            Rect excludedWindow = new Rect(X, Y, windowWidth, windowHeight);
                            
                            if (excludedWindow.IntersectsWith(CollisionBox))
                            {
                                intersectWithExcludedArea = true;
                                break;
                            }
                        }

                        if (!intersectWithExcludedArea)
                        {
                            double probabilityTest = randomGenerator.NextDouble();

                            if (probabilityTest <= probability)
                            {
                                CropImage(out BitmapSource cropedWindow, ref image, (int)i, (int)j, windowSize, windowSize, resize, resizeAlways, resizeSize);
                                SaveWindow(ref cropedWindow, saveInPath);
                            }
                        }
                    }
                }
            }
        }

        private void CropBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string statusLabel;
            string logMessage;
            string error = null;

            if (e.Cancelled)
            {
                statusLabel = "Status: Croping cancelled. Check event log for details";
                logMessage = "Croping cancelled.";
            }
            else if (e.Error != null)
            {
                statusLabel = "Status: Croping completed with errors. Check event log for details.";
                logMessage = "Croping completed with errors:";
                error = e.Error.Message;
            }
            else
            {
                statusLabel = "Status: Croping completed successful. Check event log for details.";
                logMessage = "Croping completed successful.";
            }

            CompletionEventsArg args = new CompletionEventsArg(statusLabel, logMessage, error, DateTime.Now, false);
            OnCropingCompletion(args);

            worker.Dispose();
            worker = null;
        }
        #endregion
    }
}
