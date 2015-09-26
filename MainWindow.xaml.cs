using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using EDSDKLib;

namespace EVNTR
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SDKHandler CameraHandler;
        List<Camera> CamList;
        Action<BitmapImage> SetImageAction;
        ImageBrush bgbrush = new ImageBrush();
        private Button btnStartCapture;
        private Grid gridEmailGroup;

        int ErrCount;
        object ErrLock = new object();

        public MainWindow()
        {
            InitializeComponent();

            btnStartCapture = StartCapture;
            toggleCaptureBtn(true);

            gridEmailGroup = EmailGrouping;
            toggleEmailGrouping(false);

            Closing += new CancelEventHandler(this.killLiveViewOnClose);
        }

        private void toggleCaptureBtn(bool state)
        {
            if (state == true)
            btnStartCapture.Visibility = Visibility.Visible;
            else
            btnStartCapture.Visibility = Visibility.Collapsed;
        }

        private void toggleEmailGrouping(bool state)
        {
            if (state == true)
            gridEmailGroup.Visibility = Visibility.Visible;
            else
            gridEmailGroup.Visibility = Visibility.Collapsed;
        }

        private void killLiveViewOnClose(object sender, CancelEventArgs e)
        {
            if (CameraHandler.IsLiveViewOn)
                CameraHandler.StopLiveView();
        }

        private void keyTakePhotoAction(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                Console.WriteLine("Spacebar");
                // Write Image Taking / Saving logic here.
                CameraHandler.TakePhoto();
                //CameraHandler.StopLiveView();
                //CameraHandler.StartLiveView();
                toggleEmailGrouping(true);
            }
        }

        private void StopLiveViewCapture(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CameraHandler.StopLiveView();
                toggleCaptureBtn(true);
            }
        }

        private void btnStartLiveViewCapture(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Ahh you hit me!");
            toggleCaptureBtn(false);

            try
            {
                CameraHandler = new SDKHandler();
                //CameraHandler.SetSetting(EDSDK.PropID_SaveTo, (uint)EDSDK.EdsSaveTo.Both);
                //CameraHandler.CameraAdded += new SDKHandler.CameraAddedHandler(SDK_CameraAdded);
                CameraHandler.LiveViewUpdated += new SDKHandler.StreamUpdate(SDK_LiveViewUpdated);
                CamList = CameraHandler.GetCameraList();
                CameraHandler.OpenSession(CamList[0]);
                CameraHandler.SetSetting(EDSDK.PropID_SaveTo, (uint)EDSDK.EdsSaveTo.Both);
                CameraHandler.SetCapacity();
                Console.WriteLine(Environment.UserName);
                CameraHandler.ImageSaveDirectory = "C:\\Users\\" + Environment.UserName + "\\Desktop";
                SetImageAction = (BitmapImage img) => { bgbrush.ImageSource = img; };
            }
            catch (DllNotFoundException) { ReportError("Canon DLLs not found!", true); }
            catch (Exception ex) { ReportError(ex.Message, true); }

            LiveViewController();

        }

        private void LiveViewController()
        {
            if (!CameraHandler.IsLiveViewOn)
            {
                LVCanvas.Background = bgbrush;
                CameraHandler.StartLiveView();
                this.PreviewKeyDown += keyTakePhotoAction;
            }
            else
            {
                CameraHandler.StopLiveView();
                LVCanvas.Background = Brushes.LightGray;
            }
        }

        private void SDK_LiveViewUpdated(Stream img)
        {
            try
            {
                if (CameraHandler.IsLiveViewOn)
                {
                    using (WrappingStream s = new WrappingStream(img))
                    {
                        img.Position = 0;
                        BitmapImage EvfImage = new BitmapImage();
                        EvfImage.BeginInit();
                        EvfImage.StreamSource = s;
                        EvfImage.CacheOption = BitmapCacheOption.OnLoad;
                        EvfImage.EndInit();
                        EvfImage.Freeze();
                        Application.Current.Dispatcher.Invoke(SetImageAction, EvfImage);
                    }
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void ReportError(string message, bool lockdown)
        {
            int errc;
            lock (ErrLock) { errc = ++ErrCount; }

            if (errc < 4) MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            else if (errc == 4) MessageBox.Show("Many errors happened!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            lock (ErrLock) { ErrCount--; }
        }

        private void textBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
