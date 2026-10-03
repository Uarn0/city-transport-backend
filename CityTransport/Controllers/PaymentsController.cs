using CityTransport.DTOs.Payments;
using CityTransport.Services;
using Microsoft.AspNetCore.Mvc;

namespace CityTransport.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost("webhook/monobank")]
    [ProducesResponseType(typeof(PaymentWebhookResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MonobankWebhook([FromBody] MonobankWebhookDto request)
    {
        _logger.LogInformation("Received Monobank Webhook for InvoiceId: {InvoiceId}, Status: {Status}, Reference: {Reference}",
            request.InvoiceId, request.Status, request.Reference);

        try
        {
            var result = await _paymentService.ProcessMonobankWebhookAsync(request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Order not found during webhook processing.");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation during webhook processing.");
            return BadRequest(new { message = ex.Message });
        }
    }
}