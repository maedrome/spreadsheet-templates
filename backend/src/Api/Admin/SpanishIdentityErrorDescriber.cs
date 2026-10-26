using Microsoft.AspNetCore.Identity;

namespace Api.Admin;

/// <summary>Los errores de Identity que puede ver el administrador al crear su cuenta.</summary>
public class SpanishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = $"El email \"{email}\" no es válido." };

    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = $"Ya existe una cuenta con el email \"{email}\"." };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = $"Ya existe una cuenta con el email \"{userName}\"." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"La contraseña debe tener al menos {length} caracteres." };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "La contraseña debe tener al menos un número." };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "La contraseña debe tener al menos una minúscula." };

    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "La contraseña debe tener al menos una mayúscula." };

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "La contraseña debe tener al menos un símbolo." };
}
