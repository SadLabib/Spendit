﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using Spendit.DataAccess;
using Spendit.Models;
using SpenditWeb.Controllers;

namespace SpenditWeb.Areas.Identity.Pages.Account.Manage
{
    public class DownloadPersonalDataModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<DownloadPersonalDataModel> _logger;
        private readonly ApplicationDbContext _context;
        private readonly PdfGenerationService _pdfGenerationService;

        public DownloadPersonalDataModel(
            UserManager<IdentityUser> userManager,
            ILogger<DownloadPersonalDataModel> logger,
            ApplicationDbContext context,
            PdfGenerationService pdfGenerationService)
        {
            _userManager = userManager;
            _logger = logger;
            _context = context;
            _pdfGenerationService = pdfGenerationService;
        }

        public IActionResult OnGet()
        {
            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync(string format)
        {
            var user = await GetUserAsync();
            if (user == null)
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            _logger.LogInformation("User with ID '{UserId}' asked for their personal data.", _userManager.GetUserId(User));

            switch (format.ToLower())
            {
                case "pdf":
                    var htmlContent = GenerateHtmlContentForPdf(user);
                    var pdfBytes = await _pdfGenerationService.GeneratePdfFromHtml(htmlContent);
                    return File(pdfBytes, "application/pdf", "PersonalData.pdf");

                case "csv":
                    var csvContent = GenerateCsvContent(user);
                    return File(new System.Text.UTF8Encoding().GetBytes(csvContent), "text/csv", "PersonalData.csv");

                case "html":
                    var htmlData = GenerateHtmlContent(user);
                    return File(new System.Text.UTF8Encoding().GetBytes(htmlData), "text/html", "PersonalData.html");

                default:
                    return BadRequest("Invalid format selected.");
            }
        }


        private async Task<IdentityUser> GetUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        private string GenerateHtmlContentForPdf(IdentityUser user)
        {
            var personalDataProps = typeof(IdentityUser).GetProperties().Where(
                prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));

            var logins = _userManager.GetLoginsAsync(user).Result;
            string auth = _userManager.GetAuthenticatorKeyAsync(user).Result ?? "None";
            List<Category> categories = _context.Categories.Where(t => t.UserId == _userManager.GetUserId(User)).ToList();
            List<Transaction> transactions = _context.Transactions
                .Include(x => x.Category)
                .Where(y => y.Category.UserId == _userManager.GetUserId(User))
                .ToList();

            StringBuilder htmlBuilder = new StringBuilder();
            htmlBuilder.AppendLine("<html>");
            htmlBuilder.AppendLine("<head>");
            htmlBuilder.AppendLine("<style>");
            htmlBuilder.AppendLine("table { border-collapse: collapse; width: 100%; border: 2px solid #333; margin-bottom: 40px; }");
            htmlBuilder.AppendLine("th, td { border: 1px solid #333; text-align: left; padding: 8px; }");
            htmlBuilder.AppendLine("th { background-color: #ffffff; color: #333333; font-weight: bold; }");
            htmlBuilder.AppendLine(".container { max-width: 800px; margin: 0 auto; padding: 20px; }");
            htmlBuilder.AppendLine(".header { font-size: 24px; margin-bottom: 30px; }");
            htmlBuilder.AppendLine(".sub-header { font-size: 18px; margin-bottom: 15px; }");
            htmlBuilder.AppendLine(".footer { font-size: 14px; text-align: right; }");
            htmlBuilder.AppendLine("</style>");
            htmlBuilder.AppendLine("</head>");
            htmlBuilder.AppendLine("<body>");

            htmlBuilder.AppendLine("<div class=\"container\">");
            htmlBuilder.AppendLine("<h1 class=\"header\">Spendit Data</h1>");

            htmlBuilder.AppendLine("<h2 class=\"sub-header\">User Data</h2>");
            htmlBuilder.AppendLine("<table>");
            htmlBuilder.AppendLine("<tr><th>Property</th><th>Value</th></tr>");
            foreach (var p in personalDataProps)
            {
                htmlBuilder.AppendLine($"<tr><td>{p.Name}</td><td>{p.GetValue(user)?.ToString() ?? "null"}</td></tr>");
            }
            foreach (var l in logins)
            {
                htmlBuilder.AppendLine($"<tr><td>{l.LoginProvider} external login provider key</td><td>{l.ProviderKey}</td></tr>");
            }
            htmlBuilder.AppendLine($"<tr><td>Authenticator Key</td><td>{auth}</td></tr>");
            htmlBuilder.AppendLine("</table>");

            htmlBuilder.AppendLine("<h2 class=\"sub-header\">Category Data</h2>");
            htmlBuilder.AppendLine("<table>");
            htmlBuilder.AppendLine("<tr><th>Category ID</th><th>Title</th><th>Icon</th><th>Type</th></tr>");
            foreach (var category in categories)
            {
                htmlBuilder.AppendLine($"<tr><td>{category.CategoryId}</td><td>{category.Title}</td><td>{category.Icon}</td><td>{category.Type}</td></tr>");
            }
            htmlBuilder.AppendLine("</table>");

            htmlBuilder.AppendLine("<h2 class=\"sub-header\">Transaction Data</h2>");
            htmlBuilder.AppendLine("<table>");
            htmlBuilder.AppendLine("<tr><th>Transaction ID</th><th>Category</th><th>Amount</th><th>Date</th><th>Note</th></tr>");
            foreach (var transaction in transactions)
            {
                htmlBuilder.AppendLine($"<tr><td>{transaction.TransactionId}</td><td>{transaction.Category.TitleWithIcon}</td><td>{transaction.FormattedAmount}</td><td>{transaction.Date.ToString("MMMM dd, yyyy")}</td><td>{(string.IsNullOrEmpty(transaction.Note) ? "No note provided" : transaction.Note)}</td></tr>");
            }
            htmlBuilder.AppendLine("</table>");

            DateTime currentDateTime = DateTime.Now;
            TimeZoneInfo localTimeZone = TimeZoneInfo.Local;
            string footerText = $"*Generated by Spendit on {currentDateTime.ToString("dd MMM, yyyy hh:mm:ss tt")} {localTimeZone.DisplayName} time";
            htmlBuilder.AppendLine($"<p class=\"footer\">{footerText}</p>");

            htmlBuilder.AppendLine("</div>");
            htmlBuilder.AppendLine("</body>");
            htmlBuilder.AppendLine("</html>");

            return htmlBuilder.ToString();
        }

        private string GenerateCsvContent(IdentityUser user)
        {
            var personalDataProps = typeof(IdentityUser).GetProperties().Where(
                prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));

            var logins = _userManager.GetLoginsAsync(user).Result;
            string auth = _userManager.GetAuthenticatorKeyAsync(user).Result ?? "None";
            List<Category> categories = _context.Categories.Where(t => t.UserId == _userManager.GetUserId(User)).ToList();
            List<Transaction> transactions = _context.Transactions
                .Include(x => x.Category)
                .Where(y => y.Category.UserId == _userManager.GetUserId(User))
                .ToList();

            StringBuilder csvBuilder = new StringBuilder();

            // User Data
            csvBuilder.AppendLine(",User Data");
            csvBuilder.AppendLine("Property,Value");
            foreach (var p in personalDataProps)
            {
                csvBuilder.AppendLine($"{p.Name},{p.GetValue(user)?.ToString() ?? "null"}");
            }
            foreach (var l in logins)
            {
                csvBuilder.AppendLine($"{l.LoginProvider} external login provider key,{l.ProviderKey}");
            }
            csvBuilder.AppendLine($"Authenticator Key,{auth}");

            // Category Data
            csvBuilder.AppendLine();
            csvBuilder.AppendLine(",Category Data");
            csvBuilder.AppendLine("Category ID,Title,Icon,Type");
            foreach (var category in categories)
            {
                csvBuilder.AppendLine($"{category.CategoryId},{AddDoubleQuotes(category.Title)},{category.Icon},{category.Type}");
            }

            // Transaction Data
            csvBuilder.AppendLine();
            csvBuilder.AppendLine(",Transaction Data");
            csvBuilder.AppendLine("Transaction ID,Category,Amount,Date,Note");
            foreach (var transaction in transactions)
            {
                csvBuilder.AppendLine($"{transaction.TransactionId},{AddDoubleQuotes(transaction.Category.TitleWithIcon)},{AddDoubleQuotes(transaction.FormattedAmount)},{AddDoubleQuotes(transaction.Date.ToString("MMMM dd, yyyy"))},{(string.IsNullOrEmpty(transaction.Note) ? "No note provided" : AddDoubleQuotes(transaction.Note))}");
            }

            return csvBuilder.ToString();
        }

        public string AddDoubleQuotes(string input)
        {
            if (input == null)
            {
                return null;
            }

            // Escape existing double quotes by doubling them up
            input = input.Replace("\"", "\"\"");

            // Enclose the input within double quotes
            return '"' + input + '"';
        }

        private string GenerateHtmlContent(IdentityUser user)
        {
            var personalDataProps = typeof(IdentityUser).GetProperties().Where(
                prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));

            var logins = _userManager.GetLoginsAsync(user).Result;
            string auth = _userManager.GetAuthenticatorKeyAsync(user).Result ?? "None";
            List<Category> categories = _context.Categories.Where(t => t.UserId == _userManager.GetUserId(User)).ToList();
            List<Transaction> transactions = _context.Transactions
                .Include(x => x.Category)
                .Where(y => y.Category.UserId == _userManager.GetUserId(User))
                .ToList();

            StringBuilder htmlBuilder = new StringBuilder();
            htmlBuilder.AppendLine("<html>");
            htmlBuilder.AppendLine("<head>");
            htmlBuilder.AppendLine("<style>");
            htmlBuilder.AppendLine("table { border-collapse: collapse; width: 100%; border: 2px solid #333; margin-bottom: 40px; }");
            htmlBuilder.AppendLine("th, td { border: 1px solid #333; text-align: left; padding: 8px; }");
            htmlBuilder.AppendLine("th { background-color: #2c3e50; color: #ffffff; font-weight: bold; }"); // Updated styling for table headers
            htmlBuilder.AppendLine(".container { max-width: 800px; margin: 0 auto; padding: 20px; }");
            htmlBuilder.AppendLine(".header { font-size: 24px; margin-bottom: 30px; }");
            htmlBuilder.AppendLine(".sub-header { font-size: 18px; margin-bottom: 15px; }");
            htmlBuilder.AppendLine(".footer { font-size: 14px; text-align: right; }");
            htmlBuilder.AppendLine("</style>");
            htmlBuilder.AppendLine("</head>");
            htmlBuilder.AppendLine("<body>");

            htmlBuilder.AppendLine("<div class=\"container\">");
            htmlBuilder.AppendLine("<h1 class=\"header\">Spendit Data</h1>");

            htmlBuilder.AppendLine("<h2 class=\"sub-header\">User Data</h2>");
            htmlBuilder.AppendLine("<table>");
            htmlBuilder.AppendLine("<tr><th>Property</th><th>Value</th></tr>");
            foreach (var p in personalDataProps)
            {
                htmlBuilder.AppendLine($"<tr><td>{p.Name}</td><td>{p.GetValue(user)?.ToString() ?? "null"}</td></tr>");
            }
            foreach (var l in logins)
            {
                htmlBuilder.AppendLine($"<tr><td>{l.LoginProvider} external login provider key</td><td>{l.ProviderKey}</td></tr>");
            }
            htmlBuilder.AppendLine($"<tr><td>Authenticator Key</td><td>{auth}</td></tr>");
            htmlBuilder.AppendLine("</table>");

            htmlBuilder.AppendLine("<h2 class=\"sub-header\">Category Data</h2>");
            htmlBuilder.AppendLine("<table>");
            htmlBuilder.AppendLine("<tr><th>Category ID</th><th>Title</th><th>Icon</th><th>Type</th></tr>");
            foreach (var category in categories)
            {
                htmlBuilder.AppendLine($"<tr><td>{category.CategoryId}</td><td>{category.Title}</td><td>{category.Icon}</td><td>{category.Type}</td></tr>");
            }
            htmlBuilder.AppendLine("</table>");

            htmlBuilder.AppendLine("<h2 class=\"sub-header\">Transaction Data</h2>");
            htmlBuilder.AppendLine("<table>");
            htmlBuilder.AppendLine("<tr><th>Transaction ID</th><th>Category</th><th>Amount</th><th>Date</th><th>Note</th></tr>");
            foreach (var transaction in transactions)
            {
                htmlBuilder.AppendLine($"<tr><td>{transaction.TransactionId}</td><td>{transaction.Category.TitleWithIcon}</td><td>{transaction.FormattedAmount}</td><td>{transaction.Date.ToString("MMMM dd, yyyy")}</td><td>{(string.IsNullOrEmpty(transaction.Note) ? "No note provided" : transaction.Note)}</td></tr>");
            }
            htmlBuilder.AppendLine("</table>");

            DateTime currentDateTime = DateTime.Now;
            TimeZoneInfo localTimeZone = TimeZoneInfo.Local;
            string footerText = $"*Generated by Spendit on {currentDateTime.ToString("dd MMM, yyyy hh:mm:ss tt")} {localTimeZone.DisplayName} time";
            htmlBuilder.AppendLine($"<p class=\"footer\">{footerText}</p>");

            htmlBuilder.AppendLine("</div>");
            htmlBuilder.AppendLine("</body>");
            htmlBuilder.AppendLine("</html>");

            return htmlBuilder.ToString();
        }

    }
}

/* Text file generation
 using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Spendit.DataAccess;
using Spendit.Models;

namespace SpenditWeb.Areas.Identity.Pages.Account.Manage
{
    public class DownloadPersonalDataModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<DownloadPersonalDataModel> _logger;
        private readonly ApplicationDbContext _context;

        public DownloadPersonalDataModel(
            UserManager<IdentityUser> userManager,
            ILogger<DownloadPersonalDataModel> logger,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _logger = logger;
            _context = context;
        }

        public IActionResult OnGet()
        {
            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            _logger.LogInformation("User with ID '{UserId}' asked for their personal data.", _userManager.GetUserId(User));

            // Only include personal data for download
            var personalData = new List<string>();

            // User Data
            personalData.Add("--------------------------------------------------");
            personalData.Add("User Data");
            personalData.Add("--------------------------------------------------");
            personalData.Add("");

            var personalDataProps = typeof(IdentityUser).GetProperties().Where(
                prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));

            foreach (var p in personalDataProps)
            {
                personalData.Add($"{p.Name}: {p.GetValue(user)?.ToString() ?? "null"}");
            }

            var logins = await _userManager.GetLoginsAsync(user);
            foreach (var l in logins)
            {
                personalData.Add($"{l.LoginProvider} external login provider key: {l.ProviderKey}");
            }

            string auth = await _userManager.GetAuthenticatorKeyAsync(user);
            if (auth == null) auth = "None";
            personalData.Add($"Authenticator Key: " + auth);

            // Category Data
            personalData.Add("");
            personalData.Add("--------------------------------------------------");
            personalData.Add("Category Data");
            personalData.Add("--------------------------------------------------");
            personalData.Add("");

            List<Category> categories = await _context.Categories
                .Where(t => t.UserId == _userManager.GetUserId(User))
                .ToListAsync();

            foreach (var category in categories)
            {
                // Format category information
                StringBuilder categoryInfo = new StringBuilder();
                categoryInfo.AppendLine($"Category ID: {category.CategoryId}");
                categoryInfo.AppendLine($"Title: {category.Title}");
                categoryInfo.AppendLine($"Icon: {category.Icon}");
                categoryInfo.AppendLine($"Type: {category.Type}");

                // Add formatted category information to PersonalData list
                personalData.Add(categoryInfo.ToString());
            }

            // Transaction Data
            personalData.Add("");
            personalData.Add("--------------------------------------------------");
            personalData.Add("Transaction Data");
            personalData.Add("--------------------------------------------------");
            personalData.Add("");

            List<Transaction> transactions = await _context.Transactions
                .Include(x => x.Category)
                .Where(y => y.Category.UserId == _userManager.GetUserId(User))
                .ToListAsync();

            foreach (var transaction in transactions)
            {
                // Format transaction information
                StringBuilder transactionInfo = new StringBuilder();
                transactionInfo.AppendLine($"Transaction ID: {transaction.TransactionId}");
                transactionInfo.AppendLine($"Category: {transaction.Category.TitleWithIcon}");
                transactionInfo.AppendLine($"Amount: {transaction.FormattedAmount}");
                transactionInfo.AppendLine($"Date: {transaction.Date.ToString("MMMM dd, yyyy")}");
                transactionInfo.AppendLine($"Note: {(string.IsNullOrEmpty(transaction.Note) ? "No note provided" : transaction.Note)}");

                // Add formatted transaction information to PersonalData list
                personalData.Add(transactionInfo.ToString());
            }

            // Convert to text file
            var content = Encoding.UTF8.GetBytes(string.Join("\n", personalData));
            Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.txt");
            return new FileContentResult(content, "text/plain");
            
            // Convert HTML string to byte array
            // var content = Encoding.UTF8.GetBytes(htmlBuilder.ToString());
            // Set content type to HTML
            //Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.html");
            //return new FileContentResult(content, "text/html");
        }
    }
}

 */