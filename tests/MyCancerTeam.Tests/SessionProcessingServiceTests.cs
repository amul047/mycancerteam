using MyCancerTeam.Core.Agents;
using MyCancerTeam.Core.Configuration;
using MyCancerTeam.Core.Models;
using MyCancerTeam.Core.Notes;
using MyCancerTeam.Core.Sessions;
using MyCancerTeam.Core.Workflows;

namespace MyCancerTeam.Tests;

public sealed class SessionProcessingServiceTests
{
    [Fact]
    public async Task ProcessAsync_ShouldPersistSummaryAndSharedNotes()
    {
        var noteStore = new FakeNoteStore
        {
            SharedNotes = "Existing notes"
        };
        var teamLeadAgent = new FakeTeamLeadAgent();
        var configuration = new AppConfiguration
        {
            LocalWorkingFolderPath = "/tmp/local",
            OurNotesFolderPath = "/tmp/our-notes"
        };

        var service = new SessionProcessingService(noteStore, teamLeadAgent, configuration);

        var result = await service.ProcessAsync("Need radiation side effect questions", "Web UI");

        Assert.Equal(WorkflowType.RadiationPlanReview, result.WorkflowType);
        Assert.Equal(configuration.SharedNotesFilePath, result.SharedNotesPath);
        Assert.Equal(configuration.SummaryFilePath, result.SummaryPath);
        Assert.Contains("Team synthesis prepared.", result.Response.Summary);
        Assert.Contains("Source: Web UI", noteStore.SharedNotesWrites.Single());
        Assert.Contains("User input: Need radiation side effect questions", noteStore.SharedNotesWrites.Single());
        Assert.Contains("## Team Lead Summary", noteStore.SummaryWrites.Single());
        Assert.Contains("**Source:** Web UI", noteStore.SummaryWrites.Single());
        Assert.Equal("Existing notes", teamLeadAgent.SharedNotesSeen.Single());
        Assert.Equal(WorkflowType.RadiationPlanReview, teamLeadAgent.Requests.Single().WorkflowType);
    }

    [Fact]
    public async Task ProcessAsync_ShouldClassifyInsuranceInputs()
    {
        var service = new SessionProcessingService(
            new FakeNoteStore(),
            new FakeTeamLeadAgent(),
            new AppConfiguration
            {
                LocalWorkingFolderPath = "/tmp/local",
                OurNotesFolderPath = "/tmp/our-notes"
            });

        var result = await service.ProcessAsync("I need help with my insurance claim", "Interactive input");

        Assert.Equal(WorkflowType.InsuranceAndFinancial, result.WorkflowType);
    }

    private sealed class FakeNoteStore : INoteStore
    {
        public string SharedNotes { get; set; } = string.Empty;
        public List<string> SharedNotesWrites { get; } = [];
        public List<string> SummaryWrites { get; } = [];

        public Task<string> ReadSharedNotesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(SharedNotes);

        public Task WriteSharedNotesAsync(string content, CancellationToken cancellationToken = default)
        {
            SharedNotesWrites.Add(content);
            return Task.CompletedTask;
        }

        public Task WriteAgentNotesAsync(string agentFileName, string content, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task WriteSummaryAsync(string content, CancellationToken cancellationToken = default)
        {
            SummaryWrites.Add(content);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTeamLeadAgent : ITeamLeadAgent
    {
        public List<WorkflowRequest> Requests { get; } = [];
        public List<string> SharedNotesSeen { get; } = [];

        public AgentRole Role => AgentRole.TeamLead;
        public string Name => "Fake Team Lead";

        public bool CanHandle(AgentContext context) => true;

        public Task<AgentResponse> RespondAsync(AgentContext context, CancellationToken cancellationToken = default)
            => CoordinateAsync(context.WorkflowRequest, context.SharedNotes, cancellationToken);

        public Task<AgentResponse> CoordinateAsync(WorkflowRequest request, string sharedNotes, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            SharedNotesSeen.Add(sharedNotes);

            return Task.FromResult(new AgentResponse
            {
                Role = AgentRole.TeamLead,
                Summary = "Team synthesis prepared.",
                ConfidenceLevel = 0.6m,
                OpenQuestions = ["Need clinician confirmation"],
                SuggestedClinicianQuestions = ["What side effects matter most?"],
                Citations =
                [
                    new CitationMetadata
                    {
                        SourceName = "Sample",
                        Title = "Sample",
                        Url = "https://example.com"
                    }
                ]
            });
        }
    }
}
