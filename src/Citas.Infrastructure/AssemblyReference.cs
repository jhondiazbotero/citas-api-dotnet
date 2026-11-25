using System.Reflection;

namespace Citas.Infrastructure;

/// <summary>Marcador para obtener el ensamblado desde pruebas de arquitectura.</summary>
public static class AssemblyReference
{
    public static Assembly Assembly => typeof(AssemblyReference).Assembly;
}
