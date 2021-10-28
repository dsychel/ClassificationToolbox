using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.ComponentModel;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Reflection;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for FeatureExtractorImages.xaml
    /// </summary>
    public partial class FeatureExtractorImages : UserControl
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
        int filesCount = 0;
        decimal rescalePrevValue = 480;
        bool PKactive = false;
        #endregion

        public FeatureExtractorImages()
        {
            InitializeComponent();

            GlobalFunctions.InitializeDirectory(ref imagesFolderTextBox, "featureExtractionImageFolder");

            BuildImageFilesStatistic(Properties.Settings.Default.featureExtractionImageFolder);
        }

        #region Triggers
        public event EventHandler<StartedEventsArg> ExtractionStarted;
        protected virtual void OnExtractionStarted(StartedEventsArg e)
        {
            ExtractionStarted?.Invoke(this, e);
        }

        public event EventHandler<ProgressingEventsArg> ExtractionProgressing;
        protected virtual void OnExtractionProgressing(ProgressingEventsArg e)
        {
            ExtractionProgressing?.Invoke(this, e);
        }

        public event EventHandler<CompletionEventsArg> ExtractionCompleted;
        protected virtual void OnExtractionCompletion(CompletionEventsArg e)
        {
            ExtractionCompleted?.Invoke(this, e);
        }
        #endregion

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
        #endregion

        #region Events
        private void SelectImagesFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalFunctions.SelectFolder(ref imagesFolderTextBox, "featureExtractionImageFolder") == System.Windows.Forms.DialogResult.OK)
            {
                statisticTextBox.Text = "";
                BuildImageFilesStatistic(imagesFolderTextBox.Text);
            }
        }

        private void SelectFeatureFileButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog()
            {
                Filter = "Features Files (*.fet.bin) | *.fet.bin",
                FileName = "",
                InitialDirectory = Properties.Settings.Default.featureImageSaveAs
            };
            saveFileDialog.FileName = extractorSettings.ExtractorFileName;

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                featureSaveAsTextBox.Text = saveFileDialog.FileName;

                Properties.Settings.Default.featureImageSaveAs = Path.GetDirectoryName(saveFileDialog.FileName);
                if (Properties.Settings.Default.featureImageSaveAs[Properties.Settings.Default.featureImageSaveAs.Length - 1] != '\\')
                    Properties.Settings.Default.featureImageSaveAs += '\\';
                Properties.Settings.Default.Save();
            }
            saveFileDialog.Dispose();
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
                classLabel.Visibility = Visibility.Hidden;
                classNumericUpDown.Visibility = Visibility.Hidden;

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
                classLabel.Visibility = Visibility.Hidden;
                classNumericUpDown.Visibility = Visibility.Hidden;

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
                classLabel.Visibility = Visibility.Hidden;
                classNumericUpDown.Visibility = Visibility.Hidden;

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
                classLabel.Visibility = Visibility.Visible;
                classNumericUpDown.Visibility = Visibility.Visible;

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
                classLabel.Visibility = Visibility.Hidden;
                classNumericUpDown.Visibility = Visibility.Hidden;

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

        private void ClassNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            if ((decimal)e.CurrentValue == 0 && (decimal)e.PreviousValue == 1)
                classNumericUpDown.Value = -1;
            else if ((decimal)e.CurrentValue == 0 && (decimal)e.PreviousValue == -1)
                classNumericUpDown.Value = 1;
        }

        private void ExtractionButton_Click(object sender, RoutedEventArgs e)
        {
            if (imagesFolderTextBox.Text == "" || !Directory.Exists(imagesFolderTextBox.Text))
            {
                MessageBox.Show("Images path is empty or doesn't exist.");
                return;
            }
            if (featureSaveAsTextBox.Text == "")
            {
                MessageBox.Show("Save path is empty or doesn't exist.");
                return;
            }
            if (extractorSettings.CheckFileName(featureSaveAsTextBox.Text) == MessageBoxResult.No)
            {
                return;
            }

            if (worker != null && worker.IsBusy)
            {
                MessageBox.Show("Application is busy.");
                return;
            }

            string message = "Extraction started: " + extractorSettings.ExtractorDescription;
            samplesCount = 0;

            StartedEventsArg args = new StartedEventsArg("Status: Working", message, DateTime.Now, 0, true);
            OnExtractionStarted(args);

            int jpgs = 0, bmps = 0, pngs = 0;
            CheckFolder(imagesFolderTextBox.Text, ref jpgs, ref bmps, ref pngs);
            filesCount = bmps + jpgs + pngs;
            BuildImageFilesStatistic(imagesFolderTextBox.Text);

            if (filesCount == 0)
            {
                string statusLabel = "Status: Extraction completed with errors. Check event log for details";
                string logMessage = "Extraction completed with errors.";
                bool shutdown = false; //(shutdownExtractionCheckBox.IsChecked == null || shutdownExtractionCheckBox.IsChecked == false) ? false : true;
                CompletionEventsArg endArgs = new CompletionEventsArg(statusLabel, logMessage, "No images detected.", DateTime.Now, shutdown);
                OnExtractionCompletion(endArgs);
            }
            else
            {
                ProgressingEventsArg progressArgs = new ProgressingEventsArg(0, filesCount);
                OnExtractionProgressing(progressArgs);

                worker = new BackgroundWorker()
                {
                    WorkerSupportsCancellation = true,
                    WorkerReportsProgress = true
                };
                worker.DoWork += ExtractionBackgroundWorker_DoWork;
                worker.RunWorkerCompleted += ExtractionBackgroundWorker_RunWorkerCompleted;
                worker.ProgressChanged += ExtractionBackgroundWorker_ProgressChanged;
                worker.RunWorkerAsync();
            }
        }

        private void ExtractionBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressingEventsArg args = new ProgressingEventsArg(e.ProgressPercentage);
            OnExtractionProgressing(args);
        }

        unsafe private void ExtractionBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            ParallelOptions parOpt = new ParallelOptions()
            {
                MaxDegreeOfParallelism = (int)Properties.Settings.Default.tplThreads
            };

            List<string> fileList = new List<string>();
            bool includeSubFolder = false;
            bool randomMode = false, randomPKMode = false, probabilityMode = false, coordMode = false, hardNegativesMode = false;
            string imagesPath = "", savePath = "";
            Dispatcher.Invoke(new Action(() =>
            {
                includeSubFolder = (includeSubfolderCheckBox.IsChecked == null || includeSubfolderCheckBox.IsChecked == false) ? false : true;
                imagesPath = imagesFolderTextBox.Text;
                savePath = featureSaveAsTextBox.Text;

                randomMode = (cropRandomRadioButton.IsChecked == null || cropRandomRadioButton.IsChecked == false) ? false : true;
                randomPKMode = (cropRandomPKRadioButton.IsChecked == null || cropRandomPKRadioButton.IsChecked == false) ? false : true;
                hardNegativesMode = (cropHardNegativesRadioButton.IsChecked == null || cropHardNegativesRadioButton.IsChecked == false) ? false : true;
                probabilityMode = (cropWithProbabilityRadioButton.IsChecked == null || cropWithProbabilityRadioButton.IsChecked == false) ? false : true;
                coordMode = (cropAtCoordinatesRadioButton.IsChecked == null || cropAtCoordinatesRadioButton.IsChecked == false) ? false : true;
            }));

            int[] parameters = null;
            string extractorType = "";
            bool append = false;
            Dispatcher.Invoke(new Action(() =>
            {
                parameters = extractorSettings.ParametersArray;
                extractorType = extractorSettings.ExtractorName;
                append = (extractionAppendRadioButton.IsChecked == null || extractionAppendRadioButton.IsChecked == false) ? false : true;
            }));

            string oldPath = "";
            if (File.Exists(savePath))
            {
                if (append)
                {
                    oldPath = savePath;
                    savePath += " - tmp";
                    if (File.Exists(savePath))
                        File.Delete(savePath);
                }
                else
                    File.Delete(savePath);
            }

            if (coordMode)
            {
                int minWidth = Dispatcher.Invoke(new Func<int>(() => { return (int)minimumWindowNumericUpDown.Value; }));
                int maxWidth = Dispatcher.Invoke(new Func<int>(() => { return (int)maximumWindowNumericUpDown.Value; }));
                int sampleClass = Dispatcher.Invoke(new Func<int>(() => { return (int)classNumericUpDown.Value; }));

                int processedFiles = 0;

                GlobalFunctions.GetImageFiles(ref fileList, imagesPath, includeSubFolder);
                ProgressingEventsArg args = new ProgressingEventsArg(0, fileList.Count());
                OnExtractionProgressing(args);

                for (int i = 0; i < fileList.Count; i++)
                {
                    try
                    {
                        string file = fileList[i];
                        FileInfo fileInfo = new FileInfo(file);
                        if (!fileInfo.Exists)
                            throw new Exception(file + " doesn't exist.");

                        List<System.Drawing.Rectangle> extractionWindows = new List<System.Drawing.Rectangle>();
                        fileInfo = new FileInfo(file + ".txt");
                        if (fileInfo.Exists)
                        {
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
                                        System.Drawing.Rectangle window = new System.Drawing.Rectangle((int)double.Parse(values[0]), (int)double.Parse(values[1]), (int)double.Parse(values[2]), (int)double.Parse(values[2]));

                                        if (window.Width > minWidth && window.Height > minWidth && window.Width < maxWidth && window.Height < maxWidth)
                                        {
                                            extractionWindows.Add(window);
                                            samplesCount++;
                                        }
                                    }
                                    catch
                                    {
                                        errWindow++;
                                    }
                                }
                            }

                            if (extractionWindows.Count > 0)
                            {
                                System.Drawing.Point[] sizes = extractionWindows.Select(x => new System.Drawing.Point(x.Width, x.Height)).Distinct().ToArray();

                                BitmapSource bmp = new BitmapImage(new Uri(file, UriKind.Relative));
                                if (bmp.Format != PixelFormats.Bgr24)
                                    bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgr24, null, 0);

                                int height = bmp.PixelHeight;
                                int width = bmp.PixelWidth;
                                int widthbyte = (width * PixelFormats.Bgr24.BitsPerPixel + 7) / 8;
                                int stride = ((widthbyte + 3) / 4) * 4;
                                int bitsPerPixel = PixelFormats.Bgr24.BitsPerPixel / 8;
                                byte[] data = new byte[stride * height];
                                bmp.CopyPixels(data, stride, 0);

                                fixed (int* parPointer = parameters)
                                {
                                    fixed (byte* dataPointer = data)
                                    {
                                        fixed (System.Drawing.Rectangle* recPtr = (System.Drawing.Rectangle[])typeof(List<System.Drawing.Rectangle>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(extractionWindows))
                                        {
                                            fixed (System.Drawing.Point* sizePtr = sizes)
                                            {
                                                int status = NativeMethods.ExtractFromImage(extractorType, parPointer, dataPointer, bitsPerPixel, width, height, stride, recPtr, extractionWindows.Count, sizePtr, sizes.Length, savePath, sampleClass);

                                                if (status < 0)
                                                    GlobalFunctions.ThrowError(status);
                                            }
                                        }
                                    }
                                }
                                bmp.Freeze();
                            }

                            //if(extractionWindows.Count > 0)
                            //{
                            //    GC.Collect();
                            //    GC.WaitForPendingFinalizers();
                            //}

                            if (errWindow > 0)
                                throw new Exception("Image " + Path.GetFileName(file) + " windows coordinate excede image size. Bad windows count: " + errWindow);
                        }
                        else
                            throw new Exception("Image " + Path.GetFileName(file) + " does not have file with object coordinates included.");

                        worker.ReportProgress(++processedFiles);
                        if (this.worker.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        ProgressingEventsArg errArgs = new ProgressingEventsArg(processedFiles, -1, ex.Message);
                        OnExtractionProgressing(errArgs);

                        if (worker.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }
                    }
                }

                if (append && savePath.EndsWith(" - tmp"))
                {
                    bool oldFirst = sampleClass == -1 ? true : false;
                    int status = NativeMethods.ExtractFromImageFinalize(oldPath, savePath, oldFirst);

                    if (status < 0)
                        GlobalFunctions.ThrowError(status);

                    if (oldFirst)
                        File.Delete(savePath);
                    else
                    {
                        File.Delete(oldPath);
                        File.Move(savePath, oldPath);
                    }
                }

                if (processedFiles != fileList.Count)
                {
                    throw new Exception("Not all files were accessible or had no coordinates assigned.");
                }
            }
            else if (randomMode)
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
                OnExtractionProgressing(args);

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
                List<System.Drawing.Rectangle>[] extractionWindowsPerImage = new List<System.Drawing.Rectangle>[fileList.Count];
                Parallel.For(0, fileList.Count, (int f) =>
                {
                    posWindowsList[f] = new List<System.Drawing.Rectangle>();
                    extractionWindowsPerImage[f] = new List<System.Drawing.Rectangle>();
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

                Object lockMe = new Object();
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


                        BitmapSource bmp = new BitmapImage(new Uri(fileName, UriKind.Relative));
                        bmp.Freeze();
                        int nx = bmp.PixelWidth;
                        int ny = bmp.PixelHeight;

                        double scale = 1.0;
                        if (scaleMode)
                        {
                            scale = (double)rescaledHeight / ny;
                            nx = (int)Math.Floor(nx * scale);
                            ny = (int)Math.Floor(ny * scale);
                        }

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

                        lock (lockMe)
                        {
                            extractionWindowsPerImage[fileNumber].Add(new System.Drawing.Rectangle(x, y, wx, wy));
                        }
                    }
                    catch (Exception)
                    {
                    }
                });

                for (int i = 0; i < fileList.Count; i++)
                {
                    try
                    {
                        if (extractionWindowsPerImage[i].Count > 0)
                        {
                            string fileName = fileList[i];

                            FileInfo fileInfo = new FileInfo(fileName);
                            if (!fileInfo.Exists)
                                throw new Exception(fileName + " doesn't exist.");

                            System.Drawing.Point[] sizes = extractionWindowsPerImage[i].Select(x => new System.Drawing.Point(x.Width, x.Height)).Distinct().ToArray();

                            BitmapSource bmp = new BitmapImage(new Uri(fileName, UriKind.Relative));
                            if (bmp.Format != PixelFormats.Bgr24)
                                bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgr24, null, 0);
                            double scale = 1.0;
                            if (scaleMode)
                            {
                                scale = (double)rescaledHeight / bmp.PixelHeight;
                                bmp = new TransformedBitmap(bmp, new ScaleTransform(scale, scale));
                            }

                            int height = bmp.PixelHeight;
                            int width = bmp.PixelWidth;
                            int widthbyte = (width * PixelFormats.Bgr24.BitsPerPixel + 7) / 8;
                            int stride = ((widthbyte + 3) / 4) * 4;
                            int bitsPerPixel = PixelFormats.Bgr24.BitsPerPixel / 8;
                            byte[] data = new byte[stride * height];
                            bmp.CopyPixels(data, stride, 0);

                            fixed (int* parPointer = parameters)
                            {
                                fixed (byte* dataPointer = data)
                                {
                                    fixed (System.Drawing.Rectangle* recPtr = (System.Drawing.Rectangle[])typeof(List<System.Drawing.Rectangle>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(extractionWindowsPerImage[i]))
                                    {
                                        fixed (System.Drawing.Point* sizePtr = sizes)
                                        {
                                            int status = NativeMethods.ExtractFromImage(extractorType, parPointer, dataPointer, bitsPerPixel, width, height, stride, recPtr, extractionWindowsPerImage[i].Count, sizePtr, sizes.Length, savePath, -1);

                                            if (status < 0)
                                                GlobalFunctions.ThrowError(status);
                                        }
                                    }
                                }
                            }

                            bmp.Freeze();
                            samplesCount += extractionWindowsPerImage[i].Count;

                            //FOR DEBUG ONLY
                            //using (StreamWriter file = new StreamWriter(fileName + ".coords.txt"))
                            //{
                            //    foreach (System.Drawing.Rectangle window in extractionWindowsPerImage[i])
                            //    {
                            //        file.WriteLine(window.X + " " + window.Y + " " + window.Width);
                            //    }
                            //}

                        }

                        worker.ReportProgress(samplesCount);
                        if (this.worker.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        ProgressingEventsArg errArgs = new ProgressingEventsArg(samplesCount, -1, ex.Message);
                        OnExtractionProgressing(errArgs);

                        if (worker.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }
                    }
                }

                if (append && savePath.EndsWith(" - tmp"))
                {
                    bool oldFirst = true;
                    int status = NativeMethods.ExtractFromImageFinalize(oldPath, savePath, oldFirst);

                    if (status < 0)
                        GlobalFunctions.ThrowError(status);

                    if (oldFirst)
                        File.Delete(savePath);
                    else
                    {
                        File.Delete(oldPath);
                        File.Move(savePath, oldPath);
                    }
                }

                if (samplesCount < windowsCount)
                    throw new Exception("Number of created samples is lower than expected: " + samplesCount + " < " + windowsCount + ".");
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
                OnExtractionProgressing(args);

                for (int i = 0; i < fileList.Count; i++)
                {
                    try
                    {
                        string file = fileList[i];
                        FileInfo fileInfo = new FileInfo(file);
                        if (!fileInfo.Exists)
                            throw new Exception(file + " doesn't exist.");

                        fileInfo = new FileInfo(file + ".txt");
                        if (fileInfo.Exists)
                        {
                            BitmapSource bmpTmp = new BitmapImage(new Uri(file, UriKind.Relative));
                            bmpTmp.Freeze();
                            int nx = bmpTmp.PixelWidth;
                            int ny = bmpTmp.PixelHeight;
                            bmpTmp = null;

                            int errWindow = 0;
                            List<System.Drawing.Rectangle> windows = new List<System.Drawing.Rectangle>();
                            using (StreamReader sr = new StreamReader(file + ".txt"))
                            {
                                string line;
                                while ((line = sr.ReadLine()) != null)
                                {
                                    try
                                    {
                                        string[] values = line.Split(' ');

                                        int x = (int)double.Parse(values[0]);
                                        int y = (int)double.Parse(values[1]);
                                        int w = (int)double.Parse(values[2]);

                                        if (x < 0 || y < 0 || x + w > nx || y + w > ny)
                                            windows.Add(new System.Drawing.Rectangle(x, y, w, w));
                                    }
                                    catch
                                    {
                                        errWindow++;
                                    }
                                }
                            }

                            List<System.Drawing.Rectangle> extractionWindows = new List<System.Drawing.Rectangle>();
                            foreach (System.Drawing.Rectangle window in windows)
                            {
                                try
                                {
                                    for (int z = 0; z < repetitions; z++)
                                    {
                                        int side = window.Width;
                                        double xc = window.X + 0.5 * side;
                                        double yc = window.Y + 0.5 * side;

                                        int xNew = 0, yNew = 0;
                                        bool ifCropped = true;
                                        while (true)
                                        {
                                            sideSmaller = (int)(side * (sideSmallerMin + random.NextDouble() * sideSmallerRange));
                                            sideSmaller += sideSmaller % 2;
                                            if (sideSmaller < windowMinimalWidthMinusMargin)
                                            {
                                                ifCropped = false;
                                                break;
                                            }

                                            double minX = xc - 0.5 * placementRange * side;
                                            double minY = yc - 0.5 * placementRange * side;

                                            xNew = (int)(minX + random.NextDouble() * (placementRange * side - sideSmaller + 1));
                                            yNew = (int)(minY + random.NextDouble() * (placementRange * side - sideSmaller + 1));

                                            if (!((xNew < 0) || (xNew + sideSmaller > nx) || (yNew < 0) || (yNew + sideSmaller > ny)))
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
                                                extractionWindows.Add(negativeWindow);
                                        }
                                    }
                                }
                                catch
                                {
                                    errWindow++;
                                }
                            }

                            if (extractionWindows.Count > 0)
                            {
                                System.Drawing.Point[] sizes = extractionWindows.Select(x => new System.Drawing.Point(x.Width, x.Height)).Distinct().ToArray();

                                BitmapSource bmp = new BitmapImage(new Uri(file, UriKind.Relative));
                                if (bmp.Format != PixelFormats.Bgr24)
                                    bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgr24, null, 0);

                                int height = bmp.PixelHeight;
                                int width = bmp.PixelWidth;
                                int widthbyte = (width * PixelFormats.Bgr24.BitsPerPixel + 7) / 8;
                                int stride = ((widthbyte + 3) / 4) * 4;
                                int bitsPerPixel = PixelFormats.Bgr24.BitsPerPixel / 8;
                                byte[] data = new byte[stride * height];
                                bmp.CopyPixels(data, stride, 0);

                                fixed (int* parPointer = parameters)
                                {
                                    fixed (byte* dataPointer = data)
                                    {
                                        fixed (System.Drawing.Rectangle* recPtr = (System.Drawing.Rectangle[])typeof(List<System.Drawing.Rectangle>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(extractionWindows))
                                        {
                                            fixed (System.Drawing.Point* sizePtr = sizes)
                                            {
                                                int status = NativeMethods.ExtractFromImage(extractorType, parPointer, dataPointer, bitsPerPixel, width, height, stride, recPtr, extractionWindows.Count, sizePtr, sizes.Length, savePath, -1);

                                                if (status < 0)
                                                    GlobalFunctions.ThrowError(status);
                                            }
                                        }
                                    }
                                }
                                bmp.Freeze();

                                //FOR DEBUG ONLY
                                //using (StreamWriter f = new StreamWriter(file + ".coords.txt"))
                                //{
                                //    foreach (System.Drawing.Rectangle window in extractionWindows)
                                //    {
                                //        f.WriteLine(window.X + " " + window.Y + " " + window.Width);
                                //    }
                                //}
                            }
                            if (errWindow > 0)
                                throw new Exception("Image " + Path.GetFileName(file) + " windows coordinate excede image size. Bad windows count: " + errWindow);
                        }
                        else
                            throw new Exception("Image " + Path.GetFileName(file) + " does not have file with object coordinates included.");

                        worker.ReportProgress(++processedFiles);
                        if (this.worker.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        ProgressingEventsArg errArgs = new ProgressingEventsArg(processedFiles, -1, ex.Message);
                        OnExtractionProgressing(errArgs);

                        if (worker.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }
                    }
                }

                if (append && savePath.EndsWith(" - tmp"))
                {
                    bool oldFirst = true;
                    int status = NativeMethods.ExtractFromImageFinalize(oldPath, savePath, oldFirst);

                    if (status < 0)
                        GlobalFunctions.ThrowError(status);

                    if (oldFirst)
                        File.Delete(savePath);
                    else
                    {
                        File.Delete(oldPath);
                        File.Move(savePath, oldPath);
                    }
                }

                if (processedFiles != fileList.Count)
                {
                    throw new Exception("Not all files were accessible or had no coordinates assigned.");
                }
            }
            else
            {

            }

            stopwatch.Stop();
            operationTime = stopwatch.ElapsedMilliseconds;
        }

        private void ExtractionBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string statusLabel;
            string logMessage;
            string error = null;

            if (e.Error != null)
            {
                statusLabel = "Status: Extraction completed with errors. Check event log for details.";
                logMessage = "Extraction completed with errors: ";
                error = e.Error.Message;
            }
            else
            {
                statusLabel = "Status: Extraction completed successful. Check event log for details.";
                logMessage = "Extraction completed successful. Elapsed time: " + operationTime + "ms.  Processed windows: " + samplesCount;
            }

            bool shutdown = false; // (shutdownExtractionCheckBox.IsChecked == null || shutdownExtractionCheckBox.IsChecked == false) ? false : true;
            CompletionEventsArg args = new CompletionEventsArg(statusLabel, logMessage, error, DateTime.Now, shutdown);
            OnExtractionCompletion(args);

            worker.Dispose();
            worker = null;
        }
        #endregion
    }
}
