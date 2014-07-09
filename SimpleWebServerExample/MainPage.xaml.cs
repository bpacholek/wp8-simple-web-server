using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using SimpleWebServerExample.Resources;
using System.Text.RegularExpressions;
using IDCT;
using System.IO;
using System.IO.IsolatedStorage;


namespace SimpleWebServerExample
{
    public partial class MainPage : PhoneApplicationPage
    {
        IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication();
        WebServer simpleWebServer;        

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            // Sample code to localize the ApplicationBar
            //BuildLocalizedApplicationBar();
            createTestFileIfNotExist();
        }

        private void createTestFileIfNotExist()
        {
            if (isf.FileExists("test_file.txt")) return;
            StreamWriter writer = new StreamWriter( isf.CreateFile("test_file.txt") );
            writer.WriteLine("Device Name: " + Microsoft.Phone.Info.DeviceStatus.DeviceName);
            writer.Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            bool found = false;
            string ip = "";

            //get all network adapters
            var adapters = Windows.Networking.Connectivity.NetworkInformation.GetHostNames();
            if (adapters.Count == 0)
            {
                MessageBox.Show("Turn on wifi");
            }
            else
            {
                foreach (var adapter in adapters)
                {
                    //find the Wifi adapter (interface type == 71)
                    if (adapter.IPInformation.NetworkAdapter.IanaInterfaceType == 71 && (adapter.Type == Windows.Networking.HostNameType.Ipv4 || adapter.Type == Windows.Networking.HostNameType.Ipv6))
                    {
                        //if found assign it's ip to a variable
                        found = true;
                        ip = adapter.RawName;
                        break;
                    }
                }

                if (found == true)
                {
                    //create a new dictionary for server rules
                    Dictionary<Regex, RuleDeletage> rules = new Dictionary<Regex, RuleDeletage>();

                    //add a rule for homepage: url /
                    //will fire method "homePage" when triggered
                    Regex rgx = new Regex("^/$");
                    rules.Add(rgx, homePage);

                    //add a rule for file any url under subfolder /files 
                    //this is a file download example
                    Regex rgx_file = new Regex("^/files/.*$");
                    rules.Add(rgx_file, getfile);

                    //with the set of rules and IP of the network adapter create a new web server object and assign an error event method
                    simpleWebServer = new WebServer(rules, ip, "80");
                    simpleWebServer.errorOccured += ws_errorOccured;

                    ((Button)sender).IsEnabled = false;
                    ipinfo.Text = "With another device in the network\r\ngo to:\r\nhttp://" + ip + "/";
                }
                else
                {
                    MessageBox.Show("Turn on wifi");
                }
            }
        }

        webResposne homePage(webResposne request)
        {
            //prepare the response object
            webResposne newResponse = new webResposne();

            //create a new dictionary for headers - this could be done using a more advanced class for webResponse object - i just used a simple struct
            newResponse.header = new Dictionary<string, string>();

            //add content type header
            newResponse.header.Add("Content-Type", "text/html");

            Stream resposneText = new MemoryStream();
            StreamWriter contentWriter = new StreamWriter(resposneText);
            contentWriter.WriteLine("<html>");
            contentWriter.WriteLine("   <head>");
            contentWriter.WriteLine("       <title>Sample response</title>");
            contentWriter.WriteLine("   </head>");
            contentWriter.WriteLine("   <body>");
            contentWriter.WriteLine("   <p>Phone info:</p>");
            contentWriter.WriteLine("   <p><b>Platform: </b>" + Environment.OSVersion.Platform + "</p>");
            contentWriter.WriteLine("   <p><b>Device Name: </b>" + Microsoft.Phone.Info.DeviceStatus.DeviceName + "</p>");
            contentWriter.WriteLine("   <p>Download file</p>");
            contentWriter.WriteLine("   <p><a href='/files/test_file.txt'>get file</a></p>");
            contentWriter.WriteLine("   </body>");
            contentWriter.WriteLine("</html>");
            contentWriter.Flush();
            //assign the response
            newResponse.content = resposneText;

            //return the response
            return newResponse;
        }

        webResposne getfile(webResposne request)
        {
            webResposne newResponse = new webResposne();

            newResponse.header = new Dictionary<string, string>();
            Stream resposneText = new MemoryStream();

            //get the file name from the uri by removing the prefix
            string filename = request.uri.Replace("/files/", "");

            
            if (isf.FileExists("/" + filename))
            {
                //set thea content type header
                newResponse.header.Add("Content-Type", "binary/octet-stream");

                //inform that it is going to be a download with a filename as in the request
                newResponse.header.Add("Content-Disposition", "attachment; filename=" + filename);

                //assign the file to the request
                resposneText = isf.OpenFile("/" + filename, FileMode.Open);
            }
            else
            {
                //file not found
                newResponse.header.Add("Content-Type", "text/html");
                                
                StreamWriter contentWriter = new StreamWriter(resposneText);
                contentWriter.WriteLine("<html>");
                contentWriter.WriteLine("   <head>");
                contentWriter.WriteLine("       <title>Not found</title>");
                contentWriter.WriteLine("   </head>");
                contentWriter.WriteLine("   <body>");
                contentWriter.WriteLine("   <p>File not found</p>");
                contentWriter.WriteLine("   </body>");
                contentWriter.WriteLine("</html>");
                contentWriter.Flush();
                //assign the response
                newResponse.content = resposneText;
            }

            newResponse.content = resposneText;

            return newResponse;
        }

        void ws_errorOccured(int code, string message)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                MessageBox.Show("Error: " + message);
            });
        }

        // Sample code for building a localized ApplicationBar
        //private void BuildLocalizedApplicationBar()
        //{
        //    // Set the page's ApplicationBar to a new instance of ApplicationBar.
        //    ApplicationBar = new ApplicationBar();

        //    // Create a new button and set the text value to the localized string from AppResources.
        //    ApplicationBarIconButton appBarButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.add.rest.png", UriKind.Relative));
        //    appBarButton.Text = AppResources.AppBarButtonText;
        //    ApplicationBar.Buttons.Add(appBarButton);

        //    // Create a new menu item with the localized string from AppResources.
        //    ApplicationBarMenuItem appBarMenuItem = new ApplicationBarMenuItem(AppResources.AppBarMenuItemText);
        //    ApplicationBar.MenuItems.Add(appBarMenuItem);
        //}
    }
}