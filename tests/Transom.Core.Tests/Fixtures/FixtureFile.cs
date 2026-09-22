namespace Transom.Core.Tests.Fixtures;

internal static class FixtureFile
{
    public static string ReadText(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return File.ReadAllText(path);
    }

    public static Stream OpenRead(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return File.OpenRead(path);
    }
}