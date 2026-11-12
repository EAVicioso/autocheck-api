using System.Net.Http.Json;

namespace AutoCheck.Api.Features.Chatbot.Common;

public sealed record AssistanceResponse(string Respuesta, string Categoria, string Codigo);
