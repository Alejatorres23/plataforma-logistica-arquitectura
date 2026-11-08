using System.Reflection;
using Logistica.Entregas.Api;
using Logistica.Entregas.Application.UseCases;
using Logistica.Entregas.Domain;
using Logistica.Entregas.Infrastructure;

namespace Logistica.Entregas.Tests.Architecture;

/// <summary>
/// Pruebas de arquitectura (fitness functions): fallan en el CI si alguien rompe los límites.
/// Dependencias permitidas: Api → Aplicación + Infraestructura; Infraestructura → Aplicación → Dominio.
/// </summary>
public class DependencyRulesTests
{
    private static readonly Assembly Domain = typeof(Delivery).Assembly;
    private static readonly Assembly Application = typeof(AssignCourierUseCase).Assembly;
    private static readonly Assembly Infrastructure = typeof(InMemoryDeliveryRepository).Assembly;
    private static readonly Assembly Api = typeof(DeliveryEndpoints).Assembly;

    private static string[] ProjectReferences(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(a => a.Name!)
        .Where(n => n.StartsWith("Logistica."))
        .Order()
        .ToArray();

    private static string[] AllReferences(Assembly assembly) => assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    [Fact]
    public void Dominio_NoDependeDeNingunaOtraCapa()
        => Assert.Empty(ProjectReferences(Domain));

    [Fact]
    public void Aplicacion_SoloDependeDelDominio()
        => Assert.Equal([Domain.GetName().Name!], ProjectReferences(Application));

    [Fact]
    public void Infraestructura_NoDependeDeLaCapaDeEntrada()
        => Assert.DoesNotContain(Api.GetName().Name!, ProjectReferences(Infrastructure));

    [Fact]
    public void Infraestructura_ImplementaLosPuertosDeLaAplicacion()
    {
        var ports = Application.GetTypes().Where(t => t.IsInterface && t.Namespace!.EndsWith(".Ports"));

        foreach (var port in ports)
            Assert.Contains(Infrastructure.GetTypes(), t => port.IsAssignableFrom(t) && !t.IsInterface);
    }

    [Fact]
    public void DominioYAplicacion_NoUsanTecnologiasDeInfraestructura()
    {
        // Ni el dominio ni los casos de uso conocen bases de datos, HTTP, ASP.NET ni archivos.
        string[] forbidden = ["System.Data", "Microsoft.Data.Sqlite", "System.Net.Http", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "System.IO"];

        foreach (var assembly in new[] { Domain, Application })
            Assert.DoesNotContain(AllReferences(assembly), name => forbidden.Any(f => name.StartsWith(f)));
    }

    [Fact]
    public void Endpoints_NoAccedenALaBaseDeDatosDirectamente()
    {
        // La capa de entrada usa casos de uso; solo la composición (Program) y la salud conocen SQLite.
        var endpointTypes = new[] { typeof(DeliveryEndpoints) };
        foreach (var type in endpointTypes)
        {
            var usesSqlite = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(m => m.GetParameters())
                .Any(p => p.ParameterType.Namespace?.StartsWith("Microsoft.Data.Sqlite") == true);
            Assert.False(usesSqlite);
        }
    }
}
