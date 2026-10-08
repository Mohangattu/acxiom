using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCrm.Models;
using AcxiomCrm.Services;
using AcxiomCrm.ViewModels;

namespace AcxiomCrm.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditLogService _auditLog;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IAuditLogService auditLog)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _auditLog = auditLog;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                await _auditLog.LogAsync("Security", null, "FailedLogin", "Failed login attempt with unrecognized email.");
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            if (!user.IsActive)
            {
                await _auditLog.LogAsync("Security", user.Id, "FailedLoginInactive", $"Login attempt blocked for inactive account {user.Email}.", user.Id, user.FullName);
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact an Administrator.");
                return View(model);
            }

            // Enable lockout on failure tracking
            var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                await _auditLog.LogAsync("User", user.Id, "Login", $"User {user.Email} logged in successfully.", user.Id, user.FullName);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Dashboard");
            }

            if (result.IsLockedOut)
            {
                await _auditLog.LogAsync("Security", user.Id, "AccountLockout", $"Account {user.Email} locked out due to multiple failed login attempts.", user.Id, user.FullName);
                ModelState.AddModelError(string.Empty, "This account has been temporarily locked out due to multiple failed login attempts. Please try again after 15 minutes.");
                return View(model);
            }

            // Log failed attempt without exposing secrets
            await _auditLog.LogAsync("Security", user.Id, "FailedLogin", $"Failed login attempt for user {user.Email}.", user.Id, user.FullName);
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            return View(new RegisterViewModel { Role = "SalesExecutive" });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "An account with this email already exists.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                Department = model.Department,
                EmailConfirmed = true,
                IsActive = true,
                LockoutEnabled = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                // Ensure role exists or fallback to SalesExecutive
                var roleToAssign = string.IsNullOrEmpty(model.Role) ? "SalesExecutive" : model.Role;
                if (!await _roleManager.RoleExistsAsync(roleToAssign))
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleToAssign));
                }
                await _userManager.AddToRoleAsync(user, roleToAssign);

                await _auditLog.LogAsync("User", user.Id, "Register", $"New user account {user.Email} registered with role {roleToAssign}.", user.Id, user.FullName);

                await _signInManager.SignInAsync(user, isPersistent: false);
                TempData["SuccessMessage"] = "Account registered successfully! Welcome to the CRM.";
                return RedirectToAction("Index", "Dashboard");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                await _auditLog.LogAsync("User", user.Id, "Logout", $"User {user.Email} logged out.", user.Id, user.FullName);
            }

            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);

            var model = new UserProfileViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Department = user.Department,
                Role = roles.FirstOrDefault() ?? "SalesRep"
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(UserProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                var roles = await _userManager.GetRolesAsync(user);
                model.Role = roles.FirstOrDefault() ?? "SalesRep";
                return View(model);
            }

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            user.Department = model.Department;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            // Change password if provided
            if (!string.IsNullOrEmpty(model.CurrentPassword) && !string.IsNullOrEmpty(model.NewPassword))
            {
                var changePassResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
                if (!changePassResult.Succeeded)
                {
                    foreach (var error in changePassResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    var roles = await _userManager.GetRolesAsync(user);
                    model.Role = roles.FirstOrDefault() ?? "SalesRep";
                    return View(model);
                }
            }

            await _auditLog.LogAsync("User", user.Id, "ProfileUpdate", $"User {user.Email} updated profile details.");

            TempData["SuccessMessage"] = "Profile details updated successfully!";
            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
