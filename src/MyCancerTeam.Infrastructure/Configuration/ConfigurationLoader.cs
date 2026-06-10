using System.Text.Json;
using MyCancerTeam.Core.Configuration;

namespace MyCancerTeam.Infrastructure.Configuration;

public sealed class ConfigurationLoader
{
    public AppConfiguration Load(string repositoryRootPath)
    {
        var environment = Environment.GetEnvironmentVariable("MYCANCERTEAM_ENVIRONMENT") ?? "dev";
        var configPath = Path.Combine(repositoryRootPath, "config", "environments", environment, "appsettings.json");

        AppConfiguration configuration = new()
        {
            EnvironmentName = environment
        };

        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            var parsed = JsonSerializer.Deserialize<AppConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed is not null)
            {
                configuration = parsed;
                configuration.EnvironmentName = environment;
            }
        }

        ApplyEnvironmentOverrides(configuration);
        ApplyPathDefaults(configuration, repositoryRootPath);

        return configuration;
    }

    public void EnsureLocalDirectories(AppConfiguration configuration)
    {
        foreach (var folder in GetAllLocalDirectories(configuration))
        {
            Directory.CreateDirectory(folder);
        }
    }

    private static IReadOnlyList<string> GetAllLocalDirectories(AppConfiguration configuration) =>
    [
        configuration.LocalWorkingFolderPath,
        configuration.IterationsFolderPath,
        configuration.ClinicalNotesFolderPath,
        configuration.ClinicalImagingFolderPath,
        configuration.ClinicalReportsFolderPath,
        configuration.ClinicalPlansFolderPath,
        configuration.NonClinicalFolderPath,
        configuration.NonClinicalInsuranceFolderPath,
        configuration.ResearchFolderPath,
        configuration.ResearchCacheFolderPath,
        configuration.ResearchSummariesFolderPath,
        configuration.ResearchGlobalTreatmentSearchFolderPath,
        configuration.ResearchInternationalSecondOpinionsFolderPath,
        configuration.DraftCommunicationsFolderPath,
        configuration.AgentMemoryFolderPath,
        Path.GetDirectoryName(configuration.LatestSharedNotesPath) ?? configuration.LocalWorkingFolderPath,
        Path.GetDirectoryName(configuration.CaseSummaryPath) ?? configuration.LocalWorkingFolderPath,
        Path.Combine(configuration.DraftCommunicationsFolderPath, "emails"),
        Path.Combine(configuration.DraftCommunicationsFolderPath, "insurance"),
        Path.Combine(configuration.DraftCommunicationsFolderPath, "second-opinions"),
        Path.Combine(configuration.DraftCommunicationsFolderPath, "trials")
    ];

    private static void ApplyEnvironmentOverrides(AppConfiguration configuration)
    {
        configuration.AzureOpenAiEndpoint = Get("MYCANCERTEAM_AZURE_OPENAI_ENDPOINT", configuration.AzureOpenAiEndpoint);
        configuration.AzureOpenAiDeployment = Get("MYCANCERTEAM_AZURE_OPENAI_DEPLOYMENT", configuration.AzureOpenAiDeployment);
        configuration.LocalWorkingFolderPath = Get("MYCANCERTEAM_LOCAL_WORKING_FOLDER", configuration.LocalWorkingFolderPath);
        configuration.IterationsFolderPath = Get("MYCANCERTEAM_ITERATIONS_FOLDER", configuration.IterationsFolderPath);

        configuration.ClinicalNotesFolderPath = Get("MYCANCERTEAM_CLINICAL_NOTES_FOLDER", configuration.ClinicalNotesFolderPath);
        configuration.ClinicalImagingFolderPath = Get("MYCANCERTEAM_CLINICAL_IMAGING_FOLDER", configuration.ClinicalImagingFolderPath);
        configuration.ClinicalReportsFolderPath = Get("MYCANCERTEAM_CLINICAL_REPORTS_FOLDER", configuration.ClinicalReportsFolderPath);
        configuration.ClinicalPlansFolderPath = Get("MYCANCERTEAM_CLINICAL_PLANS_FOLDER", configuration.ClinicalPlansFolderPath);

        configuration.NonClinicalFolderPath = Get("MYCANCERTEAM_NON_CLINICAL_FOLDER", configuration.NonClinicalFolderPath);
        configuration.NonClinicalInsuranceFolderPath = Get("MYCANCERTEAM_NON_CLINICAL_INSURANCE_FOLDER", configuration.NonClinicalInsuranceFolderPath);

        configuration.ResearchFolderPath = Get("MYCANCERTEAM_RESEARCH_FOLDER", configuration.ResearchFolderPath);
        configuration.ResearchCacheFolderPath = Get("MYCANCERTEAM_RESEARCH_CACHE_FOLDER", configuration.ResearchCacheFolderPath);
        configuration.ResearchSummariesFolderPath = Get("MYCANCERTEAM_RESEARCH_SUMMARIES_FOLDER", configuration.ResearchSummariesFolderPath);
        configuration.ResearchGlobalTreatmentSearchFolderPath = Get("MYCANCERTEAM_RESEARCH_GLOBAL_TREATMENT_SEARCH_FOLDER", configuration.ResearchGlobalTreatmentSearchFolderPath);
        configuration.ResearchInternationalSecondOpinionsFolderPath = Get("MYCANCERTEAM_RESEARCH_INTL_SECOND_OPINIONS_FOLDER", configuration.ResearchInternationalSecondOpinionsFolderPath);

        configuration.DraftCommunicationsFolderPath = Get("MYCANCERTEAM_DRAFTS_FOLDER", configuration.DraftCommunicationsFolderPath);
        configuration.AgentMemoryFolderPath = Get("MYCANCERTEAM_AGENT_MEMORY_FOLDER", configuration.AgentMemoryFolderPath);
        configuration.LatestSharedNotesPath = Get("MYCANCERTEAM_LATEST_SHARED_NOTES_PATH", configuration.LatestSharedNotesPath);
        configuration.CaseSummaryPath = Get("MYCANCERTEAM_CASE_SUMMARY_PATH", configuration.CaseSummaryPath);
        configuration.DailyResearchRefreshSchedule = GetNullable("MYCANCERTEAM_DAILY_RESEARCH_REFRESH_SCHEDULE", configuration.DailyResearchRefreshSchedule);

        static string Get(string key, string fallback)
            => Environment.GetEnvironmentVariable(key) ?? fallback;

        static string? GetNullable(string key, string? fallback)
            => Environment.GetEnvironmentVariable(key) ?? fallback;
    }

    private static void ApplyPathDefaults(AppConfiguration configuration, string rootPath)
    {
        var localRoot = ToAbsolute(configuration.LocalWorkingFolderPath, rootPath, ".local");
        configuration.LocalWorkingFolderPath = localRoot;
        configuration.IterationsFolderPath = ToAbsolute(configuration.IterationsFolderPath, rootPath, Path.Combine(localRoot, "iterations"));

        var clinicalRoot = ToAbsolute(configuration.ClinicalNotesFolderPath, rootPath, Path.Combine(localRoot, "clinical-notes"));
        configuration.ClinicalNotesFolderPath = clinicalRoot;
        configuration.ClinicalImagingFolderPath = ToAbsolute(configuration.ClinicalImagingFolderPath, rootPath, Path.Combine(clinicalRoot, "imaging"));
        configuration.ClinicalReportsFolderPath = ToAbsolute(configuration.ClinicalReportsFolderPath, rootPath, Path.Combine(clinicalRoot, "reports"));
        configuration.ClinicalPlansFolderPath = ToAbsolute(configuration.ClinicalPlansFolderPath, rootPath, Path.Combine(clinicalRoot, "plans"));

        var nonClinicalRoot = ToAbsolute(configuration.NonClinicalFolderPath, rootPath, Path.Combine(localRoot, "non-clinical"));
        configuration.NonClinicalFolderPath = nonClinicalRoot;
        configuration.NonClinicalInsuranceFolderPath = ToAbsolute(configuration.NonClinicalInsuranceFolderPath, rootPath, Path.Combine(nonClinicalRoot, "insurance"));

        var researchRoot = ToAbsolute(configuration.ResearchFolderPath, rootPath, Path.Combine(localRoot, "research"));
        configuration.ResearchFolderPath = researchRoot;
        configuration.ResearchCacheFolderPath = ToAbsolute(configuration.ResearchCacheFolderPath, rootPath, Path.Combine(researchRoot, "cache"));
        configuration.ResearchSummariesFolderPath = ToAbsolute(configuration.ResearchSummariesFolderPath, rootPath, Path.Combine(researchRoot, "summaries"));
        configuration.ResearchGlobalTreatmentSearchFolderPath = ToAbsolute(configuration.ResearchGlobalTreatmentSearchFolderPath, rootPath, Path.Combine(researchRoot, "global-treatment-search"));
        configuration.ResearchInternationalSecondOpinionsFolderPath = ToAbsolute(configuration.ResearchInternationalSecondOpinionsFolderPath, rootPath, Path.Combine(researchRoot, "international-second-opinions"));

        configuration.DraftCommunicationsFolderPath = ToAbsolute(configuration.DraftCommunicationsFolderPath, rootPath, Path.Combine(localRoot, "drafts"));
        configuration.AgentMemoryFolderPath = ToAbsolute(configuration.AgentMemoryFolderPath, rootPath, Path.Combine(localRoot, "agent-memory"));
        configuration.LatestSharedNotesPath = ToAbsolute(configuration.LatestSharedNotesPath, rootPath, Path.Combine(localRoot, "notes", "notes.md"));
        configuration.CaseSummaryPath = ToAbsolute(configuration.CaseSummaryPath, rootPath, Path.Combine(localRoot, "case-summary.md"));
    }

    private static string ToAbsolute(string value, string rootPath, string fallback)
    {
        var selected = string.IsNullOrWhiteSpace(value) ? fallback : value;

        if (Path.IsPathRooted(selected))
        {
            return selected;
        }

        return Path.GetFullPath(Path.Combine(rootPath, selected));
    }
}
