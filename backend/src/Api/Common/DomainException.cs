namespace Api.Common;

/// <summary>Una regla de dominio impidió la operación. El mensaje está en español y se puede mostrar al admin.</summary>
public class DomainException(string message) : Exception(message);
