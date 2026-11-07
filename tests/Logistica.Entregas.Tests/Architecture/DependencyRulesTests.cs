using System.Reflection;
using Logistica.Entregas.Application.UseCases;
using Logistica.Entregas.Domain;
using Logistica.Entregas.Infrastructure;

namespace Logistica.Entregas.Tests.Architecture;

/// <summary>
/// Pruebas de arquitectura: verifican que las dependencias apunten hacia adentro
/// (Infraestructura → Aplicación → Dominio) y que el dominio no dependa de nada externo.
/// </summary>
public class DependencyRulesTests
{
    private static readonly Assembly Domain = typeof(Delivery).Assembly;
    private static readonly Assembly Application = typeof(AssignCourierUseCase).Assembly;
    private static readonly Assembly Infrastructure = typeof(InMemoryDeliveryRepository).Assembly;

    private static string[] ProjectReferences(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(a => a.Name!)
        .Where(n => n.StartsWith("Logistica."))
        .ToArray();

    [Fact]
    public void Dominio_NoDependeDeNingunaOtraCapa()
        => Assert.Empty(ProjectReferences(Domain));

    [Fact]
    public void Aplicacion_SoloDependeDelDominio()
        => Assert.Equal([Domain.GetName().Name!], ProjectReferences(Application));

    [Fact]
    public void Infraestructura_ImplementaLosPuertosDeLaAplicacion()
    {
        var ports = Application.GetTypes().Where(t => t.IsInterface && t.Namespace!.EndsWith(".Ports"));

        foreach (var port in ports)
            Assert.Contains(Infrastructure.GetTypes(), t => port.IsAssignableFrom(t) && !t.IsInterface);
    }

    [Fact]
    public void Dominio_NoUsaTiposDeInfraestructura()
    {
        // El dominio no debe conocer bases de datos, HTTP ni archivos.
        string[] forbidden = ["System.Data", "System.Net.Http", "Microsoft.EntityFrameworkCore", "System.IO"];
        var referenced = Domain.GetReferencedAssemblies().Select(a => a.Name!);

        Assert.DoesNotContain(referenced, name => forbidden.Any(f => name.StartsWith(f)));
    }
}
