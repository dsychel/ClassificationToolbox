using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for FeaturesViewer.xaml
    /// </summary>
    public partial class FeaturesViewer : UserControl
    {
        private class HaarItem
        {
            public int Templates { get; set; }
            public int PositionX { get; set; }
            public int PositionY { get; set; }
            public int ScalesX { get; set; }
            public int ScalesY { get; set; }
            public double Features { get; set; }
        }

        private class HOGItem
        {
            public int Binns { get; set; }
            public int BlockX { get; set; }
            public int BlockY { get; set; }
            public double Features { get; set; }
        }

        private class PFMMItem
        {
            public int P { get; set; }
            public int Q { get; set; }
            public int R { get; set; }
            public int Rt { get; set; }
            public double Features { get; set; }
        }

        private class ZernikeItem
        {
            public int P { get; set; }
            public int Q { get; set; }
            public int R { get; set; }
            public int Rt { get; set; }
            public double Features { get; set; }
        }

        private class ZernikeInvariantItem
        {
            public int R { get; set; }
            public int Rt { get; set; }
            public int K { get; set; }
            public int P { get; set; }
            public int Q { get; set; }
            public int V { get; set; }
            public int S { get; set; }
            public int Kind { get; set; }
            public double Features { get; set; }
        }

        #region Constructors
        public FeaturesViewer()
        {
            InitializeComponent();

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            ShowFeaturesExtractorSettings_CheckedChanged(showFeaturesExtractorSettings, new EventArgs());
        }
        #endregion

        #region Methods
        private int ReadFeaturesNumber(string path)
        {
            int count = 0;
            using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                count = reader.ReadInt32() - 1;
            }
            return count;
        }

        private void ReadFeatures(string path, int index)
        {
            showFeaturesAtIndexDataGridView.Items.Clear();

            int count, featuresNumber;
            using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                count = reader.ReadInt32();
                featuresNumber = reader.ReadInt32();

                for (int i = 0; i < index; i++)
                {
                    for (int j = 0; j < featuresNumber; j++)
                    {
                        reader.ReadDouble();
                    }
                    reader.ReadInt32();
                }

                ReadOnlyDictionary<string, int> param = showFeaturesExtractorSettings.Parameters;
                if (showFeaturesExtractorSettings.ExtractorName == "HaarExtractor")
                {
                    for (int t = 0; t < param["t"]; t++)
                    {
                        for (int sx = 1; sx <= param["s"]; sx++)
                        {
                            for (int sy = 1; sy <= param["s"]; sy++)
                            {
                                for (int px = 0; px < param["ps"]; px++)
                                {
                                    for (int py = 0; py < param["ps"]; py++)
                                    {
                                        double feature = reader.ReadDouble();
                                        showFeaturesAtIndexDataGridView.Items.Add(new HaarItem { Templates = t, ScalesX = sx, ScalesY = sy, PositionX = px, PositionY = py, Features = feature });
                                    }
                                }
                            }
                        }
                    }
                }
                else if (showFeaturesExtractorSettings.ExtractorName == "HOGExtractor")
                {
                    for (int ny = 0; ny < param["ny"]; ny++)
                    {
                        for (int nx = 0; nx < param["nx"]; nx++)
                        {
                            for (int b = 0; b < param["b"]; b++)
                            {
                                double feature = reader.ReadDouble();
                                showFeaturesAtIndexDataGridView.Items.Add(new HOGItem { Binns = b, BlockX = nx, BlockY = ny, Features = feature });
                            }
                        }
                    }
                }
                else if (showFeaturesExtractorSettings.ExtractorName == "PFMMExtractor")
                {
                    for (int r = 0; r < param["r"]; r++)
                    {
                        int hMax = 0;
                        if (param["rt"] == 1)
                            hMax = (r == param["r"] - 1) ? 0 : 1;
                        for (int rt = 0; rt <= hMax; rt++)
                        {
                            for (int p = 0; p <= param["p"]; p++)
                            {
                                for (int q = 0; q <= Math.Min(p, param["q"]); q++)
                                {
                                    double feature = reader.ReadDouble();
                                    showFeaturesAtIndexDataGridView.Items.Add(new PFMMItem { R = r, Rt = rt, P = p, Q = q, Features = feature });
                                }
                            }
                        }
                    }
                }
                else if (showFeaturesExtractorSettings.ExtractorName == "ZernikeExtractor")
                {
                    for (int r = 0; r < param["r"]; r++)
                    {
                        int hMax = 0;
                        if (param["rt"] == 1)
                            hMax = (r == param["r"] - 1) ? 0 : 1;
                        for (int rt = 0; rt <= hMax; rt++)
                        {
                            for (int p = 0; p <= param["p"]; p++)
                            {
                                //for (int q = p % 2; q <= p; q += 2)
                                for (int q = p % 2; q <= Math.Min(p, param["q"]); q += 2)
                                {
                                    double feature = reader.ReadDouble();
                                    showFeaturesAtIndexDataGridView.Items.Add(new ZernikeItem { R = r, Rt = rt, P = p, Q = q, Features = feature });
                                }
                            }
                        }
                    }
                }
                else if (showFeaturesExtractorSettings.ExtractorName == "ZernikeInvariantsExtractor")
                {
                    for (int r = 0; r < param["r"]; r++)
                    {
                        int hMax = 0;
                        if (param["rt"] == 1)
                            hMax = (r == param["r"] - 1) ? 0 : 1;
                        for (int rt = 0; rt <= hMax; rt++)
                        {
                            double feature = reader.ReadDouble();
                            showFeaturesAtIndexDataGridView.Items.Add(new ZernikeInvariantItem { R = r, Rt = rt, K = 0, P = 0, Q = 0, S = 0, V = 0, Kind = 0, Features = feature });

                            for (int k = 1; k <= param["p"]; k++)
                            {
                                int q_min = 1;
                                if (k == 1) q_min = 0;
                                for (int q = q_min; q <= param["q"]; q++)
                                {
                                    int s = k * q;
                                    for (int p = q; p <= param["p"]; p += 2)
                                    {
                                        int v_min = s;
                                        if (k == 1)
                                        {
                                            v_min = p;
                                            if (q == 0)
                                                v_min = p + 2;
                                        }
                                        for (int v = v_min; v <= param["q"]; v += 2)
                                        {
                                            feature = reader.ReadDouble();
                                            showFeaturesAtIndexDataGridView.Items.Add(new ZernikeInvariantItem { R = r, Rt = rt, K = k, P = p, Q = q, S = s, V = v, Kind = 0, Features = feature });

                                            if (!((k == 1 && p == v) || q == 0))
                                            {
                                                feature = reader.ReadDouble();
                                                showFeaturesAtIndexDataGridView.Items.Add(new ZernikeInvariantItem { R = r, Rt = rt, K = k, P = p, Q = q, S = s, V = v, Kind = 1, Features = feature });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                showSampleAtIndexClassTextBox.Text = reader.ReadInt32().ToString();
            }
        }
        #endregion

        #region Events
        private void CompareFirstFilePathBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFeatures(ref compareFirstFilePathTextBox, "featuresToShow");
        }

        private void CompareSecondFilePathBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFeatures(ref compareSecondFilePathTextBox, "featuresToShow");
        }

        //static bool ByteArrayCompare(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2)
        //{
        //    return a1.SequenceEqual(a2);
        //}



        private void CompareFilesButton_Click(object sender, RoutedEventArgs e)
        {
            compareLogTextBox.Text = "";

            int samplesFirst, featuresFirst;
            int samplesSecond, featuresSecond;

            if (compareFirstFilePathTextBox.Text == compareSecondFilePathTextBox.Text)
            {
                compareLogTextBox.Text = "Path are identical.";
                return;
            }

            //Stopwatch sw = new Stopwatch();
            //sw.Start();

            using (BinaryReader readerFirst = new BinaryReader(File.Open(compareFirstFilePathTextBox.Text, FileMode.Open)))
            {
                using (BinaryReader readerSecond = new BinaryReader(File.Open(compareSecondFilePathTextBox.Text, FileMode.Open)))
                {
                    samplesFirst = readerFirst.ReadInt32();
                    featuresFirst = readerFirst.ReadInt32();

                    samplesSecond = readerSecond.ReadInt32();
                    featuresSecond = readerSecond.ReadInt32();

                    if (samplesFirst == samplesSecond)
                    {
                        compareLogTextBox.Text += "Number of samples are equal (" + samplesFirst + ");\r\n";
                        if (featuresFirst == featuresSecond)
                        {
                            compareLogTextBox.Text += "Number of features are equal (" + featuresFirst + ");\r\n";

                            int differencesInSamplesCount = 0;
                            int differencesInClassesCount = 0;
                            for (int i = 0; i < samplesFirst; i++)
                            {
                                //for (int j = 0; j < featuresFirst; j++)
                                //{
                                //    if (readerFirst.ReadDouble() != readerSecond.ReadDouble())
                                //        differencesInSamplesCount++;
                                //}
                                byte[] features1 = readerFirst.ReadBytes(sizeof(double) * featuresFirst);
                                byte[] features2 = readerSecond.ReadBytes(sizeof(double) * featuresFirst);

                                if (NativeMethods.memcmp(features1, features2, features1.Length) != 0)
                                    differencesInSamplesCount++;

                                if (readerFirst.ReadInt32() != readerSecond.ReadInt32())
                                    differencesInClassesCount++;
                            }

                            if (differencesInSamplesCount == 0 && differencesInClassesCount == 0)
                            {
                                compareLogTextBox.Text += "Features and classes are equal;\r\n";
                                compareLogTextBox.Text = compareLogTextBox.Text.Insert(0, "Files are equal:\r\n");
                            }
                            else
                            {
                                if (differencesInSamplesCount == 0)
                                    compareLogTextBox.Text += "Features are equal;\r\n";
                                else
                                    compareLogTextBox.Text += differencesInSamplesCount + "/" + samplesFirst  + " features are different;\r\n";
                                    //compareLogTextBox.Text += differencesInSamplesCount + "/" + samplesFirst * featuresFirst + " features are different;\r\n";
                                if (differencesInClassesCount == 0)
                                    compareLogTextBox.Text += "Classes are equal;\r\n";
                                else
                                    compareLogTextBox.Text += differencesInClassesCount + "/" + samplesFirst + " classes are different;\r\n";
                                compareLogTextBox.Text = compareLogTextBox.Text.Insert(0, "Files are different:\r\n");
                            }
                        }
                        else
                        {
                            compareLogTextBox.Text += "Number of features are different (" + featuresFirst + " != " + featuresSecond + ");\r\n";
                            compareLogTextBox.Text = compareLogTextBox.Text.Insert(0, "Files are different:\r\n");
                        }
                    }
                    else
                    {
                        compareLogTextBox.Text += "Files are different: \r\n";
                        compareLogTextBox.Text += "Number of samples are different (" + samplesFirst + " != " + samplesSecond + ");\r\n";
                    }
                }
            }

            //sw.Stop();
            //MessageBox.Show("Elapsed = " + sw.Elapsed);
        }

        private void SelectFeaturesToShowButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFeatures(ref showFeaturesPathTextBox, "featuresToShow");

            if (showFeaturesPathTextBox.Text != "" && File.Exists(showFeaturesPathTextBox.Text))
            {
                showSampleAtIndexNumericUpDown.Value = -1;
                showSampleAtIndexClassTextBox.Text = "";
                showSampleAtIndexNumericUpDown.Maximum = ReadFeaturesNumber(showFeaturesPathTextBox.Text);

                showFeaturesExtractorSettings.TrySetFromString(showFeaturesPathTextBox.Text);
            }
        }

        private void SaveSampleAsTextButton_Click(object sender, RoutedEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog()
            {
                Filter = "Sample Files(*.txt) | *.txt",
                FileName = ""
            };
            if (Properties.Settings.Default.featuresToShow != "" && Directory.Exists(Path.GetDirectoryName(Properties.Settings.Default.featuresToShow)))
                saveFileDialog.InitialDirectory = Path.GetDirectoryName(Properties.Settings.Default.featuresToShow);
            else
                saveFileDialog.InitialDirectory = "";

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                using (StreamWriter sw = new StreamWriter(saveFileDialog.FileName))
                {
                    if (showFeaturesExtractorSettings.ExtractorName == "HaarExtractor")
                    {
                        sw.WriteLine("Haar Extractor");
                        foreach (HaarItem row in showFeaturesAtIndexDataGridView.Items)
                        {
                            sw.Write("t: " + row.Templates + ",");
                            sw.Write(" sx: " + row.ScalesX + ",");
                            sw.Write(" sy: " + row.ScalesY + ",");
                            sw.Write(" px: " + row.PositionX + ",");
                            sw.Write(" py: " + row.PositionY + ",");
                            sw.WriteLine(" F = " + row.Features);
                        }
                    }
                    else if (showFeaturesExtractorSettings.ExtractorName == "HOGExtractor")
                    {
                        sw.WriteLine("Haar Extractor");
                        foreach (HOGItem row in showFeaturesAtIndexDataGridView.Items)
                        {
                            sw.Write("b: " + row.Binns + ",");
                            sw.Write(" nx: " + row.BlockX + ",");
                            sw.Write(" ny: " + row.BlockY + ",");
                            sw.WriteLine(" F = " + row.Features);
                        }
                    }
                    else if (showFeaturesExtractorSettings.ExtractorName == "PFMMExtractor")
                    {
                        sw.WriteLine("PFMM Extractor");
                        foreach (PFMMItem row in showFeaturesAtIndexDataGridView.Items)
                        {
                            sw.Write("r: " + row.R + ",");
                            sw.Write(" rt: " + row.Rt + ",");
                            sw.Write(" p: " + row.P + ",");
                            sw.Write(" q: " + row.Q + ",");
                            sw.WriteLine(" F = " + row.Features);
                        }
                    }
                    else if (showFeaturesExtractorSettings.ExtractorName == "ZernikeExtractor")
                    {
                        sw.WriteLine("Zernike Extractor");
                        foreach (ZernikeItem row in showFeaturesAtIndexDataGridView.Items)
                        {
                            sw.Write("r: " + row.R + ",");
                            sw.Write(" rt: " + row.Rt + ",");
                            sw.Write(" p: " + row.P + ",");
                            sw.Write(" q: " + row.Q + ",");
                            sw.WriteLine(" F = " + row.Features);
                        }
                    }
                    else if (showFeaturesExtractorSettings.ExtractorName == "ZernikeInvariantsExtractor")
                    {
                        sw.WriteLine("Zernike Invariants Extractor");
                        foreach (ZernikeInvariantItem row in showFeaturesAtIndexDataGridView.Items)
                        {
                            sw.Write("r: " + row.R + ",");
                            sw.Write(" rt: " + row.Rt + ",");
                            sw.Write(" k: " + row.K + ",");
                            sw.Write(" p: " + row.P + ",");
                            sw.Write(" q: " + row.Q + ",");
                            sw.Write(" v: " + row.V + ",");
                            sw.Write(" s: " + row.S + ",");
                            sw.Write(" kind: " + row.Kind + ",");
                            sw.WriteLine(" F = " + row.Features);
                        }
                    }
                }
            }
            saveFileDialog.Dispose();
        }

        private void ShowFeaturesExtractorSettings_CheckedChanged(object sender, EventArgs e)
        {
            if (this.IsInitialized)
            {
                showFeaturesAtIndexDataGridView.Columns.Clear();
                if (showFeaturesExtractorSettings.ExtractorName == "HaarExtractor")
                {
                    showFeaturesAtIndexDataGridView.Columns.Add(templatesColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(scalesXColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(scalesYColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(positionXColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(positionYColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(featureColumn);
                }
                else if (showFeaturesExtractorSettings.ExtractorName == "HOGExtractor")
                {
                    showFeaturesAtIndexDataGridView.Columns.Add(binnsColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(blockXColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(blockYColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(featureColumn);
                }
                else if (showFeaturesExtractorSettings.ExtractorName == "PFMMExtractor" || showFeaturesExtractorSettings.ExtractorName == "ZernikeExtractor")
                {
                    showFeaturesAtIndexDataGridView.Columns.Add(ringsColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(ringsTypeColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(harmonicOrderColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(degreeColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(featureColumn);
                }
                else if (showFeaturesExtractorSettings.ExtractorName == "ZernikeInvariantsExtractor")
                {
                    showFeaturesAtIndexDataGridView.Columns.Add(ringsColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(ringsTypeColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(powerColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(harmonicOrderColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(degreeColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(harmonicOrderVColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(degreeSColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(kindColumn);
                    showFeaturesAtIndexDataGridView.Columns.Add(featureColumn);
                }
            }
        }

        private void ShowSampleAtIndexNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            if (this.IsInitialized)
            {
                showFeaturesAtIndexDataGridView.Items.Clear();
                showSampleAtIndexClassTextBox.Text = "";

                if (showFeaturesPathTextBox.Text != "" && File.Exists(showFeaturesPathTextBox.Text) && showSampleAtIndexNumericUpDown.Value > -1)
                {
                    ReadFeatures(showFeaturesPathTextBox.Text, (int)showSampleAtIndexNumericUpDown.Value);
                }
            }
        }
        #endregion
    }
}
