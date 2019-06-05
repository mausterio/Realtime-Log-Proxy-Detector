using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Net;
using System.Configuration;
using Newtonsoft.Json.Linq;

namespace Realtime_Log_Proxy_Detector
{
    class Program
    {
        static string proxy_checker(string s)
        {
            using (var client = new WebClient())
            {
                try
                {
                    // TODO: Add multiple API's so that if one goes down or rate limit hit, program is still functional.
                    var json = client.DownloadString(ConfigurationManager.AppSettings["API_URL"] + s);

                    if (ConfigurationManager.AppSettings["Verbose"] == "true")
                    {
                        Console.WriteLine(json);
                    }

                    dynamic data = JObject.Parse(json);
                    // Returns whether using vpn or not
                    // TODO: Find a way of allowing the configuration of data.[API_FIELD]
                    return data.vpn_or_proxy;  // vpn? yes / no
                }
                catch (Exception)
                {
                    Console.WriteLine("Error: API is not working or is not responding!");
                    return "no";
                }
            }
        }
        static void Main(string[] args)
        {
            string log_location = ConfigurationManager.AppSettings["LogLocation"];
            string output_location = ConfigurationManager.AppSettings["OutputLocation"];
            // TODO: Merge this down to one line
            string regex = ConfigurationManager.AppSettings["Regex_Prefix"] + ConfigurationManager.AppSettings["Regex_IP"] + ConfigurationManager.AppSettings["Regex_Postfix"];
            string remove1 = @ConfigurationManager.AppSettings["Regex_Prefix"];
            string remove2 = @ConfigurationManager.AppSettings["Regex_Postfix"];

            var wh = new AutoResetEvent(false);
            var fsw = new FileSystemWatcher(".");
            fsw.Filter = log_location;
            fsw.EnableRaisingEvents = true;
            fsw.Changed += (s, e) => wh.Set();

            var fs = new FileStream(log_location, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using (var sr = new StreamReader(fs))
            {
                var s = "";
                while (true)
                {
                    s = sr.ReadLine();
                    if (s != null)
                    {
                        // Match format: Prefix + Regex + Postfix
                        Match match = Regex.Match(s, regex);
                        if (match.Success)
                        {
                            s = match.Value;
                            if (ConfigurationManager.AppSettings["Verbose"] == "true")
                            {
                                Console.WriteLine(s);
                            }

                            // Removes the regex prefix before sending IP to API
                            if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["Regex_Prefix"])) {
                                try {
                                    s = Regex.Replace(s, ConfigurationManager.AppSettings["Regex_Prefix"], "");
                                } catch (Exception) {
                                    Console.WriteLine("Error: Unable to remove regex prefix!");
                                }
                            }

                            // Removes the regex postfix before sending IP to API
                            if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["Regex_Postfix"]))
                            {
                                try {
                                    s = Regex.Replace(s, ConfigurationManager.AppSettings["Regex_Postfix"], "");
                                } catch (Exception) {
                                    Console.WriteLine("Error: Unable to remove regex postfix!");
                                }
                            }

                            if (ConfigurationManager.AppSettings["Verbose"] == "true")
                            {
                                Console.WriteLine(s);
                            }
                            
                            if (proxy_checker(s) == ConfigurationManager.AppSettings["API_VALUE"])
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(s + " is likely a Proxy or VPN!");
                                Console.ResetColor();

                                // Outputs possible VPN IP addresses to vpn.txt so that they can be reviewed.
                                try {
                                    File.AppendAllText(output_location, (s + Environment.NewLine));
                                } catch (Exception)
                                {
                                    Console.WriteLine("Error: Can write to output location!");
                                }

                            }

                            else if (ConfigurationManager.AppSettings["Verbose"] == "true")
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine(s + " is likely not a Proxy or VPN!");
                                Console.ResetColor();
                            }
                        }

                    }
                    else {
                        wh.WaitOne(1000);
                    }
                }
            }
        }

    }
}
