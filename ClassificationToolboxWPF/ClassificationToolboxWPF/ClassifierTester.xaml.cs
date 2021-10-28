using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Collections.Generic;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for ClassifierTester.xaml
    /// </summary>
    public partial class ClassifierTester : UserControl
    {
        #region Chart Elemnts
        System.Drawing.Point? prevROCPosition = null;
        readonly System.Windows.Forms.ToolTip rocToolTip = new System.Windows.Forms.ToolTip();

        readonly System.Windows.Forms.DataVisualization.Charting.ChartArea ROCchartArea = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
        readonly System.Windows.Forms.DataVisualization.Charting.Legend ROCchartLegend = new System.Windows.Forms.DataVisualization.Charting.Legend();
        readonly System.Windows.Forms.DataVisualization.Charting.Series ROCchartReferenceSeries = new System.Windows.Forms.DataVisualization.Charting.Series();
        readonly System.Windows.Forms.DataVisualization.Charting.DataPoint badSensitivityDataPoint = new System.Windows.Forms.DataVisualization.Charting.DataPoint(0D, 0D);
        readonly System.Windows.Forms.DataVisualization.Charting.DataPoint badSpecificityDataPoint = new System.Windows.Forms.DataVisualization.Charting.DataPoint(1D, 1D);
        readonly System.Windows.Forms.DataVisualization.Charting.Series ROCchartMainSeries = new System.Windows.Forms.DataVisualization.Charting.Series();
        readonly System.Windows.Forms.DataVisualization.Charting.Series ROCchartThresholdSeries = new System.Windows.Forms.DataVisualization.Charting.Series();
        readonly System.Windows.Forms.DataVisualization.Charting.DataPoint currentThresholdDataPoint = new System.Windows.Forms.DataVisualization.Charting.DataPoint(0.5D, 0.5D);
        readonly System.Windows.Forms.DataVisualization.Charting.Title mainTitle = new System.Windows.Forms.DataVisualization.Charting.Title();
        readonly System.Windows.Forms.DataVisualization.Charting.Title subtitle = new System.Windows.Forms.DataVisualization.Charting.Title();
        #endregion

        #region Fields
        BackgroundWorker worker;
        long operationTime;

        int[] classes = null;
        double[] testOutputs = null;
        List<double> thresholds = null;
        double avgFeatures = 0;

        NativeMethods.ProgressCallback callback;
        #endregion

        #region Constructors
        public ClassifierTester()
        {
            InitializeComponent();
            InitializeChart();
            InitializeCombox();
            GlobalFunctions.InitializeMetrices(ref confusionMatriceDataGridView, ref metricesMatrixDataGridView);

            GlobalFunctions.InitializePath(ref testingClassifierTextBox, "testingClassifierPath");
            GlobalFunctions.InitializePath(ref testingFeaturesTextBox, "testingFeaturesPath");
        }
        #endregion

        #region Methods
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
            ROCchartThresholdSeries.ChartArea = "ROCArea";
            ROCchartThresholdSeries.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Point;
            ROCchartThresholdSeries.Legend = "Legend1";
            ROCchartThresholdSeries.MarkerBorderColor = System.Drawing.Color.Black;
            ROCchartThresholdSeries.MarkerColor = System.Drawing.Color.Red;
            ROCchartThresholdSeries.MarkerSize = 7;
            ROCchartThresholdSeries.MarkerStyle = System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Circle;
            ROCchartThresholdSeries.Name = "Threshold";
            ROCchartThresholdSeries.Points.Add(currentThresholdDataPoint);
            ROCchartThresholdSeries.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            ROCchartThresholdSeries.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            this.ROCchart.Series.Add(ROCchartReferenceSeries);
            this.ROCchart.Series.Add(ROCchartMainSeries);
            this.ROCchart.Series.Add(ROCchartThresholdSeries);
            this.ROCchart.Size = new System.Drawing.Size(1084, 332);
            this.ROCchart.TabIndex = 1;
            this.ROCchart.Text = "ROC chart";
            mainTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            mainTitle.Name = "ROCTitle";
            mainTitle.Text = "ROC Chart";
            subtitle.Docking = System.Windows.Forms.DataVisualization.Charting.Docking.Bottom;
            subtitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            subtitle.Name = "Threshold";
            subtitle.Text = "Current threshold: 0.0000";
            this.ROCchart.Titles.Add(mainTitle);
            this.ROCchart.Titles.Add(subtitle);
            this.ROCchart.MouseMove += new System.Windows.Forms.MouseEventHandler(this.ROCchart_MouseMove);

            //ChartHost.InvalidateVisual();
        }

        private void InitializeCombox()
        {
            thrOptimizationMetricesComboBox.Items.Add("Accuracy");
            thrOptimizationMetricesComboBox.Items.Add("Error");
            thrOptimizationMetricesComboBox.Items.Add("Sensitivity (TPR)");
            thrOptimizationMetricesComboBox.Items.Add("Specificity (SPC)");
            thrOptimizationMetricesComboBox.Items.Add("F1 - score");
            thrOptimizationMetricesComboBox.Items.Add("Precision (PPV)");
            thrOptimizationMetricesComboBox.Items.Add("Negative predictive value (NPV)");
            thrOptimizationMetricesComboBox.Items.Add("Fall - out (FAR)");
            thrOptimizationMetricesComboBox.Items.Add("False negative rate");
            thrOptimizationMetricesComboBox.Items.Add("False discovery rate");
            thrOptimizationMetricesComboBox.Items.Add("Matthews correlation coefficient");
            thrOptimizationMetricesComboBox.Items.Add("Informedness");
            thrOptimizationMetricesComboBox.Items.Add("Markedness");
            thrOptimizationMetricesComboBox.Items.Add("Euclidean distance (FAR = 0, TPR = 1)");
            thrOptimizationMetricesComboBox.SelectedIndex = 0;
        }

        public void StopTask()
        {
            if (worker != null && worker.IsBusy)
                worker.CancelAsync();
        }

        private delegate double MetricFunction(double TP, double FP, double TN, double FN);
        private void InitializeMetricFunctionForOptimization(out MetricFunction metricFunction, string metric)
        {
            switch (metric)
            {
                case "Accuracy":
                    metricFunction = (TP, FP, TN, FN) => -(TP + TN) / (TP + TN + FP + FN);
                    break;
                case "Error":
                    metricFunction = (TP, FP, TN, FN) => (FP + FN) / (TP + TN + FP + FN);
                    break;
                case "Sensitivity (TPR)":
                    metricFunction = (TP, FP, TN, FN) => -TP / (TP + FN);
                    break;
                case "Specificity (SPC)":
                    metricFunction = (TP, FP, TN, FN) => -TN / (TN + FP);
                    break;
                case "F1 - score":
                    metricFunction = (TP, FP, TN, FN) => -(2 * TP) / (2 * TP + FP + FN);
                    break;
                case "Precision (PPV)":
                    metricFunction = (TP, FP, TN, FN) => -TP / (TP + FP);
                    break;
                case "Negative predictive value (NPV)":
                    metricFunction = (TP, FP, TN, FN) => -TN / (TN + FN);
                    break;
                case "Fall - out (FAR)":
                    metricFunction = (TP, FP, TN, FN) => FP / (FP + TN);
                    break;
                case "False negative rate":
                    metricFunction = (TP, FP, TN, FN) => FN / (TP + FN);
                    break;
                case "False discovery rate":
                    metricFunction = (TP, FP, TN, FN) => FP / (TP + FP);
                    break;
                case "Matthews correlation coefficient":
                    metricFunction = (TP, FP, TN, FN) => -((TP * TN) - (FP * FN)) / Math.Sqrt(1.0 * (TP + FP) * (TP + FN) * (TN + FP) * (TN + FN));
                    break;
                case "Informedness":
                    metricFunction = (TP, FP, TN, FN) => -(TP / (TP + FN) + TN / (TN + FP) - 1);
                    break;
                case "Markedness":
                    metricFunction = (TP, FP, TN, FN) => -(TP / (TP + FP) + TN / (TN + FN) - 1);
                    break;
                case "Euclidean distance (FAR = 0, TPR = 1)":
                    metricFunction = (TP, FP, TN, FN) => Math.Sqrt(Math.Pow(0 - (FP / (FP + TN)), 2) + Math.Pow(1 - (TP / (TP + FN)), 2));
                    break;
                default:
                    MessageBox.Show("Incorrect metric. Switching to accuracy.");
                    metricFunction = (TP, FP, TN, FN) => -(TP + TN) / (TP + TN + FP + FN);
                    break;
            }
        }

        private void UpdateChart(double TP, double FP, double TN, double FN, double thr)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                double sensitivity = TP / (TP + FN);
                double FPR = FP / (FP + TN);

                GlobalFunctions.PopulateConfusionMatrix(ref confusionMatriceDataGridView, (int)TP, (int)TN, (int)FP, (int)FN);
                GlobalFunctions.CalculateMetrices(ref confusionMatriceDataGridView, ref metricesMatrixDataGridView);

                if (!Double.IsInfinity(sensitivity) && !Double.IsNaN(sensitivity) && !Double.IsInfinity(FPR) && !Double.IsNaN(FPR))
                {
                    if (FPR == 0.0)
                        FPR += 0.00000000000000001;
                    ROCchart.Series[2].Points.AddXY(FPR, sensitivity);
                }
                ROCchart.Titles[1].Text = "Current threshold: " + String.Format("{0:0.0000}", thr);
            }));
        }

        private void OptimizeByMetricAsc(string metric)
        {
            InitializeMetricFunctionForOptimization(out MetricFunction metricFunction, metric);

            double thr = double.NegativeInfinity;
            double value = Double.PositiveInfinity, tmpVaulue;

            double TP = 0, FP = 0, TN = 0, FN = 0;
            double TPt = 0, FPt = 0, TNt = 0, FNt = 0;

            for (int i = 0; i < classes.Length; i++)
            {
                if (classes[i] == 1)
                    TPt += 1;
                else
                    FPt += 1;
            }
            tmpVaulue = metricFunction(TPt, FPt, TNt, FNt);

            if (tmpVaulue < value)
            {
                value = tmpVaulue;
                TP = TPt; FP = FPt; TN = TNt; FN = FNt;
                thr = testOutputs[0] - 0.1;
            }

            for (int i = 0; i < testOutputs.Length; i++)
            {
                while (i < testOutputs.Length - 1 && testOutputs[i] == testOutputs[i + 1])
                {
                    if (classes[i] == -1)
                    {
                        TNt += 1;
                        FPt -= 1;
                    }
                    else
                    {
                        TPt -= 1;
                        FNt += 1;
                    }
                    i++;
                }
                if (classes[i] == -1)
                {
                    TNt += 1;
                    FPt -= 1;
                }
                else
                {
                    TPt -= 1;
                    FNt += 1;
                }
                tmpVaulue = metricFunction(TPt, FPt, TNt, FNt);

                if (tmpVaulue < value)
                {
                    value = tmpVaulue;
                    TP = TPt; FP = FPt; TN = TNt; FN = FNt;

                    if (i != testOutputs.Length - 1)
                        thr = (testOutputs[i] + testOutputs[i + 1]) / 2.0;
                    else
                        thr = testOutputs[i] + 0.1;
                }
            }

            UpdateChart(TP, FP, TN, FN, thr);
        }

        private void OptimizeByMetricDsc(string metric)
        {
            InitializeMetricFunctionForOptimization(out MetricFunction metricFunction, metric);

            double thr = Double.PositiveInfinity;
            double value = Double.PositiveInfinity, tmpVaulue;

            double TP = 0, FP = 0, TN = 0, FN = 0;
            double TPt = 0, FPt = 0, TNt = 0, FNt = 0;

            for (int i = 0; i < classes.Length; i++)
            {
                if (classes[i] == -1)
                    TNt += 1;
                else
                    FNt += 1;
            }
            tmpVaulue = metricFunction(TPt, FPt, TNt, FNt);

            if (tmpVaulue < value)
            {
                value = tmpVaulue;
                TP = TPt; FP = FPt; TN = TNt; FN = FNt;
                thr = testOutputs[testOutputs.Length - 1] + 0.1;
            }

            for (int i = testOutputs.Length - 1; i >= 0; i--)
            {
                while (i > 1 && testOutputs[i - 1] == testOutputs[i])
                {
                    if (classes[i] == 1)
                    {
                        TPt += 1;
                        FNt -= 1;
                    }
                    else
                    {
                        TNt -= 1;
                        FPt += 1;
                    }
                    i--;
                }
                if (classes[i] == 1)
                {
                    TPt += 1;
                    FNt -= 1;
                }
                else
                {
                    TNt -= 1;
                    FPt += 1;
                }
                tmpVaulue = metricFunction(TPt, FPt, TNt, FNt);

                if (tmpVaulue < value)
                {
                    value = tmpVaulue;
                    TP = TPt; FP = FPt; TN = TNt; FN = FNt;

                    if (i != 0)
                        thr = (testOutputs[i - 1] + testOutputs[i]) / 2.0;
                    else
                        thr = testOutputs[i] - 0.1;
                }
            }

            UpdateChart(TP, FP, TN, FN, thr);
        }

        private void OptimizeByThresholdValue(decimal value)
        {
            double TP = 0.0, FP = 0.0, TN = 0.0, FN = 0.0;
            for (int i = 0; i < testOutputs.Length; i++)
            {
                if (testOutputs[i] <= (double)value)
                {
                    if (classes[i] == 1)
                        FN += 1;
                    else
                        TN += 1;
                }
                else
                {
                    if (classes[i] == 1)
                        TP += 1;
                    else
                        FP += 1;
                }
            }

            UpdateChart(TP, FP, TN, FN, (double)value);
        }

        private void OptimizeByFARValue(decimal value)
        {
            double thr = Double.NegativeInfinity;
            double prevFAR = Double.PositiveInfinity, FAR;

            double TP = 0, FP = 0, TN = 0, FN = 0;

            for (int i = 0; i < classes.Length; i++)
            {
                if (classes[i] == 1)
                    TP += 1;
                else
                    FP += 1;
            }
            FAR = FP / (FP + TN);

            if (FAR == (double)value || (FAR < (double)value && (double)value < prevFAR))
                thr = testOutputs[0] - 0.1;

            if (Double.IsNegativeInfinity(thr))
            {
                for (int i = 0; i < testOutputs.Length; i++)
                {
                    while (i < testOutputs.Length - 1 && testOutputs[i] == testOutputs[i + 1])
                    {
                        if (classes[i] == -1)
                        {
                            TN += 1;
                            FP -= 1;
                        }
                        else
                        {
                            TP -= 1;
                            FN += 1;
                        }
                        i++;
                    }
                    if (classes[i] == -1)
                    {
                        TN += 1;
                        FP -= 1;
                    }
                    else
                    {
                        TP -= 1;
                        FN += 1;
                    }
                    FAR = FP / (FP + TN);

                    if (FAR == (double)value || (FAR < (double)value && (double)value < prevFAR))
                    {
                        if (i != testOutputs.Length - 1)
                            thr = (testOutputs[i] + testOutputs[i + 1]) / 2.0;
                        else
                            thr = testOutputs[i] + 0.1;
                        break;
                    }
                }
            }

            UpdateChart(TP, FP, TN, FN, thr);
        }

        private void OptimizeBySensitivityValue(decimal value)
        {
            double thr = Double.PositiveInfinity;
            double prevTPR = Double.NegativeInfinity, TPR;

            double TP = 0, FP = 0, TN = 0, FN = 0;

            for (int i = 0; i < classes.Length; i++)
            {
                if (classes[i] == -1)
                    TN += 1;
                else
                    FN += 1;
            }
            TPR = TP / (TP + FN);

            if (TPR == (double)value || (prevTPR < (double)value && (double)value < TPR))
                thr = testOutputs[0] + 0.1;

            if (Double.IsPositiveInfinity(thr))
            {
                for (int i = testOutputs.Length - 1; i >= 0; i--)
                {
                    while (i > 1 && testOutputs[i - 1] == testOutputs[i])
                    {
                        if (classes[i] == 1)
                        {
                            TP += 1;
                            FN -= 1;
                        }
                        else
                        {
                            TN -= 1;
                            FP += 1;
                        }
                        i--;
                    }
                    if (classes[i] == 1)
                    {
                        TP += 1;
                        FN -= 1;
                    }
                    else
                    {
                        TN -= 1;
                        FP += 1;
                    }
                    TPR = TP / (TP + FN);

                    if (TPR == (double)value || (prevTPR < (double)value && (double)value < TPR))
                    {
                        if (i != 0)
                            thr = (testOutputs[i - 1] + testOutputs[i]) / 2.0;
                        else
                            thr = testOutputs[i] - 0.1;
                        break;
                    }
                }
            }

            UpdateChart(TP, FP, TN, FN, thr);
        }

        private void OptimizeFPFNoccurencyDsc(double FPweight, double FNweight)
        {
            double thr = Double.PositiveInfinity;
            double value = Double.PositiveInfinity, tmpVaulue;

            double TP = 0, FP = 0, TN = 0, FN = 0;
            double TPt = 0, FPt = 0, TNt = 0, FNt = 0;
            for (int i = 0; i < classes.Length; i++)
            {
                if (classes[i] == -1)
                    TNt += 1;
                else
                    FNt += 1;
            }
            tmpVaulue = FPt * FPweight + FNt * FNweight;

            if (tmpVaulue < value)
            {
                value = tmpVaulue;
                TP = TPt; FP = FPt; TN = TNt; FN = FNt;
                thr = testOutputs[testOutputs.Length - 1] + 0.1;
            }

            for (int i = testOutputs.Length - 1; i >= 0; i--)
            {
                while (i > 1 && testOutputs[i - 1] == testOutputs[i])
                {
                    if (classes[i] == 1)
                    {
                        TPt += 1;
                        FNt -= 1;
                    }
                    else
                    {
                        TNt -= 1;
                        FPt += 1;
                    }
                    i--;
                }
                if (classes[i] == 1)
                {
                    TPt += 1;
                    FNt -= 1;
                }
                else
                {
                    TNt -= 1;
                    FPt += 1;
                }
                tmpVaulue = FPt * FPweight + FNt * FNweight;

                if (tmpVaulue < value)
                {
                    value = tmpVaulue;
                    TP = TPt; FP = FPt; TN = TNt; FN = FNt;

                    if (i != 0)
                        thr = (testOutputs[i - 1] + testOutputs[i]) / 2.0;
                    else
                        thr = testOutputs[i] - 0.1;
                }
            }

            UpdateChart(TP, FP, TN, FN, thr);
        }

        private void OptimizeFPFNoccurencyAsc(double FPweight, double FNweight)
        {
            double thr = double.NegativeInfinity;
            double value = Double.PositiveInfinity, tmpVaulue;

            double TP = 0, FP = 0, TN = 0, FN = 0;
            double TPt = 0, FPt = 0, TNt = 0, FNt = 0;

            for (int i = 0; i < classes.Length; i++)
            {
                if (classes[i] == 1)
                    TPt += 1;
                else
                    FPt += 1;
            }
            tmpVaulue = FPt * FPweight + FNt * FNweight;

            if (tmpVaulue < value)
            {
                value = tmpVaulue;
                TP = TPt; FP = FPt; TN = TNt; FN = FNt;
                thr = testOutputs[0] - 0.1;
            }

            for (int i = 0; i < testOutputs.Length; i++)
            {
                while (i < testOutputs.Length - 1 && testOutputs[i] == testOutputs[i + 1])
                {
                    if (classes[i] == -1)
                    {
                        TNt += 1;
                        FPt -= 1;
                    }
                    else
                    {
                        TPt -= 1;
                        FNt += 1;
                    }
                    i++;
                }
                if (classes[i] == -1)
                {
                    TNt += 1;
                    FPt -= 1;
                }
                else
                {
                    TPt -= 1;
                    FNt += 1;
                }
                tmpVaulue = FPt * FPweight + FNt * FNweight;

                if (tmpVaulue < value)
                {
                    value = tmpVaulue;
                    TP = TPt; FP = FPt; TN = TNt; FN = FNt;

                    if (i != testOutputs.Length - 1)
                        thr = (testOutputs[i] + testOutputs[i + 1]) / 2.0;
                    else
                        thr = testOutputs[i] + 0.1;
                }
            }

            UpdateChart(TP, FP, TN, FN, thr);
        }

        public void SaveReport()
        {
            if (testOutputs == null || classes == null)
            {
                MessageBox.Show("No data to save. Generate data using Test Classifier button.");
                return;
            }

            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog()
            {
                Filter = "Result Files(*.result.txt) | *.result.txt",
                FileName = Path.GetFileNameWithoutExtension(testingClassifierTextBox.Text)
            };
            if (Properties.Settings.Default.testingClassifierResultPath != "" && Directory.Exists(Properties.Settings.Default.testingClassifierResultPath))
                saveFileDialog.InitialDirectory = Properties.Settings.Default.testingClassifierResultPath;
            else
                saveFileDialog.InitialDirectory = "";

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Properties.Settings.Default.testingClassifierResultPath = Path.GetDirectoryName(saveFileDialog.FileName);
                Properties.Settings.Default.Save();

                GlobalFunctions.SaveMetrices(ref confusionMatriceDataGridView, ref metricesMatrixDataGridView, ref ROCchart, saveFileDialog.FileName, testingClassifierTextBox.Text, testingFeaturesTextBox.Text, thresholds.ToArray());
            }
            saveFileDialog.Dispose();
        }
        #endregion

        #region Triggers
        public event EventHandler<StartedEventsArg> TestingStarted;
        protected virtual void OnTestingStarted(StartedEventsArg e)
        {
            TestingStarted?.Invoke(this, e);
        }

        public event EventHandler<CompletionEventsArg> TestingCompletion;
        protected virtual void OnTestingCompletion(CompletionEventsArg e)
        {
            TestingCompletion?.Invoke(this, e);
        }

        public event EventHandler<ProgressingEventsArg> TestingProgressing;
        protected virtual void OnTestingProgressing(ProgressingEventsArg e)
        {
            TestingProgressing?.Invoke(this, e);
        }
        #endregion

        #region Events
        private void BrowseClassifierToTestButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectClassifier(ref testingClassifierTextBox, "testingClassifierPath");
        }

        private void BrowseTestingFeaturesButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFeatures(ref testingFeaturesTextBox, "testingFeaturesPath");
        }

        private void ThresholdRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            thrOptimizationValueNumericUpDown.Maximum = 10000;
            thrOptimizationValueNumericUpDown.Minimum = -10000;
            thrOptimizationValueNumericUpDown.Value = 0;
            thrOptimizationValueNumericUpDown.Increment = 0.5M;
        }

        private void FarRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            thrOptimizationValueNumericUpDown.Maximum = 1;
            thrOptimizationValueNumericUpDown.Minimum = 0;
            thrOptimizationValueNumericUpDown.Value = 0;
            thrOptimizationValueNumericUpDown.Increment = 0.05M;
        }

        private void SensitivityRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            thrOptimizationValueNumericUpDown.Maximum = 1;
            thrOptimizationValueNumericUpDown.Minimum = 0;
            thrOptimizationValueNumericUpDown.Value = 1;
            thrOptimizationValueNumericUpDown.Increment = 0.05M;
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
            SaveReport();
        }

        private void OptimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (testOutputs == null || classes == null)
            {
                MessageBox.Show("No data to process. Generate data using Test Classifier button.");
                return;
            }

            if (worker != null && worker.IsBusy)
            {
                MessageBox.Show("Application is busy.");
                return;
            }

            string message = "Threshold optimization started: ";
            if (thrOptimizationMetricesRadioButton.IsChecked == true)
            {
                message += "metric = " + thrOptimizationMetricesComboBox.SelectedItem.ToString();
            }
            else
            {
                if (thresholdRadioButton.IsChecked == true)
                    message += "threshold ";
                if (farRadioButton.IsChecked == true)
                    message += "FAR ";
                if (sensitivityRadioButton.IsChecked == true)
                    message += "sensitivity ";
                message += " value = " + thrOptimizationValueNumericUpDown.Value;
            }
            StartedEventsArg args = new StartedEventsArg("Status: Working", message, DateTime.Now, 0, false);
            OnTestingStarted(args);

            ROCchart.Series[2].Points.Clear();

            worker = new BackgroundWorker()
            {
                WorkerSupportsCancellation = true,
                WorkerReportsProgress = false
                
            };
            worker.DoWork += OptimizationBackgroundWorker_DoWork;
            worker.RunWorkerCompleted += OptimizationBackgroundWorker_RunWorkerCompleted;
            worker.RunWorkerAsync();
        }

        private void OptimizationBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (Dispatcher.Invoke(new Func<bool?>(() => { return thrOptimizationValueRadioButton.IsChecked; })) == true)
            {
                decimal value = Dispatcher.Invoke(new Func<decimal>(() => { return thrOptimizationValueNumericUpDown.Value; }));
                if (Dispatcher.Invoke(new Func<bool?>(() => { return thresholdRadioButton.IsChecked; })) == true)
                    OptimizeByThresholdValue(value);
                else if (Dispatcher.Invoke(new Func<bool?>(() => { return farRadioButton.IsChecked; })) == true)
                    OptimizeByFARValue(value);
                else if (Dispatcher.Invoke(new Func<bool?>(() => { return sensitivityRadioButton.IsChecked; })) == true)
                    OptimizeBySensitivityValue(value);
            }
            else if (Dispatcher.Invoke(new Func<bool?>(() => { return thrOptimizationMetricesRadioButton.IsChecked; })) == true)
            {
                string metric = Dispatcher.Invoke(new Func<string>(() => { return thrOptimizationMetricesComboBox.Text; }));
                if (Dispatcher.Invoke(new Func<bool?>(() => { return thrOptimizationOrderCheckBox.IsChecked; })) == true)
                    OptimizeByMetricAsc(metric);
                else
                    OptimizeByMetricDsc(metric);
            }
            else if (Dispatcher.Invoke(new Func<bool?>(() => { return FPFNoccurencyOptimizationRadioButton.IsChecked; })) == true)
            {
                double FPweight = 0.5, FNweight = 0.5;
                Dispatcher.Invoke(new Action(() => { FPweight = (double)FPweightNumericUpDown.Value; FNweight = (double)FNweightNumericUpDown.Value; }));

                if (FNweight <= FPweight)
                    OptimizeFPFNoccurencyAsc(FPweight, FNweight);
                else
                    OptimizeFPFNoccurencyDsc(FPweight, FNweight);
            }
        }

        private void OptimizationBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string statusLabel = "Status: Optimization completed successful. Check event log for details.";
            string logMessage = "Optimization completed successful.";

            CompletionEventsArg args = new CompletionEventsArg(statusLabel, logMessage, null, DateTime.Now, false);
            OnTestingCompletion(args);

            worker.Dispose();
            worker = null;
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (testingFeaturesTextBox.Text == "" || !File.Exists(testingFeaturesTextBox.Text))
            {
                MessageBox.Show("Features path is empty or file doesn't exist.");
                return;
            }
            if (testingClassifierTextBox.Text == "" || !File.Exists(testingClassifierTextBox.Text))
            {
                MessageBox.Show("Classifier path is empty or file doesn't exist.");
                return;
            }
            if (worker != null && worker.IsBusy)
            {
                MessageBox.Show("Application is busy.");
                return;
            }

            StartedEventsArg args = new StartedEventsArg("Status: Working", "Classifier testing started.", DateTime.Now, 1, true);
            OnTestingStarted(args);

            ROCchart.Series[1].Points.Clear();
            ROCchart.Series[2].Points.Clear();

            thrOptimizationValueRadioButton.IsChecked = true;
            thrOptimizationValueNumericUpDown.Value = 0;
            thresholdRadioButton.IsChecked = true;

            worker = new BackgroundWorker()
            {
                WorkerSupportsCancellation = true,
                WorkerReportsProgress = true
            };
            worker.DoWork += TestingBackgroundWorker_DoWork;
            worker.RunWorkerCompleted += TestingBackgroundWorker_RunWorkerCompleted;
            worker.ProgressChanged += TestingBackgroundWorker_ProgressChanged;
            worker.RunWorkerAsync();
        }

        private void TestingBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressingEventsArg args = new ProgressingEventsArg(e.ProgressPercentage);
            OnTestingProgressing(args);
        }

        unsafe private void TestingBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            callback = (value) =>
            {
                worker.ReportProgress(value);
            };

            string testFeatureFileName = Dispatcher.Invoke(new Func<string>(() => { return testingFeaturesTextBox.Text; }));
            int samples;

            using (BinaryReader reader = new BinaryReader(File.Open(testFeatureFileName, FileMode.Open)))
                samples = reader.ReadInt32();

            if (samples <= 0)
                GlobalFunctions.ThrowError((int)GlobalFunctions.ERRORS.CORRUPTED_FEATURES_FILE);

            Dispatcher.Invoke(new Action(() =>
            {
                classes = new int[samples];
                testOutputs = new double[samples];
                avgFeatures = 0;
            }));
            double[,] points = new double[samples + 1, 3];
            thresholds = new List<double>(); 

            fixed (int* classPointer = classes)
            {
                fixed (double* thresholdPointer = testOutputs)
                {
                    fixed (double* fetPointer = &avgFeatures)
                    {
                        string classifierPath = Dispatcher.Invoke(new Func<string>(() => { return testingClassifierTextBox.Text; }));

                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();

                        int status = NativeMethods.Testing(testFeatureFileName, classifierPath, classPointer, thresholdPointer, fetPointer, callback);

                        stopwatch.Stop();
                        operationTime = stopwatch.ElapsedMilliseconds;

                        if (status < 0)
                            GlobalFunctions.ThrowError(status);
                    }
                }
            }

            // Badanie jakosci
            Array.Sort(testOutputs, classes);

            double AUC = 0.0, x1, x2, y1, y2;
            double thr = testOutputs[0] - 0.1, prvThr = Double.NegativeInfinity;
            double sensitivity = 1.0, FPR = 1.0;
            double TPt = 0, FPt = 0, TNt = 0, FNt = 0;
            double currentThr = 0.0;

            int pit = 0;
            points[pit, 0] = FPR; points[pit, 1] = sensitivity; points[pit, 2] = thr;
            pit++;

            x1 = FPR;
            y1 = sensitivity;

            for (int i = 0; i < classes.Length; i++)
            {
                if (classes[i] == 1)
                    TPt += 1;
                else
                    FPt += 1;
            }
            double TP = TPt, FP = FPt, TN = TNt, FN = FNt;

            for (int i = 0; i < testOutputs.Length; i++)
            {
                if (this.worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                while (i < testOutputs.Length - 1 && testOutputs[i] == testOutputs[i + 1])
                {
                    if (classes[i] == -1)
                    {
                        TNt += 1; FPt -= 1;
                    }
                    else
                    {
                        TPt -= 1; FNt += 1;
                    }
                    i++;
                }
                if (classes[i] == -1)
                {
                    TNt += 1; FPt -= 1;
                }
                else
                {
                    TPt -= 1; FNt += 1;
                }

                prvThr = thr;
                // Wstawienie progu miedzy dwie unikatowe wartosci
                if (i != testOutputs.Length - 1)
                    thr = (testOutputs[i] + testOutputs[i + 1]) / 2.0;
                // Wstawienie progu za ostatnia probka
                else
                    thr = testOutputs[i] + 0.1;

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

            Dispatcher.Invoke(new Action(() =>
            {
                sensitivity = TP / (TP + FN);
                FPR = FP / (FP + TN);

                ((GlobalFunctions.MetricesTableRow)metricesMatrixDataGridView.Items[14]).Value = avgFeatures;
                GlobalFunctions.PopulateConfusionMatrix(ref confusionMatriceDataGridView, (int)TP, (int)TN, (int)FP, (int)FN);
                GlobalFunctions.CalculateMetrices(ref confusionMatriceDataGridView, ref metricesMatrixDataGridView);            

                if (!Double.IsInfinity(sensitivity) && !Double.IsNaN(sensitivity) && !Double.IsInfinity(FPR) && !Double.IsNaN(FPR))
                {
                    ROCchart.Series[1].Points.AddXY(points[0, 0], points[0, 1]);
                    thresholds.Add(points[0, 2]);
                    for (int i = 1; i < pit - 1; i++)
                    {
                        if (points[i - 1, 0] == points[i, 0] && points[i, 0] == points[i + 1, 0])
                            continue;
                        if (points[i - 1, 1] == points[i, 1] && points[i, 1] == points[i + 1, 1])
                            continue;
                        ROCchart.Series[1].Points.AddXY(points[i, 0], points[i, 1]);
                        thresholds.Add(points[i, 2]);
                    }

                    ROCchart.Series[1].Points.AddXY(points[pit - 1, 0], points[pit - 1, 1]);
                    thresholds.Add(points[pit - 1, 2]);
                    ROCchart.Titles[1].Text = "Current threshold: " + String.Format("{0:0.0000}", currentThr);
                    ROCchart.Series[1].LegendText = "AUC: " + String.Format("{0:0.0000}", AUC);

                    if (FPR == 0.0)
                        FPR += 0.00000000000000001;
                    ROCchart.Series[2].Points.AddXY(FPR, sensitivity);
                }
            }));
        }

        private void TestingBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string statusLabel;
            string logMessage;
            string error = null;

            if (e.Cancelled)
            {
                statusLabel = "Status: Testing cancelled. Check event log for details";
                logMessage = "Testing cancelled.";
            }
            else if (e.Error != null)
            {
                statusLabel = "Status: Testing completed with errors. Check event log for details.";
                logMessage = "Testing completed with errors:";
                error = e.Error.Message;
            }
            else
            {
                statusLabel = "Status: Testing completed successful. Check event log for details.";
                logMessage = "Testing completed successful. Elapsed time: " + operationTime + "ms.";
            }

            CompletionEventsArg args = new CompletionEventsArg(statusLabel, logMessage, error, DateTime.Now, false);
            OnTestingCompletion(args);

            worker.Dispose();
            worker = null;
        }
        #endregion
    }
}
