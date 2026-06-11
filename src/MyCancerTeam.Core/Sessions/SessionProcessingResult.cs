using MyCancerTeam.Core.Agents;
using MyCancerTeam.Core.Workflows;

namespace MyCancerTeam.Core.Sessions;

public sealed class SessionProcessingResult
{
    public required string Input { get; init; }
    public required string Source { get; init; }
    public required WorkflowType WorkflowType { get; init; }
    public required AgentResponse Response { get; init; }
    public required string SharedNotesPath { get; init; }
    public required string SummaryPath { get; init; }
}
