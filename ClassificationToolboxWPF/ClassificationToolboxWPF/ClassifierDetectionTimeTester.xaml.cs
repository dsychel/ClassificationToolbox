using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Reflection;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for ClassifierDetectionTimeTester.xaml
    /// </summary>
    public partial class ClassifierDetectionTimeTester : UserControl
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
        readonly BackgroundWorker worker = new BackgroundWorker() { WorkerReportsProgress = true };
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
            public string Name { get; set; }
            public long TimeFrame { get; set; }
            public long TimeWindow { get; set; }
            public long FPS { get; set; }
            public long WPS { get; set; }
        }
        #endregion

        public ClassifierDetectionTimeTester()
        {
            InitializeComponent();
            InitializeChart();

            //worker.WorkerSupportsCancellation = true;
            worker.WorkerReportsProgress = true;
            worker.DoWork += TimeTestBackgroundWorker_DoWork;
            worker.ProgressChanged += TimeTestBackgroundWorker_ProgressChanged;
            worker.RunWorkerCompleted += TimeTestBackgroundWorker_RunWorkerCompleted;

            GlobalFunctions.InitializePath(ref detectionClassifierPathTextBox, "testingClassifierPath");
            detectionExtractorSettings.TrySetFromString(detectionClassifierPathTextBox.Text);

            DetectionExtractorSettings_CheckedChanged(detectionExtractorSettings, new EventArgs());

            IsExtractorLoaded = false;
            IsClassifierLoaded = false;
        }

        #region Methods
        //public void StopTask()
        //{
        //    if (worker != null && worker.IsBusy)
        //        worker.CancelAsync();
        //}

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
            timeChartArea.AxisX.Title = "Iteration";
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
            timesSeries.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            timesSeries.Legend = "Legend1";
            timesSeries.Name = "Times [ms]";
            timesSeries.Points.Add(new System.Windows.Forms.DataVisualization.Charting.DataPoint(1, 0));
            timesSeries.Points.Add(new System.Windows.Forms.DataVisualization.Charting.DataPoint(100, 10));
            timesSeries.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int32;
            timesSeries.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int64;
            averageTimeSeries.BorderWidth = 2;
            averageTimeSeries.ChartArea = "TimeArea";
            averageTimeSeries.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            averageTimeSeries.Legend = "Legend1";
            averageTimeSeries.Name = "Average Time [ms]";
            averageTimeSeries.Points.Add(new System.Windows.Forms.DataVisualization.Charting.DataPoint(1, 5));
            averageTimeSeries.Points.Add(new System.Windows.Forms.DataVisualization.Charting.DataPoint(100, 5));
            averageTimeSeries.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int32;
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
                            timeToolTip.Show("Iteration = " + prop.XValue + ", Time =" + prop.YValues[0], this.timeChart,
                                            pos.X, pos.Y - 15);
                        }
                    }
                }
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
            GlobalFunctions.SelectClassifier(ref detectionClassifierPathTextBox, "testingClassifierPath");
            {
                detectionExtractorSettings.TrySetFromString(detectionClassifierPathTextBox.Text);
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
            if (detectionClassifierPathTextBox.Text == "" || !File.Exists(detectionClassifierPathTextBox.Text))
            {
                MessageBox.Show("Classifier path is empty or file doesn't exist.");
                return;
            }
            if (imagePictureBox.Source == null)
            {
                MessageBox.Show("Image is empty.");
                return;
            }
            if (!IsClassifierLoaded)
            {
                int status = NativeMethods.LoadClassifier(detectionClassifierPathTextBox.Text);
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
            
            StartedEventsArg args = new StartedEventsArg("Status: Working", "Detection time testing started.", DateTime.Now, (int)repetitionsNumericUpDown.Value, true);
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
            callback = (value) =>
            {
                worker.ReportProgress(value);
            };

            string classifierPath = Dispatcher.Invoke(new Func<string>(() => { return detectionClassifierPathTextBox.Text; }));
            string extractorType = Dispatcher.Invoke(new Func<string>(() => { return detectionExtractorSettings.ExtractorName; }));
            int repetitions = Dispatcher.Invoke(new Func<int>(() => { return (int)repetitionsNumericUpDown.Value; }));

            NativeMethods.DetectionParameters detectionParameters = new NativeMethods.DetectionParameters()
            {
                windowMinimalHeight = Dispatcher.Invoke(new Func<int>(() => { return (int)minHeightNumericUpDown.Value; })),
                windowMinimalWidth = Dispatcher.Invoke(new Func<int>(() => { return (int)minWidthNumericUpDown.Value; })),
                scales = Dispatcher.Invoke(new Func<int>(() => { return (int)scalesNumericUpDown.Value; })),
                windowJumpingRatio = Dispatcher.Invoke(new Func<double>(() => { return (double)jumpingRatioNumericUpDown.Value; })),
                windowScalingRatio = Dispatcher.Invoke(new Func<double>(() => { return (double)scalesRatioNumericUpDown.Value; }))
            };

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
            long max = 0;
            long min = long.MaxValue;
            for (int i = 0; i < repetitions; i++)
            {
                avg += results[i];
                if (results[i] > max)
                    max = results[i];
                if (results[i] < min)
                    min = results[i];
            }
            avg /= repetitions;

            Dispatcher.Invoke(new Action(() => 
            {
                long msAvg = (long)(avg / 1000000.0);
                long nsAvgPerWindow = (long)(1.0 * avg / detectionWindows.Count);
                long FPS = (long)(1.0 / (msAvg / 1000.0));
                long WPS = (long)(1.0 / (nsAvgPerWindow / 1000000000.0));

                double minimum = Math.Floor(min / 1000000.0);
                double maximum = Math.Ceiling(max / 1000000.0);
                if (Math.Abs(minimum - maximum) < 0.0000001)
                    maximum += 0.1 * minimum;

                timeChartArea.AxisY.Maximum = maximum;
                timeChartArea.AxisY.Minimum = minimum;
                timeChartArea.AxisY.IntervalAutoMode = System.Windows.Forms.DataVisualization.Charting.IntervalAutoMode.FixedCount;
                timeChartArea.AxisY.MinorGrid.Interval = (long)((maximum - minimum) / 20.0);
                timeChartArea.AxisY.MajorGrid.Interval = (long)((maximum - minimum) / 20.0);

                timeChartArea.AxisX.Maximum = repetitions;
                timeChartArea.AxisX.Minimum = 0;
                timeChartArea.AxisX.IntervalAutoMode = System.Windows.Forms.DataVisualization.Charting.IntervalAutoMode.FixedCount;
                timeChartArea.AxisX.MinorGrid.Interval = (long)(repetitions / 100.0);
                timeChartArea.AxisX.MajorGrid.Interval = (long)(repetitions / 10.0);

                timeChart.Series[1].Points.AddXY(1, msAvg);
                timeChart.Series[1].Points.AddXY(repetitions + 1, msAvg);
                timeDataGridView.Items.Add(new TimeTableRow() { Name = "Average", TimeFrame = msAvg, TimeWindow = nsAvgPerWindow, FPS = FPS, WPS = WPS });
                averageTime = msAvg;
            }));
            for (int i = 0; i < repetitions; i++)
            {
                long ms = (long)(results[i] / 1000000.0);
                long nsPerWindow = (long)(1.0 * results[i] / detectionWindows.Count);
                long FPS = (long)(1.0 / (ms / 1000.0));
                long WPS = (long)(1.0 / (nsPerWindow / 1000000000.0));

                avg += results[i];
                Dispatcher.Invoke(new Action(() => { timeDataGridView.Items.Add(new TimeTableRow() { Name = (i + 1).ToString(), TimeFrame = ms, TimeWindow = nsPerWindow, FPS = FPS, WPS = WPS }); }));
                Dispatcher.Invoke(new Action(() => { timeChart.Series[0].Points.AddXY(i + 1, (long)(results[i] / 1000000.0)); }));
            }
            Dispatcher.Invoke(new Action(() => { timeDataGridView.Items.Add(new TimeTableRow() { Name = "Feautres", TimeFrame = results[results.Length - 1] / 1000, TimeWindow = (long)(results[results.Length - 1] - Math.Floor(results[results.Length - 1] / 1000.0) * 1000), FPS = 0, WPS = 0 }); }));
        }

        private void TimeTestBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string statusLabel;
            string logMessage;
            string error = null;

            if (e.Error != null)
            {
                statusLabel = "Status: Detection time testing completed with errors. Check event log for details";
                logMessage = "Detection time testing completed with errors:";
                error = e.Error.Message;
            }
            else
            {
                statusLabel = "Status: Detection time testing completed successful. Check event log for details";
                logMessage = "Detection time testing completed successful. Average time: " + averageTime + "ms. Averge Feautres: " + results[results.Length - 1] / 1000.0 + '.';
            }

            CompletionEventsArg args = new CompletionEventsArg(statusLabel, logMessage, error, DateTime.Now, false);
            OnTestingCompletion(args);
        }
        #endregion
    }
}
