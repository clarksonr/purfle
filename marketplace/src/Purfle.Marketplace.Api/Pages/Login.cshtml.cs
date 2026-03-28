using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purfle.Marketplace.Core.Entities;

namespace Purfle.Marketplace.Api.Pages;

public sealed class LoginModel(SignInManager<Publisher> signInManager) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; set; }

    public void OnGet()
    {
        ReturnUrl ??= "/";
    }

    public async Task<IActionResult> OnPostAsync(string email, string password, string? returnUrl)
    {
        ReturnUrl = returnUrl ?? "/";

        var result = await signInManager.PasswordSignInAsync(
            email, password, isPersistent: false, lockoutOnFailure: false);

        if (result.Succeeded)
            return LocalRedirect(ReturnUrl);

        Error = "Invalid email or password.";
        return Page();
    }
}
