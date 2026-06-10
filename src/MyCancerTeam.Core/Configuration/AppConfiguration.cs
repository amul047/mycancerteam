namespace MyCancerTeam.Core.Configuration;

public sealed class AppConfiguration
{
    public string EnvironmentName { get; set; } = "dev";
    public string AzureOpenAiEndpoint { get; set; } = string.Empty;
    public string AzureOpenAiDeployment { get; set; } = string.Empty;

    // Local working root (everything below lives here by default).
    public string LocalWorkingFolderPath { get; set; } = string.Empty;

    // Clinical notes: in-person team source of truth (imaging, reports, prescriptions/plans).
    public string ClinicalNotesFolderPath { get; set; } = string.Empty;
    public string ClinicalImagingFolderPath { get; set; } = string.Empty;
    public string ClinicalReportsFolderPath { get; set; } = string.Empty;
    public string ClinicalPlansFolderPath { get; set; } = string.Empty;

    // Non-clinical: administrative / financial documents (insurance, etc.).
    public string NonClinicalFolderPath { get; set; } = string.Empty;
    public string NonClinicalInsuranceFolderPath { get; set; } = string.Empty;

    // Research: AI / web-sourced material.
    public string ResearchFolderPath { get; set; } = string.Empty;
    public string ResearchCacheFolderPath { get; set; } = string.Empty;
    public string ResearchSummariesFolderPath { get; set; } = string.Empty;
    public string ResearchGlobalTreatmentSearchFolderPath { get; set; } = string.Empty;
    public string ResearchInternationalSecondOpinionsFolderPath { get; set; } = string.Empty;

    // Operational.
    public string DraftCommunicationsFolderPath { get; set; } = string.Empty;
    public string AgentMemoryFolderPath { get; set; } = string.Empty;
    public string IterationsFolderPath { get; set; } = string.Empty;

    // Tracking notes (running log, append-only).
    public string LatestSharedNotesPath { get; set; } = string.Empty;

    // Final case summary (regenerated every run).
    public string CaseSummaryPath { get; set; } = string.Empty;

    public string? DailyResearchRefreshSchedule { get; set; }
}
