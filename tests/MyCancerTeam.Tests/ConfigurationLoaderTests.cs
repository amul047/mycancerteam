using MyCancerTeam.Infrastructure.Configuration;

namespace MyCancerTeam.Tests;

public sealed class ConfigurationLoaderTests
{
    [Fact]
    public void Load_ShouldApplyEnvironmentOverridesAndDefaults()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mycancerteam-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "config", "environments", "dev"));

        File.WriteAllText(
            Path.Combine(root, "config", "environments", "dev", "appsettings.json"),
            """
            {
              "AzureOpenAiEndpoint": "https://from-config.openai.azure.com/",
              "AzureOpenAiDeployment": "config-deployment"
            }
            """);

        var previousEnvironment = Environment.GetEnvironmentVariable("MYCANCERTEAM_ENVIRONMENT");
        var previousDeployment = Environment.GetEnvironmentVariable("MYCANCERTEAM_AZURE_OPENAI_DEPLOYMENT");

        Environment.SetEnvironmentVariable("MYCANCERTEAM_ENVIRONMENT", "dev");
        Environment.SetEnvironmentVariable("MYCANCERTEAM_AZURE_OPENAI_DEPLOYMENT", "env-deployment");

        try
        {
            var loader = new ConfigurationLoader();
            var config = loader.Load(root);

            Assert.Equal("https://from-config.openai.azure.com/", config.AzureOpenAiEndpoint);
            Assert.Equal("env-deployment", config.AzureOpenAiDeployment);
            Assert.True(Path.IsPathRooted(config.LocalWorkingFolderPath));

            Assert.EndsWith(Path.Combine(".local", "notes", "notes.md"), config.LatestSharedNotesPath);
            Assert.EndsWith(Path.Combine(".local", "case-summary.md"), config.CaseSummaryPath);

            Assert.EndsWith(Path.Combine(".local", "clinical-notes"), config.ClinicalNotesFolderPath);
            Assert.EndsWith(Path.Combine(".local", "clinical-notes", "imaging"), config.ClinicalImagingFolderPath);
            Assert.EndsWith(Path.Combine(".local", "clinical-notes", "reports"), config.ClinicalReportsFolderPath);
            Assert.EndsWith(Path.Combine(".local", "clinical-notes", "plans"), config.ClinicalPlansFolderPath);

            Assert.EndsWith(Path.Combine(".local", "non-clinical"), config.NonClinicalFolderPath);
            Assert.EndsWith(Path.Combine(".local", "non-clinical", "insurance"), config.NonClinicalInsuranceFolderPath);

            Assert.EndsWith(Path.Combine(".local", "research"), config.ResearchFolderPath);
            Assert.EndsWith(Path.Combine(".local", "research", "cache"), config.ResearchCacheFolderPath);
            Assert.EndsWith(Path.Combine(".local", "research", "summaries"), config.ResearchSummariesFolderPath);
            Assert.EndsWith(Path.Combine(".local", "research", "global-treatment-search"), config.ResearchGlobalTreatmentSearchFolderPath);
            Assert.EndsWith(Path.Combine(".local", "research", "international-second-opinions"), config.ResearchInternationalSecondOpinionsFolderPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MYCANCERTEAM_ENVIRONMENT", previousEnvironment);
            Environment.SetEnvironmentVariable("MYCANCERTEAM_AZURE_OPENAI_DEPLOYMENT", previousDeployment);
            Directory.Delete(root, true);
        }
    }
}
