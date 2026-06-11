using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using MyCancerTeam.Core.Configuration;
using MyCancerTeam.Core.Notes;
using MyCancerTeam.Core.Sessions;
using MyCancerTeam.Infrastructure.Notes;

namespace MyCancerTeam.App;

public sealed class SimpleWebUiHost
{
    private const string DefaultUrl = "http://127.0.0.1:5078";

    private readonly SessionProcessingService _sessionProcessingService;
    private readonly IFolderNoteScanner _scanner;
    private readonly AppConfiguration _configuration;

    public SimpleWebUiHost(
        SessionProcessingService sessionProcessingService,
        IFolderNoteScanner scanner,
        AppConfiguration configuration)
    {
        _sessionProcessingService = sessionProcessingService;
        _scanner = scanner;
        _configuration = configuration;
    }

    public async Task RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls(DefaultUrl);

        var app = builder.Build();

        app.MapGet("/", () => Results.Content(BuildPageHtml(), "text/html; charset=utf-8"));

        app.MapPost("/ask", async (QuestionRequest request, CancellationToken requestCancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Input))
            {
                return Results.BadRequest(new { error = "Please enter a question or update." });
            }

            try
            {
                var result = await _sessionProcessingService.ProcessAsync(
                    request.Input.Trim(),
                    "Web UI",
                    requestCancellationToken);

                return Results.Json(new
                {
                    summary = result.Response.Summary,
                    confidence = result.Response.ConfidenceLevel,
                    suggestedClinicianQuestions = result.Response.SuggestedClinicianQuestions,
                    openQuestions = result.Response.OpenQuestions,
                    summaryPath = result.SummaryPath,
                    sharedNotesPath = result.SharedNotesPath
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        Console.WriteLine($"MyCancerTeam web UI started at {DefaultUrl}");
        Console.WriteLine("Press Ctrl+C to stop the web server.");

        await app.RunAsync(cancellationToken);
    }

    private string BuildPageHtml()
    {
        var watchedFolders = string.Join(
            "",
            _scanner.WatchedFolders.Select(static folder => $"<li><code>{WebUtility.HtmlEncode(folder)}</code></li>"));

        var summaryPath = WebUtility.HtmlEncode(_configuration.SummaryFilePath);
        var sharedNotesPath = WebUtility.HtmlEncode(_configuration.SharedNotesFilePath);
        var pageDataJson = JsonSerializer.Serialize(new
        {
            summaryPath = _configuration.SummaryFilePath,
            sharedNotesPath = _configuration.SharedNotesFilePath
        });

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>MyCancerTeam</title>
    <style>
        :root {
            color-scheme: light;
            font-family: system-ui, sans-serif;
        }

        body {
            margin: 0;
            background: #f5f7fb;
            color: #1f2937;
        }

        main {
            max-width: 960px;
            margin: 0 auto;
            padding: 32px 20px 48px;
        }

        .grid {
            display: grid;
            gap: 20px;
            grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
        }

        .card {
            background: white;
            border-radius: 16px;
            padding: 20px;
            box-shadow: 0 10px 30px rgba(15, 23, 42, 0.08);
        }

        h1, h2, h3 {
            margin-top: 0;
        }

        textarea {
            width: 100%;
            min-height: 160px;
            resize: vertical;
            border-radius: 12px;
            border: 1px solid #cbd5e1;
            padding: 12px;
            font: inherit;
            box-sizing: border-box;
        }

        button {
            border: 0;
            border-radius: 999px;
            background: #2563eb;
            color: white;
            font: inherit;
            padding: 12px 18px;
            cursor: pointer;
        }

        button:disabled {
            opacity: 0.6;
            cursor: wait;
        }

        .meta {
            color: #475569;
            font-size: 0.95rem;
        }

        .status {
            min-height: 1.5rem;
            font-weight: 600;
        }

        pre {
            white-space: pre-wrap;
            word-break: break-word;
            background: #eff6ff;
            border-radius: 12px;
            padding: 16px;
        }

        ul {
            padding-left: 20px;
        }

        code {
            font-family: ui-monospace, monospace;
        }
    </style>
</head>
<body>
    <main>
        <h1>MyCancerTeam</h1>
        <p class="meta">A very simple local UI for asking the team lead a question and reviewing the latest summary.</p>

        <div class="grid">
            <section class="card">
                <h2>Ask a question</h2>
                <form id="ask-form">
                    <label for="input">Question or update</label>
                    <textarea id="input" name="input" placeholder="Example: What questions should I ask about radiation side effects?"></textarea>
                    <p>
                        <button id="submit-button" type="submit">Submit</button>
                    </p>
                </form>
                <div class="status" id="status"></div>
            </section>

            <section class="card">
                <h2>Local status</h2>
                <p><strong>Summary file:</strong> <code>{{summaryPath}}</code></p>
                <p><strong>Shared notes:</strong> <code>{{sharedNotesPath}}</code></p>
                <h3>Watched folders</h3>
                <ul>{{watchedFolders}}</ul>
            </section>
        </div>

        <section class="card" style="margin-top: 20px;">
            <h2>Team lead response</h2>
            <p class="meta">Confidence: <span id="confidence">—</span></p>
            <pre id="summary">No response yet.</pre>

            <h3>Suggested clinician questions</h3>
            <ul id="suggested-questions">
                <li>No questions yet.</li>
            </ul>

            <h3>Open questions</h3>
            <ul id="open-questions">
                <li>No open questions yet.</li>
            </ul>
        </section>
    </main>

    <script>
        const form = document.getElementById('ask-form');
        const input = document.getElementById('input');
        const status = document.getElementById('status');
        const summary = document.getElementById('summary');
        const confidence = document.getElementById('confidence');
        const submitButton = document.getElementById('submit-button');
        const suggestedQuestions = document.getElementById('suggested-questions');
        const openQuestions = document.getElementById('open-questions');
        const pageData = {{pageDataJson}};

        function renderList(element, items, emptyText) {
            element.innerHTML = '';

            if (!items || items.length === 0) {
                const li = document.createElement('li');
                li.textContent = emptyText;
                element.appendChild(li);
                return;
            }

            for (const item of items) {
                const li = document.createElement('li');
                li.textContent = item;
                element.appendChild(li);
            }
        }

        form.addEventListener('submit', async (event) => {
            event.preventDefault();

            const value = input.value.trim();
            if (!value) {
                status.textContent = 'Please enter a question or update.';
                return;
            }

            submitButton.disabled = true;
            status.textContent = 'Submitting...';

            try {
                const response = await fetch('/ask', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ input: value })
                });

                const payload = await response.json();
                if (!response.ok) {
                    throw new Error(payload.error || 'Request failed.');
                }

                summary.textContent = payload.summary;
                confidence.textContent = `${Math.round(payload.confidence * 100)}%`;
                renderList(suggestedQuestions, payload.suggestedClinicianQuestions, 'No suggested clinician questions.');
                renderList(openQuestions, payload.openQuestions, 'No open questions.');
                status.textContent = `Saved latest output to ${pageData.summaryPath}.`;
            } catch (error) {
                status.textContent = error.message;
            } finally {
                submitButton.disabled = false;
            }
        });
    </script>
</body>
</html>
""";
    }

    private sealed record QuestionRequest(string Input);
}
