using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for BatchDetectionResultParser.xaml
    /// </summary>
    public partial class BatchDetectionResultParser : UserControl
    {
        readonly SortedDictionary<double, int[]> results = new SortedDictionary<double, int[]>();

        public BatchDetectionResultParser()
        {
            InitializeComponent();

            GlobalFunctions.InitializeMetrices(ref confusionFirstDataGridView, ref metricesFirstDataGridView);
            GlobalFunctions.InitializeMetrices(ref confusionSecondDataGridView, ref metricesSecondDataGridView);
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            //if (GlobalFunctions.SelectFolder(ref folderTextBox, "batchResultFolder") == System.Windows.Forms.DialogResult.OK)
            if(GlobalFunctions.SelectFile(ref folderTextBox, "batchResultFile", "Result file (*.txt) | *.txt") == System.Windows.Forms.DialogResult.OK)
            {
                results.Clear();
                resultTextBox.Text = "";

                nextThrFirstButton.IsEnabled = true;
                prevThrFirstButton.IsEnabled = false;
                nextThrSecondButton.IsEnabled = true;
                prevThrSecondButton.IsEnabled = false;

                //if (File.Exists(folderTextBox.Text + "\\result.txt"))
                if (File.Exists(folderTextBox.Text))
                {
                    //using (StreamReader sr = new StreamReader(folderTextBox.Text + "\\result.txt"))
                    using (StreamReader sr = new StreamReader(folderTextBox.Text))
                    {
                        string line;
                        double currentThreshold = 0.0;

                        matlabTextBox.Text = "";

                        string threshold = "thr = [";
                        string far = "far = [";
                        string sens = "sen = [";
                        string acc = "acc = [";

                        while ((line = sr.ReadLine()) != null)
                        {
                            if (line.StartsWith("Threshold"))
                            {
                                resultTextBox.Text += line + "\r\n";
                                currentThreshold = Double.Parse(line.Split()[1], CultureInfo.InvariantCulture);
                                results.Add(currentThreshold, new int[4]);
                                threshold += line.Split()[1] + ",";
                            }
                            else if (line.StartsWith("Classifier:"))
                            {
                                classifierTextBox.Text = line.Split()[1];
                            }
                            else if (line.StartsWith("Features:"))
                            {
                                featuresTextBox.Text = line.Split()[1];
                            }
                            else if (line.StartsWith("TP:"))
                            {
                                resultTextBox.Text += line + "\r\n";
                                results[currentThreshold][0] = int.Parse(line.Split()[1], CultureInfo.InvariantCulture);
                            }
                            else if (line.StartsWith("FP:"))
                            {
                                resultTextBox.Text += line + "\r\n";
                                results[currentThreshold][1] = int.Parse(line.Split()[1], CultureInfo.InvariantCulture);
                            }
                            else if (line.StartsWith("FN:"))
                            {
                                resultTextBox.Text += line + "\r\n";
                                results[currentThreshold][2] = int.Parse(line.Split()[1], CultureInfo.InvariantCulture);
                            }
                            else if (line.StartsWith("TN:"))
                            {
                                resultTextBox.Text += line + "\r\n";
                                results[currentThreshold][3] = int.Parse(line.Split()[1], CultureInfo.InvariantCulture);
                            }
                            else if (line.StartsWith("Accuracy"))
                            {
                                resultTextBox.Text += line + "\r\n";
                                acc += line.Split()[1] + ",";
                            }
                            else if (line.StartsWith("Sensitivity"))
                            {
                                resultTextBox.Text += line + "\r\n";
                                sens += line.Split()[2] + ",";
                            }
                            else if (line.StartsWith("Fall - out (FAR)"))
                            {
                                resultTextBox.Text += line + "\r\n" + "\r\n";
                                far += line.Split()[4] + ",";
                            }
                        }


                        threshold = threshold.Substring(0, threshold.Length -1) + "];\r\n";
                        far = far.Substring(0, far.Length - 1) + "];\r\n";
                        sens = sens.Substring(0, sens.Length - 1) + "];\r\n";
                        acc = acc.Substring(0, acc.Length - 1) + "];\r\n";

                        matlabTextBox.Text += threshold + acc + sens + far;
                    }

                    if (results.Count > 0)
                    {
                        if(results.Count == 1)
                        {
                            nextThrFirstButton.IsEnabled = false;
                            nextThrSecondButton.IsEnabled = false;
                        }

                        double key = results.Keys.Min();
                        int TP = results[key][0];
                        int FP = results[key][1];
                        int FN = results[key][2];
                        int TN = results[key][3];
                        currentThrFirstTextBox.Text = key.ToString();
                        currentThrSecondTextBox.Text = key.ToString();
                        GlobalFunctions.PopulateConfusionMatrix(ref confusionFirstDataGridView, TP, TN, FP, FN);
                        GlobalFunctions.CalculateMetrices(ref confusionFirstDataGridView, ref metricesFirstDataGridView);
                        GlobalFunctions.PopulateConfusionMatrix(ref confusionSecondDataGridView, TP, TN, FP, FN);
                        GlobalFunctions.CalculateMetrices(ref confusionSecondDataGridView, ref metricesSecondDataGridView);
                    }
                }
            }
        }

        private void PrevThrSecondButton_Click(object sender, RoutedEventArgs e)
        {
            if (results != null && currentThrSecondTextBox.Text != "")
            {
                double threshold = Double.Parse(currentThrSecondTextBox.Text, CultureInfo.InvariantCulture);

                var keyList = results.Keys.ToList();
                int index = keyList.IndexOf(threshold);
                double newKey = keyList[index - 1];

                currentThrSecondTextBox.Text = newKey.ToString(CultureInfo.InvariantCulture);

                int TP = results[newKey][0];
                int FP = results[newKey][1];
                int FN = results[newKey][2];
                int TN = results[newKey][3];

                GlobalFunctions.PopulateConfusionMatrix(ref confusionSecondDataGridView, TP, TN, FP, FN);
                GlobalFunctions.CalculateMetrices(ref confusionSecondDataGridView, ref metricesSecondDataGridView);

                nextThrSecondButton.IsEnabled = true;
                if (index - 1 == 0)
                    prevThrSecondButton.IsEnabled = false;
            }
        }

        private void NextThrSecondButton_Click(object sender, RoutedEventArgs e)
        {
            if (results != null && currentThrSecondTextBox.Text != "")
            {
                double threshold = Double.Parse(currentThrSecondTextBox.Text, CultureInfo.InvariantCulture);

                var keyList = results.Keys.ToList();
                int index = keyList.IndexOf(threshold);
                double newKey = keyList[index + 1];

                currentThrSecondTextBox.Text = newKey.ToString(CultureInfo.InvariantCulture);

                int TP = results[newKey][0];
                int FP = results[newKey][1];
                int FN = results[newKey][2];
                int TN = results[newKey][3];

                GlobalFunctions.PopulateConfusionMatrix(ref confusionSecondDataGridView, TP, TN, FP, FN);
                GlobalFunctions.CalculateMetrices(ref confusionSecondDataGridView, ref metricesSecondDataGridView);

                prevThrSecondButton.IsEnabled = true;
                if (index + 1 == keyList.Count - 1)
                    nextThrSecondButton.IsEnabled = false;
            }
        }

        private void PrevThrFirstButton_Click(object sender, RoutedEventArgs e)
        {
            if (results != null && currentThrFirstTextBox.Text != "")
            {
                double threshold = Double.Parse(currentThrFirstTextBox.Text, CultureInfo.InvariantCulture);

                var keyList = results.Keys.ToList();
                int index = keyList.IndexOf(threshold);
                double newKey = keyList[index - 1];

                currentThrFirstTextBox.Text = newKey.ToString(CultureInfo.InvariantCulture);

                int TP = results[newKey][0];
                int FP = results[newKey][1];
                int FN = results[newKey][2];
                int TN = results[newKey][3];

                GlobalFunctions.PopulateConfusionMatrix(ref confusionFirstDataGridView, TP, TN, FP, FN);
                GlobalFunctions.CalculateMetrices(ref confusionFirstDataGridView, ref metricesFirstDataGridView);

                nextThrFirstButton.IsEnabled = true;
                if (index - 1 == 0)
                    prevThrFirstButton.IsEnabled = false;
            }
        }

        private void NextThrFirstButton_Click(object sender, RoutedEventArgs e)
        {
            if (results != null && currentThrFirstTextBox.Text != "")
            {
                double threshold = Double.Parse(currentThrFirstTextBox.Text, CultureInfo.InvariantCulture);

                var keyList = results.Keys.ToList();
                int index = keyList.IndexOf(threshold);
                double newKey = keyList[index + 1];

                currentThrFirstTextBox.Text = newKey.ToString(CultureInfo.InvariantCulture);

                int TP = results[newKey][0];
                int FP = results[newKey][1];
                int FN = results[newKey][2];
                int TN = results[newKey][3];

                GlobalFunctions.PopulateConfusionMatrix(ref confusionFirstDataGridView, TP, TN, FP, FN);
                GlobalFunctions.CalculateMetrices(ref confusionFirstDataGridView, ref metricesFirstDataGridView);

                prevThrFirstButton.IsEnabled = true;
                if (index + 1 == keyList.Count - 1)
                    nextThrFirstButton.IsEnabled = false;
            }
        }
    }
}
