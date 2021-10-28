using System.Windows;
using System.Windows.Media;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for ImagePreviewer.xaml
    /// </summary>
    public partial class ImagePreviewer : Window
    {
        public ImagePreviewer(ImageSource bmp)
        {
            InitializeComponent();

            imageBox.Source = bmp;
            if (imageBox.Source.Width > imageBox.ActualWidth || imageBox.Source.Height > imageBox.ActualHeight)
                imageBox.Stretch = System.Windows.Media.Stretch.Uniform;
            else
                imageBox.Stretch = System.Windows.Media.Stretch.None;
        }

        private void ImageBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (imageBox.Source.Width > imageBox.ActualWidth || imageBox.Source.Height > imageBox.ActualHeight)
                imageBox.Stretch = System.Windows.Media.Stretch.Uniform;
            else
                imageBox.Stretch = System.Windows.Media.Stretch.None;
        }
    }
}
