using System.Runtime.InteropServices;
using FlubuCore.Context;
using FlubuCore.Context.Attributes.BuildProperties;
using FlubuCore.Context.FluentInterface.TaskExtensions;
using FlubuCore.Scripting;
using FlubuCore.Scripting.Attributes;
using FlubuCore.Tasks.Attributes;
using FlubuCore.Tasks.Iis;
using FlubuCore.Tasks.Versioning;
using Newtonsoft.Json;
using RestSharp;

//// Examine build scripts in other projects(especialy mvc .net461 example) for more use cases. Also see FlubuCore buildscript on https://github.com/flubu-core/flubu.core/blob/master/BuildScript/BuildScript.cs
[Include("./BuildScript/BuildScriptHelper.cs")]
public class MyBuildScript : DefaultBuildScript
{
    [ProductId] public string ProductId { get; set; } = "FlubuCoreExample";

    [BuildConfiguration] public string BuildConfiguration { get; set; } = "Release";

    [SolutionFileName] public string SolutionFileName { get; set; } = "FlubuExample.sln";

    [FetchBuildVersionFromFile] public BuildVersion BuildVersion { get; set; }

    protected override void ConfigureTargets(ITaskContext context)
    {
        context.CreateTarget("Fetch.FlubuCore.Version")
            .Do(UpdateFlubuCoreNugetPackageToLatest);

        var compile = context
            .CreateTarget("compile")
            .SetDescription("Compiles the VS solution")
            .AddCoreTask(x => x.ExecuteDotnetTask("restore").WithArguments("FlubuExample.sln"))
            .AddCoreTask(x => x.Build().Version(BuildVersion.Version.ToString(4)));

        var package = context
            .CreateTarget("Package")
            .AddCoreTask(x => x.Publish("FlubuExample"))
            .AddCoreTask(x => x.CreateZipPackageFromProjects("FlubuExample", "netstandard2.0", "FlubuExample"));

        //// Can be used instead of CreateZipPackageFromProject. See MVC_NET4.61 project for full example of PackageTask
        //// context.CreateTarget("Package2").AddTask(x => x.PackageTask("FlubuExample"));

        var test = context.CreateTarget("test")
            .AddCoreTaskAsync(x => x.Test().Project("FlubuExample.Tests"))
            .AddCoreTaskAsync(x => x.Test().Project("FlubuExample.Tests2"));

        var doExample = context.CreateTarget("DoExample").Do(DoExample);
        var doExample2 = context.CreateTarget("DoExample2").Do(DoExample2);

        context.CreateTarget("iis.install").Do(IisInstall);

        //// todo include package into rebuild.
        context.CreateTarget("Rebuild")
            .SetAsDefault()
            .DependsOnAsync(doExample, doExample2)
            .DependsOn(compile, test, package);
    }

    public static void IisInstall(ITaskContext context)
    {
        context.Tasks().IisTasks()
            .CreateAppPoolTask("SomeAppPoolName")
            .ManagedRuntimeVersion("No Managed Code")
            .Mode(CreateApplicationPoolMode.DoNothingIfExists)
            .Execute(context);

        context.Tasks().IisTasks()
            .CreateWebsiteTask()
            .WebsiteName("SomeWebSiteName")
            .BindingProtocol("Http")
            .Port(2000)
            .PhysicalPath("SomePhysicalPath")
            .ApplicationPoolName("SomeAppPoolName")
            .WebsiteMode(CreateWebApplicationMode.DoNothingIfExists)
            .Execute(context);
    }

    private void UpdateFlubuCoreNugetPackageToLatest(ITaskContext context)
    {
        var fetchBuildVersionFromFileTask = context.Tasks().FetchBuildVersionFromFileTask();

        fetchBuildVersionFromFileTask.ProjectVersionFileName(@"..\FlubuCore.ProjectVersion.txt");
        var version = fetchBuildVersionFromFileTask.Execute(context);
        context.Tasks()
                .UpdateXmlFileTask("BuildScript.csproj")
                .UpdatePath("//DotNetCliToolReference[@Version]/@Version", version.Version.ToString(3))
                .Execute(context);
    }

    private void DoExample(ITaskContext context)
    {
        RestClient client = new RestClient();
        BuildScriptHelper.SomeMethod(); //// Just an a example that referencing other cs file works.

        ////Example of predefined propertie. Propertie are predefined by flubu.
        var osPlatform = context.Properties.Get<OSPlatform>(PredefinedBuildProperties.OsPlatform);

        if (osPlatform == OSPlatform.Windows)
        {
            context.LogInfo("Running on windows");
        }
        else if(osPlatform ==OSPlatform.Linux)
        {
            context.LogInfo("running on linux");
        }
    }

    private void DoExample2(ITaskContext context)
    {
        //// run 'dotnet flubu Rebuild -argName=SomeValue' to pass argument
        var example = context.ScriptArgs["argName"];
        if (string.IsNullOrEmpty(example))
        {
            example = "no vaule through script argument argName";
        }


        JsonConvert.SerializeObject(example);
		var client = new RestClient("http://example.com");
    }
}