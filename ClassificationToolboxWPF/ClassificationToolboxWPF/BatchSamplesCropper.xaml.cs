using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for BatchSamplesCropper.xaml
    /// </summary>
    public partial class BatchSamplesCropper : UserControl
    {
        #region Class
        public static class StaticRandom
        {
            static readonly Random generator = new Random();

            static readonly ThreadLocal<Random> random =
                new ThreadLocal<Random>(() => new Random(SeedInitializer()));

            private static int SeedInitializer()
            {
                lock (generator) return generator.Next();
            }

            public static int Next(int min, int max)
            {
                return random.Value.Next(min, max);
            }

            public static double NextDouble()
            {
                return random.Value.NextDouble();
            }
        }
        #endregion

        #region Fields
        BackgroundWorker worker = new BackgroundWorker();

        long operationTime = 0;
        int samplesCount = 0;
        decimal rescalePrevValue = 480;
        bool PKactive = false;
        #endregion

        public BatchSamplesCropper()
        {
            InitializeComponent();

            GlobalFunctions.InitializeDirectory(ref imagesFolderTextBox, "batchCropImageFolder");
            GlobalFunctions.InitializeDirectory(ref outputFolderTextBox, "batchCropSaveInFolder");

            BuildImageFilesStatistic(Properties.Settings.Default.batchCropImageFolder);
        }

        #region Methods
        public void StopTask()
        {
            if (worker != null && worker.IsBusy)
                worker.CancelAsync();
        }

        private void CheckFolder(string path, ref int jpgs, ref int bmps, ref int pngs)
        {
            try
            {
                SearchOption so = SearchOption.TopDirectoryOnly;
                StringComparison sc = StringComparison.OrdinalIgnoreCase;
                IEnumerable<string> files = Directory.EnumerateFiles(path, "*.*", so);

                jpgs += files.Count(s => s.EndsWith(".jpg", sc) || s.EndsWith(".jpeg", sc));
                bmps += files.Count(s => s.EndsWith(".bmp", sc));
                pngs += files.Count(s => s.EndsWith(".png", sc));

                statisticTextBox.Text += path + " (included)\r\n";
                if (includeSubfolderCheckBox.IsChecked == true)
                {
                    IEnumerable<string> directories = Directory.EnumerateDirectories(path);

                    foreach (string directory in directories)
                        CheckFolder(directory, ref jpgs, ref bmps, ref pngs);
                }
            }
            catch
            {
                statisticTextBox.Text += path + " (ignored)\r\n";
            }
        }

        private void BuildImageFilesStatistic(string path)
        {
            statisticTextBox.Text = "Folders: \r\n";
            int bmps = 0, jpgs = 0, pngs = 0, total;

            if (Directory.Exists(path))
            {
                CheckFolder(path, ref jpgs, ref bmps, ref pngs);
                string imageStatistic = "";

                imageStatistic += "PNG files count: " + pngs + "\r\n";
                imageStatistic += "BMP files count: " + bmps + "\r\n";
                imageStatistic += "JPG files count: " + jpgs + "\r\n" + "\r\n";
                total = bmps + jpgs + pngs;

                imageStatistic += "All files: " + total + "\r\n" + "\r\n";

                statisticTextBox.Text = statisticTextBox.Text.Insert(0, imageStatistic);
            }
            else
            {
                Properties.Settings.Default.grayScaleConversionImagePath = "";
                Properties.Settings.Default.Save();
                imagesFolderTextBox.Text = "";
            }
        }

        private void SaveWindow(ref BitmapSource cropedWindow, string saveInPath, string desc = "")
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            string date = DateTime.Now.Ticks.ToString();
            string fileName = saveInPath + "window_" + Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + date.Substring(date.Length - 7) + desc;
            if (cropSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.binary8bit ||
                cropSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.binary64bit ||
                cropSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.text)
            {
                int widthbyte = (cropedWindow.PixelWidth * cropedWindow.Format.BitsPerPixel + 7) / 8;
                int stride = ((widthbyte + 3) / 4) * 4;
                int bytePerPixel = cropedWindow.Format.BitsPerPixel / 8;
                byte[] data = new byte[stride * cropedWindow.PixelHeight];
                cropedWindow.CopyPixels(data, stride, 0);

                if (cropSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.binary8bit)
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
                else if (cropSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.binary64bit)
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
                else if (cropSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.text)
                {
                    double outData;
                    string format = "{0:0." + new string('#', Dispatcher.Invoke(new Func<int>(() => { return (int)cropSaveSettings.DecimalPlaces; }))) + "} ";
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
            else if (cropSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.jpeg)
            {
                using (var fileStream = new FileStream(fileName + ".jpg", FileMode.Create))
                {
                    BitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(cropedWindow));
                    encoder.Save(fileStream);
                }
            }
            else if (cropSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.bitmap)
            {
                using (var fileStream = new FileStream(fileName + ".bmp", FileMode.Create))
                {
                    BitmapEncoder encoder = new BmpBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(cropedWindow));
                    encoder.Save(fileStream);
                }
            }
            else if (cropSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.png)
            {
                using (var fileStream = new FileStream(fileName + ".png", FileMode.Create))
                {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(cropedWindow));
                    encoder.Save(fileStream);
                }
            }
        }
        #endregion

        #region Triggers
        public event EventHandler<StartedEventsArg> SampleCroppingStarted;
        protected virtual void OnSampleCroppingStarted(StartedEventsArg e)
        {
            SampleCroppingStarted?.Invoke(this, e);
        }

        public event EventHandler<ProgressingEventsArg> SampleCroppingProgressing;
        protected virtual void OnSampleCroppingProgressing(ProgressingEventsArg e)
        {
            SampleCroppingProgressing?.Invoke(this, e);
        }

        public event EventHandler<CompletionEventsArg> SampleCroppingCompletion;
        protected virtual void OnSampleCroppingCompletion(CompletionEventsArg e)
        {
            SampleCroppingCompletion?.Invoke(this, e);
        }
        #endregion

        #region Events
        private void SelectImagesFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalFunctions.SelectFolder(ref imagesFolderTextBox, "batchCropImageFolder") == System.Windows.Forms.DialogResult.OK)
            {
                statisticTextBox.Text = "";
                BuildImageFilesStatistic(imagesFolderTextBox.Text);
            }
        }

        private void SelectSaveFolderButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFolder(ref outputFolderTextBox, "batchCropSaveInFolder");
        }

        private void IncludeSubfolderCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (statisticTextBox != null)
            {
                statisticTextBox.Text = "";
                BuildImageFilesStatistic(imagesFolderTextBox.Text);
            }
        }

        private void CropRandomRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded == true)
            {
                cropSettingsGroupBox.Visibility = Visibility.Visible;

                minimumLabel.Visibility = Visibility.Visible;
                windowSizeUnitComboBox.Visibility = Visibility.Visible;
                minNumericUpDown.Visibility = Visibility.Visible;
                scalesRatioLabel.Visibility = Visibility.Visible;
                scalesLabel.Visibility = Visibility.Visible;
                scalesRatioNumericUpDown.Visibility = Visibility.Visible;
                scalesNumericUpDown.Visibility = Visibility.Visible;

                probabilityLabel.Visibility = Visibility.Hidden;
                probabilityNumericUpDown.Visibility = Visibility.Hidden;
                jumpingRatioLabel.Visibility = Visibility.Hidden;
                jumpingRatioNumericUpDown.Visibility = Visibility.Hidden;

                repetitionsLabel.Visibility = Visibility.Hidden;
                repetitionsNumericUpDown.Visibility = Visibility.Hidden;

                windowSizeUnitComboBox.IsEnabled = true;

                windowsCountLabel.Visibility = Visibility.Visible;
                windowsCountNumericUpDown.Visibility = Visibility.Visible;
                randomModeCheckBox.Visibility = Visibility.Visible;
                rescaleCheckBox.Visibility = Visibility.Visible;
                rescaleLabel.Visibility = Visibility.Hidden;
                rescaleNumericUpDown.Visibility = Visibility.Visible;
                rescaleNumericUpDown.IsEnabled = true;
                marginLabel.Visibility = Visibility.Visible;
                marginNumericUpDown.Visibility = Visibility.Visible;
                minimumWindowLabel.Visibility = Visibility.Hidden;
                maximumWindowLabel.Visibility = Visibility.Hidden;
                minimumWindowNumericUpDown.Visibility = Visibility.Hidden;
                maximumWindowNumericUpDown.Visibility = Visibility.Hidden;
                if (PKactive)
                    rescaleNumericUpDown.Value = rescalePrevValue;
                PKactive = false;
            }
        }

        private void CropRandomPKRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded == true)
            {
                cropSettingsGroupBox.Visibility = Visibility.Visible;

                minimumLabel.Visibility = Visibility.Visible;
                windowSizeUnitComboBox.Visibility = Visibility.Visible;
                minNumericUpDown.Visibility = Visibility.Visible;
                scalesRatioLabel.Visibility = Visibility.Visible;
                scalesLabel.Visibility = Visibility.Visible;
                scalesRatioNumericUpDown.Visibility = Visibility.Visible;
                scalesNumericUpDown.Visibility = Visibility.Visible;

                probabilityLabel.Visibility = Visibility.Hidden;
                probabilityNumericUpDown.Visibility = Visibility.Hidden;
                jumpingRatioLabel.Visibility = Visibility.Visible;
                jumpingRatioNumericUpDown.Visibility = Visibility.Visible;

                repetitionsLabel.Visibility = Visibility.Hidden;
                repetitionsNumericUpDown.Visibility = Visibility.Hidden;

                windowSizeUnitComboBox.SelectedItem = pxSizeComboBoxItem;
                windowSizeUnitComboBox.IsEnabled = false;

                windowsCountLabel.Visibility = Visibility.Visible;
                windowsCountNumericUpDown.Visibility = Visibility.Visible;
                randomModeCheckBox.Visibility = Visibility.Hidden;
                rescaleCheckBox.Visibility = Visibility.Hidden;
                rescaleLabel.Visibility = Visibility.Visible;
                rescaleNumericUpDown.Visibility = Visibility.Visible;
                rescaleNumericUpDown.IsEnabled = false;
                rescalePrevValue = rescaleNumericUpDown.Value;
                rescaleNumericUpDown.Value = 480;
                marginLabel.Visibility = Visibility.Visible;
                marginNumericUpDown.Visibility = Visibility.Visible;
                minimumWindowLabel.Visibility = Visibility.Hidden;
                maximumWindowLabel.Visibility = Visibility.Hidden;
                minimumWindowNumericUpDown.Visibility = Visibility.Hidden;
                maximumWindowNumericUpDown.Visibility = Visibility.Hidden;
                PKactive = true;
            }
        }

        private void CropWithProbabilityRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded == true)
            {
                cropSettingsGroupBox.Visibility = Visibility.Visible;

                minimumLabel.Visibility = Visibility.Visible;
                windowSizeUnitComboBox.Visibility = Visibility.Visible;
                minNumericUpDown.Visibility = Visibility.Visible;
                scalesRatioLabel.Visibility = Visibility.Visible;
                scalesLabel.Visibility = Visibility.Visible;
                scalesRatioNumericUpDown.Visibility = Visibility.Visible;
                scalesNumericUpDown.Visibility = Visibility.Visible;

                repetitionsLabel.Visibility = Visibility.Hidden;
                repetitionsNumericUpDown.Visibility = Visibility.Hidden;

                probabilityLabel.Visibility = Visibility.Visible;
                probabilityNumericUpDown.Visibility = Visibility.Visible;
                jumpingRatioLabel.Visibility = Visibility.Visible;
                jumpingRatioNumericUpDown.Visibility = Visibility.Visible;

                windowSizeUnitComboBox.IsEnabled = true;

                windowsCountLabel.Visibility = Visibility.Hidden;
                windowsCountNumericUpDown.Visibility = Visibility.Hidden;
                randomModeCheckBox.Visibility = Visibility.Hidden;
                rescaleCheckBox.Visibility = Visibility.Visible;
                rescaleLabel.Visibility = Visibility.Hidden;
                rescaleNumericUpDown.Visibility = Visibility.Visible;
                rescaleNumericUpDown.IsEnabled = true;
                marginLabel.Visibility = Visibility.Hidden;
                marginNumericUpDown.Visibility = Visibility.Hidden;
                minimumWindowLabel.Visibility = Visibility.Hidden;
                maximumWindowLabel.Visibility = Visibility.Hidden;
                minimumWindowNumericUpDown.Visibility = Visibility.Hidden;
                maximumWindowNumericUpDown.Visibility = Visibility.Hidden;
                if (PKactive)
                    rescaleNumericUpDown.Value = rescalePrevValue;
                PKactive = false;
            }
        }

        private void CropAtCoordinatesRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded == true)
            {
                cropSettingsGroupBox.Visibility = Visibility.Visible;

                minimumWindowLabel.Visibility = Visibility.Visible;
                maximumWindowLabel.Visibility = Visibility.Visible;
                minimumWindowNumericUpDown.Visibility = Visibility.Visible;
                maximumWindowNumericUpDown.Visibility = Visibility.Visible;

                minimumLabel.Visibility = Visibility.Hidden;
                windowSizeUnitComboBox.Visibility = Visibility.Hidden;
                minNumericUpDown.Visibility = Visibility.Hidden;
                scalesRatioLabel.Visibility = Visibility.Hidden;
                scalesLabel.Visibility = Visibility.Hidden;
                scalesRatioNumericUpDown.Visibility = Visibility.Hidden;
                scalesNumericUpDown.Visibility = Visibility.Hidden;

                repetitionsLabel.Visibility = Visibility.Hidden;
                repetitionsNumericUpDown.Visibility = Visibility.Hidden;

                probabilityLabel.Visibility = Visibility.Hidden;
                probabilityNumericUpDown.Visibility = Visibility.Hidden;
                jumpingRatioLabel.Visibility = Visibility.Hidden;
                jumpingRatioNumericUpDown.Visibility = Visibility.Hidden;
                windowsCountLabel.Visibility = Visibility.Hidden;
                windowsCountNumericUpDown.Visibility = Visibility.Hidden;
                randomModeCheckBox.Visibility = Visibility.Hidden;
                rescaleCheckBox.Visibility = Visibility.Hidden;
                rescaleLabel.Visibility = Visibility.Hidden;
                rescaleNumericUpDown.Visibility = Visibility.Hidden;
                rescaleNumericUpDown.IsEnabled = true;
                marginLabel.Visibility = Visibility.Hidden;
                marginNumericUpDown.Visibility = Visibility.Hidden;
                if (PKactive)
                    rescaleNumericUpDown.Value = rescalePrevValue;
                PKactive = false;
                windowSizeUnitComboBox.IsEnabled = false;
            }
        }

        private void CropHardNegativesRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded == true)
            {
                cropSettingsGroupBox.Visibility = Visibility.Visible;

                minimumWindowLabel.Visibility = Visibility.Visible;
                maximumWindowLabel.Visibility = Visibility.Hidden;
                minimumWindowNumericUpDown.Visibility = Visibility.Visible;
                maximumWindowNumericUpDown.Visibility = Visibility.Hidden;

                repetitionsLabel.Visibility = Visibility.Visible;
                repetitionsNumericUpDown.Visibility = Visibility.Visible;

                minimumLabel.Visibility = Visibility.Hidden;
                windowSizeUnitComboBox.Visibility = Visibility.Hidden;
                minNumericUpDown.Visibility = Visibility.Hidden;
                scalesRatioLabel.Visibility = Visibility.Visible;
                scalesLabel.Visibility = Visibility.Visible;
                scalesRatioNumericUpDown.Visibility = Visibility.Visible;
                scalesNumericUpDown.Visibility = Visibility.Visible;

                probabilityLabel.Visibility = Visibility.Hidden;
                probabilityNumericUpDown.Visibility = Visibility.Hidden;
                jumpingRatioLabel.Visibility = Visibility.Hidden;
                jumpingRatioNumericUpDown.Visibility = Visibility.Hidden;
                windowsCountLabel.Visibility = Visibility.Hidden;
                windowsCountNumericUpDown.Visibility = Visibility.Hidden;
                randomModeCheckBox.Visibility = Visibility.Hidden;
                rescaleCheckBox.Visibility = Visibility.Hidden;
                rescaleLabel.Visibility = Visibility.Hidden;
                rescaleNumericUpDown.Visibility = Visibility.Hidden;
                rescaleNumericUpDown.IsEnabled = true;
                marginLabel.Visibility = Visibility.Hidden;
                marginNumericUpDown.Visibility = Visibility.Hidden;
                if (PKactive)
                    rescaleNumericUpDown.Value = rescalePrevValue;
                PKactive = false;
                windowSizeUnitComboBox.IsEnabled = false;
            }
        }

        private void WindowSizeUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded == true)
            {
                if (windowSizeUnitComboBox.SelectedItem == pxSizeComboBoxItem)
                {
                    minNumericUpDown.Minimum = 48;
                    minNumericUpDown.Maximum = 10000;
                    minNumericUpDown.Value = minNumericUpDown.Value;
                }
                else
                {
                    minNumericUpDown.Minimum = 10;
                    minNumericUpDown.Maximum = 100;
                    minNumericUpDown.Value = minNumericUpDown.Value;
                }
            }
        }

        private void CropButton_Click(object sender, RoutedEventArgs e)
        {
            if (imagesFolderTextBox.Text == "" || !Directory.Exists(imagesFolderTextBox.Text))
            {
                MessageBox.Show("Images path is empty or doesn't exist.");
                return;
            }
            if (outputFolderTextBox.Text == "" || !Directory.Exists(outputFolderTextBox.Text))
            {
                MessageBox.Show("Output path is empty or doesn't exist.");
                return;
            }

            if (worker.IsBusy)
            {
                MessageBox.Show("Application is busy.");
                return;
            }

            samplesCount = 0;

            StartedEventsArg args = new StartedEventsArg("Status: Samples croping", "Samples croping started.", DateTime.Now, 0, true);
            OnSampleCroppingStarted(args);

            worker = new BackgroundWorker()
            {
                WorkerSupportsCancellation = true,
                WorkerReportsProgress = true
            };
            worker.DoWork += SamplesCropingBackgroundWorker_DoWork;
            worker.RunWorkerCompleted += SamplesCropingBackgroundWorker_RunWorkerCompleted;
            worker.ProgressChanged += SamplesCropingBackgroundWorker_ProgressChanged;
            worker.RunWorkerAsync();
        }

        private void SamplesCropingBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressingEventsArg args = new ProgressingEventsArg(e.ProgressPercentage);
            OnSampleCroppingProgressing(args);
        }

        private void SamplesCropingBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            ParallelOptions parOpt = new ParallelOptions()
            {
                MaxDegreeOfParallelism = (int)Properties.Settings.Default.tplThreads
            };

            List<string> fileList = new List<string>();

            bool includeSubFolder = false, keepFolderStructure = false;
            bool randomMode = false, randomPKMode = false, probabilityMode = false, coordMode = false, hardNegativesMode = false;
            string imagesPath = "", outputPath = "";
            Dispatcher.Invoke(new Action(() =>
            {
                includeSubFolder = (includeSubfolderCheckBox.IsChecked == null || includeSubfolderCheckBox.IsChecked == false) ? false : true;
                keepFolderStructure = (keepFolderStructureCheckBox.IsChecked == null || keepFolderStructureCheckBox.IsChecked == false) ? false : true;
                imagesPath = imagesFolderTextBox.Text;
                outputPath = outputFolderTextBox.Text;

                randomMode = (cropRandomRadioButton.IsChecked == null || cropRandomRadioButton.IsChecked == false) ? false : true;
                randomPKMode = (cropRandomPKRadioButton.IsChecked == null || cropRandomPKRadioButton.IsChecked == false) ? false : true;
                hardNegativesMode = (cropHardNegativesRadioButton.IsChecked == null || cropHardNegativesRadioButton.IsChecked == false) ? false : true;
                probabilityMode = (cropWithProbabilityRadioButton.IsChecked == null || cropWithProbabilityRadioButton.IsChecked == false) ? false : true;
                coordMode = (cropAtCoordinatesRadioButton.IsChecked == null || cropAtCoordinatesRadioButton.IsChecked == false) ? false : true;

                BuildImageFilesStatistic(imagesPath);
            }));

            Object lockMe = new Object();
            if (randomMode)
            {
                int windowsCount = Dispatcher.Invoke(new Func<int>(() => { return (int)windowsCountNumericUpDown.Value; }));

                int minWindowPercent = Dispatcher.Invoke(new Func<int>(() => { return (int)minNumericUpDown.Value; }));
                int scales = Dispatcher.Invoke(new Func<int>(() => { return (int)scalesNumericUpDown.Value; }));
                double scalingRatio = Dispatcher.Invoke(new Func<double>(() => { return (double)scalesRatioNumericUpDown.Value; }));
                bool linearMode = Dispatcher.Invoke(new Func<bool>(() => { return (randomModeCheckBox.IsChecked == null || randomModeCheckBox.IsChecked == false) ? false : true; }));
                bool scaleMode = Dispatcher.Invoke(new Func<bool>(() => { return (rescaleCheckBox.IsChecked == null || rescaleCheckBox.IsChecked == false) ? false : true; }));
                bool windowSizeInPixel = Dispatcher.Invoke(new Func<bool>(() => { return windowSizeUnitComboBox.SelectedItem == pxSizeComboBoxItem; }));
                int rescaledHeight = Dispatcher.Invoke(new Func<int>(() => { return (int)rescaleNumericUpDown.Value; }));
                double marginRatio = Dispatcher.Invoke(new Func<double>(() => { return (double)marginNumericUpDown.Value; }));

                GlobalFunctions.GetImageFiles(ref fileList, imagesPath, includeSubFolder);
                ProgressingEventsArg args = new ProgressingEventsArg(0, windowsCount + 1);
                OnSampleCroppingProgressing(args);

                double[] scalesProbability = new double[scales];
                if (linearMode)
                {
                    for (int s = 0; s < scales; s++)
                        scalesProbability[s] = 1.0 / scales;
                }
                else
                {

                    double[] windows = new double[scales];
                    windows[0] = 1;
                    double windowSum = 1;
                    for (int s = 1; s < scales; s++)
                    {
                        windows[s] = windows[s - 1] * Math.Pow(1.0 / scalingRatio, 2);
                        windowSum += windows[s];
                    }
                    for (int s = 0; s < scales; s++)
                    {
                        scalesProbability[s] = windows[s] / windowSum;
                    }
                }

                Dispatcher.Invoke(new Action(() =>
                {
                    statisticTextBox.Text += "\r\nScales probability: [";
                    for (int s = 0; s < scales - 1; s++)
                        statisticTextBox.Text += scalesProbability[s] + ", ";
                    statisticTextBox.Text += scalesProbability[scales - 1] + "]";
                }));

                List<System.Drawing.Rectangle>[] posWindowsList = new List<System.Drawing.Rectangle>[fileList.Count];
                Parallel.For(0, fileList.Count, (int f) =>
                {
                    posWindowsList[f] = new List<System.Drawing.Rectangle>();
                    using (var image = new System.Drawing.Bitmap(fileList[f]))
                    {
                        int height = image.Height;
                        int width = image.Width;

                        double scale = 1.0;
                        if (scaleMode)
                            scale = (double)rescaledHeight / height;

                        string coordsFileName = fileList[f] + ".txt";
                        FileInfo fileInfo = new FileInfo(coordsFileName);
                        if (fileInfo.Exists)
                        {
                            using (StreamReader sr = new StreamReader(coordsFileName))
                            {
                                string line;
                                while ((line = sr.ReadLine()) != null)
                                {
                                    line = line.Trim().Replace("   ", " ");
                                    string[] values = line.Split(' ');
                                    int xp = (int)double.Parse(values[0]);
                                    int yp = (int)double.Parse(values[1]);
                                    int size = (int)double.Parse(values[2]);
                                    if (scaleMode)
                                    {
                                        xp = (int)(xp * scale);
                                        yp = (int)(yp * scale);
                                        size = (int)(size * scale);
                                    }
                                    int margin = (int)(marginRatio * size);

                                    posWindowsList[f].Add(new System.Drawing.Rectangle(xp + margin, yp + margin, size - (2 * margin), size - (2 * margin)));
                                }
                            }
                        }
                    }
                });

                Parallel.For(0, windowsCount, (long w) =>
                 {
                     try
                     {
                         int filesCount = fileList.Count;
                         int fileNumber = StaticRandom.Next(0, filesCount);
                         string fileName = fileList[fileNumber];

                         FileInfo fileInfo = new FileInfo(fileName);
                         if (!fileInfo.Exists)
                             throw new Exception(fileName + " doesn't exist.");

                         string path;
                         if (keepFolderStructure == true && fileInfo.Directory.FullName.Length + 1 != imagesPath.Length)
                         {
                             string folder = outputPath + fileInfo.Directory.FullName.Substring(imagesPath.Length);
                             if (!Directory.Exists(folder))
                                 Directory.CreateDirectory(folder);
                             path = folder + "\\";
                         }
                         else
                             path = outputPath + "\\";

                         BitmapSource bmp = new BitmapImage(new Uri(fileName, UriKind.Relative));
                         if (bmp.Format != PixelFormats.Bgr24)
                             bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgr24, null, 0);
                         double scale = 1.0;
                         if (scaleMode)
                         {
                             scale = (double)rescaledHeight / bmp.PixelHeight;
                             bmp = new TransformedBitmap(bmp, new ScaleTransform(scale, scale));
                         }
                         bmp.Freeze();

                         int nx = bmp.PixelWidth;
                         int ny = bmp.PixelHeight;

                         int minWindow = 48;
                         if (windowSizeInPixel)
                         {
                             minWindow = minWindowPercent;
                         }
                         else
                         {
                             int minN = Math.Min(nx, ny);
                             minWindow = (int)(minN * (minWindowPercent / 100.0));
                         }

                         double scaleTest = StaticRandom.NextDouble();
                         int scaleNumber = 0;
                         double threshold = 0;
                         for (int s = scales - 1; s >= 0; s--)
                         {
                             if (scaleTest < scalesProbability[s] + threshold)
                             {
                                 scaleNumber = s;
                                 break;
                             }
                             threshold += scalesProbability[s];
                         }

                         int wx = (int)Math.Round(Math.Pow(scalingRatio, scaleNumber) * minWindow);
                         int wy = (int)Math.Round(Math.Pow(scalingRatio, scaleNumber) * minWindow);
                         if (wx > nx) wx = nx;
                         if (wy > ny) wy = ny;

                         wx = wy = Math.Min(wx, wy);
                         if (wx % 2 == 1)
                             wx = wy = wx - 1;

                         List<System.Drawing.Rectangle> posWindows = posWindowsList[fileNumber];
                         bool repeat = true;
                         int x = 0, y = 0;
                         while (repeat)
                         {
                             repeat = false;
                             x = StaticRandom.Next(0, nx - wx);
                             y = StaticRandom.Next(0, ny - wy);

                             System.Drawing.Rectangle currentWindow = new System.Drawing.Rectangle(x, y, wx, wy);
                             foreach (System.Drawing.Rectangle window in posWindows)
                             {
                                 if (window.IntersectsWith(currentWindow))
                                 {
                                     repeat = true;
                                     break;
                                 }
                             }
                         }

                         BitmapSource cropedWindow = new CroppedBitmap(bmp, new Int32Rect(x, y, wx, wy));
                         SaveWindow(ref cropedWindow, path, "_" + w + "_" + fileNumber + "_" + scaleNumber + "_" + wx + "_" + wy + "_" + x + "_" + y);

                         lock (lockMe)
                         {
                             worker.ReportProgress(++samplesCount);

                             if (this.worker.CancellationPending)
                             {
                                 e.Cancel = true;
                                 return;
                             }
                         }
                     }
                     catch (Exception ex)
                     {
                         lock (lockMe)
                         {
                             ProgressingEventsArg errArgs = new ProgressingEventsArg(samplesCount, -1, ex.Message);
                             OnSampleCroppingProgressing(errArgs);

                             if (worker.CancellationPending)
                             {
                                 e.Cancel = true;
                                 return;
                             }
                         }
                     }
                 });
            }
            else if (randomPKMode)
            {
                int windowsCount = Dispatcher.Invoke(new Func<int>(() => { return (int)windowsCountNumericUpDown.Value; }));

                int minWindowPercent = Dispatcher.Invoke(new Func<int>(() => { return (int)minNumericUpDown.Value; }));
                int scales = Dispatcher.Invoke(new Func<int>(() => { return (int)scalesNumericUpDown.Value; }));
                double scalingRatio = Dispatcher.Invoke(new Func<double>(() => { return (double)scalesRatioNumericUpDown.Value; }));
                double jumpingRatio = Dispatcher.Invoke(new Func<double>(() => { return (double)jumpingRatioNumericUpDown.Value; }));
                int rescaledHeight = Dispatcher.Invoke(new Func<int>(() => { return (int)rescaleNumericUpDown.Value; }));
                double marginRatio = Dispatcher.Invoke(new Func<double>(() => { return (double)marginNumericUpDown.Value; }));

                GlobalFunctions.GetImageFiles(ref fileList, imagesPath, includeSubFolder);
                ProgressingEventsArg args = new ProgressingEventsArg(0, windowsCount + 1);
                OnSampleCroppingProgressing(args);

                int nx = 640;
                int ny = 480;

                double[] distribution = new double[scales];
                int winCount = 0;
                for (int s = 0; s < scales; s++)
                {
                    int wx = (int)Math.Round(Math.Pow(scalingRatio, s) * minWindowPercent);
                    int wy = (int)Math.Round(Math.Pow(scalingRatio, s) * minWindowPercent);
                    if (wx > nx) wx = nx;
                    if (wy > ny) wy = ny;
                    int W = wx - wx % 2; // must be even for ZMs

                    int dx = (int)Math.Round(jumpingRatio * wx);
                    int dy = (int)Math.Round(jumpingRatio * wy);

                    // scanning loops
                    int xHalfRemainder = ((nx - wx) % dx) / 2;
                    int xMin = xHalfRemainder; // min anchoring point for window of width wx
                    int xMax = nx - wx - xHalfRemainder; // max anchoring point for window of width wx
                    int yHalfRemainder = ((ny - wy) % dy) / 2;
                    int yMin = yHalfRemainder; // min anchoring point for window of width wy
                    int yMax = ny - wy - yHalfRemainder; // max anchoring point for window of width wy

                    int xIterations = (xMax - xMin) / dx + 1;
                    int yIterations = (yMax - yMin) / dy + 1;
                    distribution[s] = xIterations * yIterations;
                    winCount += (int)distribution[s];
                }

                for (int s = 0; s < scales; s++)
                    distribution[s] /= (double)winCount;

                Dispatcher.Invoke(new Action(() =>
                {
                    statisticTextBox.Text += "\r\nScales distribution: [";
                    for (int s = 0; s < scales - 1; s++)
                        statisticTextBox.Text += distribution[s] + ", ";
                    statisticTextBox.Text += distribution[scales - 1] + "]";
                }));

                List<System.Drawing.Rectangle>[] posWindowsList = new List<System.Drawing.Rectangle>[fileList.Count];
                Parallel.For(0, fileList.Count, (int f) =>
                {
                    posWindowsList[f] = new List<System.Drawing.Rectangle>();
                    using (var image = new System.Drawing.Bitmap(fileList[f]))
                    {
                        int height = image.Height;
                        int width = image.Width;

                        double scale = (double)rescaledHeight / height;

                        string coordsFileName = fileList[f] + ".txt";
                        FileInfo fileInfo = new FileInfo(coordsFileName);
                        if (fileInfo.Exists)
                        {
                            using (StreamReader sr = new StreamReader(coordsFileName))
                            {
                                string line;
                                while ((line = sr.ReadLine()) != null)
                                {
                                    line = line.Trim().Replace("   ", " ");
                                    string[] values = line.Split(' ');
                                    int xp = (int)double.Parse(values[0]);
                                    int yp = (int)double.Parse(values[1]);
                                    int size = (int)double.Parse(values[2]);

                                        xp = (int)(xp * scale);
                                        yp = (int)(yp * scale);
                                        size = (int)(size * scale);
                                    int margin = (int)(marginRatio * size);

                                    posWindowsList[f].Add(new System.Drawing.Rectangle(xp + margin, yp + margin, size - (2 * margin), size - (2 * margin)));
                                }
                            }
                        }
                    }
                });

                Parallel.For(0, windowsCount, (long w) =>
                {
                    try
                    {
                        int filesCount = fileList.Count;
                        int fileNumber = StaticRandom.Next(0, filesCount);
                        string fileName = fileList[fileNumber];

                        FileInfo fileInfo = new FileInfo(fileName);
                        if (!fileInfo.Exists)
                            throw new Exception(fileName + " doesn't exist.");

                        string path;
                        if (keepFolderStructure == true && fileInfo.Directory.FullName.Length + 1 != imagesPath.Length)
                        {
                            string folder = outputPath + fileInfo.Directory.FullName.Substring(imagesPath.Length);
                            if (!Directory.Exists(folder))
                                Directory.CreateDirectory(folder);
                            path = folder + "\\";
                        }
                        else
                            path = outputPath + "\\";

                        BitmapSource bmp = new BitmapImage(new Uri(fileName, UriKind.Relative));
                        if (bmp.Format != PixelFormats.Bgr24)
                            bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgr24, null, 0);

                        double scale = (double)rescaledHeight / bmp.PixelHeight;
                        bmp = new TransformedBitmap(bmp, new ScaleTransform(scale, scale));
                        bmp.Freeze();

                        nx = bmp.PixelWidth;
                        ny = bmp.PixelHeight;

                        int minWindow = minWindowPercent;

                        double scaleTest = StaticRandom.NextDouble();
                        int scaleNumber = 0;
                        double threshold = 0;
                        for (int s = scales - 1; s >= 0; s--)
                        {
                            if (scaleTest < distribution[s] + threshold)
                            {
                                scaleNumber = s;
                                break;
                            }
                            threshold += distribution[s];
                        }

                        int wx = (int)Math.Round(Math.Pow(scalingRatio, scaleNumber) * minWindow);
                        int wy = (int)Math.Round(Math.Pow(scalingRatio, scaleNumber) * minWindow);
                        if (wx > nx) wx = nx;
                        if (wy > ny) wy = ny;

                        wx = wy = Math.Min(wx, wy);
                        if (wx % 2 == 1)
                            wx = wy = wx - 1;

                        List<System.Drawing.Rectangle> posWindows = posWindowsList[fileNumber];

                        bool repeat = true;
                        int x = 0, y = 0;
                        while (repeat)
                        {
                            repeat = false;
                            x = StaticRandom.Next(0, nx - wx);
                            y = StaticRandom.Next(0, ny - wy);

                            System.Drawing.Rectangle currentWindow = new System.Drawing.Rectangle(x, y, wx, wy);
                            foreach (System.Drawing.Rectangle window in posWindows)
                            {
                                if (window.IntersectsWith(currentWindow))
                                {
                                    repeat = true;
                                    break;
                                }
                            }
                        }

                        BitmapSource cropedWindow = new CroppedBitmap(bmp, new Int32Rect(x, y, wx, wy));
                        SaveWindow(ref cropedWindow, path, "_" + w + "_" + fileNumber + "_" + scaleNumber + "_" + wx + "_" + wy + "_" + x + "_" + y);

                        lock (lockMe)
                        {
                            worker.ReportProgress(++samplesCount);

                            if (this.worker.CancellationPending)
                            {
                                e.Cancel = true;
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (lockMe)
                        {
                            ProgressingEventsArg errArgs = new ProgressingEventsArg(samplesCount, -1, ex.Message);
                            OnSampleCroppingProgressing(errArgs);

                            if (worker.CancellationPending)
                            {
                                e.Cancel = true;
                                return;
                            }
                        }
                    }
                });
            }
            else if (probabilityMode)
            {
                int processedFiles = 0;
                int minWindowPercent = Dispatcher.Invoke(new Func<int>(() => { return (int)minNumericUpDown.Value; }));
                int scales = Dispatcher.Invoke(new Func<int>(() => { return (int)scalesNumericUpDown.Value; }));
                double jumpingRatio = Dispatcher.Invoke(new Func<double>(() => { return (double)jumpingRatioNumericUpDown.Value; }));
                double scalingRatio = Dispatcher.Invoke(new Func<double>(() => { return (double)scalesRatioNumericUpDown.Value; }));
                double probability = Dispatcher.Invoke(new Func<double>(() => { return (double)probabilityNumericUpDown.Value; }));
                bool scaleMode = Dispatcher.Invoke(new Func<bool>(() => { return (rescaleCheckBox.IsChecked == null || rescaleCheckBox.IsChecked == false) ? false : true; }));
                bool windowSizeInPixel = Dispatcher.Invoke(new Func<bool>(() => { return windowSizeUnitComboBox.SelectedItem == pxSizeComboBoxItem; }));
                int rescaledHeight = Dispatcher.Invoke(new Func<int>(() => { return (int)rescaleNumericUpDown.Value; }));

                GlobalFunctions.GetImageFiles(ref fileList, imagesPath, includeSubFolder);
                ProgressingEventsArg args = new ProgressingEventsArg(0, fileList.Count());
                OnSampleCroppingProgressing(args);

                Parallel.ForEach(fileList, parOpt, (string file) =>
                {
                    try
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        if (!fileInfo.Exists)
                            throw new Exception(file + " doesn't exist.");

                        string path;
                        if (keepFolderStructure == true && fileInfo.Directory.FullName.Length + 1 != imagesPath.Length)
                        {
                            string folder = outputPath + fileInfo.Directory.FullName.Substring(imagesPath.Length);
                            if (!Directory.Exists(folder))
                                Directory.CreateDirectory(folder);
                            path = folder + "\\";
                        }
                        else
                            path = outputPath + "\\";

                        BitmapSource bmp = new BitmapImage(new Uri(file, UriKind.Relative));
                        if (bmp.Format != PixelFormats.Bgr24)
                            bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgr24, null, 0);
                        if (scaleMode)
                        {
                            double scale = (double)rescaledHeight / bmp.PixelHeight;
                            bmp = new TransformedBitmap(bmp, new ScaleTransform(scale, scale));
                        }
                        bmp.Freeze();

                        int nx = bmp.PixelWidth;
                        int ny = bmp.PixelHeight;

                        int minWindow;
                        if (windowSizeInPixel)
                        {
                            minWindow = minWindowPercent;
                        }
                        else
                        {
                            int minN = Math.Min(nx, ny);
                            minWindow = (int)(minN * (minWindowPercent / 100.0));
                        }

                        for (int s = 0; s < scales; s++)
                        {
                            int wx = (int)Math.Round(Math.Pow(scalingRatio, s) * minWindow);
                            int wy = (int)Math.Round(Math.Pow(scalingRatio, s) * minWindow);
                            if (wx > nx) wx = nx;
                            if (wy > ny) wy = ny;

                            wx = wy = Math.Min(wx, wy);
                            if (wx % 2 == 1)
                                wx = wy = wx - 1;

                            int dx = (int)Math.Round(jumpingRatio * wx);
                            int dy = (int)Math.Round(jumpingRatio * wy);

                            int xHalfRemainder = ((nx - wx) % dx) / 2;
                            int xMin = xHalfRemainder; // min anchoring point for window of width wx
                            int xMax = nx - wx - xHalfRemainder; // max anchoring point for window of width wx
                            int yHalfRemainder = ((ny - wy) % dy) / 2;
                            int yMin = yHalfRemainder; // min anchoring point for window of width wy
                            int yMax = ny - wy - yHalfRemainder; // max anchoring point for window of width wy

                            for (int x = xMin; x <= xMax; x += dx)
                            {
                                for (int y = yMin; y <= yMax; y += dy)
                                {
                                    if (StaticRandom.NextDouble() <= probability)
                                    {
                                        BitmapSource cropedWindow = new CroppedBitmap(bmp, new Int32Rect(x, y, wx, wy));
                                        SaveWindow(ref cropedWindow, path);

                                        Interlocked.Increment(ref samplesCount);
                                    }
                                }
                            }
                        }

                        lock (lockMe)
                        {
                            worker.ReportProgress(++processedFiles);

                            if (this.worker.CancellationPending)
                            {
                                e.Cancel = true;
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (lockMe)
                        {
                            ProgressingEventsArg errArgs = new ProgressingEventsArg(processedFiles, -1, ex.Message);
                            OnSampleCroppingProgressing(errArgs);

                            if (worker.CancellationPending)
                            {
                                e.Cancel = true;
                                return;
                            }
                        }
                    }
                });

                if (processedFiles != fileList.Count)
                {
                    throw new Exception("Not all files were accessible.");
                }
            }
            else if (hardNegativesMode)
            {
                int minWidth = Dispatcher.Invoke(new Func<int>(() => { return (int)minimumWindowNumericUpDown.Value; }));
                int repetitions = Dispatcher.Invoke(new Func<int>(() => { return (int)repetitionsNumericUpDown.Value; }));
                int scales = Dispatcher.Invoke(new Func<int>(() => { return (int)scalesNumericUpDown.Value; }));
                int scalesRatio = Dispatcher.Invoke(new Func<int>(() => { return (int)scalesRatioNumericUpDown.Value; }));

                int processedFiles = 0;

                double windowScalingRatioHalf = 1.0 + 0.5 * (scalesRatio - 1.0);
                int windowMinimalWidthMinusMargin = (int)Math.Round(minWidth * Math.Pow(windowScalingRatioHalf, -1));
                const double sideSmallerMin = 0.3; //0.25
                const double sideSmallerRange = 0.5 - sideSmallerMin; //0.5 - 
                const double placementRange = 1.25; // as a proportion to side
                int sideSmaller;

                Random random = new Random(0); // reseeding to 0

                GlobalFunctions.GetImageFiles(ref fileList, imagesPath, includeSubFolder);
                ProgressingEventsArg args = new ProgressingEventsArg(0, fileList.Count());
                OnSampleCroppingProgressing(args);

                Parallel.ForEach(fileList, parOpt, (string file) =>
                {
                    try
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        if (!fileInfo.Exists)
                            throw new Exception(file + " doesn't exist.");

                        string path;
                        if (keepFolderStructure == true && fileInfo.Directory.FullName.Length + 1 != imagesPath.Length)
                        {
                            string folder = outputPath + fileInfo.Directory.FullName.Substring(imagesPath.Length);
                            if (!Directory.Exists(folder))
                                Directory.CreateDirectory(folder);
                            path = folder + "\\";
                        }
                        else
                            path = outputPath + "\\";

                        fileInfo = new FileInfo(file + ".txt");
                        if (fileInfo.Exists)
                        {
                            BitmapSource bmp = new BitmapImage(new Uri(file, UriKind.Relative));
                            if (bmp.Format != PixelFormats.Bgr24)
                                bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgr24, null, 0);
                            bmp.Freeze();

                            int errWindow = 0;
                            using (StreamReader sr = new StreamReader(file + ".txt"))
                            {
                                string line;
                                List<System.Drawing.Rectangle> windows = new List<System.Drawing.Rectangle> ();
                                while ((line = sr.ReadLine()) != null)
                                {
                                    try
                                    {
                                        string[] values = line.Split(' ');

                                        int x = (int)double.Parse(values[0]);
                                        int y = (int)double.Parse(values[1]);
                                        int w = (int)double.Parse(values[2]);

                                        windows.Add(new System.Drawing.Rectangle(x, y, w, w));
                                    }
                                    catch
                                    {
                                        errWindow++;
                                    }
                                }

                                foreach (System.Drawing.Rectangle window in windows)
                                { 
                                    try
                                    {
                                        for (int z = 0; z < repetitions; z++)
                                        {
                                            int side = window.Width; 
                                            double xc = window.X + 0.5 * side;
                                            double yc = window.Y + 0.5 * side;

                                            int xNew= 0, yNew =0;
                                            bool ifCropped = true;
                                            while (true)
                                            {
                                                sideSmaller = (int)(side * (sideSmallerMin + random.NextDouble() * sideSmallerRange));
                                                if (sideSmaller < windowMinimalWidthMinusMargin)
                                                {
                                                    //if ((int)(side * (sideSmallerMin + sideSmallerRange)) < windowMinimalWidthMinusMargin)
                                                    ifCropped = false;
                                                    break;
                                                    //sideSmaller = windowMinimalWidthMinusMargin; // because even with maximum randomized sideSmaller would not be sufficient
                                                    //else
                                                    //    continue;
                                                }

                                                double minX = xc - 0.5 * placementRange * side;
                                                double minY = yc - 0.5 * placementRange * side;

                                                xNew = (int)(minX + random.NextDouble() * (placementRange * side - sideSmaller + 1));
                                                yNew = (int)(minY + random.NextDouble() * (placementRange * side - sideSmaller + 1));

                                                if (!((xNew < 0) || (xNew + sideSmaller > bmp.PixelWidth) || (yNew < 0) || (yNew + sideSmaller > bmp.PixelHeight)))
                                                    break;
                                            }

                                            if (ifCropped)
                                            {
                                                System.Drawing.Rectangle negativeWindow = new System.Drawing.Rectangle(xNew, yNew, sideSmaller, sideSmaller);

                                                bool colide = false;
                                                foreach (System.Drawing.Rectangle windowToCheck in windows)
                                                {
                                                    if (windowToCheck.Width < window.Width)
                                                    {
                                                        System.Drawing.Rectangle intersection = System.Drawing.Rectangle.Intersect(windowToCheck, negativeWindow);
                                                        double windowField = windowToCheck.Width * windowToCheck.Height;
                                                        double intersectionField = intersection.Width * intersection.Height;
                                                        if (intersectionField / (double)windowField > 0.5)
                                                            colide = true;
                                                    }
                                                }

                                                if (!colide)
                                                {
                                                    Int32Rect negative = new Int32Rect(xNew, yNew, sideSmaller, sideSmaller);
                                                    BitmapSource cropedWindow = new CroppedBitmap(bmp, negative);
                                                    SaveWindow(ref cropedWindow, path);
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        errWindow++;
                                    }
                                }
                            }
                            if (errWindow > 0)
                                throw new Exception("Image " + Path.GetFileName(file) + " windows coordinate excede image size. Bad windows count: " + errWindow);
                        }
                        else
                            throw new Exception("Image " + Path.GetFileName(file) + " does not have file with object coordinates included.");

                        lock (lockMe)
                        {
                            worker.ReportProgress(++processedFiles);

                            if (this.worker.CancellationPending)
                            {
                                e.Cancel = true;
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (lockMe)
                        {
                            ProgressingEventsArg errArgs = new ProgressingEventsArg(processedFiles, -1, ex.Message);
                            OnSampleCroppingProgressing(errArgs);

                            if (worker.CancellationPending)
                            {
                                e.Cancel = true;
                                return;
                            }
                        }
                    }
                });

                if (processedFiles != fileList.Count)
                {
                    throw new Exception("Not all files were accessible.");
                }
            }
            else
            {
                int minWidth = Dispatcher.Invoke(new Func<int>(() => { return (int)minimumWindowNumericUpDown.Value; }));
                int maxWidth = Dispatcher.Invoke(new Func<int>(() => { return (int)maximumWindowNumericUpDown.Value; }));

                int processedFiles = 0;

                GlobalFunctions.GetImageFiles(ref fileList, imagesPath, includeSubFolder);
                ProgressingEventsArg args = new ProgressingEventsArg(0, fileList.Count());
                OnSampleCroppingProgressing(args);

                Parallel.ForEach(fileList, parOpt, (string file) =>
                {
                    try
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        if (!fileInfo.Exists)
                            throw new Exception(file + " doesn't exist.");

                        string path;
                        if (keepFolderStructure == true && fileInfo.Directory.FullName.Length + 1 != imagesPath.Length)
                        {
                            string folder = outputPath + fileInfo.Directory.FullName.Substring(imagesPath.Length);
                            if (!Directory.Exists(folder))
                                Directory.CreateDirectory(folder);
                            path = folder + "\\";
                        }
                        else
                            path = outputPath + "\\";

                        fileInfo = new FileInfo(file + ".txt");
                        if (fileInfo.Exists)
                        {
                            BitmapSource bmp = new BitmapImage(new Uri(file, UriKind.Relative));
                            if (bmp.Format != PixelFormats.Bgr24)
                                bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgr24, null, 0);
                            bmp.Freeze();

                            int errWindow = 0;
                            using (StreamReader sr = new StreamReader(file + ".txt"))
                            {
                                string line;
                                while ((line = sr.ReadLine()) != null)
                                {
                                    line = line.Trim().Replace("   ", " ");
                                    try
                                    {
                                        string[] values = line.Split(' ');
                                        Int32Rect window = new Int32Rect((int)double.Parse(values[0]), (int)double.Parse(values[1]), (int)double.Parse(values[2]), (int)double.Parse(values[2]));

                                        if (window.Width > minWidth && window.Height > minWidth)
                                        {
                                            BitmapSource cropedWindow = new CroppedBitmap(bmp, window);
                                            if (window.Width > maxWidth || window.Height > maxWidth)
                                            {
                                                int oldMax = Math.Max(window.Width, window.Height);
                                                double scale = (double)maxWidth / oldMax;
                                                cropedWindow = new TransformedBitmap(cropedWindow, new ScaleTransform(scale, scale));
                                            }
                                            SaveWindow(ref cropedWindow, path);

                                            Interlocked.Increment(ref samplesCount);
                                        }
                                    }
                                    catch
                                    {
                                        errWindow++;
                                    }
                                }
                            }
                            if(errWindow > 0)
                                throw new Exception("Image " + Path.GetFileName(file) + " windows coordinate excede image size. Bad windows count: " + errWindow);
                        }
                        else
                            throw new Exception("Image " + Path.GetFileName(file) + " does not have file with object coordinates included.");

                        lock (lockMe)
                        {
                            worker.ReportProgress(++processedFiles);

                            if (this.worker.CancellationPending)
                            {
                                e.Cancel = true;
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (lockMe)
                        {
                            ProgressingEventsArg errArgs = new ProgressingEventsArg(processedFiles, -1, ex.Message);
                            OnSampleCroppingProgressing(errArgs);

                            if (worker.CancellationPending)
                            {
                                e.Cancel = true;
                                return;
                            }
                        }
                    }
                });

                if (processedFiles != fileList.Count)
                {
                    throw new Exception("Not all files were accessible.");
                }
            }

            stopwatch.Stop();
            operationTime = stopwatch.ElapsedMilliseconds;
        }

        private void SamplesCropingBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
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
                logMessage = "Croping completed with errors: " + samplesCount + " samples created.";
                error = e.Error.Message;
                statisticTextBox.Text += "\r\n" + samplesCount + " samples created." + "\r\n" + e.Error.Message;
            }
            else
            {
                statusLabel = "Status: Croping completed successful. Check event log for details.";
                logMessage = "Croping completed successful. Elapsed time: " + operationTime + "ms. Created samples: " + samplesCount;
                statisticTextBox.Text += "\r\n" + samplesCount + " samples created.";
            }

            CompletionEventsArg args = new CompletionEventsArg(statusLabel, logMessage, error, DateTime.Now, false);
            OnSampleCroppingCompletion(args);
        }

        #endregion

        
    }
}
