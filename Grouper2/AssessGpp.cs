﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Grouper2
{
    public class AssessGpp
    {
        private readonly JObject _GPP;

        public AssessGpp(JObject GPP)
        {
            _GPP = GPP;
        }

        public JObject GetAssessed(string assessName)
        {
            //construct the method name based on the assessName and get it using reflection
            MethodInfo mi = this.GetType().GetMethod("GetAssessed" + assessName, BindingFlags.NonPublic | BindingFlags.Instance);
            //invoke the found method
            try
            {
                JObject gppToAssess = (JObject)_GPP[assessName];
                if (mi != null)
                {
                    JObject assessedThing = (JObject)mi.Invoke(this, parameters: new object[] { gppToAssess });
                    if (assessedThing != null)
                    {
                        return assessedThing;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    Utility.DebugWrite("Failed to find method: GetAssessed" + assessName);
                    return null;
                }
            }
            catch (Exception e)
            {
                Utility.DebugWrite(e.ToString());
                return null;
            }
        }

        private JObject GetAssessedFiles(JObject gppCategory)
        {
            Dictionary<string, Dictionary<string, string>> assessedFilesDict = new Dictionary<string, Dictionary<string, string>>();

            if (gppCategory["File"] is JArray)
            {
                foreach (JObject gppFile in gppCategory["File"])
                {
                    assessedFilesDict.Add(gppFile["@uid"].ToString(), GetAssessedFile(gppFile));
                }
            }
            else
            {
                JObject gppFile = (JObject)JToken.FromObject(gppCategory["File"]);
                assessedFilesDict.Add(gppFile["@uid"].ToString(), GetAssessedFile(gppFile));
            }
            JObject assessedGppFiles = (JObject)JToken.FromObject(assessedFilesDict);
            return assessedGppFiles;
        }

        private Dictionary<string, string> GetAssessedFile(JObject gppFile)
        {
            int interestLevel = 3;
            Dictionary<string, string> assessedFileDict = new Dictionary<string, string>();
            JToken gppFileProps = gppFile["Properties"];
            assessedFileDict.Add("Name", gppFile["@name"].ToString());
            assessedFileDict.Add("Status", gppFile["@status"].ToString());
            assessedFileDict.Add("Changed", gppFile["@changed"].ToString());
            string gppFileAction = Utility.GetActionString(gppFileProps["@action"].ToString());
            assessedFileDict.Add("Action", gppFileAction);
            string fromPath = gppFileProps["@fromPath"].ToString();
            assessedFileDict.Add("From Path", fromPath);
            assessedFileDict.Add("Target Path", gppFileProps["@targetPath"].ToString());
            //TODO some logic to check from path file perms
            if (GlobalVar.OnlineChecks && (fromPath.Length > 0))
            {
                bool writable = false;
                writable = Utility.CanIWrite(fromPath);
                if (writable)
                {
                    interestLevel = 10;
                    assessedFileDict.Add("From Path Writable", "True");
                }
            }

            // if it's too boring to be worth showing, return an empty dict.
            if (interestLevel < GlobalVar.IntLevelToShow)
            {
                assessedFileDict = new Dictionary<string, string>();
            }
            return assessedFileDict;
        }

        private JObject GetAssessedGroups(JObject gppCategory)
        {
            Dictionary<string, Dictionary<string, string>> assessedGroupsDict = new Dictionary<string, Dictionary<string, string>>();
            Dictionary<string, Dictionary<string, string>> assessedUsersDict = new Dictionary<string, Dictionary<string, string>>();

            if (gppCategory["Group"] is JArray)
            {
                foreach (JObject gppGroup in gppCategory["Group"])
                {
                    Dictionary<string, string> assessedGroup = GetAssessedGroup(gppGroup);
                    if (assessedGroup.Count > 0)
                    {
                        assessedGroupsDict.Add(gppGroup["@uid"].ToString(), assessedGroup);
                    }
                }
            }
            else
            {
                JObject gppGroup = (JObject)JToken.FromObject(gppCategory["Group"]);
                Dictionary<string, string> assessedGroup = GetAssessedGroup(gppGroup);
                if (assessedGroup.Count > 0)
                {
                    assessedGroupsDict.Add(gppGroup["@uid"].ToString(), assessedGroup);
                }
            }
            JObject assessedGppGroups = (JObject)JToken.FromObject(assessedGroupsDict);

            if (gppCategory["User"] is JArray)
            {
                foreach (JObject gppUser in gppCategory["User"])
                {
                    Dictionary<string, string> assessedUser = GetAssessedUser(gppUser);
                    if (assessedUser.Count > 0)
                    {
                        assessedUsersDict.Add(gppUser["@uid"].ToString(), assessedUser);
                    }
                }
            }
            else
            {
                JObject gppUser = (JObject)JToken.FromObject(gppCategory["User"]);
                Dictionary<string, string> assessedUser = GetAssessedUser(gppUser);
                if (assessedUser.Count > 0)
                {
                    assessedUsersDict.Add(gppUser["@uid"].ToString(), assessedUser);
                }
            }
            JObject assessedGppUsers = (JObject)JToken.FromObject(assessedUsersDict);

            // cast our Dictionaries back into JObjects
            JProperty assessedUsersJson = new JProperty("GPPUserSettings", assessedGppUsers);
            JProperty assessedGroupsJson = new JProperty("GPPGroupSettings", assessedGppGroups);
            // chuck the users and groups together in one JObject
            JObject assessedGppGroupsJson = new JObject();
            // only want to actually output these things if there's anything useful in them.
            if (assessedUsersDict.Count > 0)
            {
                assessedGppGroupsJson.Add(assessedUsersJson);
            }
            if (assessedGroupsDict.Count > 0)
            {
                assessedGppGroupsJson.Add(assessedGroupsJson);
            }
            return assessedGppGroupsJson;
        }

        private Dictionary<string, string> GetAssessedUser(JObject gppUser)
        {
            //foreach (JToken gppUser in gppUsers) {
            // dictionary for results from this specific user.
            Dictionary<string, string> assessedUserDict = new Dictionary<string, string>();

            //set base interest level
            int interestLevel = 3;

            JToken gppUserProps = gppUser["Properties"];

            // check what the entry is doing to the user and turn it into real word
            string userAction = gppUserProps["@action"].ToString();
            userAction = Utility.GetActionString(userAction);

            // get the username and a bunch of other details:
            assessedUserDict.Add("Name", gppUser["@name"].ToString());
            assessedUserDict.Add("User Name", gppUserProps["@userName"].ToString());
            assessedUserDict.Add("DateTime Changed", gppUser["@changed"].ToString());
            assessedUserDict.Add("Account Disabled", gppUserProps["@acctDisabled"].ToString());
            assessedUserDict.Add("Password Never Expires", gppUserProps["@neverExpires"].ToString());
            assessedUserDict.Add("Description", gppUserProps["@description"].ToString());
            assessedUserDict.Add("Full Name", gppUserProps["@fullName"].ToString());
            assessedUserDict.Add("New Name", gppUserProps["@newName"].ToString());
            assessedUserDict.Add("Action", userAction);

            // check for cpasswords
            string cpassword = gppUserProps["@cpassword"].ToString();
            if (cpassword.Length > 0)
            {
                string decryptedCpassword = "";
                decryptedCpassword = Utility.DecryptCpassword(cpassword);
                // if we find one, that's super interesting.
                assessedUserDict.Add("Cpassword", decryptedCpassword);
                interestLevel = 10;
            }
            // if it's too boring to be worth showing, return an empty dict.
            if (interestLevel < GlobalVar.IntLevelToShow)
            {
                assessedUserDict = new Dictionary<string, string>();
            }
            return assessedUserDict;
        }

        private Dictionary<string, string> GetAssessedGroup(JObject gppGroup)
        {
            //foreach (JToken gppGroup in gppGroups)
            //{
            //dictionary for results from this specific group
            Dictionary<string, string> assessedGroupDict = new Dictionary<string, string>();
            int interestLevel = 3;

            JToken gppGroupProps = gppGroup["Properties"];

            // check what the entry is doing to the group and turn it into real word
            string groupAction = gppGroupProps["@action"].ToString();
            groupAction = Utility.GetActionString(groupAction);

            // get the group name and a bunch of other details:
            assessedGroupDict.Add("Name", Utility.GetSafeString(gppGroup, "@name"));
            assessedGroupDict.Add("DateTime Changed", Utility.GetSafeString(gppGroup,"@changed"));
            assessedGroupDict.Add("Description", Utility.GetSafeString(gppGroupProps, "@description"));
            assessedGroupDict.Add("New Name", Utility.GetSafeString(gppGroupProps, "@newName"));
            assessedGroupDict.Add("Delete All Users", Utility.GetSafeString(gppGroupProps,"@deleteAllUsers"));
            assessedGroupDict.Add("Delete All Groups", Utility.GetSafeString(gppGroupProps,"@deleteAllGroups"));
            assessedGroupDict.Add("Remove Accounts", Utility.GetSafeString(gppGroupProps,"@removeAccounts"));
            assessedGroupDict.Add("Action", groupAction);
            //assessedGroupDict.Add("Group Members", gppGroup);
            Console.WriteLine(gppGroup.ToString());
            Utility.DebugWrite("You still need to figure out group members.");

            if (interestLevel < GlobalVar.IntLevelToShow)
            {
                assessedGroupDict = new Dictionary<string, string>();
            }
            return assessedGroupDict;

        }
       
       private JObject GetAssessedDrives(JObject gppCategory)
       {
           int interestLevel = 3;
           JProperty gppDriveProp = new JProperty("Drive", gppCategory["Drive"]);
           JObject assessedGppDrives = new JObject(gppDriveProp);
           if (interestLevel < GlobalVar.IntLevelToShow)
           {
               assessedGppDrives = new JObject();
           }
           return assessedGppDrives;
        }
  

    private JObject GetAssessedEnvironmentVariables(JObject gppCategory)
       {
           int interestLevel = 1;
           JProperty gppEVProp = new JProperty("EnvironmentVariable", gppCategory["EnvironmentVariable"]);
           JObject assessedGppEVs = new JObject(gppEVProp);
           if (interestLevel < GlobalVar.IntLevelToShow)
           {
               assessedGppEVs = new JObject();
           }
            return assessedGppEVs;
       }

       private JObject GetAssessedShortcuts(JObject gppCategory)
       {
           int interestLevel = 3;
           JProperty gppShortcutProp = new JProperty("Shortcut", gppCategory["Shortcut"]);
           JObject assessedGppShortcuts = new JObject(gppShortcutProp);
           if (interestLevel < GlobalVar.IntLevelToShow)
           {
               assessedGppShortcuts = new JObject();
           }
            return assessedGppShortcuts;
       }

       private JObject GetAssessedScheduledTasks(JObject gppCategory)
       {
           int interestLevel = 4;
           JProperty assessedGppSchedTasksTaskProp = new JProperty("Task", gppCategory["Task"]);
           JProperty assessedGppSchedTasksImmediateTaskProp = new JProperty("ImmediateTaskV2", gppCategory["ImmediateTaskV2"]);
           JObject assessedGppSchedTasksAllJson = new JObject(assessedGppSchedTasksTaskProp, assessedGppSchedTasksImmediateTaskProp);
           if (interestLevel < GlobalVar.IntLevelToShow)
           {
               assessedGppSchedTasksAllJson = new JObject();
           }
            return assessedGppSchedTasksAllJson;
       }

       private JObject GetAssessedRegistrySettings(JObject gppCategory)
       {
           int interestLevel = 2;
           JProperty gppRegSettingsProp = new JProperty("RegSettings", gppCategory["Registry"]);
           JObject assessedGppRegSettings = new JObject(gppRegSettingsProp);
           if (interestLevel < GlobalVar.IntLevelToShow)
           {
               assessedGppRegSettings = new JObject();
           }
            return assessedGppRegSettings;
       }

       private JObject GetAssessedNTServices(JObject gppCategory)
       {
           int interestLevel = 3;
           JProperty ntServiceProp = new JProperty("NTService", gppCategory["NTService"]);
           JObject assessedNtServices = new JObject(ntServiceProp);
           if (interestLevel < GlobalVar.IntLevelToShow)
           {
               assessedNtServices = new JObject();
           }
            return assessedNtServices;
       }

       private JObject GetAssessedNetworkOptions(JObject gppCategory)
       {
           int interestLevel = 1;
           JProperty gppNetworkOptionsProp = new JProperty("DUN", gppCategory["DUN"]);
           JObject assessedGppNetworkOptions = new JObject(gppNetworkOptionsProp);
           if (interestLevel < GlobalVar.IntLevelToShow)
           {
               assessedGppNetworkOptions = new JObject();
           }
            return assessedGppNetworkOptions;
       }

       private JObject GetAssessedFolders(JObject gppCategory)
       {
           int interestLevel = 1;
           JProperty gppFoldersProp = new JProperty("Folder", gppCategory["Folder"]);
           JObject assessedGppFolders = new JObject(gppFoldersProp);
           if (interestLevel < GlobalVar.IntLevelToShow)
           {
               assessedGppFolders = new JObject();
           }
            return assessedGppFolders;
       }

       private JObject GetAssessedNetworkShareSettings(JObject gppCategory)
       {
           int interestLevel = 1;
           JProperty gppNetSharesProp = new JProperty("NetShare", gppCategory["NetShare"]);
           JObject assessedGppNetShares = new JObject(gppNetSharesProp);
           if (interestLevel < GlobalVar.IntLevelToShow)
           {
               assessedGppNetShares = new JObject();
           }
            return assessedGppNetShares;
       }


       private JObject GetAssessedIniFiles(JObject gppCategory)
       {
           int interestLevel = 2;
           JProperty gppIniFilesProp = new JProperty("Ini", gppCategory["Ini"]);
           JObject assessedGppIniFiles = new JObject(gppIniFilesProp);
           if (interestLevel < GlobalVar.IntLevelToShow)
           {
               assessedGppIniFiles = new JObject();
           }
            return assessedGppIniFiles;
       }
    }
}
