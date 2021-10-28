using System;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Globalization;
using System.Reflection;

namespace ClassificationToolboxWPF
{
    class GlobalFunctions
    {
        #region Fields
        readonly static System.Drawing.Color falseColor = System.Drawing.Color.Yellow;
        //static System.Drawing.Color currentColor = System.Drawing.Color.Blue;
        readonly static System.Drawing.Color trueColor = System.Drawing.Color.Red;
        #endregion

        #region Enums
        internal enum ERRORS
        {
            UNKNOWN_CLASSIFIER = -1,
            UNKNOWN_EXTRACTOR = -2,
            UNSUPPORTED_IMAGE_FORMAT = -3,
            CORRUPTED_FEATURES_FILE = -4,
            CORRUPTED_CLASSIFIER_FILE = -5,
            CORRUPTED_FILE = -6,
            INCORRECT_METRICES = -7,
            INCONSISTENT_FEATURES = -8,
            INCONSISTENT_WEIGHTS = -9,
            OPERATION_CANCELED = -10,
            NOT_IMPLEMENTED = -100,
            UNKNOWN_ERROR = -1000
        }

        internal enum GroupingMode
        {
            SUM = 1,
            AVERAGE_FAST = 2,
            AVERAGE_SLOW = 3
        }
        #endregion

        #region Metrices
        internal class ConfusionTableRow
        {
            public string RowHeader { get; set; }
            public int N { get; set; }
            public int T { get; set; }
            public int Sum { get; set; }
        }

        internal class MetricesTableRow
        {
            public string Name { get; set; }
            public double Value { get; set; }
        }

        internal static void PopulateConfusionMatrix(ref DataGrid confMatrix, int TP, int TN, int FP, int FN)
        {
            ((ConfusionTableRow)confMatrix.Items[0]).N = TN;
            ((ConfusionTableRow)confMatrix.Items[0]).T = FN;
            ((ConfusionTableRow)confMatrix.Items[0]).Sum = TN + FN;
            ((ConfusionTableRow)confMatrix.Items[1]).N = FP;
            ((ConfusionTableRow)confMatrix.Items[1]).T = TP;
            ((ConfusionTableRow)confMatrix.Items[1]).Sum = FP + TP;
            ((ConfusionTableRow)confMatrix.Items[2]).N = TN + FP;
            ((ConfusionTableRow)confMatrix.Items[2]).T = FN + TP;
            ((ConfusionTableRow)confMatrix.Items[2]).Sum = FP + TP + TN + FN;
            confMatrix.Items.Refresh();
        }

        internal static void InitializeMetrices(ref DataGrid confMatrix, ref DataGrid metricesMatrix)
        {
            metricesMatrix.Items.Clear();
            metricesMatrix.Items.Add(new MetricesTableRow { Name = "Accuracy", Value = 0 });
            metricesMatrix.Items.Add(new MetricesTableRow { Name = "Error", Value = 0 });
            metricesMatrix.Items.Add(new MetricesTableRow { Name = "Sensitivity (TPR)", Value = 0 });
            metricesMatrix.Items.Add(new MetricesTableRow { Name = "Specificity (SPC)", Value = 0 });
            metricesMatrix.Items.Add(new MetricesTableRow { Name = "F1 - score", Value = 0 });
            metricesMatrix.Items.Add(new MetricesTableRow { Name = "Precision (PPV)", Value = 0 });
            metricesMatrix.Items.Add(new MetricesTableRow { Name = "Negative predictive value (NPV)", Value = 0 });
            metricesMatrix.Items.Add(new MetricesTableRow { Name = "Fall - out (FAR)", Value = 0 });
            metricesMatrix.Items.Add(new MetricesTableRow { Name = "False negative rate", Value = 0 });
            metricesMatrix.Items.Add(new MetricesTableRow { Name = "False discovery rate", Value = 0 });
            metricesMatrix.Items.Add(new MetricesTableRow { Name = "Matthews correlation coefficient", Value = 0 });
            metricesMatrix.Items.Add(new MetricesTableRow { Name = "Informedness", Value = 0 });
            metricesMatrix.Items.Add(new MetricesTableRow { Name = "Markedness", Value = 0 });
            metricesMatrix.Items.Add(new MetricesTableRow { Name = "Euclidean distance (FAR = 0, TPR = 1)", Value = 0 });
            metricesMatrix.Items.Add(new MetricesTableRow { Name = "Average features", Value = 0 });

            confMatrix.Items.Clear();
            confMatrix.Items.Add(new ConfusionTableRow { RowHeader = "Negative Predictions", N = 0, T = 0, Sum = 0 });
            confMatrix.Items.Add(new ConfusionTableRow { RowHeader = "Positive Predictions", N = 0, T = 0, Sum = 0 });
            confMatrix.Items.Add(new ConfusionTableRow { RowHeader = "", N = 0, T = 0, Sum = 0 });

            metricesMatrix.Items.Refresh();
            confMatrix.Items.Refresh();
        }

        internal static void CalculateMetrices(ref DataGrid confMatrix, ref DataGrid metricesMatrix)
        {
            double TP = ((ConfusionTableRow)confMatrix.Items[1]).T;
            double TN = ((ConfusionTableRow)confMatrix.Items[0]).N;
            double FP = ((ConfusionTableRow)confMatrix.Items[1]).N;
            double FN = ((ConfusionTableRow)confMatrix.Items[0]).T;

            double sensitivity = TP / (TP + FN);
            double FPR = FP / (FP + TN);

            ((MetricesTableRow)metricesMatrix.Items[0]).Value = (TP + TN) / (TP + TN + FP + FN);
            ((MetricesTableRow)metricesMatrix.Items[1]).Value = (FP + FN) / (TP + TN + FP + FN);
            ((MetricesTableRow)metricesMatrix.Items[2]).Value = (TP) / (TP + FN);
            ((MetricesTableRow)metricesMatrix.Items[3]).Value = (TN) / (TN + FP);
            ((MetricesTableRow)metricesMatrix.Items[4]).Value = (2 * TP) / (2 * TP + FP + FN);
            ((MetricesTableRow)metricesMatrix.Items[5]).Value = (TP) / (TP + FP);
            ((MetricesTableRow)metricesMatrix.Items[6]).Value = (TN) / (TN + FN);
            ((MetricesTableRow)metricesMatrix.Items[7]).Value = (FP) / (TN + FP);
            ((MetricesTableRow)metricesMatrix.Items[8]).Value = (FN) / (TP + FN);
            ((MetricesTableRow)metricesMatrix.Items[9]).Value = (FP) / (TP + FP);
            ((MetricesTableRow)metricesMatrix.Items[10]).Value = ((TP * TN) - (FP * FN)) / Math.Sqrt((TP + FP) * (TP + FN) * (TN + FP) * (TN + FN));
            ((MetricesTableRow)metricesMatrix.Items[11]).Value = (TP / (TP + FN)) + (TN / (TN + FP)) - 1.0;
            ((MetricesTableRow)metricesMatrix.Items[12]).Value = (TP / (TP + FP)) + (TN / (TN + FN)) - 1.0;
            ((MetricesTableRow)metricesMatrix.Items[13]).Value = Math.Sqrt(Math.Pow(0.0 - FPR, 2.0) + Math.Pow(1.0 - sensitivity, 2.0));
            metricesMatrix.Items.Refresh();
        }

        internal static void SaveMetrices(ref DataGrid confMatrix, ref DataGrid metricesMatrix, ref Chart roc, string path, string classifier, string features, double [] thresholds = null)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            using (StreamWriter sw = new StreamWriter(path))
            {
                sw.WriteLine("Result:");
                sw.WriteLine("Classifier: " + Path.GetFileName(classifier));
                sw.WriteLine("Features: " + Path.GetFileName(features));
                sw.WriteLine();

                if (roc != null)
                {
                    sw.WriteLine(roc.Titles[1].Text);
                    sw.WriteLine(roc.Series[1].LegendText);
                }

                sw.WriteLine("TP: " + ((ConfusionTableRow)confMatrix.Items[1]).T);
                sw.WriteLine("FP: " + ((ConfusionTableRow)confMatrix.Items[1]).N);
                sw.WriteLine("TN: " + ((ConfusionTableRow)confMatrix.Items[0]).N);
                sw.WriteLine("FN: " + ((ConfusionTableRow)confMatrix.Items[0]).T);

                foreach (MetricesTableRow row in metricesMatrix.Items)
                    sw.WriteLine(row.Name + ": " + row.Value);
                sw.WriteLine();

                if (roc != null)
                {
                    sw.WriteLine("ROC VALUES:");
                    for (int i = 0; i < roc.Series[1].Points.Count; i++)
                    {
                        if (thresholds != null)
                            sw.WriteLine("FAR: {0:0.00000000}, SENS: {1:0.00000000}, THR: {2:0.00000000}", roc.Series[1].Points[i].XValue, roc.Series[1].Points[i].YValues[0], thresholds[i]);
                        else
                            sw.WriteLine("FAR: {0:0.00000000}, SENS: {1:0.00000000}", roc.Series[1].Points[i].XValue, roc.Series[1].Points[i].YValues[0]);
                    }

                    roc.SaveImage(Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
                    roc.SaveImage(Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + ".emf", System.Drawing.Imaging.ImageFormat.Emf);
                }
            }
        }

        internal static void SaveMetrices(ref DataGrid confMatrix, ref DataGrid metricesMatrix, StreamWriter sw, double threshold)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            sw.WriteLine("Threshold: " + threshold);
            sw.WriteLine("TP: " + ((ConfusionTableRow)confMatrix.Items[1]).T);
            sw.WriteLine("FP: " + ((ConfusionTableRow)confMatrix.Items[1]).N);
            sw.WriteLine("TN: " + ((ConfusionTableRow)confMatrix.Items[0]).N);
            sw.WriteLine("FN: " + ((ConfusionTableRow)confMatrix.Items[0]).T);

            foreach (MetricesTableRow row in metricesMatrix.Items)
                sw.WriteLine(row.Name + ": " + row.Value);
            sw.WriteLine();
        }

        #endregion

        #region Error handling functions
        internal static void ThrowError(int errorCode)
        {
            throw new Exception(GetErrorDescription(errorCode));
        }

        internal static void ThrowError(int errorCode, string optionalInfo)
        {
            throw new Exception(GetErrorDescription(errorCode, optionalInfo));
        }

        internal static string GetErrorDescription(int errorCode)
        {
            string errorMessage;
            if (errorCode == (int)ERRORS.UNKNOWN_CLASSIFIER)
                errorMessage = "Unknown classifier type.";
            else if (errorCode == (int)ERRORS.UNKNOWN_EXTRACTOR)
                errorMessage = "Unknown extractor type.";
            else if (errorCode == (int)ERRORS.UNSUPPORTED_IMAGE_FORMAT)
                errorMessage = "Image have unsupported format.";
            else if (errorCode == (int)ERRORS.CORRUPTED_FEATURES_FILE)
                errorMessage = "Corrupted features file.";
            else if (errorCode == (int)ERRORS.CORRUPTED_CLASSIFIER_FILE)
                errorMessage = "Corrupted classifier file.";
            else if (errorCode == (int)ERRORS.CORRUPTED_FILE)
                errorMessage = "Corrupted file/files.";
            else if (errorCode == (int)ERRORS.INCORRECT_METRICES)
                errorMessage = "Inccorrect metrices.";
            else if (errorCode == (int)ERRORS.INCONSISTENT_FEATURES)
                errorMessage = "Inconsistent features.";
            else if (errorCode == (int)ERRORS.INCONSISTENT_WEIGHTS)
                errorMessage = "Inconsistent weights.";
            else if (errorCode == (int)ERRORS.OPERATION_CANCELED)
                errorMessage = "Operation canceled.";
            else if (errorCode == (int)ERRORS.NOT_IMPLEMENTED)
                errorMessage = "Function/Classifier not implemented.";
            else
                errorMessage = "Unknown error.";
            return errorMessage;
        }

        internal static string GetErrorDescription(int errorCode, string optionalInfo)
        {
            return GetErrorDescription(errorCode) + optionalInfo;
        }
        #endregion

        #region IO functions
        internal static void InitializeDirectory(ref TextBox textBox, string properties)
        {
            if (Directory.Exists((string)Properties.Settings.Default[properties]))
                textBox.Text = (string)Properties.Settings.Default[properties];
            else
                textBox.Text = "";

            if (textBox.Text != "" && textBox.Text[textBox.Text.Length - 1] != '\\')
            {
                textBox.Text += '\\';
                Properties.Settings.Default[properties] = (string)Properties.Settings.Default[properties] + '\\';
                Properties.Settings.Default.Save();
            }
        }

        internal static void InitializePath(ref TextBox textBox, string properties)
        {
            if (File.Exists((string)Properties.Settings.Default[properties]))
                textBox.Text = (string)Properties.Settings.Default[properties];
            else
                textBox.Text = "";
        }

        internal static System.Windows.Forms.DialogResult SelectFolder(string properties)
        {
            string path = (string)Properties.Settings.Default[properties];
            if (!Directory.Exists(path))
                path = "";

            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog() { SelectedPath = path };
            System.Windows.Forms.DialogResult result = folderBrowserDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Properties.Settings.Default[properties] = folderBrowserDialog.SelectedPath;
                Properties.Settings.Default.Save();
            }
            folderBrowserDialog.Dispose();

            return result;
        }

        internal static System.Windows.Forms.DialogResult SelectFolder(ref TextBox textBox, string properties)
        {
            string path = (string)Properties.Settings.Default[properties];
            if (!Directory.Exists(path))
                path = "";

            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog() { SelectedPath = path };
            System.Windows.Forms.DialogResult result = folderBrowserDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Properties.Settings.Default[properties] = folderBrowserDialog.SelectedPath;
                textBox.Text = folderBrowserDialog.SelectedPath;
                if (textBox.Text[textBox.Text.Length - 1] != '\\')
                {
                    textBox.Text += '\\';
                    Properties.Settings.Default[properties] = (string)Properties.Settings.Default[properties] + '\\';
                }
                Properties.Settings.Default.Save();
            }
            folderBrowserDialog.Dispose();

            return result;
        }

        internal static System.Windows.Forms.DialogResult SelectFile(ref TextBox textBox, string properties, string filter)
        {
            string path = (string)Properties.Settings.Default[properties];
            if (path != "")
                path = Path.GetDirectoryName(path);
            if (!Directory.Exists(path))
                path = "";

            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog()
            {
                Filter = filter,
                FileName = ""
            };
            if (path != "")
                openFileDialog.InitialDirectory = path;

            System.Windows.Forms.DialogResult result = openFileDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Properties.Settings.Default[properties] = openFileDialog.FileName;
                Properties.Settings.Default.Save();

                textBox.Text = openFileDialog.FileName;
            }
            openFileDialog.Dispose();

            return result;
        }

        internal static System.Windows.Forms.DialogResult SelectFile(string properties, string filter)
        {
            string path = (string)Properties.Settings.Default[properties];
            if (path != "")
                path = Path.GetDirectoryName(path);
            if (!Directory.Exists(path))
                path = "";

            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog()
            {
                Filter = filter,
                FileName = ""
            };
            if (path != "")
                openFileDialog.InitialDirectory = path;

            System.Windows.Forms.DialogResult result = openFileDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Properties.Settings.Default[properties] = openFileDialog.FileName;
                Properties.Settings.Default.Save();
            }
            openFileDialog.Dispose();

            return result;
        }

        internal static System.Windows.Forms.DialogResult SelectCooridantes(string properties)
        {
            return SelectFile(properties, "Coordinate Files | *.txt");
        }

        internal static System.Windows.Forms.DialogResult SelectClassifier(ref TextBox textBox, string properties)
        {
            return SelectFile(ref textBox, properties, "Classifier Files | *.model");
        }

        internal static System.Windows.Forms.DialogResult SelectFeatures(ref TextBox textBox, string properties)
        {
            return SelectFile(ref textBox, properties, "Features Files | *.fet.bin");
        }

        internal static System.Windows.Forms.DialogResult SelectImage(ref TextBox textBox, string properties)
        {
            return SelectFile(ref textBox, properties, "Image files (*.jpg, *.jpeg, *bmp, *.png) | *.jpg; *.jpeg; *.bmp; *.png|Bitmap Image | *.bmp|JPEG Image | *.jpg; *.jpeg|Png Image | *.png");
        }

        internal static System.Windows.Forms.DialogResult SelectImage(out string fileName, string properties)
        {
            fileName = "";
            string path = (string)Properties.Settings.Default[properties];
            if (!Directory.Exists(path))
                path = "";

            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog()
            {
                Filter = "Image files (*.jpg, *.jpeg, *bmp, *.png) | *.jpg; *.jpeg; *.bmp; *.png|Bitmap Image | *.bmp|JPEG Image | *.jpg; *.jpeg|Png Image | *.png",
                FileName = "",
                InitialDirectory = path
            };
            System.Windows.Forms.DialogResult result = openFileDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Properties.Settings.Default[properties] = Path.GetDirectoryName(openFileDialog.FileName);
                Properties.Settings.Default.Save();
                fileName = openFileDialog.FileName;
            }
            openFileDialog.Dispose();

            return result;
        }

        internal static System.Windows.Forms.DialogResult SelectOptionalFile(ref TextBox textBox, string properties, string filter)
        {
            string path = (string)Properties.Settings.Default[properties];
            if (path != "" && !Directory.Exists(path))
                path = "";

            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog()
            {
                Filter = filter,
                FileName = ""
            };
            if (path != "")
                openFileDialog.InitialDirectory = path;

            System.Windows.Forms.DialogResult result = openFileDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Properties.Settings.Default[properties] = Path.GetDirectoryName(openFileDialog.FileName);
                Properties.Settings.Default.Save();

                textBox.Text = openFileDialog.FileName;
            }
            openFileDialog.Dispose();

            return result;
        }

        internal static System.Windows.Forms.DialogResult SelectOptionalFeatures(ref TextBox textBox, string properties)
        {
            return SelectOptionalFile(ref textBox, properties, "Features Files | *.fet.bin");
        }

        internal static void GetImageFiles(ref List<string> images, string path, bool includeSubfolders)
        {
            try
            {
                SearchOption so = SearchOption.TopDirectoryOnly;
                StringComparison sc = StringComparison.OrdinalIgnoreCase;

                images.AddRange(Directory.EnumerateFiles(path, "*.*", so)
                            .Where(s => s.EndsWith(".jpg", sc) || s.EndsWith(".jpeg", sc) || s.EndsWith(".bmp", sc) || s.EndsWith(".png", sc)).ToArray());

                if (includeSubfolders)
                {
                    IEnumerable<string> directories = Directory.EnumerateDirectories(path);
                    foreach (string directory in directories)
                        GetImageFiles(ref images, directory, includeSubfolders);
                }
            }
            catch
            {
            }
        }
        #endregion

        #region Image processing
        internal static void InitializeDetectionWindows(string extractorType, int width, int height, ref NativeMethods.DetectionParameters detectionParameters, ref List<System.Drawing.Rectangle> detectionWindows, ref double[] detectionOutputs, out int maxWindowSize, ref System.Drawing.Point[] sizes)
        {
            int nx = width;
            int ny = height;

            if (extractorType == "PFMMExtractor" || extractorType.ToUpper().Contains("ZERNIKE"))
            {
                nx -= nx % 2;
                ny -= ny % 2;
            }

            maxWindowSize = 0;
            for (int s = 0; s < detectionParameters.scales; s++)
            {
                int wx = (int)Math.Round(Math.Pow(detectionParameters.windowScalingRatio, s) * detectionParameters.windowMinimalWidth);
                int wy = (int)Math.Round(Math.Pow(detectionParameters.windowScalingRatio, s) * detectionParameters.windowMinimalHeight);
                if (wx > nx) wx = nx;
                if (wy > ny) wy = ny;

                if (extractorType == "PFMMExtractor" || extractorType.ToUpper().Contains("ZERNIKE"))
                {
                    int m = Math.Min(wx, wy);
                    m -= m % 2;
                    wx = m;
                    wy = m;
                }

                if (Math.Max(wx, wy) > maxWindowSize)
                    maxWindowSize = Math.Max(wx, wy);

                sizes[s].X = wx;
                sizes[s].Y = wy;

                int dx = (int)Math.Round(detectionParameters.windowJumpingRatio * wx);
                int dy = (int)Math.Round(detectionParameters.windowJumpingRatio * wy);

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
                        System.Drawing.Rectangle window = new System.Drawing.Rectangle(x, y, wx, wy);
                        detectionWindows.Add(window);
                    }
                }
            }
            detectionOutputs = new double[detectionWindows.Count];
        }

        unsafe static internal void DetectObject(ref byte[] bmp, int width, int height, int stride, int bitsPerPixel, ref NativeMethods.DetectionParameters detectionParameters, ref double[] detectionOutputs, ref List<System.Drawing.Rectangle> detectionWindows, ref System.Drawing.Point[] sizes)
        {    
            fixed (double* detectionOutputsPointer = detectionOutputs)
            {
                fixed (byte* dataPointer = bmp)
                {
                    fixed (System.Drawing.Rectangle* recPtr = (System.Drawing.Rectangle[])typeof(List<System.Drawing.Rectangle>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(detectionWindows))
                    {
                        fixed (System.Drawing.Point *sizePtr = sizes)
                        {
                            int status = NativeMethods.Detection(detectionParameters, detectionOutputsPointer, dataPointer, bitsPerPixel / 8, width, height, stride, recPtr, detectionWindows.Count, sizePtr);

                            if (status < 0)
                                ThrowError(status);
                        }
                    }
                }
            }
        }

        static internal void SelectWindow(ref double[] detectionOutputs, ref List<int> selectedWindows, double threshold)//, double minJaccard)
        {
            selectedWindows.Clear();
            for (int i = 0; i < detectionOutputs.Length; i++)
            {
                if (detectionOutputs[i] >= threshold)
                {
                    selectedWindows.Add(i);
                }
            }
        }

        static internal void SelectAndDrawWindow(ref WriteableBitmap bmp, ref double[] detectionOutputs, ref List<System.Drawing.Rectangle> detectionWindows, ref List<int> selectedWindows, ref List<System.Drawing.Rectangle> correctWindows, double threshold, double minJaccard, int penSize)
        {
            selectedWindows.Clear();
            
            bmp.Lock();
            var bmpTmp = new System.Drawing.Bitmap(bmp.PixelWidth, bmp.PixelHeight,
                                     bmp.BackBufferStride,
                                     System.Drawing.Imaging.PixelFormat.Format24bppRgb,
                                     bmp.BackBuffer);
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmpTmp))
            {
                for (int i = 0; i < detectionOutputs.Length; i++)
                {
                    if (detectionOutputs[i] >= threshold)
                    {
                        selectedWindows.Add(i);


                        bool correct = false;

                        foreach (System.Drawing.Rectangle correctWindow in correctWindows)
                        {
                            System.Drawing.Rectangle intersection = System.Drawing.Rectangle.Intersect(correctWindow, detectionWindows[i]);
                            int intersectionField = (intersection.Width * intersection.Height);
                            int fieldsSum = (correctWindow.Width * correctWindow.Height + detectionWindows[i].Width * detectionWindows[i].Height - intersectionField);
                            double J = intersectionField / (double)fieldsSum;

                            if (J >= minJaccard)
                            {
                                correct = true;
                                break;
                            }
                        }

                        if (correct)
                        {
                            System.Drawing.Pen pen = new System.Drawing.Pen(trueColor, penSize);
                            g.DrawRectangle(pen, detectionWindows[i]);
                        }
                        else
                        {
                            System.Drawing.Pen pen = new System.Drawing.Pen(falseColor, penSize);
                            g.DrawRectangle(pen, detectionWindows[i]);
                        }
                    }
                }
            }
            bmpTmp.Dispose();
            bmp.Unlock();
        }

        static internal void GroupWindow(ref WriteableBitmap bmp, ref List<System.Drawing.Rectangle> detectionWindows, ref List<int> selectedWindows, ref List<System.Drawing.Rectangle> correctWindows,
                    double minJaccardJoining, double minJaccardAccurancy, GroupingMode mode, int penSize, System.Drawing.Font windowFont, out double TP, out double FP, out double FN, out double TN)
        {
            TP = 0;
            FP = 0;
            FN = correctWindows.Count;
            TN = detectionWindows.Count;
            List<bool> isDetected = new List<bool>();
            for (int i = 0; i < correctWindows.Count; i++)
                isDetected.Add(false);

            List<System.Drawing.Rectangle> onGoing = new List<System.Drawing.Rectangle>();
            List<int> joinWeights = new List<int>();

            foreach (int index in selectedWindows)
            {
                onGoing.Add(detectionWindows[index]);
                joinWeights.Add(1);
            }

            bmp.Lock();
            var bmpTmp = new System.Drawing.Bitmap(bmp.PixelWidth, bmp.PixelHeight,
                                     bmp.BackBufferStride,
                                     System.Drawing.Imaging.PixelFormat.Format24bppRgb,
                                     bmp.BackBuffer);

            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmpTmp))
            {
                if (mode == GroupingMode.SUM || mode == GroupingMode.AVERAGE_FAST)
                {
                    int windowsCount = 0;
                    while (onGoing.Count != 0)
                    {
                        bool draw = true;

                        System.Drawing.Rectangle window = onGoing[onGoing.Count - 1];
                        int countOne = joinWeights[onGoing.Count - 1];
                        int countSum = countOne;

                        onGoing.RemoveAt(onGoing.Count - 1);
                        joinWeights.RemoveAt(joinWeights.Count - 1);
                        for (int i = 0; i < onGoing.Count; i++)
                        {
                            System.Drawing.Rectangle secondWindow = onGoing[i];
                            int countTwo = joinWeights[i];

                            System.Drawing.Rectangle intersection = System.Drawing.Rectangle.Intersect(window, secondWindow);
                            int intersectionField = intersection.Width * intersection.Height;
                            int fieldsSum = window.Width * window.Height + secondWindow.Width * secondWindow.Height - intersectionField;
                            double J = intersectionField / (double)fieldsSum;
                            if (J > minJaccardJoining)
                            {
                                countSum += countTwo;

                                onGoing.RemoveAt(i);
                                joinWeights.RemoveAt(i);
                                if (mode == GroupingMode.SUM)
                                {
                                    window = System.Drawing.Rectangle.Union(window, secondWindow);
                                }
                                else if (mode == GroupingMode.AVERAGE_FAST)
                                {
                                    int width = (countOne * window.Width + countTwo * secondWindow.Width) / countSum;
                                    int height = (countOne * window.Height + countTwo * secondWindow.Height) / countSum;
                                    int x = (countOne * window.X + countTwo * secondWindow.X) / countSum;
                                    int y = (countOne * window.Y + countTwo * secondWindow.Y) / countSum;
                                    window = new System.Drawing.Rectangle(x, y, width, height);
                                }
                                draw = false;
                                break;
                            }
                        }

                        if (draw == true)
                        {
                            windowsCount++;
                            bool correct = false;
                            for (int cw = 0; cw < correctWindows.Count; cw++)
                            {
                                System.Drawing.Rectangle correctWindow = correctWindows[cw];
                                System.Drawing.Rectangle intersection = System.Drawing.Rectangle.Intersect(correctWindow, window);
                                int intersectionField = intersection.Width * intersection.Height;
                                int fieldsSum = correctWindow.Width * correctWindow.Height + window.Width * window.Height - intersectionField;
                                double J = intersectionField / (double)fieldsSum;

                                if (J >= minJaccardAccurancy)
                                {
                                    if (isDetected[cw])
                                    {
                                        correct = false;
                                    }
                                    else
                                    {
                                        TP++;
                                        FN--;
                                        TN -= countSum;

                                        correct = true;
                                    }
                                    isDetected[cw] = true;
                                    break;
                                }

                            }

                            if (correct)
                            {
                                System.Drawing.Pen pen = new System.Drawing.Pen(trueColor, penSize);
                                g.DrawRectangle(pen, window);
                                g.DrawString(windowsCount.ToString(), windowFont, new System.Drawing.SolidBrush(trueColor), new System.Drawing.Point(window.X, window.Y));
                            }
                            else
                            {
                                FP++;
                                TN -= countSum;

                                System.Drawing.Pen pen = new System.Drawing.Pen(falseColor, penSize);
                                g.DrawRectangle(pen, window);
                                g.DrawString(windowsCount.ToString(), windowFont, new System.Drawing.SolidBrush(falseColor), new System.Drawing.Point(window.X, window.Y));
                            }
                        }
                        else
                        {
                            onGoing.Add(window);
                            joinWeights.Add(countSum);
                        }
                    }
                }
                else
                {
                    List<System.Drawing.Rectangle> result = new List<System.Drawing.Rectangle>();
                    const double absoluteSufficientJaccard = 0.95;
                    while (onGoing.Count >= 1)
                    {
                        if (onGoing.Count == 1)
                        {
                            result.Add(onGoing.ElementAt(0));
                            break;
                        }

                        int maxI = 0;
                        int maxJ = 0;
                        double maxJaccardJoining = 0.0;
                        bool absoluteSufficientJaccardReached = false;

                        System.Drawing.Rectangle iWindow;
                        System.Drawing.Rectangle jWindow;

                        for (int i = 0; i < onGoing.Count; i++)
                        {
                            iWindow = onGoing.ElementAt(i);
                            for (int j = i + 1; j < onGoing.Count; j++)
                            {
                                jWindow = onGoing.ElementAt(j);

                                System.Drawing.Rectangle intersection = System.Drawing.Rectangle.Intersect(iWindow, jWindow);
                                double intersectionField = intersection.Width * intersection.Height;
                                if (intersectionField <= 0)
                                    continue;

                                double unionField = iWindow.Width * iWindow.Height + jWindow.Width * jWindow.Height - intersectionField;
                                double jaccard = intersectionField / (double)unionField;

                                if (jaccard > maxJaccardJoining)
                                {
                                    maxI = i;
                                    maxJ = j;
                                    maxJaccardJoining = jaccard;
                                    if (maxJaccardJoining >= absoluteSufficientJaccard)
                                    {
                                        absoluteSufficientJaccardReached = true;
                                        break;
                                    }
                                }
                            }
                            if (absoluteSufficientJaccardReached)
                                break;
                        }
                        if (maxJaccardJoining >= minJaccardJoining)
                        {
                            iWindow = onGoing.ElementAt(maxI);
                            jWindow = onGoing.ElementAt(maxJ);
                            onGoing.RemoveAt(maxI);
                            onGoing.RemoveAt(maxJ - 1);

                            int weight1 = joinWeights.ElementAt(maxI);
                            int weight2 = joinWeights.ElementAt(maxJ);
                            int weightSum = weight1 + weight2;
                            joinWeights.RemoveAt(maxI);
                            joinWeights.RemoveAt(maxJ - 1);

                            int width = (weight1 * iWindow.Width + weight2 * jWindow.Width) / weightSum;
                            int height = (weight1 * iWindow.Height + weight2 * jWindow.Height) / weightSum;
                            int x = (weight1 * iWindow.X + weight2 * jWindow.X) / weightSum;
                            int y = (weight1 * iWindow.Y + weight2 * jWindow.Y) / weightSum;
                            System.Drawing.Rectangle joint = new System.Drawing.Rectangle(x, y, width, height);

                            onGoing.Add(joint);
                            joinWeights.Add(weightSum);
                        }
                        else
                        {
                            for (int k = 0; k < onGoing.Count; k++)
                                result.Add(onGoing.ElementAt(k));
                            break;
                        }
                    }

                    int windowsCount = 0;
                    for (int k = 0; k < result.Count; k++)
                    {
                        windowsCount++;
                        System.Drawing.Rectangle window = result[k];

                        bool correct = false;
                        for (int cw = 0; cw < correctWindows.Count; cw++)
                        {
                            System.Drawing.Rectangle correctWindow = correctWindows[cw];
                            System.Drawing.Rectangle intersection = System.Drawing.Rectangle.Intersect(correctWindow, window);
                            double intersectionField = intersection.Width * intersection.Height;
                            double fieldsSum = correctWindow.Width * correctWindow.Height + window.Width * window.Height - intersectionField;
                            double J = intersectionField / fieldsSum;

                            if (J >= minJaccardAccurancy)
                            {
                                if (isDetected[cw])
                                {
                                    correct = false;
                                }
                                else
                                {
                                    TP++;
                                    FN--;
                                    TN -= joinWeights[k];

                                    correct = true;
                                }
                                isDetected[cw] = true;
                                break;
                            }

                        }

                        if (correct)
                        {
                            System.Drawing.Pen pen = new System.Drawing.Pen(trueColor, penSize);
                            g.DrawRectangle(pen, window);
                            g.DrawString(windowsCount.ToString(), windowFont, new System.Drawing.SolidBrush(trueColor), new System.Drawing.Point(window.X, window.Y));
                        }
                        else
                        {
                            FP++;
                            TN -= joinWeights[k];

                            System.Drawing.Pen pen = new System.Drawing.Pen(falseColor, penSize);
                            g.DrawRectangle(pen, window);
                            g.DrawString(windowsCount.ToString(), windowFont, new System.Drawing.SolidBrush(falseColor), new System.Drawing.Point(window.X, window.Y));
                        }
                    }
                }
            }
            bmpTmp.Dispose();
            bmp.Unlock();
        }

        static internal void CalculateConfusionMatrixForWindows(ref List<System.Drawing.Rectangle> detectionWindows, ref List<int> selectedWindows, ref List<System.Drawing.Rectangle> correctWindows,
            double minJaccardJoining, double minJaccardAccuracy, GroupingMode mode, out double TP, out double FP, out double FN, out double TN)
        {                                     
            TP = 0;
            FP = 0;
            FN = correctWindows.Count;
            TN = detectionWindows.Count;
            List<bool> isDetected = new List<bool>();
            for (int i = 0; i < correctWindows.Count; i++)
                isDetected.Add(false);

            List<System.Drawing.Rectangle> onGoing = new List<System.Drawing.Rectangle>();
            List<int> joinWeights = new List<int>();

            foreach (int index in selectedWindows)
            {
                onGoing.Add(detectionWindows[index]);
                joinWeights.Add(1);
            }

            if (mode == GroupingMode.SUM || mode == GroupingMode.AVERAGE_FAST)
            {
                //int windowsCount = 0;
                while (onGoing.Count != 0)
                {
                    bool draw = true;

                    System.Drawing.Rectangle window = onGoing[onGoing.Count - 1];
                    int countOne = joinWeights[onGoing.Count - 1];
                    int countSum = countOne;

                    onGoing.RemoveAt(onGoing.Count - 1);
                    joinWeights.RemoveAt(joinWeights.Count - 1);
                    for (int i = 0; i < onGoing.Count; i++)
                    {
                        System.Drawing.Rectangle secondWindow = onGoing[i];
                        int countTwo = joinWeights[i];

                        System.Drawing.Rectangle intersection = System.Drawing.Rectangle.Intersect(window, secondWindow);
                        int intersectionField = intersection.Width * intersection.Height;
                        int fieldsSum = window.Width * window.Height + secondWindow.Width * secondWindow.Height - intersectionField;
                        double J = intersectionField / (double)fieldsSum;
                        if (J > minJaccardJoining)
                        {
                            countSum += countTwo;

                            onGoing.RemoveAt(i);
                            joinWeights.RemoveAt(i);
                            if (mode == GroupingMode.SUM)
                            {
                                window = System.Drawing.Rectangle.Union(window, secondWindow);
                            }
                            else if (mode == GroupingMode.AVERAGE_FAST)
                            {
                                int width = (countOne * window.Width + countTwo * secondWindow.Width) / countSum;
                                int height = (countOne * window.Height + countTwo * secondWindow.Height) / countSum;
                                int x = (countOne * window.X + countTwo * secondWindow.X) / countSum;
                                int y = (countOne * window.Y + countTwo * secondWindow.Y) / countSum;
                                window = new System.Drawing.Rectangle(x, y, width, height);
                            }
                            draw = false;
                            break;
                        }
                    }

                    if (draw == true)
                    {
                        //windowsCount++;
                        bool correct = false;
                        for (int cw = 0; cw < correctWindows.Count; cw++)
                        {
                            System.Drawing.Rectangle correctWindow = correctWindows[cw];
                            System.Drawing.Rectangle intersection = System.Drawing.Rectangle.Intersect(correctWindow, window);
                            int intersectionField = intersection.Width * intersection.Height;
                            int fieldsSum = correctWindow.Width * correctWindow.Height + window.Width * window.Height - intersectionField;
                            double J = intersectionField / (double)fieldsSum;

                            if (J >= minJaccardAccuracy)
                            {
                                if (isDetected[cw])
                                {
                                    correct = false;
                                }
                                else
                                {
                                    TP++;
                                    FN--;
                                    TN -= countSum;

                                    correct = true;
                                }
                                isDetected[cw] = true;
                                break;
                            }

                        }

                        if (!correct)
                        {
                            FP++;
                            TN -= countSum;
                        }
                    }
                    else
                    {
                        onGoing.Add(window);
                        joinWeights.Add(countSum);
                    }
                }
            }
            else
            {
                List<System.Drawing.Rectangle> result = new List<System.Drawing.Rectangle>();
                const double absoluteSufficientJaccard = 0.95;
                while (onGoing.Count >= 1)
                {
                    if (onGoing.Count == 1)
                    {
                        result.Add(onGoing.ElementAt(0));
                        break;
                    }

                    int maxI = 0;
                    int maxJ = 0;
                    double maxJaccardJoining = 0.0;
                    bool absoluteSufficientJaccardReached = false;

                    System.Drawing.Rectangle iWindow;
                    System.Drawing.Rectangle jWindow;

                    for (int i = 0; i < onGoing.Count; i++)
                    {
                        iWindow = onGoing.ElementAt(i);
                        for (int j = i + 1; j < onGoing.Count; j++)
                        {
                            jWindow = onGoing.ElementAt(j);

                            System.Drawing.Rectangle intersection = System.Drawing.Rectangle.Intersect(iWindow, jWindow);
                            double intersectionField = intersection.Width * intersection.Height;
                            if (intersectionField <= 0)
                                continue;

                            double unionField = iWindow.Width * iWindow.Height + jWindow.Width * jWindow.Height - intersectionField;
                            double jaccard = intersectionField / (double)unionField;

                            if (jaccard > maxJaccardJoining)
                            {
                                maxI = i;
                                maxJ = j;
                                maxJaccardJoining = jaccard;
                                if (maxJaccardJoining >= absoluteSufficientJaccard)
                                {
                                    absoluteSufficientJaccardReached = true;
                                    break;
                                }
                            }
                        }
                        if (absoluteSufficientJaccardReached)
                            break;
                    }
                    if (maxJaccardJoining >= minJaccardJoining)
                    {
                        iWindow = onGoing.ElementAt(maxI);
                        jWindow = onGoing.ElementAt(maxJ);
                        onGoing.RemoveAt(maxI);
                        onGoing.RemoveAt(maxJ - 1);

                        int weight1 = joinWeights.ElementAt(maxI);
                        int weight2 = joinWeights.ElementAt(maxJ);
                        int weightSum = weight1 + weight2;
                        joinWeights.RemoveAt(maxI);
                        joinWeights.RemoveAt(maxJ - 1);

                        int width = (weight1 * iWindow.Width + weight2 * jWindow.Width) / weightSum;
                        int height = (weight1 * iWindow.Height + weight2 * jWindow.Height) / weightSum;
                        int x = (weight1 * iWindow.X + weight2 * jWindow.X) / weightSum;
                        int y = (weight1 * iWindow.Y + weight2 * jWindow.Y) / weightSum;
                        System.Drawing.Rectangle joint = new System.Drawing.Rectangle(x, y, width, height);

                        onGoing.Add(joint);
                        joinWeights.Add(weightSum);
                    }
                    else
                    {
                        for (int k = 0; k < onGoing.Count; k++)
                            result.Add(onGoing.ElementAt(k));
                        break;
                    }
                }

                int windowsCount = 0;
                for (int k = 0; k < result.Count; k++)
                {
                    windowsCount++;
                    System.Drawing.Rectangle window = result[k];

                    bool correct = false;
                    for (int cw = 0; cw < correctWindows.Count; cw++)
                    {
                        System.Drawing.Rectangle correctWindow = correctWindows[cw];
                        System.Drawing.Rectangle intersection = System.Drawing.Rectangle.Intersect(correctWindow, window);
                        double intersectionField = intersection.Width * intersection.Height;
                        double fieldsSum = correctWindow.Width * correctWindow.Height + window.Width * window.Height - intersectionField;
                        double J = intersectionField / fieldsSum;

                        if (J >= minJaccardAccuracy)
                        {
                            if (isDetected[cw])
                            {
                                correct = false;
                            }
                            else
                            {
                                TP++;
                                FN--;
                                TN -= joinWeights[k];

                                correct = true;
                            }
                            isDetected[cw] = true;
                            break;
                        }

                    }

                    if (!correct)
                    { 
                        FP++;
                        TN -= joinWeights[k];
                    }
                }
            }
        }
        #endregion

        internal static IEnumerable<double> SteppedIterator(double startIndex, double endIndex, double stepSize)
        {
            for (double i = startIndex; i <= endIndex; i += stepSize)
            {
                yield return i;
            }
        }
    }

    internal static class NativeMethods
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int memcmp(byte[] b1, byte[] b2, long count);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void ProgressCallback(int value);

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "InitializePFMM", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        internal static extern void InitializePFMM();

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "ClearMemoryPFMM", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        internal static extern void ClearMemoryPFMM();

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "InitializeZernike", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        internal static extern void InitializeZernike();

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "ClearMemoryZernike", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        internal static extern void ClearMemoryZernike();

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "InitializeZernikeFP", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        internal static extern void InitializeZernikeFP();

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "ClearMemoryZernikeFP", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        internal static extern void ClearMemoryZernikeFP();

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "SetParallelity", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        internal static extern void SetParallelity(int ompThreads, int bufferSize);

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "ZernikeComparison", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        unsafe internal static extern int ZernikeComparison(double* resultsII, double* resultsFPII, int* extractorParameters, byte* image, int bytesPerPixel, int width, int height, int stride, int winSize, double threshold, [MarshalAs(UnmanagedType.FunctionPtr)] ProgressCallback callback);

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "Extraction", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        unsafe internal static extern int Extraction(string samplesPath, string positiveSamplesPath, string savePath, string extractor, int* parameters, ImageFileTypeSelector.FileType saveMode, [MarshalAs(UnmanagedType.U1)] bool append, [MarshalAs(UnmanagedType.FunctionPtr)] ProgressCallback callback);

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "ExtractFromImage", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        unsafe internal static extern int ExtractFromImage(string extractor, int* parameters, byte* image, int bytesPerPixel,
            int width, int height, int stride, System.Drawing.Rectangle* rectangle, int winCount, System.Drawing.Point* sizes, int sizesCount,
            string savePath, int cls);

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "ExtractFromImageFinalize", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        unsafe internal static extern int ExtractFromImageFinalize(string savePath, string tmpPath, [MarshalAs(UnmanagedType.U1)] bool firstOrignial);

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "Testing", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        unsafe internal static extern int Testing(string featuresPath, string classifierPath, int* classes, double* thresholds, double* avgFeatures, [MarshalAs(UnmanagedType.FunctionPtr)] ProgressCallback callback);
       
        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "LoadExtractor", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        unsafe internal static extern int LoadExtractor(string extractorName, int* extractorParameters);

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "LoadClassifier", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        unsafe internal static extern int LoadClassifier(string classifierPath);

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "TestDetectionTime", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        unsafe internal static extern int TestDetectionTime(int repetitions, DetectionParameters detectionParameters,
            long* outputs, byte* image, int bytesPerPixel, int width, int height, int stride, System.Drawing.Rectangle* rectangle, int winCount, System.Drawing.Point* sizes, [MarshalAs(UnmanagedType.FunctionPtr)] ProgressCallback callback);

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "Detection", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        unsafe internal static extern int Detection(DetectionParameters detectionParameters,double* outputs, byte* image, int bytesPerPixel, 
            int width, int height, int stride, System.Drawing.Rectangle* rectangle, int winCount, System.Drawing.Point* sizes);

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "Learn", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        unsafe internal static extern double Learn(string classifierPath, string classifierType, ClassifierParameters parameters, string validationSetPath);

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "LoadLearningData", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        unsafe internal static extern int LoadLearningData(string featuresPath);

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "FreeLearningData", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        unsafe internal static extern int FreeLearningData();

        [DllImport("ClassificationToolboxDll.dll", EntryPoint = "RemoveSamples", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        unsafe internal static extern int RemoveSamples(string path, bool removeNegatives, bool removePositives);


        //[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        //internal struct RectangleSimple
        //{
        //    public int x;
        //    public int y;
        //    public int w;
        //    public int h;
        //}

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct ClassifierParameters
        {
            // Cascade parameters
            public int cascadeStages;
            public double maxFAR;
            public double minSpecificity;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string boostingType;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 10)]
            public string learningMethod;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
            public string nonFaceImagesPath;
            public int childsCount;
            public int splits;
            [MarshalAs(UnmanagedType.U1)]
            public bool isGraph;
            [MarshalAs(UnmanagedType.U1)]
            public bool isDijkstra;
            [MarshalAs(UnmanagedType.U1)]
            public bool isUniform;
            public double pruningFactor;

            // Seeds
            [MarshalAs(UnmanagedType.U1)]
            public bool forceSeeds;
            public int validSeed1;
            public int validSeed2;
            public int trainSeed1;
            public int trainSeed2;
            [MarshalAs(UnmanagedType.U1)]
            public bool resizeSetsWhenResampling;
            public int resamplingMaxValSize;
            public int resamplingMaxTrainSize1;
            public int resamplingMaxTrainSize2;

            // Extractor parameters;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string extractorType;
            public int p;
            public int q;
            public int r;
            public int rt;
            public int w;
            public int d;
            public int t;
            public int s;
            public int ps;
            public int b;
            public int nx;
            public int ny;

            // Resampling
            public int repetitionPerImage;
            public int resScales;
            public int minWindow;
            public double jumpingFactor;
            public double scaleFactor;

            // Boosting parameters	
            public int boostingStages;
            public int realBoostBins;
            [MarshalAs(UnmanagedType.U1)]
            public bool useWeightTrimming;
            public double weightTrimmingThreshold;
            public double weightTrimmingMinSamples;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string classifierType;

            // Classifier parameters
            public int maxIterations;
            public double learningRate;
            public int maxTreeLevel;
            public int treeBins;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string impurityMetric;

            // Other
            public double outlayerPercent;
        };

        [StructLayout(LayoutKind.Sequential)]
        internal struct DetectionParameters
        {
            public int windowMinimalWidth;
            public int windowMinimalHeight;
            public int scales;
            public double windowScalingRatio;
            public double windowJumpingRatio;
        };
    }
}
