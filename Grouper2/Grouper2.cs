﻿/*
 *      .,-:::::/  :::::::..       ...      ...    :::::::::::::. .,::::::  :::::::..     .:::.  
 *    ,;;-'````'   ;;;;``;;;;   .;;;;;;;.   ;;     ;;; `;;;```.;;;;;;;''''  ;;;;``;;;;   ,;'``;. 
 *    [[[   [[[[[[/ [[[,/[[['  ,[[     \[[,[['     [[[  `]]nnn]]'  [[cccc    [[[,/[[['   ''  ,[['
 *    "$$c.    "$$  $$$$$$c    $$$,     $$$$$      $$$   $$$""     $$""""    $$$$$$c     .c$$P'  
 *     `Y8bo,,,o88o 888b "88bo,"888,_ _,88P88    .d888   888o      888oo,__  888b "88bo,d88 _,oo,
 *       `'YMUP"YMM MMMM   "W"   "YMMMMMP"  "YmmMMMM""   YMMMb     """"YUMMM MMMM   "W" MMMUP*"^^
 *
 *      Alpha
 *                        By Mike Loss (@mikeloss)                                                
 */


using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using CommandLineParser.Arguments;
using CommandLineParser.Exceptions;
using Grouper2.Properties;

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
                    _instance = JObject.Parse(Resources.PolData);
                }
                return _instance;
            }
        }
    }

    public class GlobalVar
    {
        public static bool OnlineChecks;
        public static int IntLevelToShow;
        public static bool DebugMode;
    }

    internal class Grouper2
    {
        private static void Main(string[] args)
        {
            Utility.PrintBanner();

            CommandLineParser.CommandLineParser parser = new CommandLineParser.CommandLineParser();
            //SwitchArgument debugArg = new SwitchArgument('d', "debug", "Enables debug mode. Will also show you the names of any categories of policies that Grouper saw but didn't have any means of processing. I eagerly await your pull request.", false);
            SwitchArgument offlineArg = new SwitchArgument('o', "offline", "Disables checks that require LDAP comms with a DC or SMB comms with file shares found in policy settings. Requires that you define a value for --sysvol.", false);
            ValueArgument<string> sysvolArg = new ValueArgument<string>('s', "sysvol", "Set the path to a domain SYSVOL directory.");
            ValueArgument<int> intlevArg = new ValueArgument<int>('i', "interestlevel", "The minimum interest level to display. i.e. findings with an interest level lower than x will not be seen in output. Defaults to 1, i.e. show everything except some extremely dull defaults. If you want to see those too, do -i 0.");
            //ValueArgument<string> domainArg = new ValueArgument<string>('d', "domain", "The domain to connect to. If not specified, connects to current user context domain.");
            //ValueArgument<string> usernameArg = new ValueArgument<string>('u', "username", "Username to authenticate as. SMB permissions checks will be run from this user's perspective.");
            //ValueArgument<string> passwordArg = new ValueArgument<string>('p', "password", "Password to use for authentication.");
            //parser.Arguments.Add(domainArg);
            //parser.Arguments.Add(usernameArg);
            //parser.Arguments.Add(passwordArg);
            //parser.Arguments.Add(debugArg);
            parser.Arguments.Add(intlevArg);
            parser.Arguments.Add(sysvolArg);
            parser.Arguments.Add(offlineArg);

            // set a couple of defaults
            string sysvolPolDir = "";
            GlobalVar.OnlineChecks = true;

            try
            {
                parser.ParseCommandLine(args);
                if (offlineArg.Parsed && offlineArg.Value && sysvolArg.Parsed)
                {
                    // args config for valid offline run.
                    GlobalVar.OnlineChecks = false;
                    sysvolPolDir = sysvolArg.Value;
                }
                if (offlineArg.Parsed && offlineArg.Value && !sysvolArg.Parsed)
                {
                    // handle someone trying to run in offline mode without giving a value for sysvol
                    Console.WriteLine("Offline mode requires you to provide a value for -s, the path where Grouper2 can find the domain SYSVOL share, or a copy of it at least.");
                    Environment.Exit(1);
                }

                if (intlevArg.Parsed)
                {
                    // handle interest level parsing
                    Console.WriteLine("Roger. Everything with an Interest Level lower than " + intlevArg.Value.ToString() +" is getting thrown on the floor.");
                    GlobalVar.IntLevelToShow = intlevArg.Value;
                }
                else
                {
                    GlobalVar.IntLevelToShow = 1;
                }

                if (sysvolArg.Parsed)
                {
                    sysvolPolDir = sysvolArg.Value;
                }
            }
            catch (CommandLineException e)
            {
                Console.WriteLine(e.Message);
            }

            JObject domainGpos = new JObject();

            // Ask the DC for GPO details
            if (GlobalVar.OnlineChecks)
            {
                Console.WriteLine("Trying to figure out what AD domain we're working with.");
                string currentDomainString = Domain.GetCurrentDomain().ToString();
                Console.WriteLine("Current AD Domain is: " + currentDomainString);

                string[] sysvolPolDirs =
                    Directory.GetDirectories(@"\\" + currentDomainString + @"\sysvol\" + currentDomainString);
                Console.WriteLine("SYSVOL dir has this stuff in it. If you see NTFRS in any of the folder names there is probably some value in manually targeting each of those dirs for closer looks.\r\n");
                foreach (string line in sysvolPolDirs) { Console.WriteLine(line);}
                Console.WriteLine("");

                if (sysvolPolDir == "")
                {
                    sysvolPolDir = @"\\" + currentDomainString + @"\sysvol\" + currentDomainString + @"\Policies\";
                }
            }

            Console.WriteLine("Targeting SYSVOL at: " + sysvolPolDir);

            // if we're online, get a bunch of metadata about the GPOs via LDAP
            if (GlobalVar.OnlineChecks)
            {
                try
                {
                    domainGpos = LDAPstuff.GetDomainGpos();
                }
                catch (Exception e)
                {
                    Utility.DebugWrite("Failed to get all the GPO Data from DC.");
                    Utility.DebugWrite(e.ToString());
                }
            }

            List<string> gpoPaths = new List<string>();
            try
            {
                gpoPaths = Directory.GetDirectories(sysvolPolDir).ToList();
            }
            catch
            {
                Console.WriteLine("Sysvol path is broken. You should fix it.");
                Environment.Exit(1);
            }

            // create a JObject to put all our output goodies in.
            JObject grouper2Output = new JObject();
            // so for each uid directory (including ones with that dumb broken domain replication condition)
            // we're going to gather up all our goodies and put them into that dict we just created.
            foreach (var gpoPath in gpoPaths)
            {
                try
                {
                    // create a dict to put the stuff we find for this GPO into.
                    JObject gpoResult = new JObject();
                    // Get the UID of the GPO from the file path.
                    string[] splitPath = gpoPath.Split(Path.DirectorySeparatorChar);
                    string gpoUid = splitPath[splitPath.Length - 1];

                    // Make a JObject for GPO metadata
                    JObject gpoProps = new JObject();
                    // If we're online and talking to the domain, just use that data
                    if (GlobalVar.OnlineChecks)
                    {
                        try
                        {
                            // select the GPO's details from the gpo data we got
                            JToken domainGpo = domainGpos[gpoUid];
                            gpoProps = (JObject) JToken.FromObject(domainGpo);
                        }
                        catch (ArgumentNullException e)
                        {
                            Utility.DebugWrite("Couldn't get GPO Properties from the domain for the following GPO: " + gpoUid);
                            if (GlobalVar.DebugMode) {
                                Utility.DebugWrite(e.ToString());
                            }
                            // if we weren't able to select the GPO's details, do what we can with what we have.
                            gpoProps = new JObject()
                            {
                                {"gpoUID", gpoUid},
                                {"gpoPath", gpoPath}
                            };
                        }
                    }
                    // otherwise do what we can with what we have
                    else
                    {
                        gpoProps = new JObject()
                        {
                            {"gpoUID", gpoUid},
                            {"gpoPath", gpoPath}
                        };
                    }


                    // Add all this crap into a dict, if we found anything of interest.
                    gpoResult.Add("GPOProps", gpoProps);
                    // turn dict of data for this gpo into jobj
                    JObject gpoResultJson = (JObject) JToken.FromObject(gpoResult);

                    // if I were smarter I would have done this shit with the machine and user dirs inside the Process methods instead of calling each one twice out here.
                    // @liamosaur you reckon you can see how to clean it up after the fact?
                    // Get the paths for the machine policy and user policy dirs
                    string machinePolPath = Path.Combine(gpoPath, "Machine");
                    string userPolPath = Path.Combine(gpoPath, "User");

                    // Process Inf and Xml Policy data for machine and user
                    JArray machinePolInfResults = ProcessInf(machinePolPath);
                    JArray userPolInfResults = ProcessInf(userPolPath);
                    JArray machinePolGppResults = ProcessGpXml(machinePolPath);
                    JArray userPolGppResults = ProcessGpXml(userPolPath);
                    JArray machinePolScriptResults = ProcessScriptsIni(machinePolPath);
                    JArray userPolScriptResults = ProcessScriptsIni(userPolPath);

                    // add all our findings to a JArray in what seems a very inefficient manner but it's the only way i could see to avoid having a JArray of JArrays of Findings.
                    JArray userFindings = new JArray();
                    JArray machineFindings = new JArray();
                    if (machinePolGppResults != null && machinePolGppResults.HasValues)
                    {
                        foreach (JObject finding in machinePolGppResults)
                        {
                            machineFindings.Add(finding);
                        }
                    }

                    if (userPolGppResults != null && userPolGppResults.HasValues)
                    {
                        foreach (JObject finding in userPolGppResults)
                        {
                            userFindings.Add(finding);
                        }
                    }

                    if (machinePolGppResults != null && machinePolInfResults.HasValues)
                    {
                        foreach (JObject finding in machinePolInfResults)
                        {
                            machineFindings.Add(finding);
                        }
                    }

                    if (userPolInfResults != null && userPolInfResults.HasValues)
                    {
                        foreach (JObject finding in userPolInfResults)
                        {
                            userFindings.Add(finding);
                        }
                    }

                    if (machinePolScriptResults != null && machinePolScriptResults.HasValues)
                    {
                        foreach (JObject finding in machinePolScriptResults)
                        {
                            machineFindings.Add(finding);
                        }
                    }

                    if (userPolScriptResults != null && userPolScriptResults.HasValues)
                    {
                        foreach (JObject finding in userPolScriptResults)
                        {
                            userFindings.Add(finding);
                        }
                    }

                    // if there are any Findings, add it to the final output.
                    if (userFindings.HasValues)
                    {
                        JProperty userFindingsJProp = new JProperty("Findings in User Policy", userFindings);
                        gpoResultJson.Add(userFindingsJProp);
                    }

                    if (machineFindings.HasValues)
                    {
                        JProperty machineFindingsJProp = new JProperty("Findings in Machine Policy", machineFindings);
                        gpoResultJson.Add(machineFindingsJProp);
                    }

                    // put into final output
                    if (userFindings.HasValues || machineFindings.HasValues)
                    {
                        grouper2Output.Add(gpoPath, gpoResultJson);
                    }
                }
                catch (UnauthorizedAccessException e)
                {
                    Utility.DebugWrite(e.ToString());
                }
            }

            try
            {
                // Final output is finally happening finally here:
                Console.WriteLine("RESULT!");
                Console.WriteLine("");
                Console.WriteLine(grouper2Output);
                Console.WriteLine("");
                // wait for 'anykey'
                Console.ReadKey();
            }
            catch (Exception e)
            {
                Utility.DebugWrite(e.ToString());
            }
        }


        private static JArray ProcessInf(string Path)
        {
            // find all the GptTmpl.inf files
            List<string> gpttmplInfFiles = new List<string>();
            try
            {
                gpttmplInfFiles = Directory.GetFiles(Path, "GptTmpl.inf", SearchOption.AllDirectories).ToList();
            }
            catch (System.IO.DirectoryNotFoundException e)
            {
                if (GlobalVar.DebugMode)
                {
                    Utility.DebugWrite(e.ToString());
                }
                return null;
            }
            catch (System.UnauthorizedAccessException e)
            {
                if (GlobalVar.DebugMode)
                {
                    Utility.DebugWrite(e.ToString());
                }
                return null;
            }

            // make a JArray for our results
            JArray processedInfs = new JArray();
            // iterate over the list of inf files we found
            foreach (string infFile in gpttmplInfFiles)
            {
                //parse the inf file into a manageable format
                JObject parsedInfFile = Parsers.ParseInf(infFile);
                //send the inf file to be assessed
                JObject assessedGpTmpl = AssessHandlers.AssessGptmpl(parsedInfFile);

                //add the result to our results
                if (assessedGpTmpl.HasValues)
                {
                    processedInfs.Add(assessedGpTmpl);
                }
            }
            return processedInfs;
        }

        private static JArray ProcessScriptsIni(string Path)
        {
            List<string> scriptsIniFiles = new List<string>();

            try
            {
                scriptsIniFiles = Directory.GetFiles(Path, "Scripts.ini", SearchOption.AllDirectories).ToList();

            }
            catch (System.IO.DirectoryNotFoundException)
            {
                return null;
            }

            JArray processedScriptsIniFiles = new JArray();

            foreach (string iniFile in scriptsIniFiles)
            {
                JObject preParsedScriptsIniFile = Parsers.ParseInf(iniFile); // Not a typo, the formats are almost the same.
                if (preParsedScriptsIniFile != null)
                {
                    JObject parsedScriptsIniFile = Parsers.ParseScriptsIniJson(preParsedScriptsIniFile);
                    JObject assessedScriptsIniFile = AssessScriptsIni.GetAssessedScriptsIni(parsedScriptsIniFile);
                    if (assessedScriptsIniFile != null)
                    {
                        processedScriptsIniFiles.Add(assessedScriptsIniFile);
                    }
                }
            }

            return processedScriptsIniFiles;
        }

        private static JArray ProcessGpXml(string Path)
        {
            if(!Directory.Exists(Path))
            {
                return null;
            }
            // Group Policy Preferences are all XML so those are handled here.
            string[] xmlFiles = Directory.GetFiles(Path, "*.xml", SearchOption.AllDirectories);
            // create a dict for the stuff we find
            JArray processedGpXml = new JArray();
            // if we find any xml files
            if (xmlFiles.Length >= 1)
                foreach (var xmlFile in xmlFiles)
                {
                    // send each one to get mangled into json
                    JObject parsedGppXmlToJson = Parsers.ParseGppXmlToJson(xmlFile);
                    // then send each one to get assessed for fun things
                    JObject assessedGpp = AssessHandlers.AssessGppJson(parsedGppXmlToJson);
                    if (assessedGpp.HasValues) processedGpXml.Add(assessedGpp);
                }

            return processedGpXml;
        }
    }
}