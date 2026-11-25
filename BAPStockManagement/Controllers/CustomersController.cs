using BAPStockManagement.Constants;
using BAPStockManagement.Models;
using BAPStockManagement.Services;
using BAPStockManagement.ViewModels.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BAPStockManagement.Controllers;

[Authorize(Roles = AppRoles.StockView)]
public class CustomersController : Controller
{
    private readonly BAPStockContext _context;

    public CustomersController(BAPStockContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? search, bool showInactive = false)
    {
        var query = _context.Customers.AsNoTracking().AsQueryable();

        if (!showInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c =>
                c.Name.Contains(term) ||
                c.CustomerCode.ToString().Contains(term) ||
                (c.District != null && c.District.Contains(term)) ||
                (c.Province != null && c.Province.Contains(term)) ||
                (c.Phone1 != null && c.Phone1.Contains(term)));
        }

        var items = await query
            .OrderBy(c => c.IsActive ? 0 : 1)
            .ThenBy(c => c.CustomerCode)
            .Select(c => new CustomerListItemViewModel
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                Name = c.Name,
                District = c.District,
                Province = c.Province,
                Phone1 = c.Phone1,
                SalesZone = c.SalesZone,
                IsActive = c.IsActive
            })
            .Take(500)
            .ToListAsync();

        return View(new CustomerIndexViewModel
        {
            Search = search,
            ShowInactive = showInactive,
            Items = items
        });
    }

    [Authorize(Roles = AppRoles.AdminManage)]
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CustomerFormViewModel());
    }

    [Authorize(Roles = AppRoles.AdminManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var codeExists = await _context.Customers.AnyAsync(c => c.CustomerCode == model.CustomerCode);
        if (codeExists)
        {
            ModelState.AddModelError(nameof(model.CustomerCode), "รหัสลูกค้านี้มีอยู่แล้ว");
            return View(model);
        }

        var now = DateTime.Now;
        var customer = MapToEntity(model, new Customer
        {
            CreatedAt = now,
            UpdatedAt = now
        });

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"เพิ่มลูกค้า {customer.Name} สำเร็จ";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = AppRoles.AdminManage)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            return NotFound();
        }

        return View(MapToForm(customer));
    }

    [Authorize(Roles = AppRoles.AdminManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CustomerFormViewModel model)
    {
        if (!model.CustomerId.HasValue)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var customer = await _context.Customers.FindAsync(model.CustomerId.Value);
        if (customer == null)
        {
            return NotFound();
        }

        var codeExists = await _context.Customers.AnyAsync(c =>
            c.CustomerId != model.CustomerId.Value &&
            c.CustomerCode == model.CustomerCode);
        if (codeExists)
        {
            ModelState.AddModelError(nameof(model.CustomerCode), "รหัสลูกค้านี้มีอยู่แล้ว");
            return View(model);
        }

        MapToEntity(model, customer);
        customer.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        TempData["Success"] = $"บันทึกลูกค้า {customer.Name} สำเร็จ";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = AppRoles.AdminManage)]
    [HttpGet]
    public IActionResult Import()
    {
        return View(new CustomerImportViewModel());
    }

    [Authorize(Roles = AppRoles.AdminManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(CustomerImportViewModel model, [FromForm] string? operation = null)
    {
        var save = operation == "import";
        if (model.File == null || model.File.Length == 0)
        {
            ModelState.AddModelError(nameof(model.File), "กรุณาเลือกไฟล์ Excel");
            return View(model);
        }

        var ext = Path.GetExtension(model.File.FileName).ToLowerInvariant();
        if (ext is not ".xlsx" and not ".xlsm")
        {
            ModelState.AddModelError(nameof(model.File), "รองรับเฉพาะไฟล์ .xlsx");
            return View(model);
        }

        CustomerExcelReader.Result result;
        int inserted;
        int updated;
        try
        {
            await using var stream = model.File.OpenReadStream();
            result = CustomerExcelReader.Read(stream, HttpContext.RequestAborted);
            if (!save)
            {
                model.Preview = new CustomerImportPreview(result.Customers.Count, DescribeImportIssues(result));
                return View(model);
            }

            if (result.Customers.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "ไม่พบรายการลูกค้าที่นำเข้าได้");
                var issues = DescribeImportIssues(result);
                if (issues.Length > 0)
                {
                    ModelState.AddModelError(string.Empty, issues);
                }
                return View(model);
            }

            (inserted, updated) = await SaveImportedCustomersAsync(result.Customers, HttpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var action = save ? "นำเข้าไฟล์" : "ตรวจไฟล์";
            ModelState.AddModelError(string.Empty, $"{action}ไม่สำเร็จ: {ex.Message}");
            return View(model);
        }

        TempData["Success"] = $"นำเข้าลูกค้าสำเร็จ: เพิ่ม {inserted} รายการ, อัปเดต {updated} รายการ";
        TempData["CustomerImportIssues"] = DescribeImportIssues(result);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Lookup(int id)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .Where(c => c.CustomerId == id && c.IsActive)
            .Select(c => new
            {
                c.CustomerId,
                c.CustomerCode,
                c.Name,
                c.Address,
                c.District,
                c.Province,
                c.Phone1,
                c.Phone2,
                c.SalesZone,
                c.ShippingInfo
            })
            .FirstOrDefaultAsync();

        if (customer == null)
        {
            return NotFound();
        }

        return Json(customer);
    }

    [HttpGet]
    public async Task<IActionResult> Search(string? q)
    {
        var term = (q ?? string.Empty).Trim();
        var query = _context.Customers.AsNoTracking().Where(c => c.IsActive);

        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(c =>
                c.Name.Contains(term) ||
                c.CustomerCode.ToString().Contains(term));
        }

        var items = await query
            .OrderBy(c => c.Name)
            .Take(30)
            .Select(c => new
            {
                c.CustomerId,
                c.CustomerCode,
                c.Name,
                c.District,
                c.Province
            })
            .ToListAsync();

        return Json(items);
    }

    private async Task<(int Inserted, int Updated)> SaveImportedCustomersAsync(
        IReadOnlyCollection<Customer> customers, CancellationToken cancellationToken)
    {
        var existing = await _context.Customers.ToDictionaryAsync(c => c.CustomerCode, cancellationToken);
        var inserted = 0;
        var updated = 0;
        var now = DateTime.Now;
        foreach (var imported in customers)
        {
            imported.UpdatedAt = now;
            if (!existing.TryGetValue(imported.CustomerCode, out var customer))
            {
                imported.CreatedAt = now;
                _context.Customers.Add(imported);
                inserted++;
                continue;
            }

            imported.CustomerId = customer.CustomerId;
            imported.CreatedAt = customer.CreatedAt;
            _context.Entry(customer).CurrentValues.SetValues(imported);
            updated++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (inserted, updated);
    }

    private static string DescribeImportIssues(CustomerExcelReader.Result result)
    {
        var issues = new List<string>();
        if (result.InvalidRows > 0)
        {
            var rows = string.Join(", ", result.InvalidRowExamples);
            issues.Add($"ข้าม {result.InvalidRows} แถวที่รหัสหรือชื่อไม่ครบหรือไม่ถูกต้อง (เช่น แถว {rows})");
        }
        if (result.ConflictingCodes.Count > 0)
        {
            var codes = string.Join(", ", result.ConflictingCodes.Order().Take(12));
            issues.Add($"ข้าม {result.ConflictingCodes.Count} รหัสที่มีหลายชื่อ ({codes})");
        }
        if (result.DuplicateRows > 0)
        {
            issues.Add($"รวม {result.DuplicateRows} แถวที่รหัสและชื่อซ้ำ โดยใช้ข้อมูลจากแถวท้ายสุด");
        }
        return string.Join(" · ", issues);
    }

    private static Customer MapToEntity(CustomerFormViewModel model, Customer customer)
    {
        customer.CustomerCode = model.CustomerCode;
        customer.Name = model.Name.Trim();
        customer.Address = NullIfEmpty(model.Address);
        customer.District = NullIfEmpty(model.District);
        customer.Province = NullIfEmpty(model.Province);
        customer.Phone1 = NullIfEmpty(model.Phone1);
        customer.Phone2 = NullIfEmpty(model.Phone2);
        customer.SalesZone = NullIfEmpty(model.SalesZone);
        customer.TaxId = NullIfEmpty(model.TaxId);
        customer.ShippingInfo = NullIfEmpty(model.ShippingInfo);
        customer.CreditLimit = model.CreditLimit;
        customer.IsActive = model.IsActive;
        return customer;
    }

    private static CustomerFormViewModel MapToForm(Customer customer) => new()
    {
        CustomerId = customer.CustomerId,
        CustomerCode = customer.CustomerCode,
        Name = customer.Name,
        Address = customer.Address,
        District = customer.District,
        Province = customer.Province,
        Phone1 = customer.Phone1,
        Phone2 = customer.Phone2,
        SalesZone = customer.SalesZone,
        TaxId = customer.TaxId,
        ShippingInfo = customer.ShippingInfo,
        CreditLimit = customer.CreditLimit,
        IsActive = customer.IsActive
    };

    private static string? NullIfEmpty(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
