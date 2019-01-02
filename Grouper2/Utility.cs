﻿using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Grouper2
{
    class Utility
    {
        public static JObject GetFileDaclJObject(string filePathString)
        {
            JObject fileDaclsJObject = new JObject();
            FileSecurity filePathSecObj = new FileSecurity();
            try
            {
                filePathSecObj = File.GetAccessControl(filePathString);
            }
            catch (System.ArgumentException e)
            {
                Console.WriteLine("Tried to check file permissions on invalid path: " + filePathString.ToString());
                return fileDaclsJObject;
            }

            AuthorizationRuleCollection fileAccessRules =
                filePathSecObj.GetAccessRules(true, true, typeof(SecurityIdentifier));

            foreach (FileSystemAccessRule fileAccessRule in fileAccessRules)
            {
                // get inheritance and access control type values
                string isInheritedString = "False";
                if (fileAccessRule.IsInherited) isInheritedString = "True";
                string accessControlTypeString = "Allow";
                if (fileAccessRule.AccessControlType == AccessControlType.Deny) accessControlTypeString = "Deny";

                // get the user's SID
                string identityReferenceString = fileAccessRule.IdentityReference.ToString();
                string displayNameString = LDAPstuff.GetUserFromSid(identityReferenceString);
                // get the rights
                string fileSystemRightsString = fileAccessRule.FileSystemRights.ToString();
                // strip spaces
                fileSystemRightsString = fileSystemRightsString.Replace(" ", "");
                // turn them into an array
                string[] fileSystemRightsArray = fileSystemRightsString.Split(',');
                // then into a JArray
                JArray fileSystemRightsJArray = new JArray();
                foreach (string x in fileSystemRightsArray)
                {
                    fileSystemRightsJArray.Add(x);
                }

                JObject fileDaclJObject = new JObject();
                fileDaclJObject.Add("Display Name", displayNameString);
                fileDaclJObject.Add("Allow or Deny?", accessControlTypeString);
                fileDaclJObject.Add("Inherited?", isInheritedString);
                fileDaclJObject.Add("Rights", fileSystemRightsJArray);
                fileDaclsJObject.Add(identityReferenceString, fileDaclJObject);
            }

            return fileDaclsJObject;
        }

        public static string SidPrivHighOrLow(string sid)
        // checks if a Sid belongs to a user who is canonically 'high' or 'low' priv.
        // by canonically, I mean 'I Reckon'.
        {
            string highOrLow = null;
            JToken checkedSid = Utility.CheckSid(sid);
            if (checkedSid != null)
            {
                string highPriv = checkedSid["highPriv"].ToString();
                string lowPriv = checkedSid["lowPriv"].ToString();
                if (highPriv == "True")
                {
                    highOrLow = "High";
                }

                if (lowPriv == "True")
                {
                    highOrLow = "Low";
                }
            }
            
            return highOrLow;
        }

    public static string DecryptCpassword(string cpassword)
        {
            // reimplemented based on @obscuresec's Get-GPPPassword PowerShell
            int cpassMod = cpassword.Length % 4;
            string padding = "";
            switch (cpassMod)
            {
                case 1:
                    padding = "=";
                    break;
                case 2:
                    padding = "==";
                    break;
                case 3:
                    padding = "=";
                    break;
            }
            string cpasswordPadded = cpassword + padding;
            byte[] decodedCpassword = Convert.FromBase64String(cpasswordPadded);
            AesCryptoServiceProvider aesProvider = new AesCryptoServiceProvider();
            byte[] aesKey = {0x4e, 0x99, 0x06, 0xe8, 0xfc, 0xb6, 0x6c, 0xc9, 0xfa, 0xf4, 0x93, 0x10, 0x62, 0x0f, 0xfe, 0xe8,
                                 0xf4, 0x96, 0xe8, 0x06, 0xcc, 0x05, 0x79, 0x90, 0x20, 0x9b, 0x09, 0xa4, 0x33, 0xb6, 0x6c, 0x1b };
            aesProvider.IV = new byte[aesProvider.IV.Length];
            aesProvider.Key = aesKey;
            ICryptoTransform decryptor = aesProvider.CreateDecryptor();
            byte[] decryptedBytes = decryptor.TransformFinalBlock(decodedCpassword, 0, decodedCpassword.Length);
            string decryptedCpassword = Encoding.Unicode.GetString(decryptedBytes);
            return decryptedCpassword;
        }

        public static JToken CheckSid(string sid)
        {
            JObject jsonData = JankyDb.Instance;
            JArray wellKnownSids = (JArray)jsonData["trustees"]["item"];

            bool sidMatches = false;
            // iterate over the list of well known sids to see if any match.
            foreach (JToken wellKnownSid in wellKnownSids)
            {
                string sidToMatch = (string)wellKnownSid["SID"];
                // a bunch of well known sids all include the domain-unique sid, so we gotta check for matches amongst those.
                if ((sidToMatch.Contains("DOMAIN")) && (sid.Length >= 14))
                {
                    string[] trusteeSplit = sid.Split("-".ToCharArray());
                    string[] wkSidSplit = sidToMatch.Split("-".ToCharArray());
                    if (trusteeSplit[trusteeSplit.Length - 1] == wkSidSplit[wkSidSplit.Length - 1])
                    {
                        sidMatches = true;
                    }
                }
                // check if we have a direct match
                if ((string)wellKnownSid["SID"] == sid)
                {
                    sidMatches = true;
                }
                if (sidMatches == true)
                {
                    JToken checkedSid = wellKnownSid;
                    return checkedSid;
                }
            }
            return null;
        }

        public static bool DoesFileExist(string inPath)
        {
            bool fileExists = false;
            try
            {
                fileExists = File.Exists(inPath);
            }
            catch (System.ArgumentException e)
            {
                Utility.DebugWrite("Checked if file " + inPath + " exists but it doesn't seem to be a valid file path.");
            }
            return fileExists;
        }

        public static bool CanIWrite(string inPath)
        {
            bool canWrite = false;
            try
            {
                FileStream stream = File.OpenWrite(inPath);
                canWrite = stream.CanWrite;
                stream.Close();
            }
            catch (System.UnauthorizedAccessException e)
            {
                Utility.DebugWrite("Tested write perms for " + inPath + " and couldn't write.");
            }
            catch (System.ArgumentException e)
            {
                Utility.DebugWrite("Tested write perms for " + inPath + " but it doesn't seem to be a valid file path.");
            }
            catch (Exception e)
            {
                Utility.DebugWrite(e.ToString());
            }
            return canWrite;
        }

        public static string GetSafeString(JToken json, string inString)
        {
            string stringOut;
            try
            {
                stringOut = json[inString].ToString();
            }
            catch (System.NullReferenceException)
            {
                stringOut = "";
            }
            return stringOut;
        }

        public static void DebugWrite(string textToWrite)
        {
            Console.BackgroundColor = ConsoleColor.Yellow;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(textToWrite);
            Console.ResetColor();
        }

        public static void WriteColor(string textToWrite, ConsoleColor fgColor)
        {
            Console.ForegroundColor = fgColor;
            Console.Write(textToWrite);
            Console.ResetColor();
        }

        public static void WriteColorLine(string textToWrite, ConsoleColor fgColor)
        {
            Console.ForegroundColor = fgColor;
            Console.WriteLine(textToWrite);
            Console.ResetColor();
        }

        public static void PrintBanner()
        {
            string barf = @"  .,-:::::/  :::::::..       ...      ...    :::::::::::::. .,::::::  :::::::..     .:::.   
,;;-'````'   ;;;;``;;;;   .;;;;;;;.   ;;     ;;; `;;;```.;;;;;;;''''  ;;;;``;;;;   ,;'``;.  
[[[   [[[[[[/ [[[,/[[['  ,[[     \[[,[['     [[[  `]]nnn]]'  [[cccc    [[[,/[[['   ''  ,[[' 
*$$c.    *$$  $$$$$$c    $$$,     $$$$$      $$$   $$$**     $$****    $$$$$$c     .c$$P'   
 `Y8bo,,,o88o 888b *88bo,*888,_ _,88P88    .d888   888o      888oo,__  888b *88bo,d88 _,oo, 
   `'YMUP*YMM MMMM   *W*   *YMMMMMP*  *YmmMMMM**   YMMMb     ****YUMMM MMMM   *W* MMMUP**^^ 
                                                            Now even Grouperer.              
                                                            github.com/mikeloss/Grouper2    
                                                            @mikeloss                          ";
            string[] barfLines = barf.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            ConsoleColor[] patternOne = { ConsoleColor.White, ConsoleColor.Yellow, ConsoleColor.Red, ConsoleColor.Red, ConsoleColor.DarkRed, ConsoleColor.DarkRed, ConsoleColor.White, ConsoleColor.White, ConsoleColor.White, ConsoleColor.White };
            ConsoleColor[] patternTwo =
            {
                ConsoleColor.White, ConsoleColor.White, ConsoleColor.Cyan, ConsoleColor.Blue, ConsoleColor.DarkBlue,
                ConsoleColor.DarkBlue, ConsoleColor.White, ConsoleColor.White, ConsoleColor.White, ConsoleColor.White,
            };
            int i = 0;
            foreach (string barfLine in barfLines)
            {
                string barfOne = barfLine.Substring(0, 82);
                string barfTwo = barfLine.Substring(82, 9);
                WriteColor(barfOne, patternOne[i]);
                WriteColorLine(barfTwo, patternTwo[i]);
                i += 1;
            }
        }

        public static string GetActionString(string actionChar)
            // shut up, i know it's not really a char.
        {
            string actionString = "";

            switch (actionChar)
            {
                case "U":
                    actionString = "Update";
                    break;
                case "A":
                    actionString = "Add";
                    break;
                case "D":
                    actionString = "Delete";
                    break;
                case "C":
                    actionString = "Create";
                    break;
                default:
                    Utility.DebugWrite("oh no this is new");
                    Utility.DebugWrite(actionChar);
                    actionString = "Broken";
                    break;
            }

            return actionString;
        }
    }
}