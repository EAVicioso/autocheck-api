namespace AutoCheck.Api.Features.Auth.RestablecerContrasena;

public sealed class RestablecerContrasenaHandler(IIdentityProvider provider, PasswordResetRevocation revocation)
{
    public async Task Execute(ResetRequest request, string correlation, CancellationToken requestAborted)
    {
        var authId = await provider.ValidateRecovery(request.Token, requestAborted);
        var operationId = await revocation.Prepare(authId, correlation, requestAborted);
        // After the durable commit, client cancellation cannot abort security work.
        // The worker's lease exceeds both this budget and the finalization budget.
        using var externalTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var outcome = await ExecuteExternal(request, authId, externalTimeout.Token);
        using var finalizationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await revocation.Finish(operationId, outcome, finalizationTimeout.Token);
        ThrowIfNotCompleted(outcome);
    }

    private async Task<string> ExecuteExternal(ResetRequest request, Guid authId, CancellationToken ct)
    {
        try
        {
            var confirmed = await provider.Reset(request.Token, request.NuevaContrasena, ct);
            return confirmed == authId ? PasswordResetStates.Completed : PasswordResetStates.Uncertain;
        }
        catch (RecoveryRejectedException)
        {
            return PasswordResetStates.Rejected;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or ApiException or JsonException)
        {
            // Delivery outcome is unknown. Revoke conservatively; never replay secrets.
            return PasswordResetStates.Uncertain;
        }
    }

    private static void ThrowIfNotCompleted(string outcome)
    {
        if (outcome == PasswordResetStates.Rejected)
        {
            throw new ApiException(401, "RECUPERACION_INVALIDA", "El enlace o la contraseña de recuperación no son válidos.");
        }
        if (outcome == PasswordResetStates.Uncertain)
        {
            throw ApiContract.Unavailable();
        }
    }
}
