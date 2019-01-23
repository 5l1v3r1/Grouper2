﻿using Newtonsoft.Json.Linq;

namespace Grouper2
{
    public partial class AssessGpp
    {
        private JObject GetAssessedDataSources(JObject gppCategory)
        {
            JObject assessedGppDataSources = new JObject();
            if (gppCategory["DataSource"] is JArray)
            {
                foreach (JToken gppDataSource in gppCategory["DataSource"])
                {
                    JProperty assessedGppDataSource = AssessGppDataSource(gppDataSource);
                    assessedGppDataSources.Add(assessedGppDataSource);
                }
            }
            else
            {
                JProperty assessedGppDataSource = AssessGppDataSource(gppCategory["DataSource"]);
                assessedGppDataSources.Add(assessedGppDataSource);
            }

            if (assessedGppDataSources.HasValues)
            {
                return assessedGppDataSources;
            }
            else
            {
                return null;
            }
            
        }
        
        static JProperty AssessGppDataSource(JToken gppDataSource)
        {
            //Utility.DebugWrite(gppDataSource.ToString());
            int interestLevel = 1;
            string gppDataSourceUid = Utility.GetSafeString(gppDataSource, "@uid");
            string gppDataSourceName = Utility.GetSafeString(gppDataSource, "@name");
            string gppDataSourceChanged = Utility.GetSafeString(gppDataSource, "@changed");
            
            JToken gppDataSourceProps = gppDataSource["Properties"];
            string gppDataSourceAction = Utility.GetActionString(gppDataSourceProps["@action"].ToString());
            string gppDataSourceUserName = Utility.GetSafeString(gppDataSourceProps, "@username");
            string gppDataSourcecPassword = Utility.GetSafeString(gppDataSourceProps, "@cpassword");
            string gppDataSourcePassword = "";
            if (gppDataSourcecPassword.Length > 0)
            {
                gppDataSourcePassword = Utility.DecryptCpassword(gppDataSourcecPassword);
                interestLevel = 10;
            }

            string gppDataSourceDsn = Utility.GetSafeString(gppDataSourceProps, "@dsn");
            string gppDataSourceDriver = Utility.GetSafeString(gppDataSourceProps, "@driver");
            string gppDataSourceDescription = Utility.GetSafeString(gppDataSourceProps, "@description");
            JToken gppDataSourceAttributes = gppDataSourceProps["Attributes"];

            if (interestLevel >= GlobalVar.IntLevelToShow)
            {
                JObject assessedGppDataSource = new JObject();
                assessedGppDataSource.Add("Name", gppDataSourceName);
                assessedGppDataSource.Add("Changed", gppDataSourceChanged);
                assessedGppDataSource.Add("Action", gppDataSourceAction);
                assessedGppDataSource.Add("Username", gppDataSourceUserName);
                assessedGppDataSource.Add("cPassword", gppDataSourcecPassword);
                assessedGppDataSource.Add("Decrypted Password", gppDataSourcePassword);
                assessedGppDataSource.Add("DSN", gppDataSourceDsn);
                assessedGppDataSource.Add("Driver", gppDataSourceDriver);
                assessedGppDataSource.Add("Description", gppDataSourceDescription);
                assessedGppDataSource.Add("Attributes", gppDataSourceAttributes);
               
                return new JProperty(gppDataSourceUid, assessedGppDataSource);
            }

            return null;
        }
    }
}