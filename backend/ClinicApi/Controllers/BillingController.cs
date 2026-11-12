using ClinicApi.Data;
using ClinicApi.Dtos;
using ClinicApi.Models;
using ClinicApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicApi.Controllers;

[Route("api/[controller]"), Authorize(Roles = "Admin,Staff")]
public class BillingController(ClinicDbContext db, IClinicClock clock, IConfiguration cfg) : ApiBase
{
    [HttpGet("invoices")]
    public async Task<List<InvoiceListDto>> Invoices([FromQuery] InvoiceStatus? status, [FromQuery] string? search, [FromQuery] DateTime? date)
    {
        var q = db.Invoices.AsQueryable();
        if (status.HasValue) q = q.Where(i => i.Status == status.Value);
        if (date.HasValue)
        {
            var d = date.Value.Date;
            q = q.Where(i => i.CreatedAt >= d && i.CreatedAt < d.AddDays(1));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(i => i.InvoiceNo.Contains(s) || i.Patient.PatientNo.Contains(s)
                             || (i.Patient.FirstName + " " + i.Patient.LastName).Contains(s));
        }
        return await q.OrderByDescending(i => i.CreatedAt).Take(300)
            .Select(i => new InvoiceListDto(i.Id, i.InvoiceNo, i.PatientId, i.Patient.FirstName + " " + i.Patient.LastName,
                i.Patient.PatientNo, i.Doctor.User.FullName, i.Total, i.AmountPaid, i.Total - i.AmountPaid, i.Status, i.CreatedAt))
            .ToListAsync();
    }

    [HttpGet("invoices/{id:int}")]
    public async Task<ActionResult<InvoiceDetailDto>> Invoice(int id)
    {
        var i = await db.Invoices.AsNoTracking()
            .Include(x => x.Items).Include(x => x.Payments).Include(x => x.Patient).Include(x => x.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (i is null) return NotFound(new { message = "Invoice not found." });
        return new InvoiceDetailDto(i.Id, i.InvoiceNo, i.PatientId, $"{i.Patient.FirstName} {i.Patient.LastName}", i.Patient.PatientNo,
            i.Doctor.User.FullName, i.Total, i.AmountPaid, i.Total - i.AmountPaid, i.Status, i.CreatedAt,
            i.Items.Select(x => new InvoiceItemDto(x.Description, x.Quantity, x.UnitPrice, x.LineTotal)).ToList(),
            i.Payments.OrderBy(p => p.Id).Select(p => new PaymentSummaryDto(p.Id, p.ReceiptNo, p.PaidAt, p.AmountApplied, p.AmountTendered, p.ChangeGiven, p.Method)).ToList());
    }

    /// <summary>
    /// Collect a payment (algorithm 1.7). Less than the balance is recorded as a partial payment and the
    /// remaining balance is returned; the full balance or more marks the invoice Paid and works out the change.
    /// </summary>
    [HttpPost("invoices/{id:int}/pay")]
    public async Task<ActionResult<ReceiptDto>> Pay(int id, PayRequest req)
    {
        var invoice = await db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null) return NotFound(new { message = "Invoice not found." });

        var balance = invoice.Total - invoice.AmountPaid;
        if (balance <= 0) return Fail("This invoice is already paid.", 409);

        var tendered = Math.Round(req.Amount, 2);
        decimal applied, change;
        if (tendered >= balance) { applied = balance; change = tendered - balance; }
        else { applied = tendered; change = 0; }

        if (change > 0 && req.Method != PaymentMethod.Cash)
            return Fail($"Card and e-wallet payments cannot be more than the balance of {balance:N2}.");

        var now = clock.Now;
        invoice.AmountPaid += applied;
        invoice.Status = invoice.AmountPaid >= invoice.Total ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            AmountTendered = tendered,
            AmountApplied = applied,
            ChangeGiven = change,
            Method = req.Method,
            Reference = string.IsNullOrWhiteSpace(req.Reference) ? null : req.Reference.Trim(),
            ReceivedByUserId = CurrentUserId,
            PaidAt = now,
        };

        try
        {
            await db.AddWithNumberAsync(payment, (x, n) => x.ReceiptNo = n, x => $"RCT-{x.Id:D6}");
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail("Another cashier just updated this invoice. Reload it and try again.", 409);
        }
        return await BuildReceipt(payment.Id);
    }

    [HttpGet("payments/{id:int}/receipt")]
    public async Task<ActionResult<ReceiptDto>> Receipt(int id) => await BuildReceipt(id);

    private async Task<ActionResult<ReceiptDto>> BuildReceipt(int paymentId)
    {
        var p = await db.Payments.AsNoTracking()
            .Include(x => x.ReceivedBy)
            .Include(x => x.Invoice).ThenInclude(i => i.Items)
            .Include(x => x.Invoice).ThenInclude(i => i.Patient)
            .Include(x => x.Invoice).ThenInclude(i => i.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(x => x.Id == paymentId);
        if (p is null) return NotFound(new { message = "Receipt not found." });

        var inv = p.Invoice;
        var paidUpToThis = await db.Payments.Where(x => x.InvoiceId == inv.Id && x.Id <= p.Id).SumAsync(x => x.AmountApplied);
        var balanceAfter = inv.Total - paidUpToThis;

        return new ReceiptDto(
            cfg["Clinic:Name"] ?? "Clinic", cfg["Clinic:Address"] ?? "", p.ReceiptNo, p.PaidAt, inv.InvoiceNo,
            $"{inv.Patient.FirstName} {inv.Patient.LastName}", inv.Patient.PatientNo, inv.Doctor.User.FullName,
            inv.Items.Select(x => new InvoiceItemDto(x.Description, x.Quantity, x.UnitPrice, x.LineTotal)).ToList(),
            inv.Total, p.AmountTendered, p.AmountApplied, p.ChangeGiven, balanceAfter,
            balanceAfter <= 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid, p.Method, p.Reference, p.ReceivedBy.FullName);
    }
}
