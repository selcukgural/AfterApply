using System.Globalization;
using System.Security.Claims;
using AfterApply.Api.Extensions;
using AfterApply.Application.Admin;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Payments;
using AfterApply.Application.Payments.Contracts;
using AfterApply.Infrastructure;
using AfterApply.Infrastructure.Payments;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace AfterApply.Api.Endpoints;

/// <summary>
/// PayTR iFrame payments for the Pro plan. Three surfaces with three different gates: the user's
/// checkout (signed in, and only while <c>PayTr:Enabled</c>), PayTR's notification (anonymous by
/// necessity — verified by HMAC, not by session — and gated only on the secrets being present so a
/// late notification is applied even after the feature is switched off), and the admin's payments
/// panel (admin, independent of every flag: money records stay readable).
/// </summary>
public static class PaymentEndpoints
{
    private const int DefaultHistoryTake = 20;

    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        MapUser(app);
        MapCallback(app);
        MapAdmin(app);
        return app;
    }

    private static void MapUser(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments").WithTags("Payments").RequireAuthorization()
            .WithDescription("Hidden behind PayTr:Enabled — every route 404s while the flag is off or the merchant secrets are missing.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.AddEndpointFilter(EnabledFilter);

        group.MapGet("/plans", async (ClaimsPrincipal user, IPaymentCheckoutService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetPlansAsync(user.GetUserId(), cancellationToken)))
            .WithSummary("The Pro plans on sale, their prices in kuruş, and the caller's current entitlement")
            .Produces<PaymentPlansResponse>();

        group.MapPost("/checkout", async (StartCheckoutRequest request, ClaimsPrincipal user, HttpContext httpContext,
                IPaymentCheckoutService service, CancellationToken cancellationToken) =>
            {
                var locale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                var response = await service.StartAsync(user.GetUserId(), request, httpContext.GetClientIpAddress(), locale, cancellationToken);
                httpContext.Response.Headers[HeaderNames.CacheControl] = "private, no-store";
                return Results.Ok(response);
            })
            .WithValidation<StartCheckoutRequest>()
            .RequireRateLimiting(DependencyInjection.PaymentCheckoutRateLimitPolicy)
            .WithSummary("Start a PayTR checkout: creates a pending order and returns the payment frame URL")
            .WithDescription("Step 1 of the PayTR iFrame API. The order is confirmed only by PayTR's notification, never by " +
                             "the page the user returns to. A pending order for the same plan with time left is resumed.")
            .Produces<CheckoutResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/billing-defaults", async (ClaimsPrincipal user, HttpContext httpContext, IPaymentCheckoutService service,
                CancellationToken cancellationToken) =>
            {
                httpContext.Response.Headers[HeaderNames.CacheControl] = "private, no-store";
                return Results.Ok(await service.GetBillingDefaultsAsync(user.GetUserId(), cancellationToken));
            })
            .WithSummary("The billing details from the caller's most recent order, as defaults for the checkout form")
            .Produces<BillingDefaultsResponse>();

        group.MapGet("/orders", async (int? take, ClaimsPrincipal user, IPaymentCheckoutService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.ListOrdersAsync(user.GetUserId(), take ?? DefaultHistoryTake, cancellationToken)))
            .WithSummary("The caller's payment history, newest first")
            .Produces<IReadOnlyList<PaymentOrderResponse>>();

        group.MapGet("/orders/{orderId:guid}", async (Guid orderId, ClaimsPrincipal user, HttpContext httpContext,
                IPaymentCheckoutService service, CancellationToken cancellationToken) =>
            {
                httpContext.Response.Headers[HeaderNames.CacheControl] = "private, no-store";
                var order = await service.GetOrderAsync(user.GetUserId(), orderId, cancellationToken);
                return order is null ? Results.NotFound() : Results.Ok(order);
            })
            .WithSummary("One of the caller's orders — what the checkout and result pages poll")
            .Produces<PaymentOrderResponse>();

        group.MapPost("/orders/{orderId:guid}/cancel", async (Guid orderId, ClaimsPrincipal user, IPaymentCheckoutService service,
                CancellationToken cancellationToken) =>
                await service.CancelAsync(user.GetUserId(), orderId, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .RequireRateLimiting(DependencyInjection.PaymentCheckoutRateLimitPolicy)
            .WithSummary("Close a pending order without paying")
            .WithDescription("Only a pending order can be cancelled. A notification of success that arrives afterwards is still honoured.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/orders/{orderId:guid}/refund-request", async (Guid orderId, RequestRefundRequest request, ClaimsPrincipal user,
                IPaymentRefundService service, CancellationToken cancellationToken) =>
            {
                var order = await service.RequestAsync(user.GetUserId(), orderId, request.Reason, cancellationToken);
                return order is null ? Results.NotFound() : Results.Ok(order);
            })
            .WithValidation<RequestRefundRequest>()
            .RequireRateLimiting(DependencyInjection.PaymentCheckoutRateLimitPolicy)
            .WithSummary("Ask for a refund of a paid order; an admin decides")
            .Produces<PaymentOrderResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static void MapCallback(IEndpointRouteBuilder app)
    {
        // PayTR posts application/x-www-form-urlencoded and wants the literal text "OK" back.
        // No auth (PayTR has no session), no antiforgery (no browser), no request-audit opt-out
        // (the middleware records PayTR's address; harmless). Not behind PayTr:Enabled on purpose.
        app.MapPost("/api/payments/paytr/callback", async (HttpContext httpContext, IPayTrCallbackService service,
                IOptions<PayTrOptions> options, CancellationToken cancellationToken) =>
            {
                if (!options.Value.IsConfigured)
                {
                    return Results.NotFound();
                }

                if (!httpContext.Request.HasFormContentType)
                {
                    return Results.Text("PAYTR notification failed: expected a form", "text/plain", statusCode: StatusCodes.Status400BadRequest);
                }

                var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                var fields = form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString(), StringComparer.Ordinal);
                var result = await service.HandleAsync(fields, cancellationToken);
                return Results.Text(result.Body, "text/plain", statusCode: result.StatusCode);
            })
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithTags("Payments")
            .WithSummary("PayTR's payment-result notification (Bildirim URL)")
            .WithDescription("Step 2 of the PayTR iFrame API. Verifies the HMAC, applies the result once per merchant_oid, " +
                             "answers the text OK. Anything but OK makes PayTR retry.")
            .Produces(StatusCodes.Status200OK, contentType: "text/plain")
            .Produces(StatusCodes.Status400BadRequest, contentType: "text/plain");
    }

    private static void MapAdmin(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/payments").WithTags("Admin").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        group.AddEndpointFilter(AdminFilter);

        group.MapGet("/summary", async (int? months, IPaymentAdminService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetSummaryAsync(months ?? 12, cancellationToken)))
            .WithSummary("Monthly sales, refunds and cancellations, this month first")
            .Produces<PaymentsSummaryResponse>();

        group.MapGet("/orders", async (string? status, string? month, string? q, int? page, int? pageSize, IPaymentAdminService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.ListOrdersAsync(new PaymentOrderListQuery(status, month, q, page ?? 1, pageSize ?? 25), cancellationToken)))
            .WithSummary("All orders, filterable by status, month (yyyy-MM) and e-mail/merchant_oid")
            .Produces<PagedResult<AdminPaymentOrderResponse>>();

        group.MapGet("/orders/{orderId:guid}", async (Guid orderId, IPaymentAdminService service, CancellationToken cancellationToken) =>
            {
                var detail = await service.GetOrderAsync(orderId, cancellationToken);
                return detail is null ? Results.NotFound() : Results.Ok(detail);
            })
            .WithSummary("One order with every notification PayTR sent for it and the user's entitlement")
            .Produces<AdminPaymentOrderDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/refund-requests", async (IPaymentAdminService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.ListRefundRequestsAsync(cancellationToken)))
            .WithSummary("Open refund requests, oldest first")
            .Produces<IReadOnlyList<AdminPaymentOrderResponse>>();

        group.MapGet("/alerts", async (int? days, string? outcome, string? status, bool? testMode, string? q, string? sort, string? dir,
                int? page, int? pageSize, IPaymentAdminService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAlertsAsync(
                    new PaymentAlertListQuery(days, outcome, status, testMode, q, sort, dir, page ?? 1, pageSize), cancellationToken)))
            .WithSummary("Rejected or late notifications (paged, ten by default) and amount mismatches")
            .WithDescription("Notifications filter by outcome, PayTR status, test mode and merchant_oid; sort by receivedAt " +
                             "(default, newest first), outcome, status or merchantOid with dir=asc|desc. Mismatches follow days only.")
            .Produces<PaymentAlertsResponse>();

        group.MapPost("/orders/{orderId:guid}/refund", async (Guid orderId, AdminRefundRequest request, ClaimsPrincipal user,
                IPaymentRefundService service, CancellationToken cancellationToken) =>
            {
                var order = await service.RefundAsync(user.GetUserId(), orderId, request.AmountMinor, cancellationToken);
                return order is null ? Results.NotFound() : Results.Ok(order);
            })
            .WithValidation<AdminRefundRequest>()
            .WithSummary("Refund through the PayTR refund API — the only way a refund is ever made")
            .WithDescription("Full when no amount is given. A full refund takes back the Pro period the order added. " +
                             "Never refund from the PayTR merchant panel: this system would not know.")
            .Produces<AdminPaymentOrderResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/orders/{orderId:guid}/reject-refund", async (Guid orderId, RejectRefundRequest request, ClaimsPrincipal user,
                IPaymentRefundService service, CancellationToken cancellationToken) =>
            {
                var order = await service.RejectAsync(user.GetUserId(), orderId, request.Note, cancellationToken);
                return order is null ? Results.NotFound() : Results.Ok(order);
            })
            .WithValidation<RejectRefundRequest>()
            .WithSummary("Turn a refund request down; the note is e-mailed to the user")
            .Produces<AdminPaymentOrderResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/orders/{orderId:guid}/mark-refunded", async (Guid orderId, MarkRefundedRequest request, ClaimsPrincipal user,
                IPaymentRefundService service, CancellationToken cancellationToken) =>
            {
                var order = await service.MarkRefundedAsync(user.GetUserId(), orderId, request.AmountMinor, request.ReferenceNo, cancellationToken);
                return order is null ? Results.NotFound() : Results.Ok(order);
            })
            .WithValidation<MarkRefundedRequest>()
            .WithSummary("Record a refund that PayTR already made (verified in the merchant panel) without calling PayTR")
            .Produces<AdminPaymentOrderResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/orders/{orderId:guid}/cancel", async (Guid orderId, ClaimsPrincipal user, IPaymentAdminService service,
                CancellationToken cancellationToken) =>
            {
                var order = await service.CancelAsync(user.GetUserId(), orderId, cancellationToken);
                return order is null ? Results.NotFound() : Results.Ok(order);
            })
            .WithSummary("Close a user's pending order")
            .Produces<AdminPaymentOrderResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async ValueTask<object?> EnabledFilter(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<PayTrOptions>>().Value;
        return options.Enabled && options.IsConfigured ? await next(context) : Results.NotFound();
    }

    private static async ValueTask<object?> AdminFilter(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var adminAccess = context.HttpContext.RequestServices.GetRequiredService<IAdminAccessService>();
        var isAdmin = await adminAccess.IsAdminAsync(context.HttpContext.User.GetUserId(), context.HttpContext.RequestAborted);
        return isAdmin ? await next(context) : Results.Forbid();
    }
}
