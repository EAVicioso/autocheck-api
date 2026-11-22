// AutoCheck / INTEC | SRS 1.1 | Source history: Git.

namespace AutoCheck.Api.Features.Auth.RestablecerContrasena;

public sealed class RestablecerContrasenaHandler(IIdentityProvider provider, IPasswordResetRevocation revocation)
{
    public async Task Execute(ResetRequest request, string correlation, CancellationToken requestAborted)
    {
        var proof = await provider.ValidateRecovery(request.Token, requestAborted);
        var operationId = await revocation.Prepare(proof.AuthId, correlation, requestAborted);
        // After the durable commit, client cancellation cannot abort security work.
        // The worker's lease exceeds both this budget and the finalization budget.
        using var externalTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var outcome = await ExecuteExternal(request with { Token = proof.AccessToken }, proof.AuthId, externalTimeout.Token);
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
            throw new ApiException(401, ApiErrorCode.RecuperacionInvalida, "El enlace o la contraseña de recuperación no son válidos.");
        }
        if (outcome == PasswordResetStates.Uncertain)
        {
            throw ApiContract.Unavailable();
        }
    }
}
