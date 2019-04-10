using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using HtmlAgilityPack;
using System.Text;
using System.Threading.Tasks;
using System.Net;


namespace Crawler
{
    class Program
    {
        static void Main(string[] args)
        {
            crewler_s();
        }


        //Crawler Function
        private static void crewler_s()
        {
            
            HtmlWeb hw = new HtmlWeb();
            //Creating new object using HtmlAgility 
            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            //Load html web file to object
            Uri myUri = new Uri("http://www.deadlinkcity.com/");
            string host = myUri.Host;
            doc = hw.Load(myUri.ToString());
            //Creat list contains all the links of the website
            var cbl_items = new List<String>();
            // Run on the website and choose all the href
            foreach (HtmlNode link in doc.DocumentNode.SelectNodes("//a[@href]"))
            {
                // Get the value of the HREF attribute
                string hrefValue = link.GetAttributeValue("href", string.Empty);
                //Condition of link structure
                if (!hrefValue.Contains("http"))
                {
                    var fix = "/" + hrefValue;
                    fix = fix.Replace("//", "/");

                    if (cbl_items.Contains("http://" + host + fix))
                        continue;
                    cbl_items.Add("http://" + host + fix);
                }
                else
                {
                    if (cbl_items.Contains(hrefValue))
                        continue;
                    cbl_items.Add(hrefValue);
                }
            }
            //Creat var for the bad links (using it in the end for the CSV)
            var badLinks = new List<String>();
            // Run on the list of links
            foreach (String element in cbl_items)
                {
                //prints of the Test links
                    Console.WriteLine("Testing:  " +element);
                    HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(element);
                   // webRequest.Timeout = 500;
                    webRequest.AllowAutoRedirect = true;
                try
                {
                    using (HttpWebResponse response = (HttpWebResponse)webRequest.GetResponse())
                    {
                        Console.WriteLine("Good link");
                    }
                }
                catch
                {
                    badLinks.Add(element);
                }   finally
                {
                    webRequest.Abort();
                }
            }
            //Writing CSV file of Bad Links
            string csv = String.Join("\n", badLinks.Select(x => x.ToString()).ToArray());
            //Destination Path of CSV Exporting 
            System.IO.File.WriteAllText(@"C:\Users\Eli\Desktop\Crawler.cs\output.csv", csv);
        }
    }
}
