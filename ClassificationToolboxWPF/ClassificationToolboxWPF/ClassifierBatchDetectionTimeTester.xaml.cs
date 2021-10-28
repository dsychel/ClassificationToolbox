using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.ComponentModel;
using System.Reflection;
using System.Linq;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for ClassifierBatchDetectionTimeTester.xaml
    /// </summary>
    public partial class ClassifierBatchDetectionTimeTester : UserControl
    {
        #region Chart Elemnts
        System.Drawing.Point? prevTime = null;
        readonly System.Windows.Forms.ToolTip timeToolTip = new System.Windows.Forms.ToolTip();

        readonly System.Windows.Forms.DataVisualization.Charting.ChartArea timeChartArea = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
        readonly System.Windows.Forms.DataVisualization.Charting.Legend timesChartLegend = new System.Windows.Forms.DataVisualization.Charting.Legend();
        readonly System.Windows.Forms.DataVisualization.Charting.Series timesSeries = new System.Windows.Forms.DataVisualization.Charting.Series();
        readonly System.Windows.Forms.DataVisualization.Charting.Series averageTimeSeries = new System.Windows.Forms.DataVisualization.Charting.Series();
        readonly System.Windows.Forms.DataVisualization.Charting.Title timeChartTitle = new System.Windows.Forms.DataVisualization.Charting.Title();
        #endregion

        #region Fields
        readonly BackgroundWorker worker = new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
        NativeMethods.ProgressCallback callback;

        long averageTime = 0;
        long[] results = null;

        // Image Data
        int width;
        int height;
        int stride;
        int bitsPerPixel;
        byte[] data = null;

        public bool IsExtractorLoaded { get; set; } = false;
        public bool IsClassifierLoaded { get; set; } = false;
        #endregion

        #region Structs
        private class TimeTableRow
        {
            public int ID { get; set; }
            public long TimeFrame { get; set; }
            public long TimeWindow { get; set; }
            public long FPS { get; set; }
            public double avgFet { get; set; }
            public double expect { get; set; }
            public double expectRed { get; set; }
        }

        private class ClassifierItem
        {
            public string Name { get; set; }
            public int ID { get; set; }
        }
        #endregion

        public ClassifierBatchDetectionTimeTester()
        {
            InitializeComponent(); InitializeChart();

            //worker.WorkerSupportsCancellation = true;
            worker.WorkerReportsProgress = true;
            worker.DoWork += TimeTestBackgroundWorker_DoWork;
            worker.ProgressChanged += TimeTestBackgroundWorker_ProgressChanged;
            worker.RunWorkerCompleted += TimeTestBackgroundWorker_RunWorkerCompleted;

            GlobalFunctions.InitializeDirectory(ref detectionClassifierPathTextBox, "testingClassifierFolder");
            PopulateClassifierList();

            DetectionExtractorSettings_CheckedChanged(detectionExtractorSettings, new EventArgs());

            IsExtractorLoaded = false;
            IsClassifierLoaded = false;
        }


        #region Methods
        public void StopTask()
        {
            if (worker != null && worker.IsBusy)
                worker.CancelAsync();
        }

        private void InitializeChart()
        {
            this.timeChart.BorderlineColor = System.Drawing.Color.Transparent;
            this.timeChart.BorderlineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            //chartArea2.AxisX.Interval = 0.1D;
            //chartArea2.AxisX.MajorGrid.Interval = 0.2D;
            timeChartArea.AxisX.MajorGrid.LineColor = System.Drawing.Color.DimGray;
            timeChartArea.AxisX.MajorTickMark.Enabled = false;
            timeChartArea.AxisX.MajorTickMark.Interval = 10;
            timeChartArea.AxisX.Maximum = 100;
            timeChartArea.AxisX.Minimum = 1;
            timeChartArea.AxisX.MinorGrid.Enabled = true;
            timeChartArea.AxisX.MinorGrid.Interval = 1;
            timeChartArea.AxisX.MinorGrid.LineColor = System.Drawing.Color.DarkGray;
            timeChartArea.AxisX.Title = "Average Features";
            timeChartArea.AxisY.MajorGrid.Interval = 2;
            timeChartArea.AxisY.MajorGrid.LineColor = System.Drawing.Color.DimGray;
            timeChartArea.AxisY.MajorTickMark.Enabled = false;
            timeChartArea.AxisY.Maximum = 10;
            timeChartArea.AxisY.Minimum = 0;
            timeChartArea.AxisY.MinorGrid.Enabled = true;
            timeChartArea.AxisY.MinorGrid.Interval = 1;
            timeChartArea.AxisY.MinorGrid.LineColor = System.Drawing.Color.DarkGray;
            timeChartArea.AxisY.Title = "Time [ms]";
            timeChartArea.BorderColor = System.Drawing.Color.Transparent;
            timeChartArea.BorderDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            timeChartArea.Name = "TimeArea";
            this.timeChart.ChartAreas.Add(timeChartArea);
            //this.ROCchart.Dock = System.Windows.Forms.DockStyle.Fill;
            timesChartLegend.LegendStyle = System.Windows.Forms.DataVisualization.Charting.LegendStyle.Column;
            timesChartLegend.Name = "Legend1";
            this.timeChart.Legends.Add(timesChartLegend);
            this.timeChart.Location = new System.Drawing.Point(0, 0);
            this.timeChart.Margin = new System.Windows.Forms.Padding(0);
            this.timeChart.Name = "timeChart";
            this.timeChart.Palette = System.Windows.Forms.DataVisualization.Charting.ChartColorPalette.Bright;
            timesSeries.BorderWidth = 2;
            timesSeries.ChartArea = "TimeArea";
            timesSeries.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Point;
            timesSeries.Legend = "Legend1";
            timesSeries.Name = "Times [ms]";
            timesSeries.Points.Add(new System.Windows.Forms.DataVisualization.Charting.DataPoint(1, 0));
            timesSeries.Points.Add(new System.Windows.Forms.DataVisualization.Charting.DataPoint(100, 10));
            timesSeries.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            timesSeries.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int64;
            averageTimeSeries.BorderWidth = 2;
            averageTimeSeries.ChartArea = "TimeArea";
            averageTimeSeries.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            averageTimeSeries.Legend = "Legend1";
            averageTimeSeries.Name = "Average Time [ms]";
            averageTimeSeries.Points.Add(new System.Windows.Forms.DataVisualization.Charting.DataPoint(1, 5));
            averageTimeSeries.Points.Add(new System.Windows.Forms.DataVisualization.Charting.DataPoint(100, 5));
            averageTimeSeries.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            averageTimeSeries.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int64;
            this.timeChart.Series.Add(timesSeries);
            this.timeChart.Series.Add(averageTimeSeries);
            this.timeChart.Size = new System.Drawing.Size(1084, 332);
            this.timeChart.TabIndex = 1;
            this.timeChart.Text = "Times Chart";
            timeChartTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            timeChartTitle.Name = "TimeTitle";
            timeChartTitle.Text = "Times Chart";
            this.timeChart.Titles.Add(timeChartTitle);
            this.timeChart.MouseMove += new System.Windows.Forms.MouseEventHandler(this.TimeChart_MouseMove);

            //ChartHost.InvalidateVisual();
        }

        private void TimeChart_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            var pos = e.Location;
            if (prevTime.HasValue && pos == prevTime.Value)
                return;
            timeToolTip.RemoveAll();
            prevTime = pos;
            var results = timeChart.HitTest(pos.X, pos.Y, false, System.Windows.Forms.DataVisualization.Charting.ChartElementType.DataPoint);
            foreach (var result in results)
            {
                if (result.ChartElementType == System.Windows.Forms.DataVisualization.Charting.ChartElementType.DataPoint)
                {
                    if (result.Object is System.Windows.Forms.DataVisualization.Charting.DataPoint prop)
                    {
                        var pointXPixel = result.ChartArea.AxisX.ValueToPixelPosition(prop.XValue);
                        var pointYPixel = result.ChartArea.AxisY.ValueToPixelPosition(prop.YValues[0]);

                        // check if the cursor is really close to the point (5 pixels around the point)
                        if (Math.Abs(pos.X - pointXPixel) < 5 &&
                            Math.Abs(pos.Y - pointYPixel) < 5)
                        {
                            timeToolTip.Show("Average = " + prop.XValue + ", Time =" + prop.YValues[0], this.timeChart,
                                            pos.X, pos.Y - 15);
                        }
                    }
                }
            }
        }

        private void PopulateClassifierList()
        {
            classifiersList.Items.Clear();
            string path = detectionClassifierPathTextBox.Text;
            if (path != "" && Directory.Exists(path))
            {
                SearchOption so = SearchOption.TopDirectoryOnly;
                StringComparison sc = StringComparison.OrdinalIgnoreCase;
                IEnumerable<string> files = Directory.EnumerateFiles(path, "*.model*", so);

                int id = 0;
                foreach (string file in files)
                {
                    classifiersList.Items.Add(new ClassifierItem { ID = ++id, Name = file });
                }
                classifiersList.SelectAll();

                if (classifiersList.Items.Count > 0)
                    detectionExtractorSettings.TrySetFromString(Path.GetFileName(((ClassifierItem)classifiersList.Items[0]).Name));
            }
        }
        #endregion

        #region Triggers
        public event EventHandler<StartedEventsArg> TestingStarted;
        protected virtual void OnTestingStarted(StartedEventsArg e)
        {
            TestingStarted?.Invoke(this, e);
        }

        public event EventHandler<ProgressingEventsArg> TestingProgressing;
        protected virtual void OnTestingProgressing(ProgressingEventsArg e)
        {
            TestingProgressing?.Invoke(this, e);
        }

        public event EventHandler<StatusChangedArg> TestingStatusChanged;
        protected virtual void OnTestingStatusChanged(StatusChangedArg e)
        {
            TestingStatusChanged?.Invoke(this, e);
        }

        public event EventHandler<CompletionEventsArg> TestingCompletion;
        protected virtual void OnTestingCompletion(CompletionEventsArg e)
        {
            TestingCompletion?.Invoke(this, e);
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
        private void SelectClassifierButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFolder(ref detectionClassifierPathTextBox, "testingClassifierFolder");
            {
                PopulateClassifierList();

                IsClassifierLoaded = false;
            }
        }

        private void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalFunctions.SelectImage(ref imagePathTextBox, "testingImagePath") == System.Windows.Forms.DialogResult.OK)
            {
                PixelFormat pf = PixelFormats.Bgr24;

                BitmapSource bmp = new BitmapImage(new Uri(imagePathTextBox.Text, UriKind.Relative));
                if (bmp.Format != PixelFormats.Bgr24)
                    bmp = new FormatConvertedBitmap(bmp, pf, null, 0);

                height = bmp.PixelHeight;
                width = bmp.PixelWidth;
                int widthbyte = (width * pf.BitsPerPixel + 7) / 8;
                stride = ((widthbyte + 3) / 4) * 4;
                bitsPerPixel = pf.BitsPerPixel;
                data = new byte[stride * height];
                bmp.CopyPixels(data, stride, 0);

                imagePictureBox.Source = BitmapSource.Create(width, height, 96, 96, pf, null, data, stride);

                imageSizeLabel.Content = bmp.PixelWidth + " x " + bmp.PixelHeight;

                minHeightNumericUpDown.Maximum = bmp.PixelHeight;
                minWidthNumericUpDown.Maximum = bmp.PixelWidth;

                if (detectionExtractorSettings.ExtractorName == "HaarExtractor")
                {
                    minHeightNumericUpDown.Value = height * (10.0M / 100.0M);
                    minWidthNumericUpDown.Value = width * (10.0M / 100.0M);
                }
                else if (detectionExtractorSettings.ExtractorName == "HOGExtractor")
                {
                    minHeightNumericUpDown.Value = height * (10.0M / 100.0M);
                    minWidthNumericUpDown.Value = width * (10.0M / 100.0M);
                }
                else if (detectionExtractorSettings.ExtractorName == "PFMMExtractor")
                {
                    decimal minimum = Math.Min(width, height);
                    minWidthNumericUpDown.Value = minimum * (10.0M / 100.0M);
                    minHeightNumericUpDown.Value = minimum * (10.0M / 100.0M);
                }
                else if (detectionExtractorSettings.ExtractorName == "ZernikeExtractor")
                {
                    decimal minimum = Math.Min(width, height);
                    minWidthNumericUpDown.Value = minimum * (10.0M / 100.0M);
                    minHeightNumericUpDown.Value = minimum * (10.0M / 100.0M);
                }
            }
        }

        private void DetectionExtractorSettings_CheckedChanged(object sender, EventArgs e)
        {
            if (detectionExtractorSettings.ExtractorName == "HaarExtractor")
            {
                minHeightNumericUpDown.IsEnabled = true;
            }
            else if (detectionExtractorSettings.ExtractorName == "HOGExtractor")
            {
                minHeightNumericUpDown.IsEnabled = true;
            }
            else if (detectionExtractorSettings.ExtractorName == "PFMMExtractor" || detectionExtractorSettings.ExtractorName.StartsWith("Zernike"))
            {
                minHeightNumericUpDown.Value = minWidthNumericUpDown.Value;
                minHeightNumericUpDown.IsEnabled = false;

                decimal maxWindow = Math.Round((decimal)Math.Pow((double)scalesRatioNumericUpDown.Value, (double)scalesNumericUpDown.Value - 1) * minWidthNumericUpDown.Value);
                detectionExtractorSettings.SetMinimum(maxWindow, "d");
                detectionExtractorSettings.SetMinimum(maxWindow, "w");
            }

            IsExtractorLoaded = false;
        }

        private void DetectionExtractorSettings_ValueChanged(object sender, EventArgs e)
        {
            IsExtractorLoaded = false;
        }

        private void MinWidthNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            if (detectionExtractorSettings.ExtractorName == "PFMMExtractor" || detectionExtractorSettings.ExtractorName.StartsWith("Zernike"))
            {
                minHeightNumericUpDown.Value = minWidthNumericUpDown.Value;

                decimal maxWindow = Math.Round((decimal)Math.Pow((double)scalesRatioNumericUpDown.Value, (double)scalesNumericUpDown.Value - 1) * minWidthNumericUpDown.Value);
                detectionExtractorSettings.SetMinimum(maxWindow, "d");
                detectionExtractorSettings.SetMinimum(maxWindow, "w");

                IsExtractorLoaded = false;
            }
        }

        private void ScalesNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            if (detectionExtractorSettings.ExtractorName == "PFMMExtractor" || detectionExtractorSettings.ExtractorName.StartsWith("Zernike"))
            {
                decimal maxWindow = Math.Ceiling((decimal)Math.Pow((double)scalesRatioNumericUpDown.Value, (double)scalesNumericUpDown.Value - 1) * minWidthNumericUpDown.Value);
                detectionExtractorSettings.SetMinimum(maxWindow, "d");
                detectionExtractorSettings.SetMinimum(maxWindow, "w");

                IsExtractorLoaded = false;
            }
        }

        private void ScalesRatioNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            if (detectionExtractorSettings.ExtractorName == "PFMMExtractor" || detectionExtractorSettings.ExtractorName.StartsWith("Zernike"))
            {
                decimal maxWindow = Math.Ceiling((decimal)Math.Pow((double)scalesRatioNumericUpDown.Value, (double)scalesNumericUpDown.Value - 1) * minWidthNumericUpDown.Value);
                detectionExtractorSettings.SetMinimum(maxWindow, "d");
                detectionExtractorSettings.SetMinimum(maxWindow, "w");

                IsExtractorLoaded = false;
            }
        }

        unsafe private void TimeTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (classifiersList.SelectedItems.Count == 0)
            {
                MessageBox.Show("No classifier is selected.");
                return;
            }
            if (imagePictureBox.Source == null)
            {
                MessageBox.Show("Image is empty.");
                return;
            }

            if (!IsExtractorLoaded)
            {
                int[] parameters = detectionExtractorSettings.ParametersArray;

                int status;
                fixed (int* parPointer = parameters)
                {
                    status = NativeMethods.LoadExtractor(detectionExtractorSettings.ExtractorName, parPointer);
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
            if (worker.IsBusy)
            {
                MessageBox.Show("Application is busy.");
                return;
            }

            results = new long[(int)repetitionsNumericUpDown.Value + 2];
            timeChart.Series[0].Points.Clear();
            timeChart.Series[1].Points.Clear();
            timeDataGridView.Items.Clear();

            StartedEventsArg args = new StartedEventsArg("Status: Working", "Detection time testing started.", DateTime.Now, (int)classifiersList.SelectedItems.Count, true);
            OnTestingStarted(args);

            worker.RunWorkerAsync();
        }

        private void TimeTestBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressingEventsArg args = new ProgressingEventsArg(e.ProgressPercentage);
            OnTestingProgressing(args);
        }

        unsafe private void TimeTestBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            #region Configuration
            int clsCount = Dispatcher.Invoke(new Func<int>(() => { return classifiersList.SelectedItems.Count; }));

            string extractorType = Dispatcher.Invoke(new Func<string>(() => { return detectionExtractorSettings.ExtractorName; }));
            NativeMethods.DetectionParameters detectionParameters = new NativeMethods.DetectionParameters()
            {
                windowMinimalHeight = Dispatcher.Invoke(new Func<int>(() => { return (int)minHeightNumericUpDown.Value; })),
                windowMinimalWidth = Dispatcher.Invoke(new Func<int>(() => { return (int)minWidthNumericUpDown.Value; })),
                scales = Dispatcher.Invoke(new Func<int>(() => { return (int)scalesNumericUpDown.Value; })),
                windowJumpingRatio = Dispatcher.Invoke(new Func<double>(() => { return (double)jumpingRatioNumericUpDown.Value; })),
                windowScalingRatio = Dispatcher.Invoke(new Func<double>(() => { return (double)scalesRatioNumericUpDown.Value; }))
            };
            int repetitions = Dispatcher.Invoke(new Func<int>(() => { return (int)repetitionsNumericUpDown.Value; }));

            System.Drawing.Point[] sizes = new System.Drawing.Point[detectionParameters.scales];
            List<System.Drawing.Rectangle> detectionWindows = new List<System.Drawing.Rectangle>();
            for (int s = 0; s < detectionParameters.scales; s++)
            {
                int wx = (int)Math.Round(Math.Pow(detectionParameters.windowScalingRatio, s) * detectionParameters.windowMinimalWidth);
                int wy = (int)Math.Round(Math.Pow(detectionParameters.windowScalingRatio, s) * detectionParameters.windowMinimalHeight);
                if (wx > width) wx = width;
                if (wy > height) wy = height;

                if (extractorType == "PFMMExtractor" || extractorType.ToUpper().Contains("ZERNIKE"))
                {
                    int m = Math.Min(wx, wy);
                    m -= m % 2;
                    wx = m;
                    wy = m;
                }

                sizes[s].X = wx;
                sizes[s].Y = wy;

                int dx = (int)Math.Round(detectionParameters.windowJumpingRatio * wx);
                int dy = (int)Math.Round(detectionParameters.windowJumpingRatio * wy);

                int xHalfRemainder = ((width - wx) % dx) / 2;
                int xMin = xHalfRemainder; // min anchoring point for window of width wx
                int xMax = width - wx - xHalfRemainder; // max anchoring point for window of width wx
                int yHalfRemainder = ((height - wy) % dy) / 2;
                int yMin = yHalfRemainder; // min anchoring point for window of width wy
                int yMax = height - wy - yHalfRemainder; // max anchoring point for window of width wy

                for (int x = xMin; x <= xMax; x += dx)
                {
                    for (int y = yMin; y <= yMax; y += dy)
                    {
                        System.Drawing.Rectangle window = new System.Drawing.Rectangle(x, y, wx, wy);
                        detectionWindows.Add(window);
                    }
                }
            }

            Dispatcher.Invoke(new Action(() => { windowsCountLabel.Content = detectionWindows.Count; }));
            #endregion

            long[] avgTimes = new long[clsCount];
            double[] avgFeatures = new double[clsCount];
            double[] expFeatures = new double[clsCount];

            for (int c = 0; c < clsCount; c++)
            {
                if (this.worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                string classifierPath = Dispatcher.Invoke(new Func<string>(() => { return ((ClassifierItem)classifiersList.SelectedItems[c]).Name; }));
                int id = Dispatcher.Invoke(new Func<int>(() => { return ((ClassifierItem)classifiersList.SelectedItems[c]).ID; }));

                callback = (value) =>
                {
                    this.OnTestingStatusChanged(new StatusChangedArg("Status: Working | Classifier ID: " + id.ToString() + " | Repetition: " + value.ToString() + "/" + repetitions.ToString()));
                };

                int clsStatus = NativeMethods.LoadClassifier(classifierPath);
                if (clsStatus == 0)
                {
                    IsClassifierLoaded = true;
                    OnClassifierLoaded(new EventArgs());

                    fixed (long* resultsPointer = results)
                    {
                        fixed (byte* dataPointer = data)
                        {
                            fixed (System.Drawing.Rectangle* recPtr = (System.Drawing.Rectangle[])typeof(List<System.Drawing.Rectangle>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(detectionWindows))
                            {
                                fixed (System.Drawing.Point* sizesPtr = sizes)
                                {
                                    int status = NativeMethods.TestDetectionTime(repetitions, detectionParameters, resultsPointer, dataPointer, bitsPerPixel / 8, width, height, stride, recPtr, detectionWindows.Count, sizesPtr, callback);

                                    if (status < 0)
                                        GlobalFunctions.ThrowError(status);
                                }
                            }
                        }
                    }

                    long avg = 0;
                    for (int i = 0; i < repetitions; i++)
                        avg += results[i];
                    avg /= repetitions;

                    Dispatcher.Invoke(new Action(() =>
                    {
                        long msAvg = (long)(avg / 1000000.0);
                        long nsAvgPerWindow = (long)(1.0 * avg / detectionWindows.Count);
                        long FPS = (long)(1.0 / (msAvg / 1000.0));
                        double avgFet = results[results.Length - 1] / 1000.0;
                        double expect = double.NaN;
                        double expectRed = double.NaN;


                        using (var reader = new StreamReader(classifierPath))
                        {
                            string line;
                            reader.ReadLine();
                            reader.ReadLine();
                            reader.ReadLine();
                            line = reader.ReadLine();

                            if (line.Contains("ClassifierCascade"))
                            {
                                reader.ReadLine();
                                reader.ReadLine();
                                reader.ReadLine();
                                reader.ReadLine();
                                reader.ReadLine();
                                reader.ReadLine();
                                reader.ReadLine();
                                line = reader.ReadLine();
                                expect = Math.Round(Double.Parse(line.Split()[1]), 4);
                                line = reader.ReadLine();
                                expectRed = Math.Round(Double.Parse(line.Split()[1]), 4);
                            }
                        }

                        timeDataGridView.Items.Add(new TimeTableRow() { ID = id, TimeFrame = msAvg, TimeWindow = nsAvgPerWindow, FPS = FPS, avgFet = avgFet, expect = expect, expectRed = expectRed });
                        avgTimes[c] = msAvg;
                        avgFeatures[c] = avgFet;
                        expFeatures[c] = expect;

                    }));
                }
                else
                {
                    // add some error to log
                    IsClassifierLoaded = false;
                }
                worker.ReportProgress(c + 1);
            }


            Dispatcher.Invoke(new Action(() =>
            {
                long max = avgTimes.Max();
                long min = avgTimes.Min();
                double avg = avgTimes.Average();

                if (Math.Abs(min - max) < 0.0000001)
                    max += (long)(0.1 * min);

                timeChartArea.AxisY.Maximum = max;
                timeChartArea.AxisY.Minimum = min;
                timeChartArea.AxisY.IntervalAutoMode = System.Windows.Forms.DataVisualization.Charting.IntervalAutoMode.FixedCount;
                timeChartArea.AxisY.MinorGrid.Interval = (max - min) / 20;
                timeChartArea.AxisY.MajorGrid.Interval = (max - min) / 20;

                double maxFet = avgFeatures.Max();
                double minFet = avgFeatures.Min();
                timeChartArea.AxisX.Maximum = maxFet;
                timeChartArea.AxisX.Minimum = minFet;
                timeChartArea.AxisX.IntervalAutoMode = System.Windows.Forms.DataVisualization.Charting.IntervalAutoMode.FixedCount;
                timeChartArea.AxisX.MinorGrid.Interval = (maxFet - minFet) / 20;
                timeChartArea.AxisX.MajorGrid.Interval = (maxFet - minFet) / 20;

                timeChart.Series[1].Points.AddXY(minFet, avg);
                timeChart.Series[1].Points.AddXY(maxFet, avg);
            }));
            string times = "times = [", averages = "averages = [", expectations = "exprectations = [";
            for (int i = 0; i < clsCount; i++)
            {
                times += avgTimes[i].ToString() + " ";
                averages += avgFeatures[i].ToString() + " ";
                expectations += expFeatures[i].ToString() + " ";
                Dispatcher.Invoke(new Action(() => { timeChart.Series[0].Points.AddXY(avgFeatures[i], avgTimes[i]); }));
            }
            times += "];";
            averages += "];";
            expectations += "];";
            Dispatcher.Invoke(new Action(() => { matlabTableTextBox.Text = times + "\r\n" + averages + "\r\n" + expectations; }));
        }

        private void TimeTestBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string statusLabel;
            string logMessage;
            string error = null;

            if (e.Error != null)
            {
                statusLabel = "Status: Batch detection time testing completed with errors. Check event log for details";
                logMessage = "Batch detection time testing completed with errors:";
                error = e.Error.Message;
            }
            else if (e.Cancelled)
            {
                statusLabel = "Status:  Batch detection time testing cancelled. Check event log for details";
                logMessage = " Batch detection time testing cancelled.";
            }
            else
            {
                statusLabel = "Status: Batch detection time testing completed successful. Check event log for details";
                logMessage = "Batch detection time testing completed successful.";
            }

            CompletionEventsArg args = new CompletionEventsArg(statusLabel, logMessage, error, DateTime.Now, false);
            OnTestingCompletion(args);
        }
        #endregion
    }
}
