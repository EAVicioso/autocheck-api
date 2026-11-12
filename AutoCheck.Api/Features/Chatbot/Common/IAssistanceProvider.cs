using System.Net.Http.Json;

namespace AutoCheck.Api.Features.Chatbot.Common;

public interface IAssistanceProvider
{
    Task<AssistanceResponse> Answer(string question, CancellationToken ct);
}
