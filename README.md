wp8-simple-web-server
=====================

Simple Web Server for Windows Phone 8 capable of handling GET requests and responses. Supports also file downloads. Useful for sharing phone files over WiFi.

Consists two projects:

- SimpleWebServer: the main library
- SImpleWebServerExample: Windows Phone 8 sample application which uses the library

Quite well documented within the code.

This web server is still a stub, yet allows basic operation. Used in **AuRec** application:
http://windowsphone.com/s?appId=c82fafe9-46e9-4aab-9c3a-cb06aaad9dd1

Usage
=====

Add a reference to the library and use the namespace **IDCT**.

Assign new server object using:

    WebServer myWebServer = new WebServer(rules, ip, port); 
    
where **ip** and **port** are listening bindings and **rules** is a dictionary of URL rules to match with delegate methods to be triggered when a rule is matched.

For example:
If you add a rule:

    Regex rgx_file = new Regex("^/files/.*$");
    rules.Add(rgx_file, getfile);
    
then if user will enter any path starting with:
**http://your_ip/files/**
your webserver will call the method **getfile** (created by you) to generate the output.

In the example you can find samples for a dynamic html page and file download.