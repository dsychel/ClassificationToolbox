using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for BatchDetectionControl.xaml
    /// </summary>
    public partial class BatchDetectionControl : UserControl
    {
        #region Chart Elemnts
        readonly System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
        readonly System.Windows.Forms.DataVisualization.Charting.Legend legend1 = new System.Windows.Forms.DataVisualization.Charting.Legend();
        readonly System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
        readonly System.Windows.Forms.DataVisualization.Charting.DataPoint dataPoint1 = new System.Windows.Forms.DataVisualization.Charting.DataPoint(0D, 0D);
        readonly System.Windows.Forms.DataVisualization.Charting.DataPoint dataPoint2 = new System.Windows.Forms.DataVisualization.Charting.DataPoint(1D, 1D);
        readonly System.Windows.Forms.DataVisualization.Charting.Series series2 = new System.Windows.Forms.DataVisualization.Charting.Series();
        readonly System.Windows.Forms.DataVisualization.Charting.Series series3 = new System.Windows.Forms.DataVisualization.Charting.Series();
        readonly System.Windows.Forms.DataVisualization.Charting.DataPoint dataPoint3 = new System.Windows.Forms.DataVisualization.Charting.DataPoint(0.5D, 0.5D);
        readonly System.Windows.Forms.DataVisualization.Charting.Title title1 = new System.Windows.Forms.DataVisualization.Charting.Title();
        readonly System.Windows.Forms.DataVisualization.Charting.Title title2 = new System.Windows.Forms.DataVisualization.Charting.Title();
        #endregion

        #region Fields
        BackgroundWorker worker;

        System.Drawing.Font windowFont = new System.Drawing.Font("Microsoft Sans Serif", 12.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));

        int penSize = 2;
        int fontSize = 12;

        SortedDictionary<double, int[]> results;

        long detectionTime;
        long operationTime;
        int fileNumber;

        public bool IsExtractorLoaded { get; set; } = false;
        public bool IsClassifierLoaded { get; set; } = false;
        #endregion

        #region Constructors
        public BatchDetectionControl()
        {
            InitializeComponent();

            GlobalFunctions.InitializeDirectory(ref multipleImageSaveInTextBox, "batchDetectionResultFolder");
            GlobalFunctions.InitializeDirectory(ref multipleImageImagesTextBox, "batchDetectionImageFolder");
            GlobalFunctions.InitializePath(ref multipleImageClassifierTextBox, "batchDetectionClassifierPath");
            InitializeChart();

            CheckFolder(multipleImageImagesTextBox.Text);

            GlobalFunctions.InitializeMetrices(ref multipleImagesConfusionDataGridView, ref multipleImagesMetricesDataGridView);

            multipleImageExtractorSettings.TrySetFromString(multipleImageClassifierTextBox.Text);
            MultipleImageExtractorSettings_CheckedChanged(multipleImageExtractorSettings, new EventArgs());

            IsExtractorLoaded = false;
            IsClassifierLoaded = false;
        }
        #endregion

        #region Methods
        private void InitializeChart()
        {
            this.ROCchart.BorderlineColor = System.Drawing.Color.Transparent;
            this.ROCchart.BorderlineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            chartArea1.AxisX.Interval = 0.1D;
            chartArea1.AxisX.MajorGrid.Interval = 0.2D;
            chartArea1.AxisX.MajorGrid.LineColor = System.Drawing.Color.DimGray;
            chartArea1.AxisX.MajorTickMark.Enabled = false;
            chartArea1.AxisX.MajorTickMark.Interval = 0.2D;
            chartArea1.AxisX.Maximum = 1D;
            chartArea1.AxisX.Minimum = 0D;
            chartArea1.AxisX.MinorGrid.Enabled = true;
            chartArea1.AxisX.MinorGrid.Interval = 0.1D;
            chartArea1.AxisX.MinorGrid.LineColor = System.Drawing.Color.DarkGray;
            chartArea1.AxisX.Title = "1 - Specificity";
            chartArea1.AxisY.MajorGrid.Interval = 0.2D;
            chartArea1.AxisY.MajorGrid.LineColor = System.Drawing.Color.DimGray;
            chartArea1.AxisY.MajorTickMark.Enabled = false;
            chartArea1.AxisY.Maximum = 1D;
            chartArea1.AxisY.Minimum = 0D;
            chartArea1.AxisY.MinorGrid.Enabled = true;
            chartArea1.AxisY.MinorGrid.Interval = 0.1D;
            chartArea1.AxisY.MinorGrid.LineColor = System.Drawing.Color.DarkGray;
            chartArea1.AxisY.Title = "Sensitivity";
            chartArea1.BorderColor = System.Drawing.Color.Transparent;
            chartArea1.BorderDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            chartArea1.Name = "ROCArea";
            this.ROCchart.ChartAreas.Add(chartArea1);
            legend1.LegendStyle = System.Windows.Forms.DataVisualization.Charting.LegendStyle.Column;
            legend1.Name = "Legend1";
            this.ROCchart.Legends.Add(legend1);
            this.ROCchart.Location = new System.Drawing.Point(0, 0);
            this.ROCchart.Margin = new System.Windows.Forms.Padding(0);
            this.ROCchart.Name = "ROCchart";
            this.ROCchart.Palette = System.Windows.Forms.DataVisualization.Charting.ChartColorPalette.Bright;
            series1.BorderWidth = 2;
            series1.ChartArea = "ROCArea";
            series1.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series1.Legend = "Legend1";
            series1.Name = "AUC: 0.5000";
            series1.Points.Add(dataPoint1);
            series1.Points.Add(dataPoint2);
            series2.BorderWidth = 2;
            series2.ChartArea = "ROCArea";
            series2.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series2.Legend = "Legend1";
            series2.Name = "AUC: 0.0000";
            series3.ChartArea = "ROCArea";
            series3.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Point;
            series3.Legend = "Legend1";
            series3.MarkerBorderColor = System.Drawing.Color.Black;
            series3.MarkerColor = System.Drawing.Color.Red;
            series3.MarkerSize = 7;
            series3.MarkerStyle = System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Circle;
            series3.Name = "Threshold";
            series3.Points.Add(dataPoint3);
            series3.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            series3.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            this.ROCchart.Series.Add(series1);
            this.ROCchart.Series.Add(series2);
            this.ROCchart.Series.Add(series3);
            this.ROCchart.Size = new System.Drawing.Size(626, 381);
            this.ROCchart.TabIndex = 7;
            this.ROCchart.Text = "ROC chart";
            title1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            title1.Name = "ROCTitle";
            title1.Text = "ROC";
            title2.Docking = System.Windows.Forms.DataVisualization.Charting.Docking.Bottom;
            title2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            title2.Name = "Threshold";
            title2.Text = "Current threshold: 0.0000";
            this.ROCchart.Titles.Add(title1);
            this.ROCchart.Titles.Add(title2);

            //ChartHost.InvalidateVisual();
        }

        public void StopTask()
        {
            if (worker != null && worker.IsBusy)
                worker.CancelAsync();
        }

        private void GetImageFiles(ref List<string> images, string path)
        {
            try
            {
                SearchOption so = SearchOption.TopDirectoryOnly;
                StringComparison sc = StringComparison.OrdinalIgnoreCase;

                images.AddRange(Directory.EnumerateFiles(path, "*.*", so)
                            .Where(s => s.EndsWith(".jpg", sc) || s.EndsWith(".jpeg", sc) || s.EndsWith(".bmp", sc) || s.EndsWith(".png", sc)).ToArray());
            }
            catch
            {
            }
        }

        private void CheckFolder(string path)
        {
            try
            {
                SearchOption so = SearchOption.TopDirectoryOnly;
                StringComparison sc = StringComparison.OrdinalIgnoreCase;

                IEnumerable<string> files = Directory.EnumerateFiles(path, "*.*", so);

                int imageCount = 0;
                imageCount += files.Count(s => s.EndsWith(".jpg", sc) || s.EndsWith(".jpeg", sc));
                imageCount += files.Count(s => s.EndsWith(".bmp", sc));
                imageCount += files.Count(s => s.EndsWith(".png", sc));

                imageCountLabel.Content = imageCount;
            }
            catch
            {
            }
        }
        #endregion

        #region Triggers
        public event EventHandler<StartedEventsArg> DetectionStarted;
        protected virtual void OnDetectionStarted(StartedEventsArg e)
        {
            DetectionStarted?.Invoke(this, e);
        }

        public event EventHandler<ProgressingEventsArg> DetectionProgressing;
        protected virtual void OnDetectionProgressing(ProgressingEventsArg e)
        {
            DetectionProgressing?.Invoke(this, e);
        }

        public event EventHandler<CompletionEventsArg> DetectionCompletion;
        protected virtual void OnDetectionCompletion(CompletionEventsArg e)
        {
            DetectionCompletion?.Invoke(this, e);
        }

        public event EventHandler<EventArgs> ClassifierLoaded;
        protected virtual void OnClassifierLoaded(EventArgs e)
        {
            ClassifierLoaded?.Invoke(this, e);
        }

        public event EventHandler<EventArgs> ExtractorLoaded;
        protected virtual void OnExtractorLoaded(EventArgs e)
        {
            ExtractorLoaded?.Invoke(this, e);
        }
        #endregion

        #region Events
        private void MultipleImageSelectClassifierButton_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalFunctions.SelectClassifier(ref multipleImageClassifierTextBox, "batchDetectionClassifierPath") == System.Windows.Forms.DialogResult.OK)
            {
                multipleImageExtractorSettings.TrySetFromString(multipleImageClassifierTextBox.Text);
                IsClassifierLoaded = false;
            }
        }

        private void MultipleImageSelectImagesButton_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalFunctions.SelectFolder(ref multipleImageImagesTextBox, "batchDetectionImageFolder") == System.Windows.Forms.DialogResult.OK)
            {
                CheckFolder(multipleImageImagesTextBox.Text);
            }
        }

        private void MultipleImageSaveInButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFolder(ref multipleImageSaveInTextBox, "batchDetectionResultFolder");
        }

        private void MultipleImageExtractorSettings_CheckedChanged(object sender, EventArgs e)
        {
            if (multipleImageExtractorSettings.ExtractorName == "HaarExtractor")
            {
                multipleImageMinHeightNumericUpDown.IsEnabled = true;
            }
            else if (multipleImageExtractorSettings.ExtractorName == "HOGExtractor")
            {
                multipleImageMinHeightNumericUpDown.IsEnabled = true;
            }
            else if (multipleImageExtractorSettings.ExtractorName == "PFMMExtractor" || multipleImageExtractorSettings.ExtractorName.StartsWith("Zernike"))
            {
                multipleImageMinHeightNumericUpDown.Value = multipleImageMinWidthNumericUpDown.Value;
                multipleImageMinHeightNumericUpDown.IsEnabled = false;

                decimal maxWindow = Math.Round((decimal)Math.Pow((double)multipleImageScalingRatioNumericUpDown.Value, (double)multipleImageScalesNumericUpDown.Value - 1) * multipleImageMinWidthNumericUpDown.Value);
                multipleImageExtractorSettings.SetMinimum(maxWindow, "d");
                multipleImageExtractorSettings.SetMinimum(maxWindow, "w");
            }

            IsExtractorLoaded = false;
        }

        private void MultipleImageExtractorSettings_ValueChanged(object sender, EventArgs e)
        {
            IsExtractorLoaded = false;
        }

        private void MultipleImageMinWidthNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            if (multipleImageExtractorSettings.ExtractorName == "PFMMExtractor" || multipleImageExtractorSettings.ExtractorName.StartsWith("Zernike"))
            {
                multipleImageMinHeightNumericUpDown.Value = multipleImageMinWidthNumericUpDown.Value;

                decimal maxWindow = Math.Round((decimal)Math.Pow((double)multipleImageScalingRatioNumericUpDown.Value, (double)multipleImageScalesNumericUpDown.Value - 1) * multipleImageMinWidthNumericUpDown.Value);
                multipleImageExtractorSettings.SetMinimum(maxWindow, "d");
                multipleImageExtractorSettings.SetMinimum(maxWindow, "w");

                IsExtractorLoaded = false;
            }
        }

        private void MultipleImageScalingRatioNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            if (multipleImageExtractorSettings.ExtractorName == "PFMMExtractor" || multipleImageExtractorSettings.ExtractorName.StartsWith("Zernike"))
            {
                decimal maxWindow = Math.Round((decimal)Math.Pow((double)multipleImageScalingRatioNumericUpDown.Value, (double)multipleImageScalesNumericUpDown.Value - 1) * multipleImageMinWidthNumericUpDown.Value);
                multipleImageExtractorSettings.SetMinimum(maxWindow, "d");
                multipleImageExtractorSettings.SetMinimum(maxWindow, "w");

                IsExtractorLoaded = false;
            }
        }

        private void MultipleImageMinThresholdNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            if (multipleImageMaxThresholdNumericUpDown.Value < multipleImageMinThresholdNumericUpDown.Value)
                multipleImageMaxThresholdNumericUpDown.Value = multipleImageMinThresholdNumericUpDown.Value;
        }

        private void MultipleImageMaxThresholdNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            if (multipleImageMaxThresholdNumericUpDown.Value < multipleImageMinThresholdNumericUpDown.Value)
                multipleImageMinThresholdNumericUpDown.Value = multipleImageMaxThresholdNumericUpDown.Value;
        }

        private void PrevThrButton_Click(object sender, RoutedEventArgs e)
        {
            if (results != null)
            {
                double threshold = Double.Parse(currentThrTextBox.Text, CultureInfo.InvariantCulture);

                var keyList = results.Keys.ToList();
                int index = keyList.IndexOf(threshold);
                double newKey = keyList[index - 1];

                currentThrTextBox.Text = newKey.ToString(CultureInfo.InvariantCulture);

                int TP = results[newKey][0];
                int FP = results[newKey][1];
                int FN = results[newKey][2];
                int TN = results[newKey][3];

                GlobalFunctions.PopulateConfusionMatrix(ref multipleImagesConfusionDataGridView, TP, TN, FP, FN);
                GlobalFunctions.CalculateMetrices(ref multipleImagesConfusionDataGridView, ref multipleImagesMetricesDataGridView);

                nextThrButton.IsEnabled = true;
                if (index - 1 == 0)
                    prevThrButton.IsEnabled = false;

                if (worker == null || !worker.IsBusy)
                {
                    ROCchart.Series[2].Points.Clear();

                    double sensitivity = (double)TP / (TP + FN);
                    double FPR = (double)FP / (FP + TN);
                    if (!Double.IsInfinity(sensitivity) && !Double.IsNaN(sensitivity) && !Double.IsInfinity(FPR) && !Double.IsNaN(FPR))
                    {
                        if (FPR == 0)
                            FPR += 0.000000001;

                        ROCchart.Series[2].Points.AddXY(FPR, sensitivity);
                        ROCchart.Titles[1].Text = "Current threshold: " + String.Format("{0:0.0000}", newKey);
                    }
                }
            }
        }

        private void NextThrbutton_Click(object sender, RoutedEventArgs e)
        {
            if (results != null && results.Count > 1)
            {
                double threshold = Double.Parse(currentThrTextBox.Text, CultureInfo.InvariantCulture);

                var keyList = results.Keys.ToList();
                int index = keyList.IndexOf(threshold);
                double newKey = keyList[index + 1];

                currentThrTextBox.Text = newKey.ToString(CultureInfo.InvariantCulture);

                int TP = results[newKey][0];
                int FP = results[newKey][1];
                int FN = results[newKey][2];
                int TN = results[newKey][3];

                GlobalFunctions.PopulateConfusionMatrix(ref multipleImagesConfusionDataGridView, TP, TN, FP, FN);
                GlobalFunctions.CalculateMetrices(ref multipleImagesConfusionDataGridView, ref multipleImagesMetricesDataGridView);

                prevThrButton.IsEnabled = true;
                if (index + 1 == keyList.Count - 1)
                    nextThrButton.IsEnabled = false;

                if (worker == null || !worker.IsBusy)
                {
                    ROCchart.Series[2].Points.Clear();

                    double sensitivity = (double)TP / (TP + FN);
                    double FPR = (double)FP / (FP + TN);
                    if (!Double.IsInfinity(sensitivity) && !Double.IsNaN(sensitivity) && !Double.IsInfinity(FPR) && !Double.IsNaN(FPR))
                    {
                        if (FPR == 0)
                            FPR += 0.000000001;

                        ROCchart.Series[2].Points.AddXY(FPR, sensitivity);
                        ROCchart.Titles[1].Text = "Current threshold: " + String.Format("{0:0.0000}", newKey);
                    }
                }
            }
            else
                nextThrButton.IsEnabled = false;
        }

        unsafe private void MultipleImageDetectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (multipleImageClassifierTextBox.Text == "" || !File.Exists(multipleImageClassifierTextBox.Text))
            {
                MessageBox.Show("Classifier path is empty or file doesn't exist.");
                return;
            }
            if (multipleImageImagesTextBox.Text == "" || !Directory.Exists(multipleImageImagesTextBox.Text) || (int)imageCountLabel.Content == 0)
            {
                MessageBox.Show("Images directory is empty or doesn't exist.");
                return;
            }
            if (multipleImageSaveInTextBox.Text == "" || !Directory.Exists(multipleImageSaveInTextBox.Text))
            {
                MessageBox.Show("Save In directory is empty or doesn't exist.");
                return;
            }
            if (!IsClassifierLoaded)
            {
                int status = NativeMethods.LoadClassifier(multipleImageClassifierTextBox.Text);
                if (status != 0)
                {
                    MessageBox.Show(GlobalFunctions.GetErrorDescription(status));
                    IsClassifierLoaded = false;

                    return;
                }
                else
                {
                    IsClassifierLoaded = true;
                }
                OnClassifierLoaded(new EventArgs());
            }
            if (!IsExtractorLoaded)
            {
                int[] parameters = multipleImageExtractorSettings.ParametersArray;

                int status = 0;
                fixed (int* parPointer = parameters)
                {
                    status = NativeMethods.LoadExtractor(multipleImageExtractorSettings.ExtractorName, parPointer);
                }
                if (status != 0)
                {
                    MessageBox.Show(GlobalFunctions.GetErrorDescription(status));
                    IsExtractorLoaded = false;
                    return;
                }
                else
                {
                    IsExtractorLoaded = true;
                }
                OnExtractorLoaded(new EventArgs());
            }

            if (worker != null && worker.IsBusy)
            {
                MessageBox.Show("Application is busy.");
                return;
            }

            prevThrButton.IsEnabled = false;
            nextThrButton.IsEnabled = true;

            StartedEventsArg args = new StartedEventsArg("Status: Working", "Object detection started.", DateTime.Now, 0, true);
            OnDetectionStarted(args);


            GlobalFunctions.InitializeMetrices(ref multipleImagesConfusionDataGridView, ref multipleImagesMetricesDataGridView);

            worker = new BackgroundWorker()
            {
                WorkerSupportsCancellation = true,
                WorkerReportsProgress = true
            };
            worker.DoWork += DetectionBackgroundWorker_DoWork;
            worker.RunWorkerCompleted += DetectionBackgroundWorker_RunWorkerCompleted;
            worker.ProgressChanged += DetectionBackgroundWorker_ProgressChanged;
            worker.RunWorkerAsync();
        }

        private void DetectionBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressingEventsArg args = new ProgressingEventsArg(e.ProgressPercentage);
            OnDetectionProgressing(args);
        }

        private void DetectionBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            operationTime = 0;
            detectionTime = 0;
            Stopwatch stopwatchTotal = new Stopwatch();
            stopwatchTotal.Start();

            ParallelOptions parOpt = new ParallelOptions()
            {
                MaxDegreeOfParallelism = (int)Properties.Settings.Default.tplThreads
            };

            results = new SortedDictionary<double, int[]>();

            int rescaledSize = Dispatcher.Invoke(new Func<int>(() => { return (int)widthToRescale.Value; }));
            bool rescaledImages = Dispatcher.Invoke(new Func<bool>(() => { return resizeImages.IsChecked == true; }));
            string classifierPath = Dispatcher.Invoke(new Func<string>(() => { return multipleImageClassifierTextBox.Text; }));
            string extractorType = Dispatcher.Invoke(new Func<string>(() => { return multipleImageExtractorSettings.ExtractorName; }));
            string imageFolder = Dispatcher.Invoke(new Func<string>(() => { return multipleImageImagesTextBox.Text; }));
            string saveInPath = Dispatcher.Invoke(new Func<string>(() => { return multipleImageSaveInTextBox.Text; }));
            bool saveOutputImage = Dispatcher.Invoke(new Func<bool>(() => { return saveOutputImageCheckBox.IsChecked == true; }));
            bool ignoreWidnows = Dispatcher.Invoke(new Func<bool>(() => { return ignoreOverUnderSizePositives.IsChecked == true; }));

            NativeMethods.DetectionParameters detectionParameters = new NativeMethods.DetectionParameters()
            {
                windowMinimalHeight = Dispatcher.Invoke(new Func<int>(() => { return (int)multipleImageMinHeightNumericUpDown.Value; })),
                windowMinimalWidth = Dispatcher.Invoke(new Func<int>(() => { return (int)multipleImageMinWidthNumericUpDown.Value; })),
                scales = Dispatcher.Invoke(new Func<int>(() => { return (int)multipleImageScalesNumericUpDown.Value; })),
                windowJumpingRatio = Dispatcher.Invoke(new Func<double>(() => { return (double)multipleImageJumpingRatioNumericUpDown.Value; })),
                windowScalingRatio = Dispatcher.Invoke(new Func<double>(() => { return (double)multipleImageScalingRatioNumericUpDown.Value; }))
            };

            double minimum = Dispatcher.Invoke(new Func<double>(() => { return (double)multipleImageMinThresholdNumericUpDown.Value; }));
            double maximum = Dispatcher.Invoke(new Func<double>(() => { return (double)multipleImageMaxThresholdNumericUpDown.Value; }));
            double step = Dispatcher.Invoke(new Func<double>(() => { return (double)multipleImageStepThresholdNumericUpDown.Value; }));

            for (double threshold = minimum; threshold <= maximum; threshold += step)
            {
                if (!results.ContainsKey(threshold))
                    results.Add(threshold, new int[] { 0, 0, 0, 0 });
            }
            Dispatcher.Invoke(new Action(() => { currentThrTextBox.Text = minimum.ToString(CultureInfo.InvariantCulture); }));

            int[] parameters = null;
            Dispatcher.Invoke(new Action(() =>
            {
                parameters = multipleImageExtractorSettings.ParametersArray;
            }));

            List<string> fileList = new List<string>();
            GetImageFiles(ref fileList, imageFolder);
            ProgressingEventsArg args = new ProgressingEventsArg(0, fileList.Count());
            OnDetectionProgressing(args);
            Dispatcher.Invoke(new Action(() => { imageCountLabel.Content = fileList.Count(); }));

            fileNumber = 0;
            operationTime = 0;
            long totalWindows = 0;
            foreach (string imagePath in fileList)
            {
                if (this.worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                PixelFormat pf = PixelFormats.Bgr24;
                BitmapSource bmp = new BitmapImage(new Uri(imagePath, UriKind.Relative));
                if (bmp.Format != PixelFormats.Bgr24)
                    bmp = new FormatConvertedBitmap(bmp, pf, null, 0);

                int height = bmp.PixelHeight;
                int width = bmp.PixelWidth;
                double scale = 1;

                if (rescaledImages && height != rescaledSize)
                {
                    scale = (double)rescaledSize / height;
                    bmp = new TransformedBitmap(bmp, new ScaleTransform(scale, scale));
                    width = bmp.PixelWidth;
                    height = bmp.PixelHeight;
                }

                int widthbyte = (width * pf.BitsPerPixel + 7) / 8;
                int stride = ((widthbyte + 3) / 4) * 4;
                int bitsPerPixel = pf.BitsPerPixel;
                byte[] data = new byte[stride * height];
                bmp.CopyPixels(data, stride, 0);

                penSize = (int)Math.Max(2, 0.002 * width);
                fontSize = (int)Math.Max(12, 0.015 * width);
                windowFont = new System.Drawing.Font("Microsoft Sans Serif", fontSize, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));

                string fileName = Path.GetFileNameWithoutExtension(imagePath);
                string fullFileName = Path.GetFileName(imagePath);
                string path = Path.GetDirectoryName(imagePath) + "\\";

                double[] detectionOutputs = null;
                List<System.Drawing.Rectangle> detectionWindows = new List<System.Drawing.Rectangle>();
                List<System.Drawing.Rectangle> correctWindows = new List<System.Drawing.Rectangle>();
                System.Drawing.Point[] sizes = new System.Drawing.Point[detectionParameters.scales];

                GlobalFunctions.InitializeDetectionWindows(extractorType, width, height, ref detectionParameters, ref detectionWindows, ref detectionOutputs, out int wMax, ref sizes);
                totalWindows += detectionWindows.Count;

                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                if (File.Exists(path + fullFileName + ".txt"))
                {
                    using (StreamReader sr = new StreamReader(path + fullFileName + ".txt"))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            line = line.Trim().Replace("   ", " ");
                            string[] values = line.Split(' ');
                            System.Drawing.Rectangle window = new System.Drawing.Rectangle((int)(double.Parse(values[0])*scale), (int)(double.Parse(values[1])*scale), (int)(double.Parse(values[2])*scale), (int)(double.Parse(values[2])*scale));
                            double proc = 0.2;
                            if (window.Width >= sizes[0].Y*(1-proc) && window.Width <= sizes[sizes.Length-1].Y*(1+proc) &&
                                window.Height >= sizes[0].X * (1 - proc) && window.Height <= sizes[sizes.Length - 1].X * (1 + proc))
                                correctWindows.Add(window);
                        }
                    }
                }

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                GlobalFunctions.DetectObject(ref data, width, height, stride, bitsPerPixel, ref detectionParameters, ref detectionOutputs, ref detectionWindows, ref sizes);

                stopwatch.Stop();
                detectionTime += stopwatch.ElapsedMilliseconds;

                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

                if (saveOutputImage)
                {
                    List<int> selectedWindows = new List<int>();
                    for (double threshold = minimum; threshold <= maximum; threshold += step)
                    {
                        string thresholdVal = threshold.ToString(CultureInfo.InvariantCulture).Replace('.', '_');

                        if (!Directory.Exists(saveInPath + "thr_" + thresholdVal))
                            Directory.CreateDirectory(saveInPath + "thr_" + thresholdVal);

                        WriteableBitmap clone = new WriteableBitmap(bmp);
                        double jaccardIntersection = Dispatcher.Invoke(new Func<double>(() => { return (double)multipleImageMinJackardNumericUpDown.Value; }));
                        double jaccardGrouping = Dispatcher.Invoke(new Func<double>(() => { return (double)multipleImageMinJackardGroupingNumericUpDown.Value; }));

                        GlobalFunctions.GroupingMode mode;
                        if (Dispatcher.Invoke(new Func<bool>(() => { return multipleImageWindowsSumModeRadioButton.IsChecked == true; })))
                            mode = GlobalFunctions.GroupingMode.SUM;
                        else if (Dispatcher.Invoke(new Func<bool>(() => { return multipleImageWindowsAverageModeRadioButton.IsChecked == true; })))
                            mode = GlobalFunctions.GroupingMode.AVERAGE_FAST;
                        else
                            mode = GlobalFunctions.GroupingMode.AVERAGE_SLOW;

                        GlobalFunctions.SelectWindow(ref detectionOutputs, ref selectedWindows, threshold);
                        GlobalFunctions.GroupWindow(ref clone, ref detectionWindows, ref selectedWindows, ref correctWindows, jaccardGrouping, jaccardIntersection, mode,
                            penSize, windowFont, out double TP, out double FP, out double FN, out double TN);

                        results[threshold][0] += (int)TP;
                        results[threshold][1] += (int)FP;
                        results[threshold][2] += (int)FN;
                        results[threshold][3] += (int)TN;

                        BitmapEncoder encoder = new BmpBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(clone));
                        encoder.Save(new FileStream(saveInPath + "thr_" + thresholdVal + "\\" + fileName + "_result.bmp", FileMode.Create));

                        using (StreamWriter file = new StreamWriter(saveInPath + "thr_" + thresholdVal + "\\" + fileName + "_coords.txt"))
                        {
                            foreach (int window in selectedWindows)
                            {
                                    file.WriteLine(detectionWindows[window].X + " " + detectionWindows[window].Y + " " + detectionWindows[window].Width);
                            }
                        }
                    }
                }
                else
                {
                    Parallel.ForEach(GlobalFunctions.SteppedIterator(minimum, maximum, step), (double threshold) =>
                    {
                        string thresholdVal = threshold.ToString(CultureInfo.InvariantCulture).Replace('.', '_');

                        double jaccardIntersection = Dispatcher.Invoke(new Func<double>(() => { return (double)multipleImageMinJackardNumericUpDown.Value; }));
                        double jaccardGrouping = Dispatcher.Invoke(new Func<double>(() => { return (double)multipleImageMinJackardGroupingNumericUpDown.Value; }));

                        GlobalFunctions.GroupingMode mode;
                        if (Dispatcher.Invoke(new Func<bool>(() => { return multipleImageWindowsSumModeRadioButton.IsChecked == true; })))
                            mode = GlobalFunctions.GroupingMode.SUM;
                        else if (Dispatcher.Invoke(new Func<bool>(() => { return multipleImageWindowsAverageModeRadioButton.IsChecked == true; })))
                            mode = GlobalFunctions.GroupingMode.AVERAGE_FAST;
                        else
                            mode = GlobalFunctions.GroupingMode.AVERAGE_SLOW;

                        List<int> selectedWindows = new List<int>();
                        GlobalFunctions.SelectWindow(ref detectionOutputs, ref selectedWindows, threshold);
                        GlobalFunctions.CalculateConfusionMatrixForWindows(ref detectionWindows, ref selectedWindows, ref correctWindows, jaccardGrouping, jaccardIntersection, mode, out double TP, out double FP, out double FN, out double TN);

                        results[threshold][0] += (int)TP;
                        results[threshold][1] += (int)FP;
                        results[threshold][2] += (int)FN;
                        results[threshold][3] += (int)TN;
                    });
                }

                bmp.Freeze();
                Dispatcher.Invoke(new Action(() =>
                {
                    if (saveFPCheckBox.IsChecked == true)
                    {
                        if (!Directory.Exists(saveInPath + "FP windows"))
                            Directory.CreateDirectory(saveInPath + "FP windows");

                        for (int i = 0; i < detectionOutputs.Length; i++)
                        {
                            if (detectionOutputs[i] >= 0.0)
                            {
                                bool correct = false;
                                foreach (System.Drawing.Rectangle correctWindow in correctWindows)
                                {
                                    System.Drawing.Rectangle intersection = System.Drawing.Rectangle.Intersect(correctWindow, detectionWindows[i]);
                                    int intersectionField = (intersection.Width * intersection.Height);
                                    int fieldsSum = (correctWindow.Width * correctWindow.Height + detectionWindows[i].Width * detectionWindows[i].Height - intersectionField);
                                    double J = intersectionField / (double)fieldsSum;

                                    if (J >= (double)multipleImageMinJackardNumericUpDown.Value)
                                    {
                                        correct = true;
                                        break;
                                    }
                                }

                                if (!correct)
                                {
                                    BitmapSource cropedWindow = new CroppedBitmap(bmp, new Int32Rect(detectionWindows[i].X, detectionWindows[i].Y, detectionWindows[i].Width, detectionWindows[i].Height));

                                    string date = DateTime.Now.Ticks.ToString();
                                    string windowName = saveInPath + "FP windows\\window_" + Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + date.Substring(date.Length - 7);
                                    using (var fileStream = new FileStream(windowName + ".bmp", FileMode.Create))
                                    {
                                        BitmapEncoder encoder = new BmpBitmapEncoder();
                                        encoder.Frames.Add(BitmapFrame.Create(cropedWindow));
                                        encoder.Save(fileStream);
                                    }
                                }
                            }
                        }
                    }

                    double newKey = Double.Parse(currentThrTextBox.Text, CultureInfo.InvariantCulture);

                    int TP = results[newKey][0];
                    int FP = results[newKey][1];
                    int FN = results[newKey][2];
                    int TN = results[newKey][3];

                    GlobalFunctions.PopulateConfusionMatrix(ref multipleImagesConfusionDataGridView, TP, TN, FP, FN);
                    GlobalFunctions.CalculateMetrices(ref multipleImagesConfusionDataGridView, ref multipleImagesMetricesDataGridView);
                }));

                worker.ReportProgress(++fileNumber);
            }

            Dispatcher.Invoke(new Action(() =>
            {
                ROCchart.Series[1].Points.Clear();
                ROCchart.Series[2].Points.Clear();

                double AUC = 0.0;
                double preFPR = 1.0, preSens = 1.0;
                ROCchart.Series[1].Points.AddXY(preFPR, preSens);

                string features = multipleImageExtractorSettings.ExtractorFileName;

                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

                string fileName = multipleImageSaveInTextBox.Text + "Batch_" + Path.GetFileNameWithoutExtension(multipleImageClassifierTextBox.Text) + ".result.txt";
                int i = 2;
                while (File.Exists(fileName))
                {
                    fileName = multipleImageSaveInTextBox.Text + "Batch_" + Path.GetFileNameWithoutExtension(multipleImageClassifierTextBox.Text) + " (" + i + ").result.txt";
                    i++;
                    if (i > 10000)
                        break;
                }

                using (StreamWriter sw = new StreamWriter(fileName))
                {
                    sw.WriteLine("Result:");
                    sw.WriteLine("Classifier: " + Path.GetFileName(classifierPath));
                    sw.WriteLine("Features: " + Path.GetFileName(features));
                    sw.WriteLine();

                    for (double threshold = minimum; threshold <= maximum; threshold += step)
                    {
                        string thresholdVal = threshold.ToString(CultureInfo.InvariantCulture).Replace('.', '_');

                        int TP = results[threshold][0];
                        int FP = results[threshold][1];
                        int FN = results[threshold][2];
                        int TN = results[threshold][3];

                        GlobalFunctions.PopulateConfusionMatrix(ref multipleImagesConfusionDataGridView, TP, TN, FP, FN);

                        double sensitivity = (double)TP / (TP + FN);
                        double FPR = (double)FP / (FP + TN);
                        if (!Double.IsInfinity(sensitivity) && !Double.IsNaN(sensitivity) && !Double.IsInfinity(FPR) && !Double.IsNaN(FPR))
                        {
                            AUC += Math.Abs(((preSens + sensitivity) / 2.0) * (preFPR - FPR));

                            preFPR = FPR;
                            preSens = sensitivity;

                            ROCchart.Series[1].Points.AddXY(FPR, sensitivity);
                        }
                        GlobalFunctions.CalculateMetrices(ref multipleImagesConfusionDataGridView, ref multipleImagesMetricesDataGridView);

                        GlobalFunctions.SaveMetrices(ref multipleImagesConfusionDataGridView, ref multipleImagesMetricesDataGridView, sw, threshold);

                        sw.WriteLine();
                    }
                    AUC += Math.Abs(((preSens + 0.0) / 2.0) * (preFPR - 0.0));
                    ROCchart.Series[1].Points.AddXY(0.0, 0.0);
                    ROCchart.Series[1].LegendText = "AUC: " + String.Format("{0:0.0000}", AUC);

                    {
                        double newKey = Double.Parse(currentThrTextBox.Text, CultureInfo.InvariantCulture);

                        int TP = results[newKey][0];
                        int FP = results[newKey][1];
                        int FN = results[newKey][2];
                        int TN = results[newKey][3];

                        GlobalFunctions.PopulateConfusionMatrix(ref multipleImagesConfusionDataGridView, TP, TN, FP, FN);

                        double sensitivity = (double)TP / (TP + FN);
                        double FPR = (double)FP / (FP + TN);
                        if (!Double.IsInfinity(sensitivity) && !Double.IsNaN(sensitivity) && !Double.IsInfinity(FPR) && !Double.IsNaN(FPR))
                        {
                            if (FPR == 0)
                                FPR += 0.000000001;

                            ROCchart.Series[2].Points.AddXY(FPR, sensitivity);
                            ROCchart.Titles[1].Text = "Current threshold: " + String.Format("{0:0.0000}", newKey);
                        }

                        GlobalFunctions.CalculateMetrices(ref multipleImagesConfusionDataGridView, ref multipleImagesMetricesDataGridView);
                    }
                    sw.WriteLine("--------------");
                    sw.WriteLine("Total Windows: " + totalWindows);
                }

                i--;
                fileName = multipleImageSaveInTextBox.Text + "Batch_Config.settings.txt";
                if (i > 1)
                    fileName = multipleImageSaveInTextBox.Text + "Batch_Config (" + i + ").settings.txt";
                using (StreamWriter sw = new StreamWriter(fileName))
                {
                    sw.WriteLine("Staring width:{0}", multipleImageMinWidthNumericUpDown.Value);
                    sw.WriteLine("Staring height: {0}", multipleImageMinHeightNumericUpDown.Value);
                    sw.WriteLine("Jumping ratio: {0:0.00}", multipleImageJumpingRatioNumericUpDown.Value);
                    sw.WriteLine("Scales: {0}", multipleImageScalesNumericUpDown.Value);
                    sw.WriteLine("Scaling ratio: {0:0.00}", multipleImageScalingRatioNumericUpDown.Value);
                    sw.WriteLine("Jaccard for Detection Check: {0:0.00}", multipleImageMinJackardNumericUpDown.Value);
                    sw.WriteLine("Jaccard for Windows Grouping: {0:0.00}", multipleImageMinJackardGroupingNumericUpDown.Value);
                    sw.WriteLine("Grouping mode: {0}", multipleImageWindowsSumModeRadioButton.IsChecked == true ? "sum" : multipleImageWindowsAverageModeRadioButton.IsChecked == true ? "average - fast" : "average - slow" );
                }
            }));

            stopwatchTotal.Stop();
            operationTime = stopwatchTotal.ElapsedMilliseconds;
        }

        private void DetectionBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string statusLabel;
            string logMessage;
            string error = null;

            if (e.Cancelled)
            {
                statusLabel = "Status: Bath detection cancelled. Check event log for details";
                logMessage = "Bath detection cancelled.";
            }
            else if (e.Error != null)
            {
                statusLabel = "Status: Bath detection completed with errors. Check event log for details";
                logMessage = "Bath detection completed with errors:";
                error = e.Error.Message;
            }
            else
            {
                statusLabel = "Status: Bath detection completed successful. Check event log for details";
                logMessage = "Bath detection completed successful. Elapsed time: " + operationTime + "ms. Detection time: " + detectionTime + "ms. Processed files: " + fileNumber;
            }

            CompletionEventsArg args = new CompletionEventsArg(statusLabel, logMessage, error, DateTime.Now, false);
            OnDetectionCompletion(args);

            worker.Dispose();
            worker = null;

            if (results.Count == 1)
                nextThrButton.IsEnabled = false;
        }
        #endregion

    }
}
