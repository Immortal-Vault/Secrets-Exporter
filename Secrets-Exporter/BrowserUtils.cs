using System.Diagnostics;
using System.Net;
using System.Text;

namespace Secrets_Exporter;

public static class BrowserUtils
{
    public static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to open browser: " + ex);
        }
    }
    
    public static async Task<string?> StartLocalServer(string expectedState)
    {
        var port = PortUtils.GetFirstAvailablePort(3000, 3100);

        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/oauth2callback/");
        listener.Start();

        try
        {
            var context = await listener.GetContextAsync();
            var request = context.Request;
            var response = context.Response;

            var query = request.QueryString;
            var code = query["code"];
            var state = query["state"];

            if (state != expectedState)
            {
                Console.WriteLine("State mismatch!");
                return null;
            }

            const string responseString = """
                                          <html>
                                          <head>
                                              <style>
                                                  body {
                                                      background-color: #121212;
                                                      color: #ffffff;
                                                      font-family: Arial, sans-serif;
                                                      display: flex;
                                                      align-items: center;
                                                      justify-content: center;
                                                      height: 100vh;
                                                      margin: 0;
                                                  }
                                              </style>
                                          </head>
                                          <body>
                                              <p>Authentication successful! You can close this tab.</p>
                                          </body>
                                          </html>
                                          """;
            
            var buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            var output = response.OutputStream;
            await output.WriteAsync(buffer, 0, buffer.Length);
            output.Close();

            return code;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error in local server: " + ex.Message);
            return null;
        }
        finally
        {
            listener.Stop();
        }
    }
}