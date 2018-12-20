﻿/***
 *      .,-:::::/  :::::::..       ...      ...    :::::::::::::. .,::::::  :::::::..     .:::.  
 *    ,;;-'````'   ;;;;``;;;;   .;;;;;;;.   ;;     ;;; `;;;```.;;;;;;;''''  ;;;;``;;;;   ,;'``;. 
 *    [[[   [[[[[[/ [[[,/[[['  ,[[     \[[,[['     [[[  `]]nnn]]'  [[cccc    [[[,/[[['   ''  ,[['
 *    "$$c.    "$$  $$$$$$c    $$$,     $$$$$      $$$   $$$""     $$""""    $$$$$$c     .c$$P'  
 *     `Y8bo,,,o88o 888b "88bo,"888,_ _,88P88    .d888   888o      888oo,__  888b "88bo,d88 _,oo,
 *       `'YMUP"YMM MMMM   "W"   "YMMMMMP"  "YmmMMMM""   YMMMb     """"YUMMM MMMM   "W" MMMUP*"^^
 *                                                                                               
 *                        By Mike Loss (@mikeloss)                                                
 */

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;

namespace Grouper2
{
    // Create a singleton that contains our big GPO data blob so we can access it without reparsing it.
    public static class JankyDb
    {
        private static JObject _instance;

        public static JObject Instance
        {
            get
            {
                if(_instance == null)
                {
                    _instance = JObject.Parse(File.ReadAllText("PolData.Json"));
                }
                return _instance;
            }
        }
    }

    public static class GlobalVar
    {
        public static bool OnlineChecks;
    }

    internal class Grouper2
    {
        private static void Main(string[] args)
        {
            Utility.PrintBanner();
            string sysvolPolDir = "";

            JObject domainGpos = new JObject();

            // Ask the DC for GPO details
            if (args.Length == 0)
            {
                Console.WriteLine("Trying to figure out what AD domain we're working with.");
                string currentDomainString = Domain.GetCurrentDomain().ToString();
                Console.WriteLine("Current AD Domain is: " + currentDomainString);
                sysvolPolDir = @"\\" + currentDomainString + @"\sysvol\" + currentDomainString + @"\Policies\";
                Utility.DebugWrite("SysvolPolDir is " + sysvolPolDir);
                GlobalVar.OnlineChecks = true;
            }

            // or if the user gives a path argument, just look for policies in there
            if (args.Length == 1)
            {
                Console.WriteLine("OK, I trust you know where you're aiming me.");
                sysvolPolDir = args[0];
            }

            Console.WriteLine("We gonna look at the policies in: " + sysvolPolDir);
            if (GlobalVar.OnlineChecks) domainGpos = LDAPstuff.GetDomainGpos();

            string[] gpoPaths = Directory.GetDirectories(sysvolPolDir);

            // create a dict to put all our output goodies in.
            Dictionary<string, JObject> grouper2OutputDict = new Dictionary<string, JObject>();

            foreach (var gpoPath in gpoPaths)
            {
                // create a dict to put the stuff we find for this GPO into.
                Dictionary<string, JObject> gpoResultDict = new Dictionary<string, JObject>();
                //

                // Get the UID of the GPO from the file path.
                string[] splitPath = gpoPath.Split(Path.DirectorySeparatorChar);
                string gpoUid = splitPath[splitPath.Length - 1];

                // Make a JObject for GPO metadata
                JObject gpoPropsJson = new JObject();
                // If we're online and talking to the domain, just use that data
                if (GlobalVar.OnlineChecks)
                {
                    JToken domainGpo = domainGpos[gpoUid];
                    gpoPropsJson = (JObject) JToken.FromObject(domainGpo);
                }
                // otherwise do what we can with what we have
                else
                {
                    Dictionary<string, string> gpoPropsDict = new Dictionary<string, string>();
                    gpoPropsDict.Add("GPO UID", gpoUid);
                    gpoPropsDict.Add("GPO Path", gpoPath);
                    gpoPropsJson = (JObject)JToken.FromObject(gpoPropsDict);
                }

                // TODO (and put in GPOProps)
                // get the policy owner
                // get whether it's linked and where
                // get whether it's enabled

                // Get the paths for the machine policy and user policy dirs
                string machinePolPath = Path.Combine(gpoPath, "Machine");
                string userPolPath = Path.Combine(gpoPath, "User");

                // Process Inf and Xml Policy data for machine and user
                JObject machinePolInfResults = ProcessInf(machinePolPath);
                JObject userPolInfResults = ProcessInf(userPolPath);
                JObject machinePolGppResults = ProcessGpXml(machinePolPath);
                JObject userPolGppResults = ProcessGpXml(userPolPath);
                
                // Add all this crap into a dict
                gpoResultDict.Add("GPOProps", gpoPropsJson);
                gpoResultDict.Add("Machine Policy from GPP XML files", machinePolGppResults);
                gpoResultDict.Add("User Policy from GPP XML files", userPolGppResults);
                gpoResultDict.Add("Machine Policy from Inf files", machinePolInfResults);
                gpoResultDict.Add("User Policy from Inf files", userPolInfResults);

                // turn dict of data for this gpo into jobj
                JObject gpoResultJson = (JObject) JToken.FromObject(gpoResultDict);

                // put into final jobj
                grouper2OutputDict.Add(gpoPath, gpoResultJson);

                //  TODO
                //  Parse other inf sections:
                //  System Access
                //  Kerberos Policy
                //  Event Audit
                //  Registry Values
                //  Registry Keys
                //  Group Membership
                //  Service General Setting
                //  Parse XML files
                //  Parse ini files
                //  Grep scripts for creds.
                //  File permissions for referenced files.
            }

            // Final output is finally happening finally here:
            Utility.DebugWrite("Final Output:");
            JObject grouper2OutputJson = (JObject) JToken.FromObject(grouper2OutputDict);
            Console.WriteLine("");
            Console.WriteLine(grouper2OutputJson);
            Console.WriteLine("");
            // wait for 'anykey'
            Console.ReadKey();
        }


        private static JObject ProcessInf(string Path)
        {
            // find all the GptTmpl.inf files
            List<string> gpttmplInfFiles = new List<string>();
            try
            {
                gpttmplInfFiles = Directory.GetFiles(Path, "GptTmpl.inf", SearchOption.AllDirectories).ToList();
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                return null;
            }

            // make a dict for our results
            Dictionary<string, JObject> processedInfsDict = new Dictionary<string, JObject>();
            // iterate over the list of inf files we found
            foreach (string infFile in gpttmplInfFiles)
            {
                //parse the inf file into a manageable format
                JObject parsedInfFile = Parsers.ParseInf(infFile);
                //send the inf file to be assessed
                JObject assessedGpTmpl = AssessHandlers.AssessGptmpl(parsedInfFile);

                //add the result to our results
                processedInfsDict.Add(infFile, assessedGpTmpl);
            }

            return (JObject) JToken.FromObject(processedInfsDict);
        }

        private static JObject ProcessGpXml(string Path)
        {
            if(!Directory.Exists(Path))
            {
                return null;
            }
            // Group Policy Preferences are all XML so those are handled here.
            string[] xmlFiles = Directory.GetFiles(Path, "*.xml", SearchOption.AllDirectories);
            // create a dict for the stuff we find
            Dictionary<string, JObject> processedGpXml = new Dictionary<string, JObject>();
            // if we find any xml files
            if (xmlFiles.Length >= 1)
                foreach (var xmlFile in xmlFiles)
                {
                    // send each one to get mangled into json
                    JObject parsedGppXmlToJson = Parsers.ParseGppXmlToJson(xmlFile);
                    // then send each one to get assessed for fun things
                    JObject assessedGpp = AssessHandlers.AssessGppJson(parsedGppXmlToJson);
                    if (assessedGpp != null) processedGpXml.Add(xmlFile, assessedGpp);
                }

            return (JObject) JToken.FromObject(processedGpXml);
        }
    }
}