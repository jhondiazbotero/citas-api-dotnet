using System.Reflection;

namespace Citas.Application;

/// <summary>Marker used to get the assembly from architecture tests.</summary>
public static class AssemblyReference
{
    public static Assembly Assembly => typeof(AssemblyReference).Assembly;
}
