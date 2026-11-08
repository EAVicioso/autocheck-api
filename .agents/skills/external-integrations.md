# External integrations
1. Place reusable integration interfaces and adapters in shared `AutoCheck.Api/` infrastructure and orchestration in feature slices; inject clients/configuration and protect credentials.
2. For SendGrid appointment and notification email, configure sender identity, minimize personal data and handle failures; retry only with safe deduplication.
3. For Gemini FAQ, bound input/output and timeouts, treat prompts and output as untrusted, avoid sending secrets or unnecessary personal data, and handle unavailability safely.
4. For payment sandbox requests, validate amount and currency server-side, use idempotency and durable state, verify supported webhook signatures, and never store card data or treat a redirect as payment confirmation. Verify the selected provider contract.
5. Propagate cancellation, use bounded timeouts and retry only safe transient requests. Test success, timeouts, malformed responses and duplicate operations with fakes and isolated sandbox data.