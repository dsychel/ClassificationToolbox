using System.Windows;
using System.Windows.Threading;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (e.Exception is System.Runtime.InteropServices.COMException comException && comException.ErrorCode == -2147221040)
                e.Handled = true;
        }
    }
}
