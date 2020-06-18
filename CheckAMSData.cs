using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace AMSDataValidation
{
    public enum BaseType
    {
        Aircraft,
        Airline,
        Airport,
        AircraftType,
        None
    }
    public class Rule
    {
        public BaseType type;
        public string regex;
        public string xpath;
        public string message;
        public bool valid = true;

        public Rule(string[] entries)
        {
            try
            {
                switch (entries[0])
                {
                    case "Aircraft":
                        type = BaseType.Aircraft;
                        break;
                    case "AircraftType":
                        type = BaseType.AircraftType;
                        break;
                    case "Airline":
                        type = BaseType.Airline;
                        break;
                    case "Airport":
                        type = BaseType.Airport;
                        break;
                    default:
                        type = BaseType.None;
                        break;
                }

                xpath = entries[1];
                regex = entries[2];
                message = entries[3];
            }
            catch (Exception)
            {
                valid = false;
            }
        }
    }
    class CheckAMSData
    {
        private readonly List<Rule> ruleList = new List<Rule>();
        private string GETAIRPORTSTemplate = @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ams6=""http://www.sita.aero/ams6-xml-api-webservice"">
<soapenv:Header/>
   <soapenv:Body>
      <ams6:GetAirports>
         <ams6:sessionToken>@token</ams6:sessionToken>
      </ams6:GetAirports>
   </soapenv:Body>
</soapenv:Envelope>";
        private string GETAIRLINESTemplate = @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ams6=""http://www.sita.aero/ams6-xml-api-webservice"">
<soapenv:Header/>
   <soapenv:Body>
      <ams6:GetAirlines>
         <ams6:sessionToken>@token</ams6:sessionToken>
      </ams6:GetAirlines>
   </soapenv:Body>
</soapenv:Envelope>";
        private string GETAIRCRAFTSTemplate = @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ams6=""http://www.sita.aero/ams6-xml-api-webservice"">
<soapenv:Header/>
   <soapenv:Body>
      <ams6:GetAircrafts>
         <ams6:sessionToken>@token</ams6:sessionToken>
      </ams6:GetAircrafts>
   </soapenv:Body>
</soapenv:Envelope>";
        private string GETAIRCRAFTTYPESSTemplate = @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ams6=""http://www.sita.aero/ams6-xml-api-webservice"">
<soapenv:Header/>
   <soapenv:Body>
      <ams6:GetAircraftTypes>
         <ams6:sessionToken>@token</ams6:sessionToken>
      </ams6:GetAircraftTypes>
   </soapenv:Body>
</soapenv:Envelope>";

        private readonly string token;
        private readonly string amshost;
        private bool errorOnly = true;
        private string delimiter;
        private string rulesFile;

        private bool hasAirline = false;
        private bool hasAirport = false;
        private bool hasAircraft = false;
        private bool hasAircraftType = false;
        public CheckAMSData(string token, string amshost, string err, string delimiter, string rules)
        {
            this.token = token;
            this.amshost = amshost;
            this.delimiter = delimiter;
            this.rulesFile = rules;
            try
            {
                errorOnly = bool.Parse(err);
            }
            catch (Exception)
            {
                errorOnly = true;
            }
        }


        public void Start()
        {
            ReadRules();
            if (this.ruleList.Count == 0)
            {
                return;
            }

            Task checkTask = Task.Run(() => Execute(token, amshost));
            checkTask.Wait();
        }

        public async Task Execute(string token, string amshost)
        {
            Console.WriteLine("\n======> Checking AMS Access <==========");
            bool amsOK = await CheckAMS();
            if (!amsOK)
            {
                Console.WriteLine("======> Error: Cannot access AMS <==========");
                Console.WriteLine("\nHit Any Key to Exit..");
                Console.ReadKey();
                return;
            }
            else
            {
                Console.WriteLine("======> AMS Access Confirmed <==========");
            }

            try
            {
                if (hasAirport)
                {
                    Console.WriteLine("\n======> Checking Airports <==========");
                    XmlElement airports = await GetXML(GETAIRPORTSTemplate, token, "http://www.sita.aero/ams6-xml-api-webservice/IAMSIntegrationService/GetAirports", amshost);
                    Check(airports, "//ams:Airport", "./ams:AirportState/ams:Value[@propertyName='Name']", BaseType.Airport);
                }

                if (hasAirline)
                {
                    Console.WriteLine("\n======> Checking Airlines <==========");
                    XmlElement airlines = await GetXML(GETAIRLINESTemplate, token, "http://www.sita.aero/ams6-xml-api-webservice/IAMSIntegrationService/GetAirlines", amshost);
                    Check(airlines, "//ams:Airline", "./ams:AirlineState/ams:Value[@propertyName='Name']", BaseType.Airline);
                }

                if (hasAircraft)
                {
                    Console.WriteLine("\n======> Checking Aircraft <==========");
                    XmlElement aircrafts = await GetXML(GETAIRCRAFTSTemplate, token, "http://www.sita.aero/ams6-xml-api-webservice/IAMSIntegrationService/GetAircrafts", amshost);
                    Check(aircrafts, "//ams:Aircraft", "./ams:AircraftId/ams:Registration", BaseType.Aircraft);
                }

                if (hasAircraftType)
                {
                    Console.WriteLine("\n======> Checking Aircraft Types <==========");
                    XmlElement actypes = await GetXML(GETAIRCRAFTTYPESSTemplate, token, "http://www.sita.aero/ams6-xml-api-webservice/IAMSIntegrationService/GetAircraftTypes", amshost);
                    Check(actypes, "//ams:AircraftType", "./ams:AircraftTypeState/ams:Value[@propertyName='Name']", BaseType.AircraftType);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("\nHit Any Key to Exit..");
                Console.ReadKey();
            }

            Console.WriteLine("\nHit Any Key to Exit..");
            Console.ReadKey();
        }


        public void ReadRules()
        {
            try
            {
                using (TextFieldParser parser = new TextFieldParser(rulesFile))
                {
                    parser.TextFieldType = FieldType.Delimited;
                    parser.SetDelimiters(delimiter);

                    Console.WriteLine("Validation Rules:");
                    while (!parser.EndOfData)
                    {
                        string[] fields = parser.ReadFields();
                        if (fields[0].StartsWith("#") || fields[0].StartsWith(" "))
                        {
                            continue;
                        }
                        if (fields[0] == "Airline")
                        {
                            hasAirline = true;
                        }
                        if (fields[0] == "Aircraft")
                        {
                            hasAircraft = true;
                        }
                        if (fields[0] == "AircraftType")
                        {
                            hasAircraftType = true;
                        }
                        if (fields[0] == "Airport")
                        {
                            hasAirport = true;
                        }
                        this.ruleList.Add(new Rule(fields));
                        Console.WriteLine($"Base Data Type = {fields[0]}, XPath = {fields[1]}, Regex = {fields[2]}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not read rules file ({rulesFile}). {ex.Message}");
                Console.WriteLine("\nHit Any Key to Exit..");
                Console.ReadKey();
                return;
            }
        }

        public async Task<bool> CheckAMS()
        {
            string query = @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ams6=""http://www.sita.aero/ams6-xml-api-webservice"">
   <soapenv:Header/>
   <soapenv:Body>
      <ams6:GetAvailableHomeAirportsForLogin>
         <!--Optional:-->
         <ams6:token>@token</ams6:token>
      </ams6:GetAvailableHomeAirportsForLogin>
   </soapenv:Body>
</soapenv:Envelope>";

            query = query.Replace("@token", token);

            try
            {
                using (var client = new HttpClient())
                {
                    HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, amshost)
                    {
                        Content = new StringContent(query, Encoding.UTF8, "text/xml")
                    };
                    requestMessage.Headers.Add("SOAPAction", "http://www.sita.aero/ams6-xml-api-webservice/IAMSIntegrationService/GetAvailableHomeAirportsForLogin");
                    using (HttpResponseMessage response = await client.SendAsync(requestMessage))
                    {
                        if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted || response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.NoContent)
                        {
                            return true;
                        }
                        else
                        {
                            Console.WriteLine($"AMS Access Problem. Retrieval Error: {response.StatusCode}");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public async Task<XmlElement> GetXML(string queryTemplate, string token, string soapAction, string amshost)
        {

            string query = queryTemplate.Replace("@token", token);

            try
            {
                using (var client = new HttpClient())
                {
                    HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, amshost)
                    {
                        Content = new StringContent(query, Encoding.UTF8, "text/xml")
                    };
                    requestMessage.Headers.Add("SOAPAction", soapAction);
                    using (HttpResponseMessage response = await client.SendAsync(requestMessage))
                    {
                        if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted || response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.NoContent)
                        {
                            XmlDocument doc = new XmlDocument();
                            doc.LoadXml(await response.Content.ReadAsStringAsync());
                            return doc.DocumentElement;
                        }
                        else
                        {
                            Console.WriteLine($"XML Retrieval Error: {response.StatusCode}");
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public void Check(XmlElement el, string baseElement, string identifier, BaseType type)
        {
            if (el == null)
            {
                Console.WriteLine($"No elements found");
                Console.WriteLine("\nHit Any Key to Exit..");
                Console.ReadKey();
                return;
            }
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(el.OwnerDocument.NameTable);
            nsmgr.AddNamespace("ams", "http://www.sita.aero/ams6-xml-api-datatypes");

            XmlNodeList baseDataElements = el.SelectNodes(baseElement, nsmgr);

            bool errFound = false;

            foreach (XmlNode baseDataElement in baseDataElements)
            {
                string id = baseDataElement.SelectSingleNode(identifier, nsmgr).InnerText;


                List<string> errors = new List<string>();
                foreach (Rule rule in ruleList)
                {
                    if (rule.valid && rule.type == type)
                    {
                        try
                        {
                            string text;
                            try
                            {
                                text = baseDataElement.SelectSingleNode(rule.xpath, nsmgr).InnerText;

                            }
                            catch (Exception)
                            {
                                errors.Add($"{id}:  Rule Violation ==> {rule.message}. Element Not Found");
                                errFound = true;
                                continue;
                            }
                            Regex rgx = new Regex(rule.regex, RegexOptions.IgnoreCase);
                            MatchCollection matches = rgx.Matches(text);
                            if (matches.Count == 0)
                            {
                                errors.Add($"{id}:  Rule Violation ==> {rule.message}. Element value = {text}");
                                errFound = true;
                            }
                        }
                        catch (Exception)
                        {
                            errors.Add($"  (Processing Error)..{rule.message}");
                            errFound = true;
                        }
                    }
                }
                if (errors.Count > 0)
                {
                    // Console.WriteLine($"{id}");
                    foreach (string err in errors)
                    {
                        Console.WriteLine($"{err}");
                    }
                }
                else
                {
                    if (!errorOnly)
                    {
                        Console.WriteLine($"{id}..OK");
                    }
                }
            }
            if (!errFound)
            {
                Console.WriteLine("No Rule Violations Found");
            }
        }
    }
}
