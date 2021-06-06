using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using UserManagementAPI.Models;
using UserManagementAPI.ViewModels;

namespace UserManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly RoleManager<ApplicationRole> roleManager;
        private readonly ApplicationSettings appSettings;
        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<ApplicationRole> roleManager,
            IOptions<ApplicationSettings> appSettings
        )
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.roleManager = roleManager;
            this.appSettings = appSettings.Value;
        }

        //Create Guest role if doesnt exist already
        private async Task<IActionResult> CreateRole(CreateRoleViewModel role)
        {
            if (ModelState.IsValid)
            {
                ApplicationRole identityRole = new ApplicationRole
                {
                    Name = role.RoleName
                };

                IdentityResult result = await roleManager.CreateAsync(identityRole);

                if (result.Succeeded)
                {
                    //this.logService.createLog("Created user role");
                    return Ok(result);
                }

                foreach (IdentityError error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            return null;
        }

        #region addUser
        // POST: api/Account/Register
        /// <summary>
        /// This method creates new users
        /// </summary>
        /// <returns>Produces an empty Status 200OK response.</returns>
        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            IdentityResult roleResult;
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.UserName,
                    FullName = model.FullName,
                    Email = model.Email,
                };
                var result = await userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    bool guestRoleExists = await roleManager.RoleExistsAsync("Guest");
                    if (!guestRoleExists)
                    {
                        CreateRoleViewModel role = new CreateRoleViewModel
                        {
                            RoleName = "Guest"
                        };
                        CreateRole(role);
                    }
                    roleResult = await userManager.AddToRoleAsync(user, "Guest");
                    await signInManager.SignInAsync(user, isPersistent: false);
                    return Ok(result);
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            return Ok();
        }
        #endregion

        #region login
        // POST: api/Account/Login
        /// <summary>
        /// This method authenticates registered users users
        /// </summary>
        /// <returns>Produces a jwt for logged in users.</returns>
        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Invalid Login Attempt");
            }
            var user = await this.userManager.FindByNameAsync(model.UserName);
            if (user != null && await this.userManager.CheckPasswordAsync(user, model.Password))
            {
                //Get roles assigned to the user
                var role = await this.userManager.GetRolesAsync(user);
                IdentityOptions _options = new IdentityOptions();

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new Claim[]
                    {
                        new Claim("UserID", user.Id.ToString()),
                        new Claim("UserName", user.UserName.ToString()),
                        //new Claim("DepartmentId", user.DepartmentId.ToString()),
                        new Claim(_options.ClaimsIdentity.RoleClaimType,role.FirstOrDefault())
                    }),
                    Expires = DateTime.UtcNow.AddMinutes(60),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.appSettings.JWT_Secret)), SecurityAlgorithms.HmacSha256)
                };
                var tokenHandler = new JwtSecurityTokenHandler();
                var securityToken = tokenHandler.CreateToken(tokenDescriptor);
                var token = tokenHandler.WriteToken(securityToken);
                return Ok(new { token });
            }
            else
            {
                return BadRequest(new { message = "Username or password is wrong" });
            }

        }
        #endregion
    }
}