using System.Threading.Channels;
using MyCancerTeam.Core.Agents;
using MyCancerTeam.Core.Drafts;
using MyCancerTeam.Core.Notes;
using MyCancerTeam.Core.Sessions;
using MyCancerTeam.Infrastructure.Notes;

namespace MyCancerTeam.App;

/// <summary>
/// Runs the application as a continuous, concurrent loop: an interactive prompt and a
/// background folder watcher both feed a single sequential processing pipeline.
/// </summary>
public sealed class InteractiveSessionHost
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);

    private readonly IDraftCommunicationService _draftService;
    private readonly IFolderNoteScanner _scanner;
    private readonly SessionProcessingService _sessionProcessingService;
    private readonly TimeSpan _pollInterval;
    private readonly object _consoleLock = new();

    public InteractiveSessionHost(
        IDraftCommunicationService draftService,
        IFolderNoteScanner scanner,
        SessionProcessingService sessionProcessingService,
        TimeSpan? pollInterval = null)
    {
        _draftService = draftService;
        _scanner = scanner;
        _sessionProcessingService = sessionProcessingService;
        _pollInterval = pollInterval ?? DefaultPollInterval;
    }

    public async Task RunAsync(CancellationTokenSource cancellationSource, string? initialInput = null)
    {
        var channel = Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true
        });

        PrintWelcome();

        var existing = _scanner.MarkExistingNotesAsSeen();
        Log($"Watching {_scanner.WatchedFolders.Count} folder(s) for new notes. {existing} existing note file(s) ignored; only newly added notes are processed.");

        if (!string.IsNullOrWhiteSpace(initialInput))
        {
            await channel.Writer.WriteAsync(
                new WorkItem(WorkItemKind.UserQuery, initialInput.Trim(), "Command-line input"),
                cancellationSource.Token);
        }

        PrintPrompt();

        var inputTask = Task.Run(() => ReadUserInputAsync(channel.Writer, cancellationSource));
        var watchTask = Task.Run(() => WatchFoldersAsync(channel.Writer, cancellationSource.Token));

        try
        {
            await ConsumeAsync(channel.Reader, cancellationSource.Token);
        }
        finally
        {
            channel.Writer.TryComplete();
        }

        // The watcher observes cancellation cooperatively and returns promptly.
        await watchTask;

        // The input reader may be parked on a blocking Console.ReadLine(); do not hang
        // shutdown waiting for the next keystroke.
        await Task.WhenAny(inputTask, Task.Delay(TimeSpan.FromMilliseconds(250)));

        Log("Session ended.");
    }

    private async Task ReadUserInputAsync(ChannelWriter<WorkItem> writer, CancellationTokenSource cancellationSource)
    {
        try
        {
            while (!cancellationSource.IsCancellationRequested)
            {
                var line = await Task.Run(Console.ReadLine);
                if (cancellationSource.IsCancellationRequested)
                {
                    break;
                }

                if (line is null)
                {
                    // End of the input stream (Ctrl+Z or redirected input finished).
                    cancellationSource.Cancel();
                    break;
                }

                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    PrintPrompt();
                    continue;
                }

                if (IsExitCommand(trimmed))
                {
                    cancellationSource.Cancel();
                    break;
                }

                if (string.Equals(trimmed, "help", StringComparison.OrdinalIgnoreCase))
                {
                    PrintHelp();
                    PrintPrompt();
                    continue;
                }

                if (string.Equals(trimmed, "draft", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleDraftAsync(cancellationSource.Token);
                    PrintPrompt();
                    continue;
                }

                await writer.WriteAsync(
                    new WorkItem(WorkItemKind.UserQuery, trimmed, "Interactive input"),
                    cancellationSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task WatchFoldersAsync(ChannelWriter<WorkItem> writer, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var newNotes = await _scanner.ScanForNewNotesAsync(cancellationToken);
                foreach (var note in newNotes)
                {
                    if (note.RequiresOcr)
                    {
                        Log($"Skipped note with no extractable text (looks scanned/image-only; OCR not yet supported): {note.FilePath}");
                        continue;
                    }

                    Log($"New note detected: {note.FilePath}");
                    await writer.WriteAsync(
                        new WorkItem(WorkItemKind.FileNote, note.Content, $"File: {note.FilePath}"),
                        cancellationToken);
                }

                await Task.Delay(_pollInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ChannelClosedException)
        {
        }
    }

    private async Task ConsumeAsync(ChannelReader<WorkItem> reader, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in reader.ReadAllAsync(cancellationToken))
            {
                await ProcessAsync(item, cancellationToken);
                PrintPrompt();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ProcessAsync(WorkItem item, CancellationToken cancellationToken)
    {
        var result = await _sessionProcessingService.ProcessAsync(item.Input, item.Source, cancellationToken);
        PrintResponse(item, result.Response);
        Log($"Shared notes updated at: {result.SharedNotesPath}");
        Log($"Summary updated at: {result.SummaryPath}");
    }

    private async Task HandleDraftAsync(CancellationToken cancellationToken)
    {
        var type = Prompt("Draft type (emails/insurance/second-opinions/trials): ");
        var recipient = Prompt("Recipient type (clinician/hospital/insurer/trial-coordinator/etc): ");
        var subject = Prompt("Subject: ");
        var context = Prompt("Patient context summary: ");
        var details = Prompt("Additional details (optional): ");

        var result = await _draftService.CreateDraftAsync(new DraftCommunicationRequest
        {
            DraftType = string.IsNullOrWhiteSpace(type) ? "emails" : type,
            RecipientType = recipient,
            Subject = subject,
            PatientContextSummary = context,
            AdditionalDetails = details
        }, cancellationToken);

        Log($"Draft created: {result.FilePath}");
    }

    private void PrintResponse(WorkItem item, AgentResponse response)
    {
        lock (_consoleLock)
        {
            Console.WriteLine();
            Console.WriteLine($"=== Team Lead Summary ({item.Source}) ===");
            Console.WriteLine(response.Summary);
            Console.WriteLine($"Confidence: {response.ConfidenceLevel:P0}");

            if (response.SuggestedClinicianQuestions.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Suggested Clinician Questions:");
                foreach (var question in response.SuggestedClinicianQuestions)
                {
                    Console.WriteLine($"- {question}");
                }
            }
        }
    }

    private static bool IsExitCommand(string input)
        => string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase)
           || string.Equals(input, "quit", StringComparison.OrdinalIgnoreCase);

    private string Prompt(string prompt)
    {
        lock (_consoleLock)
        {
            Console.Write(prompt);
        }

        return Console.ReadLine() ?? string.Empty;
    }

    private void Log(string message)
    {
        lock (_consoleLock)
        {
            Console.WriteLine(message);
        }
    }

    private void PrintPrompt()
    {
        lock (_consoleLock)
        {
            Console.Write("> ");
        }
    }

    private void PrintWelcome()
    {
        lock (_consoleLock)
        {
            Console.WriteLine("MyCancerTeam interactive session started.");
            Console.WriteLine("Type a short update/question and press Enter, or use a command:");
            Console.WriteLine("  draft        - create a communication draft");
            Console.WriteLine("  help         - show available commands");
            Console.WriteLine("  exit / quit  - stop the session (Ctrl+C also works)");
            Console.WriteLine("New note files dropped into the watched folders are processed automatically.");
        }
    }

    private void PrintHelp()
    {
        lock (_consoleLock)
        {
            Console.WriteLine("Commands:");
            Console.WriteLine("  <text>       - send an update/question to the team lead");
            Console.WriteLine("  draft        - create a communication draft");
            Console.WriteLine("  help         - show this help");
            Console.WriteLine("  exit / quit  - stop the session");
            Console.WriteLine("Watched folders:");
            foreach (var folder in _scanner.WatchedFolders)
            {
                Console.WriteLine($"  - {folder}");
            }
        }
    }

    private enum WorkItemKind
    {
        UserQuery,
        FileNote
    }

    private sealed record WorkItem(WorkItemKind Kind, string Input, string Source);
}
