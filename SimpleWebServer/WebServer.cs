using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
//added by IDCT
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using Windows.Networking.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace IDCT
{
    //method to be called when url rule is met
    public delegate webResposne RuleDeletage(webResposne response);

    //delegate type for the error event
    public delegate void ErrorOccured(int code, string message);

    public struct webResposne
    {
        public Dictionary<string, string> header;
        public string method;
        public string uri;
        public Stream content;
    }

    public class WebServer
    {
        /// <summary>
        /// isolated storage instance as server needs to save temp files
        /// </summary>
        IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication();

        /// <summary>
        /// indicates if server should be listening
        /// </summary>
        protected bool isListening = false;

        /// <summary>
        /// random number generator
        /// </summary>
        protected Random rnd = new Random();

        /// <summary>
        /// event fired when an error occured
        /// </summary>
        public event ErrorOccured errorOccured;

        /// <summary>
        /// socket listener - the main IO part
        /// </summary>
        StreamSocketListener listener = new StreamSocketListener();

        /// <summary>
        /// rules: Url Regex => method to be called when rule is met
        /// </summary>
        private Dictionary<Regex, RuleDeletage> serverRules;

        /// <summary>
        /// initializes the new server object and puts it in listening mode
        /// </summary>
        /// <param name="rules">set of rules in a form "Url Regex" => "Method to be fired when rule is met"</param>
        /// <param name="ip">ip to bind to</param>
        /// <param name="port">port to bind to</param>
        public WebServer(Dictionary<Regex, RuleDeletage> rules, string ip, string port)
        {
            //assign passed rules to the server
            serverRules = rules;
            try
            {
                //try to turn on the server
                Task.Run(async () =>
                {
                    //start listening
                    listener.ConnectionReceived += listener_ConnectionReceived;

                    //bing do ip:port
                    await listener.BindEndpointAsync(new Windows.Networking.HostName(ip), port);
                    
                    isListening = true;
                });
            }
            catch (Exception ex)
            {
                //if possible fire the error event with the exception message
                if (errorOccured != null)
                {
                    errorOccured(-1, ex.Message);
                }
            }

        }

        void listener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            //if we should not be listening anymore, yet for some reason request was still parsed (phone not yet closed the socket) exit the method as it may be unwanted by the user for anybody to read any data
            if (isListening == false)
            {
                return;
            }

            //write request to a temporary file (better than memory in case of future handling of big post requests
            string filename = "/temp_" + rnd.Next(100, 999).ToString() + rnd.Next(1000, 9999).ToString();

            //get the request socket
            StreamSocket sck = args.Socket;

            //create a new file based on the temp file name
            IsolatedStorageFileStream plik = isf.CreateFile(filename);

            //create a new stream reader object
            DataReader read = new DataReader(sck.InputStream);
            read.InputStreamOptions = InputStreamOptions.Partial;

            //defines if we should stop reading
            bool finished = false;

            //small buffer will make requests long, too big may cut connections (probably something missing in the webserver)
            uint maxbuf = 4096;

            //create new task
            Task.Run(async () =>
            {
                //yeah yeah I know it is bad, yet does it's job :)
                while (true)
                {
                    //wait for full buffer
                    await read.LoadAsync(maxbuf);

                    //if there is any data in the buffer
                    if (read.UnconsumedBufferLength > 0)
                    {
                        //create a new byte buffer (for our internal use) long as the data in the socket buffer
                        uint len = read.UnconsumedBufferLength;
                        byte[] buffer = new byte[read.UnconsumedBufferLength];

                        //read the data from the socket to the internal buffer
                        read.ReadBytes(buffer);

                        //write the new data to the temporary file
                        plik.Write(buffer, 0, buffer.Length);

                        //if buffer was not filled it means that we got the last packet
                        if (len < maxbuf)
                        {
                            finished = true;
                        }
                    }
                    else
                    {
                        //no data in the buffer: finished
                        finished = true;
                    }
                    
                    //if we finished...
                    if (finished == true)
                    {
                        //close the temporary file
                        plik.Close();

                        //pass it for further parsing
                        parseRequest(filename, sck);

                        //break the loop
                        break;
                    }
                } //Loop
            });//Task
        }

        /// <summary>
        /// method which parses the request and decides about the action
        /// </summary>
        /// <param name="request_file">path to the temporary file holding the request packet</param>
        /// <param name="socket">socket used with the request (required for response)</param>
        public void parseRequest(string request_file, StreamSocket socket)
        {
            //open and read the request: needs stream reading support for large files
            IsolatedStorageFileStream plika = isf.OpenFile(request_file, FileMode.Open);
            StreamReader reada = new StreamReader(plika);

            //read the first ling to get request type
            string linge = reada.ReadLine();

            //create new request object (same type as response object - possibly better class naming needs to be used)
            webResposne request = new webResposne();

            //if there is any data...
            if (linge != null && linge.Length > 0)
            {
                //get the method (currently GET only supported really) and the request URL
                if (linge.Substring(0, 3) == "GET")
                {
                    request.method = "GET";
                    request.uri = linge.Substring(4);
                }
                else if (linge.Substring(0, 4) == "POST")
                {
                    request.method = "POST";
                    request.uri = linge.Substring(5);
                }
                
                //remove the HTTP version
                request.uri = Regex.Replace(request.uri, " HTTP.*$", "");

                //create a dictionary for the sent headers
                Dictionary<string, string> headers = new Dictionary<string, string>();

                //read HTTP headers into the dictionary
                string line = "";
                do
                {
                    line = reada.ReadLine();
                    string[] sepa = new string[1];
                    sepa[0] = ":";
                    string[] elems = line.Split(sepa, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (elems.Length > 0)
                    {
                        headers.Add(elems[0], elems[1]);
                    }
                } while (line.Length > 0);

                //assign headers to the request object
                request.header = headers;

                //assigne rest of the content to the request object in a form of a stream handle moved to the part with content after previous read line operations
                request.content = reada.BaseStream;

                //determines if we found a matching URL rule
                bool foundrule = false;
                
                //create a stream writer to the output stream (response)
                DataWriter writ = new DataWriter(socket.OutputStream);

                //if there are any server rules
                if (serverRules != null)
                {
                    //for every rule...
                    foreach (Regex rule_part in serverRules.Keys)
                    {
                        //if it matches the URL
                        if (rule_part.IsMatch(request.uri))
                        {
                            //create a new response object
                            //assign to it response from the method called as a delegate assigned to the URL rule
                            webResposne toSend = serverRules[rule_part](request);

                            //mark that we found a rule
                            foundrule = true;

                            //if the rule is meant to redirect...
                            if (toSend.header.ContainsKey("Location"))
                            {
                                writ.WriteString("HTTP/1.1 302\r\n");
                            }
                            else //if a normal read operation
                            {
                                writ.WriteString("HTTP/1.1 200 OK\r\n");
                            }
                            
                            //write content length to the buffer
                            writ.WriteString("Content-Length: " + toSend.content.Length + "\r\n");

                            //for each of the response headers (returned by the delegate assigned to the URL rule
                            foreach (string key in toSend.header.Keys)
                            {
                                //write it to the output
                                writ.WriteString(key + ": " + toSend.header[key] + "\r\n");
                            }
                            //add connection: close header
                            writ.WriteString("Connection: close\r\n");

                            //new line before writing content
                            writ.WriteString("\r\n");
  
                            //we lock the webserver until response is finished
                            lock (this)
                            {
                                //reset the output stream
                                toSend.content.Seek(0, SeekOrigin.Begin);
                                Task.Run(async () =>
                                {
                                    await writ.StoreAsync(); //wait for the data to be saved in the output
                                    await writ.FlushAsync(); //flush (send to the output)

                                    //write the data to the output using 1024 buffer (store and flush after every loop)
                                    while (toSend.content.Position < toSend.content.Length)
                                    {
                                        byte[] buffer;
                                        if (toSend.content.Length - toSend.content.Position < 1024)
                                        {
                                            buffer = new byte[toSend.content.Length - toSend.content.Position];
                                        }
                                        else
                                        {
                                            buffer = new byte[1024];
                                        }
                                        toSend.content.Read(buffer, 0, buffer.Length);
                                        writ.WriteBytes(buffer);
                                        
                                        await writ.StoreAsync();
                                        await writ.FlushAsync();
                                    }
                                    toSend.content.Close();
                                });
                            }
                            break;
                        }
                    }
                }
                                
                if (foundrule == false)
                {
                    writ.WriteString("HTTP/1.1 404 Not Found\r\n");
                    writ.WriteString("Content-Type: text/html\r\n");
                    writ.WriteString("Content-Length: 9\r\n");
                    writ.WriteString("Pragma: no-cache\r\n");
                    writ.WriteString("Connection: close\r\n");
                    writ.WriteString("\r\n");
                    writ.WriteString("Not found");
                    Task.Run(async () =>
                    {
                        await writ.StoreAsync();
                    });
                }
            }
        }
    }
}
