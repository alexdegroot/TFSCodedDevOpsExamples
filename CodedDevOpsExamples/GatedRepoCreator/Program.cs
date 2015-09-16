using System;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;

namespace GatedRepoCreator
{
    public class Program
    {
        private static readonly string ProjectName = "344d08c9-8191-48b3-ab62-6abfa9f440ce";
        private static readonly string VsoBaseUrl = "https://newbuilds.visualstudio.com/DefaultCollection/";
        private static readonly string VsoProjectUrl = VsoBaseUrl + ProjectName + "/";
        private static HttpClient _client;

        protected static void Main()
        {
            // Setup Client with right security settings to talk to VSO
            SetupClient();

            // Request input
            var repoName = "GatedRepo" + DateTime.Now.Ticks;

            // Create Repo
            var repoId = CreateRepo(repoName);

            // Create Build 
            var buildId = CreateBuild(repoName);

            // Setup Code Policy
            // ReSharper disable once UnusedVariable
            var policyId = SetupBuildPolicy(repoId, buildId);
        }

        private static void SetupClient()
        {
            var username = ConfigurationManager.AppSettings["Username"];
            var password = ConfigurationManager.AppSettings["Password"];

            _client = new HttpClient();

            _client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            var headerValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", headerValue);
        }

        private static string CreateRepo(string repoName)
        {
            var createRepoJsonTemplate = new JObject
            {
                {"name", repoName },
                {"project",  new JObject(new JProperty("id", ProjectName)) }
            };

            var content = new StringContent(createRepoJsonTemplate.ToString(), Encoding.UTF8, "application/json");
            var response = _client.PostAsync(VsoBaseUrl + "_apis/git/repositories?api-version=1.0", content).Result;

            // Show result
            Console.WriteLine("Creation of Repository:");
            Console.WriteLine(response.StatusCode);

            // Return Id for further usage
            var createResult = response.Content.ReadAsStringAsync().Result;
            JObject build = JObject.Parse(createResult);
            Console.WriteLine("Name: " + build.Value<string>("name"));
            Console.WriteLine("ID: " + build.Value<string>("id"));
            return build.Value<string>("id");
        }

        private static int CreateBuild(string repoName)
        {
            var createBuildDef = new JObject
            {
                {"name", "PRBuildDef-" + repoName},
                {"quality", "definition"}
            };

            // Fetch template 'vsBuild'
            var url = VsoProjectUrl + "_apis/build/definitions/templates?api-version=2.0";
            var response = _client.GetStringAsync(url).Result;

            var templates = JObject.Parse(response);
            var token = templates["value"].Children().Single(t => t["id"].Value<string>().Equals("vsBuild")).SelectToken("template");

            foreach (var prop in token.Children())
            {
                createBuildDef.Add(prop);
            }

            // Make the repo the one to use for the build
            var repo = new JObject
            {
                {"type", "tfsgit"},
                {"name", repoName},
                {"defaultBranch", "refs/heads/master"},
                {"clean", "true"}
            };
            createBuildDef.Add("repository", repo);

            var queue = new JObject
            {
                {"pool", new JObject(new JProperty("id", 2)) },
                {"id", 2 },
                {"name", "hosted" },
            };
            createBuildDef.Add("queue", queue);

            // Talk to TFS
            var content = new StringContent(createBuildDef.ToString(), Encoding.UTF8, "application/json");
            var secondReponse = _client.PostAsync(VsoProjectUrl + "_apis/build/definitions?api-version=2.0",
                   content).Result;

            // Show result
            Console.WriteLine("Creation of Build Definition:");
            Console.WriteLine(secondReponse.StatusCode);

            // Return Id for further usage
            var createResult = secondReponse.Content.ReadAsStringAsync().Result;
            JObject build = JObject.Parse(createResult);
            Console.WriteLine("Name: " + build.Value<string>("name"));
            Console.WriteLine("ID: " + build.Value<int>("id"));
            return build.Value<int>("id");
        }

        private static int SetupBuildPolicy(string repoId, int buildId)
        {
            // Setup of the policy definition and settings
            var scope = new JObject
            {
                {"repositoryId", repoId},
                {"refName", "refs/heads/master"},
                {"matchKind", "prefix"}
            };
            var settings = new JObject
            {
                {"buildDefinitionId", buildId},
                {"scope", new JArray(scope)}
            };
            var policyDef = new JObject
            {
                {"isEnabled", true},
                {"isBlocking", true},
                {"type", new JObject(new JProperty("id", "0609b952-1397-4640-95ec-e00a01b2c241"))},
                {"settings", settings }
            };

            // Talk to TFS
            var content = new StringContent(policyDef.ToString(), Encoding.UTF8, "application/json");
            var reponse = _client.PostAsync(VsoProjectUrl + "_apis/policy/configurations?api-version=2.0-preview", content).Result;

            // Show Result
            Console.WriteLine("Creation of Policy Configuration:");
            Console.WriteLine(reponse.StatusCode);

            // Return Id for further usage
            var createResult = reponse.Content.ReadAsStringAsync().Result;
            JObject build = JObject.Parse(createResult);
            Console.WriteLine("ID: " + build.Value<int>("id"));
            return build.Value<int>("id");
        }

    }
}