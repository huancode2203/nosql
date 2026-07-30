using System.Runtime.CompilerServices;
using System.Threading;
using MongoDB.Bson.Serialization.Conventions;

namespace EduManageLms.Api.Infrastructure;

/// <summary>
/// Registers MongoDB conventions as soon as the API assembly is loaded.
/// This keeps API, tests, migration tools and BSON deserialization consistent.
/// </summary>
internal static class MongoConventionBootstrap
{
    private static int _registered;

    [ModuleInitializer]
    internal static void Initialize()
    {
        EnsureRegistered();
    }

    internal static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        var conventionPack = new ConventionPack
        {
            new CamelCaseElementNameConvention()
        };

        ConventionRegistry.Register(
            "EduManageLms.Api.camelCase",
            conventionPack,
            _ => true);
    }
}