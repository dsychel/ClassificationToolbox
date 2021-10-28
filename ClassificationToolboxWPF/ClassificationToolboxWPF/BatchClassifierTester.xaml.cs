using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using System.Globalization;
using System.Threading;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for BatchClassifierTester.xaml
    /// </summary>
    public partial class BatchClassifierTester : UserControl
    {
        #region Struct
        struct TestingStatus
        {
            public string name;
            public int error;
            public long time;
            public int classifierNumber;
        }

        private class ClassifierItem
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public ImageSource Icon { get; set; }
        }
        #endregion

        #region Chart Elemnts
        System.Drawing.Point? prevROCPosition = null;
        readonly System.Windows.Forms.ToolTip rocToolTip = new System.Windows.Forms.ToolTip();

        readonly System.Windows.Forms.DataVisualization.Charting.ChartArea ROCchartArea = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
        readonly System.Windows.Forms.DataVisualization.Charting.Legend ROCchartLegend = new System.Windows.Forms.DataVisualization.Charting.Legend();
        readonly System.Windows.Forms.DataVisualization.Charting.Series ROCchartReferenceSeries = new System.Windows.Forms.DataVisualization.Charting.Series();
        readonly System.Windows.Forms.DataVisualization.Charting.DataPoint badSensitivityDataPoint = new System.Windows.Forms.DataVisualization.Charting.DataPoint(0D, 0D);
        readonly System.Windows.Forms.DataVisualization.Charting.DataPoint badSpecificityDataPoint = new System.Windows.Forms.DataVisualization.Charting.DataPoint(1D, 1D);
        readonly System.Windows.Forms.DataVisualization.Charting.Series ROCchartMainSeries = new System.Windows.Forms.DataVisualization.Charting.Series();
        readonly System.Windows.Forms.DataVisualization.Charting.Title mainTitle = new System.Windows.Forms.DataVisualization.Charting.Title();
        #endregion

        #region Fields
        readonly BackgroundWorker worker = new BackgroundWorker();
        int currentIndex = 0;
        long operationTime;

        readonly List<int[]> metrices = new List<int[]>();
        readonly List<string> classifierPaths = new List<string>();
        readonly List<string> testedClassifier = new List<string>();

        int[] classes = null;
        double[] testOutputs = null;
        double[] avgFeatures = null;
        readonly List<List<double[]>> ROCpoints = new List<List<double[]>>();
        readonly List<double> AUCs = new List<double>();

        NativeMethods.ProgressCallback callback;
        #endregion

        public BatchClassifierTester()
        {
            InitializeComponent();
            InitializeChart();

            worker.WorkerSupportsCancellation = true;
            worker.WorkerReportsProgress = true;
            worker.DoWork += BatchTestingBackgroundWorker_DoWork;
            worker.ProgressChanged += BatchTestingBackgroundWorker_ProgressChanged;
            worker.RunWorkerCompleted += BatchTestingBackgroundWorker_RunWorkerCompleted;

            GlobalFunctions.InitializeMetrices(ref confusionMatriceDataGridView, ref metricesMatrixDataGridView);
            GlobalFunctions.InitializePath(ref testingFeaturesTextBox, "testingFeaturesPath");
        }

        #region Methods
        public void StopTask()
        {
            if (worker != null && worker.IsBusy)
                worker.CancelAsync();
        }

        private void InitializeChart()
        {
            this.ROCchart.BorderlineColor = System.Drawing.Color.Transparent;
            this.ROCchart.BorderlineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            ROCchartArea.AxisX.Interval = 0.1D;
            ROCchartArea.AxisX.MajorGrid.Interval = 0.2D;
            ROCchartArea.AxisX.MajorGrid.LineColor = System.Drawing.Color.DimGray;
            ROCchartArea.AxisX.MajorTickMark.Enabled = false;
            ROCchartArea.AxisX.MajorTickMark.Interval = 0.2D;
            ROCchartArea.AxisX.Maximum = 1D;
            ROCchartArea.AxisX.Minimum = 0D;
            ROCchartArea.AxisX.MinorGrid.Enabled = true;
            ROCchartArea.AxisX.MinorGrid.Interval = 0.1D;
            ROCchartArea.AxisX.MinorGrid.LineColor = System.Drawing.Color.DarkGray;
            ROCchartArea.AxisX.Title = "1 - Specificity";
            ROCchartArea.AxisY.MajorGrid.Interval = 0.2D;
            ROCchartArea.AxisY.MajorGrid.LineColor = System.Drawing.Color.DimGray;
            ROCchartArea.AxisY.MajorTickMark.Enabled = false;
            ROCchartArea.AxisY.Maximum = 1D;
            ROCchartArea.AxisY.Minimum = 0D;
            ROCchartArea.AxisY.MinorGrid.Enabled = true;
            ROCchartArea.AxisY.MinorGrid.Interval = 0.1D;
            ROCchartArea.AxisY.MinorGrid.LineColor = System.Drawing.Color.DarkGray;
            ROCchartArea.AxisY.Title = "Sensitivity";
            ROCchartArea.BorderColor = System.Drawing.Color.Transparent;
            ROCchartArea.BorderDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            ROCchartArea.Name = "ROCArea";
            this.ROCchart.ChartAreas.Add(ROCchartArea);
            //this.ROCchart.Dock = System.Windows.Forms.DockStyle.Fill;
            ROCchartLegend.LegendStyle = System.Windows.Forms.DataVisualization.Charting.LegendStyle.Column;
            ROCchartLegend.Name = "Legend1";
            this.ROCchart.Legends.Add(ROCchartLegend);
            this.ROCchart.Location = new System.Drawing.Point(0, 0);
            this.ROCchart.Margin = new System.Windows.Forms.Padding(0);
            this.ROCchart.Name = "ROCchart";
            this.ROCchart.Palette = System.Windows.Forms.DataVisualization.Charting.ChartColorPalette.Bright;
            ROCchartReferenceSeries.BorderWidth = 2;
            ROCchartReferenceSeries.ChartArea = "ROCArea";
            ROCchartReferenceSeries.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            ROCchartReferenceSeries.Legend = "Legend1";
            ROCchartReferenceSeries.Name = "AUC: 0.5000";
            ROCchartReferenceSeries.Points.Add(badSensitivityDataPoint);
            ROCchartReferenceSeries.Points.Add(badSpecificityDataPoint);
            ROCchartMainSeries.BorderWidth = 2;
            ROCchartMainSeries.ChartArea = "ROCArea";
            ROCchartMainSeries.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            ROCchartMainSeries.Legend = "Legend1";
            ROCchartMainSeries.Name = "AUC: 0.0000";
            this.ROCchart.Series.Add(ROCchartReferenceSeries);
            this.ROCchart.Series.Add(ROCchartMainSeries);
            this.ROCchart.Size = new System.Drawing.Size(1084, 332);
            this.ROCchart.TabIndex = 1;
            this.ROCchart.Text = "ROC chart";
            mainTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            mainTitle.Name = "ROCTitle";
            mainTitle.Text = "ROC Chart";
            this.ROCchart.Titles.Add(mainTitle);
            this.ROCchart.MouseMove += new System.Windows.Forms.MouseEventHandler(this.ROCchart_MouseMove);
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
        #endregion

        #region Events
        private void BrowseTestingFeaturesButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFeatures(ref testingFeaturesTextBox, "testingFeaturesPath");
        }

        private void RemoveClassifierButton_Click(object sender, RoutedEventArgs e)
        {
            if (classifierListView.SelectedIndex >= 0)
            {
                int index = classifierListView.SelectedIndex;

                classifierListView.Items.RemoveAt(index);
                classifierPaths.RemoveAt(index);
            }
        }

        private void AddClassifierButton_Click(object sender, RoutedEventArgs e)
        {
            string path = (string)Properties.Settings.Default["testingClassifierPath"];
            if (path != "")
                path = Path.GetDirectoryName(path);
            if (!Directory.Exists(path))
                path = "";

            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog()
            {
                Filter = "Classifier Files | *.model",
                FileName = ""
            };
            if (path != "")
                openFileDialog.InitialDirectory = path;

            System.Windows.Forms.DialogResult result = openFileDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Properties.Settings.Default["testingClassifierPath"] = openFileDialog.FileName;
                Properties.Settings.Default.Save();

                string clsName = Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                string clsPath = openFileDialog.FileName;

                classifierPaths.Add(clsPath);
                classifierListView.Items.Add(new ClassifierItem { Name = clsName, Path = clsPath, Icon = new BitmapImage(new Uri(@"/Images/pending.png", UriKind.Relative)) });

            }
            openFileDialog.Dispose();
        }

        private void AllInOnChartCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (testedClassifier.Count != 0)
            {
                if (allInOnChartCheckBox.IsChecked == false)
                {
                    SaveLabel.Text = "Save Current Result";

                    ROCchart.Series.Clear();
                    ROCchart.Series.Add(ROCchartReferenceSeries);
                    ROCchart.Series.Add(ROCchartMainSeries);

                    ROCchart.Series[1].Points.Clear();

                    for (int i = 0; i < ROCpoints[currentIndex].Count; i++)
                        ROCchart.Series[1].Points.AddXY(ROCpoints[currentIndex][i][0], ROCpoints[currentIndex][i][1]);

                    ROCchart.Series[1].LegendText = "AUC: " + String.Format("{0:0.0000}", AUCs[currentIndex]);
                }
                else
                {
                    SaveLabel.Text = "Save All Result";

                    ROCchart.Series.Clear();
                    ROCchart.Series.Add(ROCchartReferenceSeries);

                    for (int c = 0; c < testedClassifier.Count; c++)
                    {
                        System.Windows.Forms.DataVisualization.Charting.Series ROCseries = new System.Windows.Forms.DataVisualization.Charting.Series
                        {
                            BorderWidth = 2,
                            ChartArea = "ROCArea",
                            ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line,
                            Legend = "Legend1",
                            Name = "ROCseries" + c
                        };
                        ROCchart.Series.Add(ROCseries);

                        for (int i = 0; i < ROCpoints[c].Count; i++)
                            ROCchart.Series[c + 1].Points.AddXY(ROCpoints[c][i][0], ROCpoints[c][i][1]);

                        ROCchart.Series[c + 1].LegendText = "Classifier " + (c + 1) + " AUC: " + String.Format("{0:0.0000}", AUCs[c]);
                    }
                }
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentIndex > 0)
            {
                currentIndex--;
                if (currentIndex == 0)
                    previousButton.IsEnabled = false;

                clsNameTextBox.Text = testedClassifier[currentIndex];
                ((GlobalFunctions.MetricesTableRow)metricesMatrixDataGridView.Items[14]).Value = avgFeatures[currentIndex];
                GlobalFunctions.PopulateConfusionMatrix(ref confusionMatriceDataGridView, metrices[currentIndex][0], metrices[currentIndex][1], metrices[currentIndex][2], metrices[currentIndex][3]);
                GlobalFunctions.CalculateMetrices(ref confusionMatriceDataGridView, ref metricesMatrixDataGridView);

                if (allInOnChartCheckBox.IsChecked == false)
                {
                    ROCchart.Series[1].Points.Clear();

                    for (int i = 0; i < ROCpoints[currentIndex].Count; i++)
                        ROCchart.Series[1].Points.AddXY(ROCpoints[currentIndex][i][0], ROCpoints[currentIndex][i][1]);

                    ROCchart.Series[1].LegendText = "AUC: " + String.Format("{0:0.0000}", AUCs[currentIndex]);
                }

                nextButton.IsEnabled = true;
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentIndex < testedClassifier.Count - 1)
            {
                currentIndex++;
                if (currentIndex == testedClassifier.Count - 1)
                    nextButton.IsEnabled = false;

                clsNameTextBox.Text = testedClassifier[currentIndex];
                ((GlobalFunctions.MetricesTableRow)metricesMatrixDataGridView.Items[14]).Value = avgFeatures[currentIndex];
                GlobalFunctions.PopulateConfusionMatrix(ref confusionMatriceDataGridView, metrices[currentIndex][0], metrices[currentIndex][1], metrices[currentIndex][2], metrices[currentIndex][3]);
                GlobalFunctions.CalculateMetrices(ref confusionMatriceDataGridView, ref metricesMatrixDataGridView);

                if (allInOnChartCheckBox.IsChecked == false)
                {
                    ROCchart.Series[1].Points.Clear();

                    for (int i = 0; i < ROCpoints[currentIndex].Count; i++)
                        ROCchart.Series[1].Points.AddXY(ROCpoints[currentIndex][i][0], ROCpoints[currentIndex][i][1]);

                    ROCchart.Series[1].LegendText = "AUC: " + String.Format("{0:0.0000}", AUCs[currentIndex]);
                }

                previousButton.IsEnabled = true;
            }
        }

        private void ROCchart_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            var pos = e.Location;
            if (prevROCPosition.HasValue && pos == prevROCPosition.Value)
                return;
            rocToolTip.RemoveAll();
            prevROCPosition = pos;
            var results = ROCchart.HitTest(pos.X, pos.Y, false, System.Windows.Forms.DataVisualization.Charting.ChartElementType.DataPoint);
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
                            rocToolTip.Show("FAR = " + prop.XValue + ", TPR =" + prop.YValues[0], this.ROCchart,
                                            pos.X, pos.Y - 15);
                        }
                    }
                }
            }
        }

        private void TestingSaveReportButton_Click(object sender, RoutedEventArgs e)
        {
            if (testedClassifier.Count == 0)
            {
                MessageBox.Show("No data to save. Generate data using Test Classifier button.");
                return;
            }

            if (allInOnChartCheckBox.IsChecked == false)
            {
                System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog()
                {
                    Filter = "Result Files(*.result.txt) | *.result.txt",
                    FileName = Path.GetFileNameWithoutExtension(clsNameTextBox.Text)
                };
                if (Properties.Settings.Default.testingClassifierResultPath != "" && Directory.Exists(Properties.Settings.Default.testingClassifierResultPath))
                    saveFileDialog.InitialDirectory = Properties.Settings.Default.testingClassifierResultPath;
                else
                    saveFileDialog.InitialDirectory = "";

                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    Properties.Settings.Default.testingClassifierResultPath = Path.GetDirectoryName(saveFileDialog.FileName);
                    Properties.Settings.Default.Save();

                    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

                    using (StreamWriter sw = new StreamWriter(saveFileDialog.FileName))
                    {
                        sw.WriteLine("Result:");
                        sw.WriteLine("Classifier: " + Path.GetFileName(clsNameTextBox.Text));
                        sw.WriteLine("Features: " + Path.GetFileName(testingFeaturesTextBox.Text));
                        sw.WriteLine();

                        sw.WriteLine("Threshold: 0.0");
                        sw.WriteLine("AUC: " + AUCs[currentIndex]);


                        sw.WriteLine("TP: " + ((GlobalFunctions.ConfusionTableRow)confusionMatriceDataGridView.Items[1]).T);
                        sw.WriteLine("FP: " + ((GlobalFunctions.ConfusionTableRow)confusionMatriceDataGridView.Items[1]).N);
                        sw.WriteLine("TN: " + ((GlobalFunctions.ConfusionTableRow)confusionMatriceDataGridView.Items[0]).N);
                        sw.WriteLine("FN: " + ((GlobalFunctions.ConfusionTableRow)confusionMatriceDataGridView.Items[0]).T);

                        foreach (GlobalFunctions.MetricesTableRow row in metricesMatrixDataGridView.Items)
                            sw.WriteLine(row.Name + ": " + row.Value);
                        sw.WriteLine();

                        sw.WriteLine("ROC VALUES:");
                        for (int i = 0; i < ROCpoints[currentIndex].Count; i++)
                            sw.WriteLine("FAR: {0:0.00000000}, SENS: {1:0.00000000}, THR: {2:0.00000000}", ROCpoints[currentIndex][i][0], ROCpoints[currentIndex][i][1], ROCpoints[currentIndex][i][2]);

                        ROCchart.SaveImage(Path.GetDirectoryName(saveFileDialog.FileName) + "\\" + Path.GetFileNameWithoutExtension(saveFileDialog.FileName) + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
                        ROCchart.SaveImage(Path.GetDirectoryName(saveFileDialog.FileName) + "\\" + Path.GetFileNameWithoutExtension(saveFileDialog.FileName) + ".emf", System.Drawing.Imaging.ImageFormat.Emf);
                    }
                }
                saveFileDialog.Dispose();
            }
            else
            {
                System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog()
                {
                    Filter = "Result Files(*.result.txt) | *.result.txt",
                    FileName = ""
                };
                if (Properties.Settings.Default.testingClassifierResultPath != "" && Directory.Exists(Properties.Settings.Default.testingClassifierResultPath))
                    saveFileDialog.InitialDirectory = Properties.Settings.Default.testingClassifierResultPath;
                else
                    saveFileDialog.InitialDirectory = "";


                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string folder = Path.GetDirectoryName(saveFileDialog.FileName);
                    //string name = Path.GetFileName(saveFileDialog.FileName);

                    for (int c = 0; c < testedClassifier.Count; c++)
                    {
                        using (StreamWriter sw = new StreamWriter(folder + "\\Classifier_" + (c + 1) + "_" + Path.GetFileNameWithoutExtension(testedClassifier[c]) + ".result.txt"))
                        {
                            sw.WriteLine("Result:");
                            sw.WriteLine("Classifier: " + testedClassifier[c]);
                            sw.WriteLine("Features: " + Path.GetFileName(testingFeaturesTextBox.Text));
                            sw.WriteLine();

                            sw.WriteLine("Threshold: 0.0");
                            sw.WriteLine("AUC: " + AUCs[c]);


                            sw.WriteLine("TP: " + metrices[c][0]);
                            sw.WriteLine("FP: " + metrices[c][2]);
                            sw.WriteLine("TN: " + metrices[c][1]);
                            sw.WriteLine("FN: " + metrices[c][3]);

                            double TP = metrices[c][0];
                            double TN = metrices[c][1];
                            double FP = metrices[c][2];
                            double FN = metrices[c][3];

                            double sensitivity = TP / (TP + FN);
                            double FPR = FP / (FP + TN);

                            sw.WriteLine("Accuracy" + ": " + (TP + TN) / (TP + TN + FP + FN));
                            sw.WriteLine("Error" + ": " + (FP + FN) / (TP + TN + FP + FN));
                            sw.WriteLine("Sensitivity (TPR)" + ": " + (TP) / (TP + FN));
                            sw.WriteLine("Specificity (SPC)" + ": " + (TN) / (TN + FP));
                            sw.WriteLine("F1 - score" + ": " + (2 * TP) / (2 * TP + FP + FN));
                            sw.WriteLine("Precision (PPV)" + ": " + (TP) / (TP + FP));
                            sw.WriteLine("Negative predictive value (NPV)" + ": " + (TN) / (TN + FN));
                            sw.WriteLine("Fall - out (FAR)" + ": " + (FP) / (TN + FP));
                            sw.WriteLine("False negative rate" + ": " + (FN) / (TP + FN));
                            sw.WriteLine("False discovery rate" + ": " + (FP) / (TP + FP));
                            sw.WriteLine("Matthews correlation coefficient" + ": " + ((TP * TN) - (FP * FN)) / Math.Sqrt((TP + FP) * (TP + FN) * (TN + FP) * (TN + FN)));
                            sw.WriteLine("Informedness" + ": " + ((TP / (TP + FN)) + (TN / (TN + FP)) - 1.0));
                            sw.WriteLine("Markedness" + ": " + ((TP / (TP + FP)) + (TN / (TN + FN)) - 1.0));
                            sw.WriteLine("Euclidean distance (FAR = 0, TPR = 1)" + ": " + Math.Sqrt(Math.Pow(0.0 - FPR, 2.0) + Math.Pow(1.0 - sensitivity, 2.0)));
                            sw.WriteLine();

                            sw.WriteLine("ROC VALUES:");
                            for (int i = 0; i < ROCpoints[c].Count; i++)
                                sw.WriteLine("FAR: {0:0.00000000}, SENS: {1:0.00000000}, THR: {2:0.00000000}", ROCpoints[c][i][0], ROCpoints[c][i][1], ROCpoints[c][i][2]);

                        }
                    }
                    ROCchart.SaveImage(Path.GetDirectoryName(saveFileDialog.FileName) + "\\" + Path.GetFileNameWithoutExtension(saveFileDialog.FileName) + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
                    ROCchart.SaveImage(Path.GetDirectoryName(saveFileDialog.FileName) + "\\" + Path.GetFileNameWithoutExtension(saveFileDialog.FileName) + ".emf", System.Drawing.Imaging.ImageFormat.Emf);
                }
                saveFileDialog.Dispose();
            }
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (testingFeaturesTextBox.Text == "" || !File.Exists(testingFeaturesTextBox.Text))
            {
                MessageBox.Show("Features path is empty or file doesn't exist.");
                return;
            }
            if (classifierListView.Items.Count == 0)
            {
                MessageBox.Show("Classifier list is empty.");
                return;
            }
            if (worker != null && worker.IsBusy)
            {
                MessageBox.Show("Application is busy.");
                return;
            }

            for (int i = 0; i < classifierListView.Items.Count; i++)
                ((ClassifierItem)classifierListView.Items[i]).Icon = new BitmapImage(new Uri(@"/Images/pending.png", UriKind.Relative));
            classifierListView.Items.Refresh();

            string logMessage = "Classifiers testing started. Features used: " + Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(testingFeaturesTextBox.Text)) + ".";
            StartedEventsArg args = new StartedEventsArg("Status: Working", logMessage, DateTime.Now, classifierListView.Items.Count, true);
            OnTestingStarted(args);

            currentIndex = 0;
            if (classifierListView.Items.Count > 1)
                nextButton.IsEnabled = true;

            if (allInOnChartCheckBox.IsChecked == true)
            {
                ROCchart.Series.Clear();
                ROCchart.Series.Add(ROCchartReferenceSeries);
            }
            else
            {
                ROCchart.Series.Clear();
                ROCchart.Series.Add(ROCchartReferenceSeries);
                ROCchart.Series.Add(ROCchartMainSeries);

                ROCchart.Series[1].Points.Clear();
            }

            worker.RunWorkerAsync();
        }

        private void BatchTestingBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            TestingStatus status = ((TestingStatus)e.UserState);

            string logMessage;
            if (status.error == -10)
            {
                logMessage = "Classifier testing canceled (" + status.name + ")";
                ((ClassifierItem)classifierListView.Items[status.classifierNumber]).Icon = new BitmapImage(new Uri(@"/Images/error.png", UriKind.Relative));
            }
            else if (status.error < 0)
            {
                logMessage = "Classifier testing (" + status.name + ") completed with error: " + GlobalFunctions.GetErrorDescription(status.error);
                ((ClassifierItem)classifierListView.Items[status.classifierNumber]).Icon = new BitmapImage(new Uri(@"/Images/error.png", UriKind.Relative));
            }
            else
            {
                logMessage = "Classifier testing (" + status.name + ") completed succesful. Elapsed time: " + status.time + "ms.";
                ((ClassifierItem)classifierListView.Items[status.classifierNumber]).Icon = new BitmapImage(new Uri(@"/Images/successful.png", UriKind.Relative));
            }
            classifierListView.Items.Refresh();

            ProgressingEventsArg args = new ProgressingEventsArg(e.ProgressPercentage, -1, logMessage);
            OnTestingProgressing(args);
        }

        unsafe private void BatchTestingBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            AUCs.Clear();
            metrices.Clear();
            testedClassifier.Clear();
            ROCpoints.Clear();

            e.Result = 0;
            operationTime = 0;
            int stage = 0;
            long currentTime = 0;
            string testFeatureFileName = Dispatcher.Invoke(new Func<string>(() => { return testingFeaturesTextBox.Text; }));
            bool allInOne = Dispatcher.Invoke(new Func<bool>(() => { return allInOnChartCheckBox.IsChecked == true; }));

            Stopwatch stopwatchfull = new Stopwatch();
            stopwatchfull.Start();

            int samples;
            using (BinaryReader reader = new BinaryReader(File.Open(testFeatureFileName, FileMode.Open)))
                samples = reader.ReadInt32();
            if (samples <= 0)
                GlobalFunctions.ThrowError((int)GlobalFunctions.ERRORS.CORRUPTED_FEATURES_FILE);

            //int code = NativeMethods.LoadLearningData(testFeatureFileName);
            //if (code < 0)
            //    GlobalFunctions.ThrowError(code);

            Dispatcher.Invoke(new Action(() =>
            {
                avgFeatures = new double[classifierPaths.Count];
            }));

            for (int i = 0; i < classifierPaths.Count; i++)
            {
                callback = (value) =>
                {
                    this.OnTestingStatusChanged(new StatusChangedArg("Status: Working | Classifier id: " + (i + 1).ToString() + " | Processed samples: " + value.ToString()));
                };

                stage = i;
                if (this.worker.CancellationPending)
                {
                    for (int j = i; j < classifierPaths.Count; j++)
                    {
                        TestingStatus cancelStatus = new TestingStatus()
                        {
                            error = (int)GlobalFunctions.ERRORS.OPERATION_CANCELED,
                            name = Path.GetFileNameWithoutExtension(classifierPaths[j]),
                            time = 0,
                            classifierNumber = j
                        };

                        worker.ReportProgress(i+1, cancelStatus);
                    }
                    e.Cancel = true;
                    return;
                }

                Dispatcher.Invoke(new Action(() =>
                {
                    classes = new int[samples];
                    testOutputs = new double[samples];
                }));
                int status;
                fixed (int* classPointer = classes)
                {
                    fixed (double* thresholdPointer = testOutputs)
                    {
                        fixed (double* fetPointer = &avgFeatures[i])
                        {
                            string classifierPath = classifierPaths[i];

                            Stopwatch stopwatch = new Stopwatch();
                            stopwatch.Start();

                            status = NativeMethods.Testing(testFeatureFileName, classifierPath, classPointer, thresholdPointer, fetPointer, callback) ;

                            stopwatch.Stop();
                            currentTime = stopwatch.ElapsedMilliseconds;

                            if (status < 0)
                                GlobalFunctions.ThrowError(status);
                        }
                    }
                }
                testedClassifier.Add(Path.GetFileNameWithoutExtension(classifierPaths[i]));

                double thr0 = 0.0;
                double TP = 0, FP = 0, TN = 0, FN = 0;
                int positives = 0, negatives = 0;
                for (int t = 0; t < samples; t++)
                {
                    if (testOutputs[t] < thr0 && classes[t] == -1)
                    {
                        negatives++;
                        TN++;
                    }
                    else if (testOutputs[t] < thr0 && classes[t] == 1)
                    {
                        positives++;
                        FN++;
                    }
                    else if (testOutputs[t] >= thr0 && classes[t] == 1)
                    {
                        positives++;
                        TP++;
                    }
                    else
                    {
                        negatives++;
                        FP++;
                    }
                }
                metrices.Add(new int[4]);
                metrices[i][0] = (int)TP;
                metrices[i][1] = (int)TN;
                metrices[i][2] = (int)FP;
                metrices[i][3] = (int)FN;

                // ROC
                Array.Sort(testOutputs, classes);

                ROCpoints.Add(new List<double[]>());
                double AUC = 0.0, x1, x2, y1, y2;
                double thr = testOutputs[0] - 0.1, prvThr = Double.NegativeInfinity;
                double sensitivity = 1.0, FPR = 1.0;
                double TPt = positives, FPt = negatives, TNt = 0, FNt = 0;
                double currentThr = 0.0;

                double[,] points = new double[samples + 1, 3];
                int pit = 0;
                points[pit, 0] = FPR; points[pit, 1] = sensitivity; points[pit, 2] = thr;
                pit++;

                x1 = FPR;
                y1 = sensitivity;
                TP = TPt; FP = FPt; TN = TNt; FN = FNt;

                for (int t = 0; t < testOutputs.Length; t++)
                {
                    while (t < testOutputs.Length - 1 && testOutputs[t] == testOutputs[t + 1])
                    {
                        if (classes[t] == -1)
                        {
                            TNt += 1; FPt -= 1;
                        }
                        else
                        {
                            TPt -= 1; FNt += 1;
                        }
                        t++;
                    }
                    if (classes[t] == -1)
                    {
                        TNt += 1; FPt -= 1;
                    }
                    else
                    {
                        TPt -= 1; FNt += 1;
                    }

                    prvThr = thr;
                    if (t != testOutputs.Length - 1)
                        thr = (testOutputs[t] + testOutputs[t + 1]) / 2.0;
                    else
                        thr = testOutputs[t] + 0.1;

                    sensitivity = TPt / (TPt + FNt);
                    FPR = FPt / (FPt + TNt);

                    points[pit, 0] = FPR; points[pit, 1] = sensitivity; points[pit, 2] = thr;
                    pit++;

                    x2 = FPR; y2 = sensitivity;
                    AUC += Math.Abs(((y1 + y2) / 2.0) * (x1 - x2));
                    x1 = x2; y1 = y2;

                    if (thr == 0.0 || (prvThr < 0.0 && thr > 0.0))
                    {
                        TP = TPt; FP = FPt; TN = TNt; FN = FNt;
                        currentThr = thr;
                    }
                }
                if (thr <= 0.0)
                {
                    TP = TPt; FP = FPt; TN = TNt; FN = FNt;
                    currentThr = thr;
                }

                AUCs.Add(AUC);

                Dispatcher.Invoke(new Action(() =>
                {
                    if(allInOne)
                    {
                        System.Windows.Forms.DataVisualization.Charting.Series ROCseries = new System.Windows.Forms.DataVisualization.Charting.Series
                        {
                            BorderWidth = 2,
                            ChartArea = "ROCArea",
                            ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line,
                            Legend = "Legend1",
                            Name = "ROCseries" + i
                        };
                        ROCchart.Series.Add(ROCseries);
                    }

                    ((GlobalFunctions.MetricesTableRow)metricesMatrixDataGridView.Items[14]).Value = avgFeatures[currentIndex];
                    GlobalFunctions.PopulateConfusionMatrix(ref confusionMatriceDataGridView, (int)TP, (int)TN, (int)FP, (int)FN);
                    GlobalFunctions.CalculateMetrices(ref confusionMatriceDataGridView, ref metricesMatrixDataGridView);

                    if (!Double.IsInfinity(sensitivity) && !Double.IsNaN(sensitivity) && !Double.IsInfinity(FPR) && !Double.IsNaN(FPR))
                    {
                        if (i == 0 && !allInOne)
                            ROCchart.Series[1].Points.AddXY(points[0, 0], points[0, 1]);
                        else if (allInOne)
                            ROCchart.Series[i + 1].Points.AddXY(points[0, 0], points[0, 1]);

                        ROCpoints[i].Add(new double[] { points[0, 0], points[0, 1], points[0, 2] });

                        for (int k = 1; k < pit - 1; k++)
                        {
                            if (points[k - 1, 0] == points[k, 0] && points[k, 0] == points[k + 1, 0])
                                continue;
                            if (points[k - 1, 1] == points[k, 1] && points[k, 1] == points[k + 1, 1])
                                continue;

                            if (i == 0 && !allInOne)
                                ROCchart.Series[1].Points.AddXY(points[k, 0], points[k, 1]);
                            else if (allInOne)
                                ROCchart.Series[i + 1].Points.AddXY(points[k, 0], points[k, 1]);

                            ROCpoints[i].Add(new double[] { points[k, 0], points[k, 1], points[k, 2] });
                        }

                        if (i == 0 && !allInOne)
                        {
                            ROCchart.Series[1].Points.AddXY(points[pit - 1, 0], points[pit - 1, 1]);
                            ROCchart.Series[1].LegendText = "AUC: " + String.Format("{0:0.0000}", AUC);
                        }
                        else if (allInOne)
                        {
                            ROCchart.Series[i + 1].Points.AddXY(points[pit - 1, 0], points[pit - 1, 1]);
                            ROCchart.Series[i + 1].LegendText = "Classifier " + (i + 1) + " AUC: " + String.Format("{0:0.0000}", AUC);
                        }

                        ROCpoints[i].Add(new double[] { points[pit - 1, 0], points[pit - 1, 1], points[pit - 1, 2] });
                    }
                }));
                // END ROC

                TestingStatus clsStatus;
                if (status < 0)
                {
                    clsStatus.error = status;
                    e.Result = status;
                }
                else
                {
                    clsStatus.error = 0;
                }
                clsStatus.name = Path.GetFileNameWithoutExtension(classifierPaths[i]);
                clsStatus.time = currentTime;
                clsStatus.classifierNumber = i;

                worker.ReportProgress(i+1, clsStatus);
            }
            //NativeMethods.FreeLearningData();

            Dispatcher.Invoke(new Action(() =>
            {
                clsNameTextBox.Text = Path.GetFileNameWithoutExtension(classifierPaths[0]);

                GlobalFunctions.PopulateConfusionMatrix(ref confusionMatriceDataGridView, metrices[0][0], metrices[0][1], metrices[0][2], metrices[0][3]);
                GlobalFunctions.CalculateMetrices(ref confusionMatriceDataGridView, ref metricesMatrixDataGridView);
            }));

            stopwatchfull.Stop();
            operationTime = stopwatchfull.ElapsedMilliseconds;
        }
        private void BatchTestingBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string statusLabel;
            string logMessage;
            string error = null;
            int errorCode = 0;

            if (!e.Cancelled && e.Error == null)
                errorCode = (int)e.Result;


            if (e.Cancelled)
            {
                statusLabel = "Status: Classifiers testing cancelled. Check event log for details";
                logMessage = "Classifiers testing cancelled.";
            }
            else if (e.Error != null)
            {
                statusLabel = "Status: Classifiers testing completed with errors. Check event log for details.";
                logMessage = "Classifiers testing completed with errors: ";
                error = e.Error.Message;
            }
            else if (errorCode < 0)
            {
                statusLabel = "Status: Classifiers testing completed with errors. Check event log for details.";
                logMessage = "Classifiers testing completed with errors.";
            }
            else
            {
                statusLabel = "Status: Classifiers testing completed successful. Check event log for details.";
                logMessage = "Classifiers testing completed successful. Elapsed time: " + operationTime + "ms.";
            }

            classes = null;
            testOutputs = null;

            CompletionEventsArg args = new CompletionEventsArg(statusLabel, logMessage, error, DateTime.Now, false);
            OnTestingCompletion(args);
        }
        #endregion
    }
}
