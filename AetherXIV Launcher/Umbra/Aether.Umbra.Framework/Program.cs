using Aether.Umbra.Framework;

if (args.Length == 1 && string.Equals(args[0], "--probe", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine(UmbraFrameworkInfo.ProbeText);
    return 0;
}

if (args.Length == 1 && string.Equals(args[0], "--bootstrap", StringComparison.OrdinalIgnoreCase))
{
    return await UmbraBootstrapRunner.RunFromEnvironmentAsync();
}

Console.WriteLine($"{UmbraFrameworkInfo.Name} {UmbraFrameworkInfo.Version}");
return 0;
