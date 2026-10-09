using System.ComponentModel.DataAnnotations;

namespace SmartInventory.Api.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage ="Email is required")]
    [EmailAddress(ErrorMessage = "Invalid Email Address")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    
    [Display(Name = "Remember me?")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

