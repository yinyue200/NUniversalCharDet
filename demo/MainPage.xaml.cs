using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Mozilla.NUniversalCharDet;
using System.Net;
using Windows.UI.Popups;

//“空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 上有介绍

namespace demo
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }


        private async void button_Click(object sender, RoutedEventArgs e)
        {
            CharSetBox.Text = "";
            PageBox.Text = "";
            button.IsEnabled = false;
            try
            {
                HttpWebRequest hwr = (HttpWebRequest)HttpWebRequest.Create(UrlBox.Text);
                HttpWebResponse res;
                try
                {
                    res = (HttpWebResponse)await hwr.GetResponseAsync();
                }
                catch
                {
                    CharSetBox.Text = "网页获取错误！";
                    return;
                }

                if (res.StatusCode == HttpStatusCode.OK)
                {
                    Stream mystream = res.GetResponseStream();
                    MemoryStream msTemp = new MemoryStream();
                    int len = 0;
                    byte[] buff = new byte[512];

                    while ((len = mystream.Read(buff, 0, 512)) > 0)
                    {
                        msTemp.Write(buff, 0, len);

                    }
                    res.Dispose();

                    if (msTemp.Length > 0)
                    {
                        msTemp.Seek(0, SeekOrigin.Begin);
                        byte[] PageBytes = new byte[msTemp.Length];
                        msTemp.Read(PageBytes, 0, PageBytes.Length);

                        msTemp.Seek(0, SeekOrigin.Begin);
                        int DetLen = 0;
                        byte[] DetectBuff = new byte[4096];
                        UniversalDetector Det = new UniversalDetector(null);
                        while ((DetLen = msTemp.Read(DetectBuff, 0, DetectBuff.Length)) > 0 && !Det.IsDone())
                        {
                            Det.HandleData(DetectBuff, 0, DetectBuff.Length);
                        }
                        Det.DataEnd();
                        if (Det.GetDetectedCharset() != null)
                        {
                            CharSetBox.Text = "OK! CharSet=" + Det.GetDetectedCharset();
                            string page = System.Text.Encoding.GetEncoding(Det.GetDetectedCharset()).GetString(PageBytes);
                            if(page.Length >2000)
                            {
                                page = page.Substring(0,2000);
                            }
                            PageBox.Text = page;
                        }
                    }


                }
            }
            catch
            {

            }
            finally
            {
                button.IsEnabled = true;
            }
        }
    }
}
