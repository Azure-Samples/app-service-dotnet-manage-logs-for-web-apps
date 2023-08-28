// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ManageWebAppLogs
{
    public class Program
    {
        /**
         * Azure App Service basic sample for managing web apps.
         *  - Create a web app under the same new app service plan:
         *    - Deploy to app using web deploy
         *    - stream logs for 120 seconds
         */

        public static async Task RunSample(ArmClient client)
        {
            // New resources
            AzureLocation region = AzureLocation.EastUS;
            string suffix         = ".azurewebsites.net";
            string appName       = Utilities.CreateRandomName("webapp1-");
            string appUrl        = appName + suffix;
            string rgName         = Utilities.CreateRandomName("rg1NEMV_");
            var lro = await client.GetDefaultSubscription().GetResourceGroups().CreateOrUpdateAsync(Azure.WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
            var resourceGroup = lro.Value;
            try {


                //============================================================
                // Create a web app with a new app service plan

                Utilities.Log("Creating web app " + appName + " in resource group " + rgName + "...");

                var webSiteCollection = resourceGroup.GetWebSites();
                var webSiteData = new WebSiteData(region)
                {
                    SiteConfig = new Azure.ResourceManager.AppService.Models.SiteConfigProperties()
                    {
                        WindowsFxVersion = "PricingTier.StandardS1",
                        NetFrameworkVersion = "NetFrameworkVersion.V4_6",
                    }
                };
                var webSite_lro = await webSiteCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, appName, webSiteData);
                var webSite = webSite_lro.Value;
                Utilities.Log("Created web app " + webSite.Data.Name);
                Utilities.Print(webSite);

                //============================================================
                // Listen to logs synchronously for 30 seconds

                var csm = new CsmPublishingProfile()
                {
                    Format = PublishingProfileFormat.Ftp
                };
                var stream_lro = await webSite.GetPublishingProfileXmlWithSecretsAsync(csm);
                using (var stream = stream_lro.Value)
                {
                    var reader = new StreamReader(stream);
                    Utilities.Log("Streaming logs from function app " + appName + "...");
                    string? line = reader.ReadLine();
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();
                    await Task.Factory.StartNew(async () =>
                    {
                        //============================================================
                        // Deploy to app 1 through zip deploy

                        Thread.Sleep(10000);
                        Utilities.Log("Deploying coffeeshop.war to " + appName + " through web deploy...");

                        var extensionResource = webSite.GetSiteExtension();
                        var deployresource = await extensionResource.CreateOrUpdateAsync(WaitUntil.Completed, new WebAppMSDeploy()
                        {
                            PackageUri = new Uri("https://github.com/Azure/azure-libraries-for-java/raw/master/azure-samples/src/main/resources/coffeeshop.zip"),
                            IsAppOffline = false,
                        }) ;

                        Utilities.Log("Deployments to web app " + webSite.Data.Name + " completed");
                        Utilities.Print(webSite);

                        // warm up
                        Utilities.Log("Warming up " + appUrl + "/coffeeshop...");
                        Utilities.CheckAddress("http://" + appUrl + "/coffeeshop");

                        Thread.Sleep(5000);
                        Utilities.Log("CURLing " + appUrl + "/coffeeshop...");
                        Utilities.Log(Utilities.CheckAddress("http://" + appUrl + "/coffeeshop"));
                        Thread.Sleep(15000);
                        Utilities.Log("CURLing " + appUrl + "/coffeeshop...");
                        Utilities.Log(Utilities.CheckAddress("http://" + appUrl + "/coffeeshop"));
                        Thread.Sleep(25000);
                        Utilities.Log("CURLing " + appUrl + "/coffeeshop...");
                        Utilities.Log(Utilities.CheckAddress("http://" + appUrl + "/coffeeshop"));
                        Thread.Sleep(35000);
                        Utilities.Log("CURLing " + appUrl + "/coffeeshop...");
                        Utilities.Log(Utilities.CheckAddress("http://" + appUrl + "/coffeeshop"));
                    });
                    while (line != null && stopWatch.ElapsedMilliseconds < 120000)
                    {
                        Utilities.Log(line);
                        line = reader.ReadLine();
                    }
                }
            }
            finally
            {
                try
                {
                    Utilities.Log("Deleting Resource Group: " + rgName);
                    await resourceGroup.DeleteAsync(WaitUntil.Completed);
                    Utilities.Log("Deleted Resource Group: " + rgName);
                }
                catch (NullReferenceException)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
                catch (Exception g)
                {
                    Utilities.Log(g);
                }
            }
        }

        public static async Task Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);

                // Print selected subscription
                Utilities.Log("Selected subscription: " + client.GetSubscriptions().Id);

                await RunSample(client);
            }
            catch (Exception e)
            {
                Utilities.Log(e);
            }
        }
    }
}