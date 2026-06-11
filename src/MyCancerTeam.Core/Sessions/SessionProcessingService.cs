using System.Text;
using MyCancerTeam.Core.Agents;
using MyCancerTeam.Core.Configuration;
using MyCancerTeam.Core.Notes;
using MyCancerTeam.Core.Workflows;

namespace MyCancerTeam.Core.Sessions;

public sealed class SessionProcessingService
{
    private readonly INoteStore _noteStore;
    private readonly ITeamLeadAgent _teamLeadAgent;
    private readonly AppConfiguration _configuration;

    public SessionProcessingService(
        INoteStore noteStore,
        ITeamLeadAgent teamLeadAgent,
        AppConfiguration configuration)
    {
        _noteStore = noteStore;
        _teamLeadAgent = teamLeadAgent;
        _configuration = configuration;
    }

    public async Task<SessionProcessingResult> ProcessAsync(
        string input,
        string source,
        CancellationToken cancellationToken = default)
    {
        var sharedNotes = await _noteStore.ReadSharedNotesAsync(cancellationToken);
        var workflowType = InferWorkflowType(input);
        var request = new WorkflowRequest
        {
            WorkflowType = workflowType,
            UserInput = input
        };

        var response = await _teamLeadAgent.CoordinateAsync(request, sharedNotes, cancellationToken);

        var updatedNotes = BuildUpdatedNotes(sharedNotes, input, source, response);
        await _noteStore.WriteSharedNotesAsync(updatedNotes, cancellationToken);

        var summary = BuildSummary(input, source, response);
        await _noteStore.WriteSummaryAsync(summary, cancellationToken);

        return new SessionProcessingResult
        {
            Input = input,
            Source = source,
            WorkflowType = workflowType,
            Response = response,
            SharedNotesPath = _configuration.SharedNotesFilePath,
            SummaryPath = _configuration.SummaryFilePath
        };
    }

    private static string BuildSummary(string input, string source, AgentResponse response)
    {
        var summary = new StringBuilder();
        summary.AppendLine("# MyCancerTeam Summary");
        summary.AppendLine();
        summary.AppendLine($"_Last updated: {DateTimeOffset.UtcNow:O}_");
        summary.AppendLine();
        summary.AppendLine($"**Source:** {source}");
        summary.AppendLine($"**Input:** {input}");
        summary.AppendLine($"**Confidence:** {response.ConfidenceLevel:P0}");
        summary.AppendLine();
        summary.AppendLine("## Team Lead Summary");
        summary.AppendLine(response.Summary);

        if (response.OpenQuestions.Count > 0)
        {
            summary.AppendLine();
            summary.AppendLine("## Open Questions");
            foreach (var question in response.OpenQuestions)
            {
                summary.AppendLine($"- {question}");
            }
        }

        if (response.SuggestedClinicianQuestions.Count > 0)
        {
            summary.AppendLine();
            summary.AppendLine("## Suggested Clinician Questions");
            foreach (var question in response.SuggestedClinicianQuestions)
            {
                summary.AppendLine($"- {question}");
            }
        }

        return summary.ToString();
    }

    private static string BuildUpdatedNotes(string sharedNotes, string input, string source, AgentResponse response)
    {
        var updatedNotes = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(sharedNotes))
        {
            updatedNotes.AppendLine(sharedNotes.Trim());
            updatedNotes.AppendLine();
        }

        updatedNotes.AppendLine($"## Update {DateTimeOffset.UtcNow:O}");
        updatedNotes.AppendLine($"Source: {source}");
        updatedNotes.AppendLine($"User input: {input}");
        updatedNotes.AppendLine();
        updatedNotes.AppendLine("### Team Lead Summary");
        updatedNotes.AppendLine(response.Summary);

        if (response.OpenQuestions.Count > 0)
        {
            updatedNotes.AppendLine();
            updatedNotes.AppendLine("### Open Questions");
            foreach (var openQuestion in response.OpenQuestions)
            {
                updatedNotes.AppendLine($"- {openQuestion}");
            }
        }

        return updatedNotes.ToString();
    }

    private static WorkflowType InferWorkflowType(string input)
    {
        var normalized = input.ToLowerInvariant();
        var tokens = normalized.Split(
            [' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '/', '\\', '-', '_', '"', '\''],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        if (ContainsAnyToken(tokens, "travel", "visa", "transport"))
        {
            return WorkflowType.TravelAndPracticalSupport;
        }

        if (ContainsAnyToken(tokens, "imaging", "scan", "mri", "ct", "pet"))
        {
            return WorkflowType.ImagingReview;
        }

        if (ContainsAnyToken(tokens, "radiation", "fraction", "proton", "photon"))
        {
            return WorkflowType.RadiationPlanReview;
        }

        if (ContainsAnyToken(tokens, "chemo", "medication", "systemic"))
        {
            return WorkflowType.MedicationPlanReview;
        }

        if (ContainsAnyToken(tokens, "insurance", "claim", "reimburse"))
        {
            return WorkflowType.InsuranceAndFinancial;
        }

        if (ContainsAnyToken(tokens, "trial", "research", "evidence"))
        {
            return WorkflowType.ResearchMonitoring;
        }

        if (ContainsAnyToken(tokens, "international", "overseas") || normalized.Contains("second opinion"))
        {
            return WorkflowType.GlobalTreatmentAccess;
        }

        if (ContainsAnyToken(tokens, "symptom", "nausea", "fatigue", "pain"))
        {
            return WorkflowType.SymptomSupport;
        }

        if (ContainsAnyToken(tokens, "exercise", "fitness", "workout", "walking", "physio", "rehabilitation", "rehab")
            || normalized.Contains("physical activity"))
        {
            return WorkflowType.PhysicalFitness;
        }

        return WorkflowType.GeneralUpdate;
    }

    private static bool ContainsAnyToken(HashSet<string> tokens, params string[] candidates)
        => candidates.Any(tokens.Contains);
}
