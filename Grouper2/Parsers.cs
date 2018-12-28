﻿using System;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Xml;
using Newtonsoft.Json;

namespace Grouper2
{
    class Parsers
    {
        public static JObject ParseScriptsIniJson(JObject scriptsIniJson)
        {
            // take the partially parsed Ini File
            // create an object for us to put output into
            JObject parsedScriptsIniJson = new JObject();

            // iterate over the types of script (e.g. startup, shutdown, logon, etc)
            foreach (KeyValuePair<string, JToken> item in scriptsIniJson)
            {
                //get the type of the script into a string for output.
                string scriptType = item.Key.ToString();
                // cast the settings from JToken to JObject.
                JObject settingsJObject = (JObject)item.Value;

                // each script has a numeric index at the beginning of each of its settings. first we need to figure out how many of these there are.
                int maxIndex = 0;
                foreach (KeyValuePair<string, JToken> setting in settingsJObject)
                {
                    string index = setting.Key.Substring(0, 1);
                    int indexInt = Convert.ToInt32(index);
                    if (maxIndex < indexInt)
                    {
                        maxIndex = indexInt;
                    }
                }

                JArray settingsJArray = new JArray();
                // iterate over each index 
               /* foreach (int i in Enumerable.Range(0, maxIndex))
                {
                    
                    string thing = settingsJObject

                    settingsJArray.Add();
                }*/
                
                // put it in a jprop and add it to the output jobj
                JProperty parsedItemJProp = new JProperty(scriptType, settingsJArray);
                parsedScriptsIniJson.Add(parsedItemJProp);
            }
            return parsedScriptsIniJson;
        }

        public static JObject ParseInf(string infFile)
        {
            //define what a heading looks like
            Regex headingRegex = new Regex(@"^\[(\w+\s?)+\]$");
            string[] infContent = File.ReadAllLines(infFile);
            var headingLines = new List<int>();

            //find all the lines that look like a heading and put the line numbers in an array.
            int i = 0;
            foreach (string infLine in infContent)
            {
                Match headingMatch = headingRegex.Match(infLine);
                if (headingMatch.Success)
                {
                    headingLines.Add(i);
                }
                i++;
            }
            // make a dictionary with K/V = start/end of each section
            // this is extraordinarily janky but it works mostly.
            Dictionary<int, int> sectionSlices = new Dictionary<int, int>();
            int fuck = 0;
            while (true)
            {
                try
                {
                    int sectionHeading = headingLines[fuck];
                    int sectionFinalLine = (headingLines[(fuck + 1)] - 1);
                    sectionSlices.Add(sectionHeading, sectionFinalLine);
                    fuck++;
                }
                catch
                {
                    int sectionHeading = headingLines[fuck];
                    int sectionFinalLine = (infContent.Length - 1);
                    sectionSlices.Add(sectionHeading, sectionFinalLine);
                    break;
                }
            }

            // define jobj that we're going to put all this in and return at the end
            JObject infResults = new JObject();

            // iterate over the identified sections and get the heading and contents of each.
            foreach (KeyValuePair<int, int> sectionSlice in sectionSlices)
            {
                //get the section heading
                char[] squareBrackets = { '[', ']' };
                string sectionSliceKey = infContent[sectionSlice.Key];
                string sectionHeading = sectionSliceKey.Trim(squareBrackets);
                //get the line where the section content starts by adding one to the heading's line
                int startSection = (sectionSlice.Key + 1);
                //get the end line of the section
                int nextSection = sectionSlice.Value;
                //subtract one from the other to get the section length, without the heading.
                int sectionLength = (nextSection - startSection);
                //get an array segment with the lines
                ArraySegment<string> sectionContent = new ArraySegment<string>(infContent, startSection, sectionLength);
                //Console.WriteLine("This section contains: ");               
                //Utility.PrintIndexAndValues(sectionContent);
                //create the dictionary that we're going to put the lines into.
                JObject section = new JObject();
                //iterate over the lines in the section
                
                for (int b = sectionContent.Offset; b < (sectionContent.Offset + sectionContent.Count); b++)
                    {
                    string line = sectionContent.Array[b];
                    // split the line into the key (before the =) and the values (after it)
                    string[] splitLine = line.Split('=');
                    string lineKey = (splitLine[0]).Trim();
                    // then get the values
                    string lineValues = (splitLine[1]).Trim();
                    // and split them into an array on ","
                    string[] splitValues = lineValues.Split(',');
                    if (splitValues.Length > 1)
                    {
                        JArray splitValuesJArray = JArray.FromObject(splitValues);
                        //Add the restructured line into the dictionary.
                        section.Add(lineKey, splitValuesJArray);
                    }
                    else
                    {
                        section.Add(lineKey, lineValues);
                    }
                    }
                //put the results into the dictionary we're gonna return
                infResults.Add(sectionHeading, section);
            }
            return infResults;
        }

        public static JObject ParseGppXmlToJson(string xmlFile)
        {
            //grab the contents of the file path in the argument
            string rawXmlFileContent = File.ReadAllText(xmlFile);
            //create an xml object
            XmlDocument xmlFileContent = new XmlDocument();
            //put the file contents in the object
            xmlFileContent.LoadXml(rawXmlFileContent);
            // turn the Xml into Json
            string jsonFromXml = JsonConvert.SerializeXmlNode(xmlFileContent.DocumentElement, Newtonsoft.Json.Formatting.Indented);
            // debug write the json
            //Console.WriteLine(JsonFromXml);
            // put the json into a JObject
            JObject parsedXmlFileToJson = JObject.Parse(jsonFromXml);
            // return the JObject
            return parsedXmlFileToJson;
        }
    }
}