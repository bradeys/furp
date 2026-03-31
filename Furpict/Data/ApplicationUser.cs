using Furpict.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace Furpict.Data;

public class ApplicationUser : IdentityUser
{
    public ICollection<Pet> Pets { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
}
