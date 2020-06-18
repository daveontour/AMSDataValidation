using System;
using System.Collections.Specialized;
using System.Configuration;

namespace AMSDataValidation
{

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("=== AMS Base Data Validation Utility ===");
            Console.WriteLine("========================================\n");
            CheckAMSData check = null;

            if (args.Length == 1)
            {
                if (args[0] == "/?")
                {
                    ShowHelp();
                }
            }

            if (args.Length == 3)
            {
                foreach (string arg in args)
                {
                    Console.WriteLine(arg);
                }

                check = new CheckAMSData(args[0], args[1], args[2], ",", "Rules.config");

            }
            else
            {
                NameValueCollection appSettings = ConfigurationManager.AppSettings;
                string token = string.IsNullOrEmpty(appSettings["token"]) ? null : appSettings["token"];
                string uri = string.IsNullOrEmpty(appSettings["uri"]) ? null : appSettings["uri"];
                string err = string.IsNullOrEmpty(appSettings["errorOnly"]) ? "true" : appSettings["errorOnly"];
                string del = string.IsNullOrEmpty(appSettings["delimiter"]) ? "," : appSettings["delimiter"];
                string rules = string.IsNullOrEmpty(appSettings["rules"]) ? "Rules.config" : appSettings["rules"];

                if (token == null || uri == null)
                {
                    Console.WriteLine($"Parameters to access AMS are not configured");
                    Console.WriteLine("\nHit Any Key to Exit..");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"URI of AMS to be checked: {uri}");
                Console.WriteLine($"AMS Access Token: {token}");
                Console.WriteLine($"Validation Rules File: {rules}\n");

                check = new CheckAMSData(token, uri, err, del, rules);
            }
            check?.Start();
        }

        private static void ShowHelp()
        {
            throw new NotImplementedException();
        }
    }
}
