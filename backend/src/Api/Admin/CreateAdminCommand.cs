using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin;

/// <summary>
/// <c>dotnet Api.dll create-admin --email &lt;email&gt;</c>. La contraseña se lee de stdin, nunca de los argumentos,
/// para que no quede en el historial del shell ni en la lista de procesos.
/// </summary>
public static class CreateAdminCommand
{
    public const string Name = "create-admin";

    public static bool IsInvoked(string[] args) => args.Length > 0 && args[0] == Name;

    public static Task<int> RunAsync(IServiceProvider services, string[] args) =>
        // Solo se pide confirmación cuando alguien la está tecleando; con stdin redirigido se lee una línea.
        RunAsync(services, args, Console.Out, ReadSecretFromConsole, confirm: !Console.IsInputRedirected);

    /// <param name="readSecret">Lee una línea sin mostrarla. Recibe el texto que se muestra antes, y devuelve <c>null</c> si stdin terminó.</param>
    public static async Task<int> RunAsync(
        IServiceProvider services, string[] args, TextWriter output, Func<string, string?> readSecret, bool confirm)
    {
        var email = ParseEmail(args);
        if (email is null)
        {
            output.WriteLine("Uso: dotnet Api.dll create-admin --email <email>");
            return 1;
        }

        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        if (await users.Users.AnyAsync())
        {
            output.WriteLine("Ya existe un administrador. La tienda tiene uno solo.");
            return 1;
        }

        var password = readSecret("Contraseña: ");
        if (string.IsNullOrEmpty(password))
        {
            output.WriteLine("No se recibió ninguna contraseña.");
            return 1;
        }
        if (confirm && readSecret("Repite la contraseña: ") != password)
        {
            output.WriteLine("Las contraseñas no coinciden.");
            return 1;
        }

        var result = await users.CreateAsync(new IdentityUser { UserName = email, Email = email, EmailConfirmed = true }, password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                output.WriteLine(error.Description);
            return 1;
        }

        output.WriteLine($"Administrador {email} creado. Al entrar por primera vez en /admin se configura la verificación en dos pasos.");
        return 0;
    }

    private static string? ParseEmail(string[] args)
    {
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--email" && i + 1 < args.Length)
                return args[i + 1].Trim();
            if (args[i].StartsWith("--email="))
                return args[i]["--email=".Length..].Trim();
        }
        return null;
    }

    private static string? ReadSecretFromConsole(string prompt)
    {
        Console.Write(prompt);
        if (Console.IsInputRedirected)
            return Console.ReadLine();

        var secret = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
                break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (secret.Length > 0)
                    secret.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                secret.Append(key.KeyChar);
            }
        }
        Console.WriteLine();
        return secret.ToString();
    }
}
